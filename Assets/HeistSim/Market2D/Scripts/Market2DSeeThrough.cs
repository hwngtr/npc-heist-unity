using System.Collections.Generic;
using UnityEngine;

namespace HeistSim.Market2D
{
    /// <summary>See-through occlusion visual modes from SpriteCook Technique 10.</summary>
    public enum SeeThroughMode
    {
        Off = 0,
        Fade = 1,
        Dither = 2,
        DitheredFade = 3,
        Cutout = 4,
    }

    /// <summary>
    /// Keeps the thief readable when sneaking behind tall isometric supermarket fixtures
    /// by easing a per-fixture Bayer 4x4 dithered cutout (or opacity fade) in RisoSpriteHighlight.shader.
    /// Uses MaterialPropertyBlock so sprite batching and existing loot highlights remain intact.
    /// </summary>
    public sealed class Market2DSeeThrough : MonoBehaviour
    {
        private static readonly int AmountId = Shader.PropertyToID("_SeeThroughAmount");
        private static readonly int TargetId = Shader.PropertyToID("_SeeThroughTarget");
        private static readonly int RadiusId = Shader.PropertyToID("_SeeThroughRadius");
        private static readonly int SoftId = Shader.PropertyToID("_SeeThroughSoft");
        private static readonly int KeepId = Shader.PropertyToID("_SeeThroughKeep");
        private static readonly int CellSizeId = Shader.PropertyToID("_SeeThroughCellSize");
        private static readonly int FadeAlphaId = Shader.PropertyToID("_SeeThroughFadeAlpha");

        private static readonly int[] Bayer4 =
        {
             0,  8,  2, 10,
            12,  4, 14,  6,
             3, 11,  1,  9,
            15,  7, 13,  5,
        };

        /// <summary>Active see-through mode (default: DitheredFade, keeping 6 of 16 pixels for riso texture).</summary>
        public SeeThroughMode mode = SeeThroughMode.DitheredFade;

        /// <summary>Linear ease duration in seconds (0.2s = 12 frames at 60 fps).</summary>
        public float easeTime = 0.20f;

        /// <summary>Clear radius around thief center in world units.</summary>
        public float clearRadius = 0.42f;

        /// <summary>Soft Bayer transition band outside clearRadius in world units.</summary>
        public float softBand = 0.34f;

        /// <summary>Minimum solid fraction for DitheredFade (0.375 keeps 6 of 16 Bayer cells).</summary>
        public float keepFloor = 0.375f;

        /// <summary>Opacity used in Fade mode.</summary>
        public float fadeOpacity = 0.40f;

        /// <summary>Bayer dither cell size in world units (2 art pixels at 64 PPU).</summary>
        public float cellWorldSize = 0.03125f;

        /// <summary>Character half-extents used for occlusion overlap checks.</summary>
        public Vector2 actorBoxSize = new(0.46f, 0.72f);

        private sealed class TrackedFixture
        {
            public SpriteRenderer Renderer;
            public float Amount;
            public bool Dirty;
        }

        private readonly List<TrackedFixture> _fixtures = new();
        private MaterialPropertyBlock _mpb;

        /// <summary>Number of tracked occluding fixtures.</summary>
        public int TrackedCount => _fixtures.Count;

        /// <summary>Collects all fixture SpriteRenderers under fixturesRoot.</summary>
        public void BindFixtures(Transform fixturesRoot)
        {
            _fixtures.Clear();
            if (fixturesRoot == null)
            {
                return;
            }

            var renderers = fixturesRoot.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var sr = renderers[i];
                if (sr == null || sr.sprite == null)
                {
                    continue;
                }

                // Skip floor-flat or tiny props; tall shelves & cases occlude the thief
                if (sr.bounds.size.y < 0.75f)
                {
                    continue;
                }

                _fixtures.Add(new TrackedFixture { Renderer = sr, Amount = 0f, Dirty = true });
            }
        }

        /// <summary>Updates per-fixture see-through amounts based on actor world position and depth sorting order.</summary>
        public void Step(Vector3 actorFeetWorld, int actorSortingOrder, float dt)
        {
            _mpb ??= new MaterialPropertyBlock();
            var actorCenter = new Vector2(actorFeetWorld.x, actorFeetWorld.y + actorBoxSize.y * 0.48f);
            var actorRect = new Rect(
                actorCenter.x - actorBoxSize.x * 0.5f,
                actorFeetWorld.y,
                actorBoxSize.x,
                actorBoxSize.y);

            for (var i = 0; i < _fixtures.Count; i++)
            {
                var item = _fixtures[i];
                var sr = item.Renderer;
                if (sr == null)
                {
                    continue;
                }

                var occluded = mode != SeeThroughMode.Off && IsOccluding(sr, actorRect, actorSortingOrder);
                var goal = occluded ? 1f : 0f;
                var next = MoveAmount(item.Amount, goal, dt, easeTime);
                var changed = Mathf.Abs(next - item.Amount) > 1e-4f || next > 0.001f || item.Dirty;
                item.Amount = next;

                if (changed)
                {
                    item.Dirty = next > 0.001f;
                    ApplyToRenderer(sr, next, actorCenter);
                }
            }
        }

