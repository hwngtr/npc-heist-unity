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
    /// Keeps the thief and NPCs readable when walking behind isometric low walls or tall
    /// fixtures by resolving front/back sorting order and easing per-actor Bayer 4x4 dithered
    /// cutouts (up to 4 simultaneous characters) in RisoSpriteHighlight.shader.
    /// Uses MaterialPropertyBlock so sprite batching and existing loot highlights remain intact.
    /// </summary>
    public sealed class Market2DSeeThrough : MonoBehaviour
    {
        private static readonly int AmountId = Shader.PropertyToID("_SeeThroughAmount");
        private static readonly int TargetId = Shader.PropertyToID("_SeeThroughTarget");
        private static readonly int Target1Id = Shader.PropertyToID("_SeeThroughTarget1");
        private static readonly int Target2Id = Shader.PropertyToID("_SeeThroughTarget2");
        private static readonly int Target3Id = Shader.PropertyToID("_SeeThroughTarget3");
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

        /// <summary>Snapshot of one actor passed to Market2DSeeThrough each frame.</summary>
        public readonly struct SeeThroughActor
        {
            /// <summary>True when tile coordinates are valid for dynamic wall/fixture front-back sorting.</summary>
            public readonly bool HasTilePos;

            /// <summary>Continuous map tile position (i, j) used for exact isometric depth comparison.</summary>
            public readonly Vector2 TilePos;

            /// <summary>Actor feet position in Unity world space.</summary>
            public readonly Vector3 FeetWorld;

            /// <summary>Actor sprite sortingOrder for this frame.</summary>
            public readonly int SortingOrder;

            /// <summary>Creates an actor sample with authoritative tile coordinates.</summary>
            public SeeThroughActor(Vector2 tilePos, Vector3 feetWorld, int sortingOrder)
            {
                HasTilePos = true;
                TilePos = tilePos;
                FeetWorld = feetWorld;
                SortingOrder = sortingOrder;
            }

            /// <summary>Creates a legacy actor sample without tile coordinates.</summary>
            public SeeThroughActor(Vector3 feetWorld, int sortingOrder)
            {
                HasTilePos = false;
                TilePos = Vector2.zero;
                FeetWorld = feetWorld;
                SortingOrder = sortingOrder;
            }
        }

        private const int MaxSeeThroughActors = 4;

        private sealed class TrackedOccluder
        {
            public SpriteRenderer Renderer;
            public Iso2DOccluderBounds Bounds;
            public readonly float[] Amounts = new float[MaxSeeThroughActors];
            public bool Dirty;
        }

        private readonly List<TrackedOccluder> _occluders = new();
        private readonly SeeThroughActor[] _singleActorScratch = new SeeThroughActor[1];
        private readonly Vector2[] _targetCentersScratch = new Vector2[MaxSeeThroughActors];
        private readonly Rect[] _actorRectsScratch = new Rect[MaxSeeThroughActors];
        private readonly bool[] _occludingScratch = new bool[MaxSeeThroughActors];
        private MaterialPropertyBlock _mpb;

        /// <summary>Number of tracked occluding fixtures and wall segments.</summary>
        public int TrackedCount => _occluders.Count;

        /// <summary>Collects all occluding fixture SpriteRenderers under fixturesRoot.</summary>
        public void BindFixtures(Transform fixturesRoot)
        {
            BindOccluders(fixturesRoot, null);
        }

        /// <summary>Collects all tall fixture and LowWall SpriteRenderers that can occlude actors.</summary>
        public void BindOccluders(Transform fixturesRoot, Transform lowWallsRoot)
        {
            _occluders.Clear();
            AddOccludersFromRoot(fixturesRoot, includeShortWithoutBounds: false);
            AddOccludersFromRoot(lowWallsRoot, includeShortWithoutBounds: true);
        }

        private void AddOccludersFromRoot(Transform root, bool includeShortWithoutBounds)
        {
            if (root == null)
            {
                return;
            }

            var renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var sr = renderers[i];
                if (sr == null || sr.sprite == null)
                {
                    continue;
                }

                var bounds = sr.GetComponent<Iso2DOccluderBounds>();
                if (!includeShortWithoutBounds && bounds == null && sr.bounds.size.y < 0.75f)
                {
                    continue;
                }

                _occluders.Add(new TrackedOccluder
                {
                    Renderer = sr,
                    Bounds = bounds,
                    Dirty = true,
                });
            }
        }

        /// <summary>Updates per-fixture see-through amounts for a single actor.</summary>
        public void Step(Vector3 actorFeetWorld, int actorSortingOrder, float dt)
        {
            _singleActorScratch[0] = new SeeThroughActor(actorFeetWorld, actorSortingOrder);
            StepActors(_singleActorScratch, 1, dt);
        }

        /// <summary>
        /// Resolves dynamic front/back depth order on walls and wide fixtures and updates
        /// up to 4 per-actor Bayer 4x4 see-through cutouts simultaneously.
        /// </summary>
        public void StepActors(SeeThroughActor[] actors, int actorCount, float dt)
        {
            _mpb ??= new MaterialPropertyBlock();
            var count = actors == null ? 0 : Mathf.Clamp(actorCount, 0, MaxSeeThroughActors);

            for (var k = 0; k < count; k++)
            {
                var feet = actors[k].FeetWorld;
                var center = new Vector2(feet.x, feet.y + actorBoxSize.y * 0.48f);
                _targetCentersScratch[k] = center;
                _actorRectsScratch[k] = new Rect(
                    center.x - actorBoxSize.x * 0.5f,
                    feet.y,
                    actorBoxSize.x,
                    actorBoxSize.y);
            }

            for (var k = count; k < MaxSeeThroughActors; k++)
            {
                _targetCentersScratch[k] = Vector2.zero;
                _actorRectsScratch[k] = default;
                _occludingScratch[k] = false;
            }

            for (var i = 0; i < _occluders.Count; i++)
            {
                var item = _occluders[i];
                var sr = item.Renderer;
                if (sr == null)
                {
                    continue;
                }

                var bounds = item.Bounds;
                var minRequiredOrder = bounds != null ? bounds.baseSortingOrder : sr.sortingOrder;
                var maxAllowedOrder = int.MaxValue;
                var hasAnyBehind = false;

                for (var k = 0; k < count; k++)
                {
                    if (!HasBodyOverlap(sr, _actorRectsScratch[k], 0.14f))
                    {
                        _occludingScratch[k] = false;
                        continue;
                    }

                    bool behind;
                    if (bounds != null && actors[k].HasTilePos)
                    {
                        behind = IsActorBehindOccluder(actors[k].TilePos, bounds);
                        if (behind)
                        {
                            hasAnyBehind = true;
                            minRequiredOrder = Mathf.Max(minRequiredOrder, actors[k].SortingOrder + 4);
                        }
                        else
                        {
                            maxAllowedOrder = Mathf.Min(maxAllowedOrder, actors[k].SortingOrder - 2);
                        }
                    }
                    else
                    {
                        behind = sr.sortingOrder > actors[k].SortingOrder;
                        if (behind)
                        {
                            hasAnyBehind = true;
                        }
                    }

                    _occludingScratch[k] = mode != SeeThroughMode.Off && behind;
                }

                if (bounds != null)
                {
                    var targetOrder = hasAnyBehind
                        ? minRequiredOrder
                        : Mathf.Min(bounds.baseSortingOrder, maxAllowedOrder);
                    if (sr.sortingOrder != targetOrder)
                    {
                        sr.sortingOrder = targetOrder;
                    }
                }

                var changed = false;
                var anyActive = false;
                for (var k = 0; k < MaxSeeThroughActors; k++)
                {
                    var goal = k < count && _occludingScratch[k] ? 1f : 0f;
                    var next = MoveAmount(item.Amounts[k], goal, dt, easeTime);
                    if (Mathf.Abs(next - item.Amounts[k]) > 1e-4f)
                    {
                        changed = true;
                    }

                    item.Amounts[k] = next;
                    if (next > 0.001f)
                    {
                        anyActive = true;
                    }
                }

                if (changed || anyActive || item.Dirty)
                {
                    item.Dirty = anyActive;
                    ApplyToRenderer(sr, item.Amounts, _targetCentersScratch);
                }
            }
        }

        /// <summary>Cycles to the next see-through visual mode and returns its name.</summary>
        public string CycleMode()
        {
            mode = (SeeThroughMode)(((int)mode + 1) % 5);
            for (var i = 0; i < _occluders.Count; i++)
            {
                _occluders[i].Dirty = true;
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

        /// <summary>
        /// Determines whether an actor at tilePos sits behind an axis-aligned occluder footprint
        /// along the isometric camera view ray (constant i - j, increasing i + j toward the viewer).
        /// </summary>
        public static bool IsActorBehindOccluder(Vector2 tilePos, Iso2DOccluderBounds bounds)
        {
            if (bounds == null)
            {
                return false;
            }

            var ai = tilePos.x;
            var aj = tilePos.y;
            var u = ai - aj;
            var xLow = Mathf.Max(bounds.minI, bounds.minJ + u);
            var xHigh = Mathf.Min(bounds.maxI, bounds.maxJ + u);
            if (xLow <= xHigh)
            {
                return ai < xLow - 0.01f;
            }

            if (u > bounds.maxI - bounds.minJ)
            {
                return (ai + aj) < (bounds.maxI + bounds.minJ - 0.01f);
            }

            return (ai + aj) < (bounds.minI + bounds.maxJ - 0.01f);
        }

        private static bool HasBodyOverlap(SpriteRenderer sr, Rect actorRect, float minOverlapFraction)
        {
            var b = sr.bounds;
            var fixRect = new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
            if (!fixRect.Overlaps(actorRect))
            {
                return false;
            }

            var ixMin = Mathf.Max(fixRect.xMin, actorRect.xMin);
            var iyMin = Mathf.Max(fixRect.yMin, actorRect.yMin);
            var ixMax = Mathf.Min(fixRect.xMax, actorRect.xMax);
            var iyMax = Mathf.Min(fixRect.yMax, actorRect.yMax);
            var overlapArea = Mathf.Max(0f, ixMax - ixMin) * Mathf.Max(0f, iyMax - iyMin);
            var actorArea = Mathf.Max(1e-4f, actorRect.width * actorRect.height);
            return (overlapArea / actorArea) >= minOverlapFraction;
        }

        private void ApplyToRenderer(SpriteRenderer sr, float[] amounts, Vector2[] centers)
        {
            sr.GetPropertyBlock(_mpb);
            var amt0 = amounts[0];
            _mpb.SetFloat(AmountId, amt0);
            _mpb.SetVector(TargetId, new Vector4(centers[0].x, centers[0].y, amt0, 0f));
            _mpb.SetVector(Target1Id, new Vector4(centers[1].x, centers[1].y, amounts[1], 0f));
            _mpb.SetVector(Target2Id, new Vector4(centers[2].x, centers[2].y, amounts[2], 0f));
            _mpb.SetVector(Target3Id, new Vector4(centers[3].x, centers[3].y, amounts[3], 0f));

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
                    _mpb.SetVector(TargetId, Vector4.zero);
                    _mpb.SetVector(Target1Id, Vector4.zero);
                    _mpb.SetVector(Target2Id, Vector4.zero);
                    _mpb.SetVector(Target3Id, Vector4.zero);
                    _mpb.SetFloat(FadeAlphaId, 1f);
                    break;
            }

            sr.SetPropertyBlock(_mpb);
        }
    }
}
