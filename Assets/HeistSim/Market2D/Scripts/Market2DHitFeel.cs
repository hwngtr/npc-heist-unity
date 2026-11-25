using System.Collections.Generic;
using UnityEngine;

namespace HeistSim.Market2D
{
    /// <summary>
    /// Unified 60 Hz fixed-tick impact feedback controller implementing SpriteCook Technique 17:
    /// hit stop (freezes actors while VFX/shake keep running), 2-frame pure-white sprite flash,
    /// cubic ease-out knockback offset, whole-pixel alternating screen shake (3, -2, 2, -1, 1, -1, 0),
    /// 4-ray impact spark star, directional particle burst, and dramatic killing-blow slow-motion.
    /// </summary>
    public sealed class Market2DHitFeel : MonoBehaviour
    {
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static readonly float[] SparkRaysPx = { 5f, 9f, 7f, 3f };

        /// <summary>Standard hit-stop freeze duration in 60 Hz ticks (5 ticks = 0.083 s).</summary>
        public int stopTicks = 5;

        /// <summary>Heavy/caught killing-blow hit-stop duration in 60 Hz ticks (9 ticks = 0.15 s).</summary>
        public int stopTicksKill = 9;

        /// <summary>Pure-white sprite flash duration in ticks (2 frames).</summary>
        public int flashTicks = 2;

        /// <summary>Cubic ease-out knockback distance in art pixels (12 px).</summary>
        public float knockPx = 12f;

        /// <summary>Knockback duration in ticks (8 ticks).</summary>
        public int knockTicks = 8;

        /// <summary>Initial screen shake amplitude in art pixels (3 px).</summary>
        public float shakePx = 3f;

        /// <summary>Screen shake duration in ticks (10 ticks).</summary>
        public int shakeTicks = 10;

        /// <summary>4-ray impact spark lifetime in ticks (4 frames).</summary>
        public int sparkTicks = 4;

        /// <summary>Slow-motion time scale after a killing blow (0.3x for 12 ticks).</summary>
        public float killSlowScale = 0.3f;

        /// <summary>Slow-motion duration in ticks after killing-blow freeze.</summary>
        public int killSlowTicks = 12;

        private sealed class ActiveHit
        {
            public int Age;
            public int Stop;
            public SpriteRenderer TargetSprite;
            public Vector3 ContactWorld;
            public Vector2 Direction;
            public bool IsKill;
            public bool RfAccent;
        }

        private readonly List<ActiveHit> _hits = new();
        private MaterialPropertyBlock _mpb;
        private Market2DFeel _feel;
        private LineRenderer[] _sparkLines;
        private Material _sparkMat;

        private float _tickAccum;
        private int _freezeCounter;
        private int _shakeAge = -1;
        private Vector2Int _shakeOffsetPx;
        private Vector2 _knockOffsetWorld;
        private float _currentTimeScale = 1f;
        private bool _initialized;

        /// <summary>True while actors should be frozen in hit stop (flash, spark, shake still run).</summary>
        public bool IsHitStopped => _freezeCounter > 0;

        /// <summary>Current whole-pixel screen shake offset (in art pixels; 0,0 when at rest).</summary>
        public Vector2Int ShakeOffsetPx => _shakeOffsetPx;

        /// <summary>Current screen shake offset in world units (at BaseWorldPpu).</summary>
        public Vector3 ShakeOffsetWorld => new(
            _shakeOffsetPx.x / Market2DRunner.BaseWorldPpu,
            _shakeOffsetPx.y / Market2DRunner.BaseWorldPpu,
            0f);

        /// <summary>Current knockback offset in world units.</summary>
        public Vector2 KnockbackOffsetWorld => _knockOffsetWorld;

        /// <summary>Current logical simulation time scale (0.3 during kill slow-mo, else 1.0).</summary>
        public float SimTimeScale => _currentTimeScale;

        /// <summary>Number of currently active hit-feel events.</summary>
        public int ActiveHitCount => _hits.Count;

        /// <summary>Ensures impact spark line renderers are created once.</summary>
        public void EnsureInitialized(Market2DFeel feel = null)
        {
            if (feel != null)
            {
                _feel = feel;
            }

            if (_initialized)
            {
                return;
            }

            _initialized = true;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _sparkMat = new Material(shader);
            }

            _sparkLines = new LineRenderer[4];
            for (var i = 0; i < 4; i++)
            {
                var go = new GameObject("VfxImpactSpark_" + i);
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.widthMultiplier = i < 2 ? 0.048f : 0.032f;
                lr.material = _sparkMat;
                lr.sortingOrder = 9650;
                lr.enabled = false;
                _sparkLines[i] = lr;
            }
        }