        /// <summary>Cycles to the next see-through visual mode and returns its name.</summary>
        public string CycleMode()
        {
            mode = (SeeThroughMode)(((int)mode + 1) % 5);
            for (var i = 0; i < _fixtures.Count; i++)
            {
                _fixtures[i].Dirty = true;
            }

            return mode.ToString();
        }

        /// <summary>Pure linear ease step toward goal over easeDuration seconds.</summary>
        public static float MoveAmount(float current, float goal, float dt, float easeDuration)
        {
            if (easeDuration <= 0.0001f)
            {
                return goal;
            }

            return Mathf.MoveTowards(current, goal, dt / easeDuration);
        }

        /// <summary>
        /// Pure CPU replica of the fragment shader Bayer 4x4 pixel-keep decision for automated unit tests.
        /// Returns true if the fixture pixel at cell (cellX, cellY) is kept, false if dropped.
        /// </summary>
        public static bool ShouldKeepPixel(
            int cellX,
            int cellY,
            Vector2 targetCell,
            float amount,
            float radiusCells,
            float softCells,
            float keep)
        {
            if (amount <= 0.001f || radiusCells <= 0f)
            {
                return true;
            }

            var d = Vector2.Distance(new Vector2(cellX + 0.5f, cellY + 0.5f), targetCell);
            var reach = (radiusCells + softCells) * amount;
            var solid = softCells > 0.0001f
                ? Mathf.Clamp01((d - reach) / softCells + 1f)
                : (d >= reach ? 1f : 0f);
            solid = Mathf.Max(solid, keep);

            var bx = ((cellX % 4) + 4) % 4;
            var by = ((cellY % 4) + 4) % 4;
            var threshold = (Bayer4[by * 4 + bx] + 0.5f) / 16f;
            return solid > threshold;
        }

        private static bool IsOccluding(SpriteRenderer sr, Rect actorRect, int actorSortingOrder)
        {
            // Only fixtures drawn IN FRONT of the actor (higher isometric sortingOrder) can hide them
            if (sr.sortingOrder <= actorSortingOrder)
            {
                return false;
            }

            var b = sr.bounds;
            var fixRect = new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
            if (!fixRect.Overlaps(actorRect))
            {
                return false;
            }

            // Require meaningful body overlap (at least ~20% of actor box area) to avoid edge triggers
            var ixMin = Mathf.Max(fixRect.xMin, actorRect.xMin);
            var iyMin = Mathf.Max(fixRect.yMin, actorRect.yMin);
            var ixMax = Mathf.Min(fixRect.xMax, actorRect.xMax);
            var iyMax = Mathf.Min(fixRect.yMax, actorRect.yMax);
            var overlapArea = Mathf.Max(0f, ixMax - ixMin) * Mathf.Max(0f, iyMax - iyMin);
            var actorArea = Mathf.Max(1e-4f, actorRect.width * actorRect.height);
            return (overlapArea / actorArea) >= 0.18f;
        }

        private void ApplyToRenderer(SpriteRenderer sr, float amount, Vector2 actorCenter)
        {
            sr.GetPropertyBlock(_mpb);
            _mpb.SetFloat(AmountId, amount);
            _mpb.SetVector(TargetId, new Vector4(actorCenter.x, actorCenter.y, 0f, 0f));

            switch (mode)
            {
                case SeeThroughMode.Fade:
                    _mpb.SetFloat(FadeAlphaId, fadeOpacity);
                    _mpb.SetFloat(RadiusId, 0f);
                    break;
                case SeeThroughMode.Dither:
                    _mpb.SetFloat(FadeAlphaId, 1f);
                    _mpb.SetFloat(RadiusId, clearRadius);
                    _mpb.SetFloat(SoftId, softBand);
                    _mpb.SetFloat(KeepId, 0f);
                    _mpb.SetFloat(CellSizeId, cellWorldSize);
                    break;
                case SeeThroughMode.DitheredFade:
                    _mpb.SetFloat(FadeAlphaId, 1f);
                    _mpb.SetFloat(RadiusId, clearRadius);
                    _mpb.SetFloat(SoftId, softBand);
                    _mpb.SetFloat(KeepId, keepFloor);
                    _mpb.SetFloat(CellSizeId, cellWorldSize);
                    break;
                case SeeThroughMode.Cutout:
                    _mpb.SetFloat(FadeAlphaId, 1f);
                    _mpb.SetFloat(RadiusId, clearRadius * 1.15f);
                    _mpb.SetFloat(SoftId, 0f);
                    _mpb.SetFloat(KeepId, 0f);
                    _mpb.SetFloat(CellSizeId, cellWorldSize);
                    break;
                default:
                    _mpb.SetFloat(AmountId, 0f);
                    _mpb.SetFloat(FadeAlphaId, 1f);
                    break;
            }

            sr.SetPropertyBlock(_mpb);
        }
    }
}