        /// <summary>
        /// Triggers unified impact feedback at contactWorld.
        /// Set customStopTicks >= 0 to override default stopTicks (e.g., lighter 3-tick freeze on loot steal).
        /// </summary>
        public void TriggerHit(
            SpriteRenderer targetSprite,
            Vector3 contactWorld,
            Vector2 direction,
            bool isKill = false,
            bool rfAccent = false,
            int customStopTicks = -1)
        {
            EnsureInitialized();
            var stop = customStopTicks >= 0 ? customStopTicks : (isKill ? stopTicksKill : stopTicks);
            var dir = direction.sqrMagnitude > 1e-5f ? direction.normalized : Vector2.right;

            var hit = new ActiveHit
            {
                Age = 0,
                Stop = stop,
                TargetSprite = targetSprite,
                ContactWorld = contactWorld,
                Direction = dir,
                IsKill = isKill,
                RfAccent = rfAccent,
            };

            _hits.Add(hit);
            if (stop > 0)
            {
                _freezeCounter++;
            }

            if (flashTicks > 0 && targetSprite != null)
            {
                SetSpriteFlash(targetSprite, 1f);
            }

            if (shakePx > 0f && shakeTicks > 0)
            {
                _shakeAge = 0;
                _shakeOffsetPx = ComputeShakePx(0, shakePx, shakeTicks, 0f);
            }

            RenderSpark(hit);
        }

        /// <summary>Advances the fixed 60 Hz hit-feel clock by unscaledDt.</summary>
        public void Advance(float unscaledDt)
        {
            if (_hits.Count == 0 && _shakeAge < 0)
            {
                return;
            }

            _tickAccum += Mathf.Min(unscaledDt, 0.1f);
            const float step = 1f / 60f;
            while (_tickAccum >= step)
            {
                _tickAccum -= step;
                StepTick();
            }
        }

        /// <summary>Advances all hit-feel counters by exactly one 60 Hz tick (public for automated unit tests).</summary>
        public void StepTick()
        {
            var tail = Mathf.Max(knockTicks, Mathf.Max(sparkTicks, killSlowTicks));
            var sparkActive = false;

            for (var i = _hits.Count - 1; i >= 0; i--)
            {
                var h = _hits[i];
                h.Age++;

                if (h.Age == flashTicks && h.TargetSprite != null)
                {
                    SetSpriteFlash(h.TargetSprite, 0f);
                }

                if (h.Age == h.Stop + 1)
                {
                    if (h.Stop > 0)
                    {
                        _freezeCounter = Mathf.Max(0, _freezeCounter - 1);
                    }

                    if (_feel != null)
                    {
                        _feel.EmitPickupBurst(h.ContactWorld, h.RfAccent || h.IsKill);
                        if (h.IsKill)
                        {
                            _feel.EmitPickupBurst(h.ContactWorld, true);
                        }
                    }

                    if (h.IsKill && killSlowScale < 1f && killSlowTicks > 0)
                    {
                        _currentTimeScale = killSlowScale;
                    }
                }

                var k = h.Age - h.Stop;
                if (k >= 1 && k <= knockTicks && knockPx > 0f)
                {
                    var delta = KnockbackStepPx(k, knockTicks, knockPx);
                    _knockOffsetWorld += h.Direction * (delta / Market2DRunner.BaseWorldPpu);
                }
                else if (k > knockTicks)
                {
                    _knockOffsetWorld = Vector2.Lerp(_knockOffsetWorld, Vector2.zero, 0.35f);
                }

                if (h.IsKill && h.Age == h.Stop + 1 + killSlowTicks)
                {
                    _currentTimeScale = 1f;
                }

                if (h.Age < sparkTicks)
                {
                    RenderSpark(h);
                    sparkActive = true;
                }

                if (h.Age >= h.Stop + 1 + tail)
                {
                    if (h.TargetSprite != null)
                    {
                        SetSpriteFlash(h.TargetSprite, 0f);
                    }

                    _hits.RemoveAt(i);
                }
            }

            if (!sparkActive)
            {
                HideSpark();
            }

            if (_hits.Count == 0)
            {
                _freezeCounter = 0;
                _knockOffsetWorld = Vector2.zero;
                _currentTimeScale = 1f;
            }

            if (_shakeAge >= 0)
            {
                _shakeAge++;
                if (_shakeAge >= shakeTicks)
                {
                    _shakeAge = -1;
                    _shakeOffsetPx = Vector2Int.zero;
                }
                else
                {
                    var yRand = Mathf.Sin(_shakeAge * 19.37f) * 0.6f;
                    _shakeOffsetPx = ComputeShakePx(_shakeAge, shakePx, shakeTicks, yRand);
                }
            }
        }

        /// <summary>Resets all active hit-feel state cleanly (e.g., on New Shift).</summary>
        public void ResetAll()
        {
            for (var i = 0; i < _hits.Count; i++)
            {
                if (_hits[i].TargetSprite != null)
                {
                    SetSpriteFlash(_hits[i].TargetSprite, 0f);
                }
            }

            _hits.Clear();
            _freezeCounter = 0;
            _shakeAge = -1;
            _shakeOffsetPx = Vector2Int.zero;
            _knockOffsetWorld = Vector2.zero;
            _currentTimeScale = 1f;
            HideSpark();
        }

        /// <summary>Cubic ease-out 1 - (1 - t)^3.</summary>
        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            var u = 1f - t;
            return 1f - u * u * u;
        }

        /// <summary>Incremental knockback step in pixels on tick k (1..totalTicks).</summary>
        public static float KnockbackStepPx(int k, int totalTicks, float totalPx)
        {
            if (totalTicks <= 0 || k < 1 || k > totalTicks)
            {
                return 0f;
            }

            return totalPx * (EaseOutCubic((float)k / totalTicks) - EaseOutCubic((float)(k - 1) / totalTicks));
        }

        /// <summary>
        /// Exact T17 whole-pixel alternating damped screen shake:
        /// At shakePx=3, shakeTicks=10, x produces: 3, -2, 2, -1, 1, -1, 0, 0, 0, 0.
        /// </summary>
        public static Vector2Int ComputeShakePx(int age, float maxPx, int totalTicks, float yFraction)
        {
            if (age < 0 || age >= totalTicks || totalTicks <= 0 || maxPx <= 0f)
            {
                return Vector2Int.zero;
            }

            var falloff = 1f - (float)age / totalTicks;
            var amp = maxPx * falloff * falloff;
            var sx = Mathf.RoundToInt(amp) * ((age % 2 == 0) ? 1 : -1);
            var sy = Mathf.RoundToInt(amp * Mathf.Clamp(yFraction, -0.6f, 0.6f));
            return new Vector2Int(sx, sy);
        }

        private void SetSpriteFlash(SpriteRenderer sr, float flash)
        {
            if (sr == null)
            {
                return;
            }

            _mpb ??= new MaterialPropertyBlock();
            sr.GetPropertyBlock(_mpb);
            _mpb.SetFloat(FlashAmountId, flash);
            sr.SetPropertyBlock(_mpb);
        }

        private void RenderSpark(ActiveHit hit)
        {
            if (_sparkLines == null || sparkTicks <= 0)
            {
                return;
            }

            var idx = Mathf.Clamp(hit.Age * SparkRaysPx.Length / Mathf.Max(1, sparkTicks), 0, SparkRaysPx.Length - 1);
            var rayWorld = (SparkRaysPx[idx] * 2.2f) / Market2DRunner.BaseWorldPpu;
            var diagWorld = rayWorld * 0.5f;
            var c = hit.ContactWorld;

            SetLine(_sparkLines[0], c - new Vector3(rayWorld, 0f, 0f), c + new Vector3(rayWorld, 0f, 0f), RisoPalette.Sun);
            SetLine(_sparkLines[1], c - new Vector3(0f, rayWorld, 0f), c + new Vector3(0f, rayWorld, 0f), RisoPalette.Sun);
            SetLine(_sparkLines[2], c - new Vector3(diagWorld, diagWorld, 0f), c + new Vector3(diagWorld, diagWorld, 0f), Color.white);
            SetLine(_sparkLines[3], c - new Vector3(diagWorld, -diagWorld, 0f), c + new Vector3(diagWorld, -diagWorld, 0f), Color.white);
        }

        private void HideSpark()
        {
            if (_sparkLines == null)
            {
                return;
            }

            for (var i = 0; i < _sparkLines.Length; i++)
            {
                if (_sparkLines[i] != null)
                {
                    _sparkLines[i].enabled = false;
                }
            }
        }

        private static void SetLine(LineRenderer lr, Vector3 a, Vector3 b, Color col)
        {
            if (lr == null)
            {
                return;
            }

            lr.enabled = true;
            lr.startColor = col;
            lr.endColor = col;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
        }
    }
}
