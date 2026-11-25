using System.Collections.Generic;
using UnityEngine;

namespace HeistSim.Market2D
{
    /// <summary>
    /// Zero-dependency pooled palette-faithful 2D riso VFX (hard-edged print confetti bursts,
    /// sprint footstep puffs, ambient drifting paper motes), diegetic channeling progress arc,
    /// and camera micro-shake following the craft-game-floors feel and VFX rules.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Market2DFeel : MonoBehaviour
    {
        private const int MaxBurstParticles = 96;
        private const int AmbientMoteCount = 16;

        private struct RisoParticle
        {
            public bool Active;
            public Vector2 Pos;
            public Vector2 Vel;
            public float Gravity;
            public float Size;
            public float Age;
            public float Lifetime;
            public float Angle;
            public float Spin;
            public Color Color;
        }

        private readonly RisoParticle[] _particles = new RisoParticle[MaxBurstParticles];
        private readonly RisoParticle[] _motes = new RisoParticle[AmbientMoteCount];

        private Mesh _particleMesh;
        private MeshRenderer _particleRenderer;
        private readonly List<Vector3> _verts = new(MaxBurstParticles * 4);
        private readonly List<Color> _cols = new(MaxBurstParticles * 4);
        private readonly List<int> _tris = new(MaxBurstParticles * 6);

        private LineRenderer _channelRing;
        private LineRenderer _acousticRing;
        private LineRenderer _targetReticle;
        private LineRenderer _staminaArc;
        private Material _vfxMat;

        private Vector3 _ambientCenter;
        private float _shakeIntensity;
        private float _shakeTimer;
        private float _shakeDuration = 0.2f;
        private float _meshAccum;
        private bool _initialized;

        /// <summary>Number of currently active pooled event particles (for live verification).</summary>
        public int ActiveParticleCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _particles.Length; i++)
                {
                    if (_particles[i].Active)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Classic ease-out cubic curve.</summary>
        public static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            var u = 1f - t;
            return 1f - u * u * u;
        }

        /// <summary>Classic ease-out back overshoot curve (1.70158).</summary>
        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            var u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void LateUpdate()
        {
            if (!_initialized)
            {
                return;
            }

            var dt = Mathf.Min(Time.deltaTime, 0.05f);
            StepParticles(dt);
            _meshAccum += dt;
            if (ActiveParticleCount > 0 || _meshAccum >= 1f / 30f)
            {
                _meshAccum = 0f;
                RebuildParticleMesh();
            }
        }

        /// <summary>Creates the pooled quad-mesh particle renderer and diegetic channel ring once.</summary>
        public void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            _vfxMat = new Material(Shader.Find("Sprites/Default"));

            var vfxGo = new GameObject("VfxRisoParticles");
            vfxGo.transform.SetParent(transform, false);
            var mf = vfxGo.AddComponent<MeshFilter>();
            _particleMesh = new Mesh { name = "RisoVfxMesh" };
            _particleMesh.MarkDynamic();
            mf.sharedMesh = _particleMesh;

            _particleRenderer = vfxGo.AddComponent<MeshRenderer>();
            _particleRenderer.sharedMaterial = _vfxMat;
            _particleRenderer.sortingOrder = 9500;

            _channelRing = CreateChannelRing("VfxChannelRing", 9600);
            _acousticRing = CreateChannelRing("VfxAcousticRing", 9450);
            _targetReticle = CreateChannelRing("VfxGuardTargetReticle", 9480);
            _staminaArc = CreateChannelRing("VfxStaminaArc", 9580);
            SeedAmbientMotes();
        }

        /// <summary>Emits a pooled 12-square palette-colored riso confetti burst at worldPos.</summary>
        public void EmitPickupBurst(Vector3 worldPos, bool rfTagged)
        {
            EnsureInitialized();
            var primary = rfTagged ? RisoPalette.Coral : RisoPalette.Sun;
            var secondary = rfTagged ? RisoPalette.Sun : RisoPalette.Teal;
            var tertiary = RisoPalette.InkBlue;

            for (var i = 0; i < 12; i++)
            {
                var angle = (i / 12f) * Mathf.PI * 2f + Random.Range(-0.22f, 0.22f);
                var speed = Random.Range(1.4f, 2.8f);
                var col = (i % 3) switch
                {
                    0 => primary,
                    1 => secondary,
                    _ => tertiary,
                };
                SpawnParticle(
                    new Vector2(worldPos.x, worldPos.y + 0.36f),
                    new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed * 0.75f + 0.9f),
                    gravity: 3.4f,
                    size: Random.Range(0.065f, 0.105f),
                    lifetime: Random.Range(0.38f, 0.56f),
                    color: col);
            }
        }

        /// <summary>Emits a pooled 3-particle sprint footstep dust puff.</summary>
        public void EmitFootstepPuff(Vector3 worldPos)
        {
            EnsureInitialized();
            var dust = new Color(RisoPalette.InkBlue2.r, RisoPalette.InkBlue2.g, RisoPalette.InkBlue2.b, 0.55f);
            for (var i = 0; i < 3; i++)
            {
                var a = Random.Range(0f, Mathf.PI * 2f);
                var spd = Random.Range(0.25f, 0.65f);
                SpawnParticle(
                    new Vector2(worldPos.x + Random.Range(-0.08f, 0.08f), worldPos.y + 0.04f),
                    new Vector2(Mathf.Cos(a) * spd, Mathf.Abs(Mathf.Sin(a)) * spd * 0.6f + 0.2f),
                    gravity: 0.4f,
                    size: Random.Range(0.045f, 0.07f),
                    lifetime: 0.28f,
                    color: dust);
            }
        }

        /// <summary>Updates the ambient paper-mote drift region around the camera.</summary>
        public void SyncAmbientCenter(Vector3 cameraWorldPos)
        {
            _ambientCenter = cameraWorldPos;
        }

        /// <summary>Updates the diegetic world-space channeling progress arc around the thief.</summary>
        public void UpdateChannelRing(Vector3 worldPos, bool active, float progress01, bool isHazard)
        {
            EnsureInitialized();
            if (_channelRing == null)
            {
                return;
            }

            _channelRing.enabled = active && progress01 > 0.01f;
            if (!_channelRing.enabled)
            {
                return;
            }

            const int segments = 28;
            var count = Mathf.Clamp(Mathf.CeilToInt(segments * Mathf.Clamp01(progress01)) + 1, 2, segments + 1);
            _channelRing.positionCount = count;
            var col = isHazard ? RisoPalette.Coral : RisoPalette.Sun;
            _channelRing.startColor = col;
            _channelRing.endColor = RisoPalette.InkBlue;

            var cx = worldPos.x;
            var cy = worldPos.y + 0.06f;
            for (var i = 0; i < count; i++)
            {
                var a = Mathf.PI * 0.5f - (i / (float)segments) * Mathf.PI * 2f;
                var x = cx + Mathf.Cos(a) * 0.44f;
                var y = cy + Mathf.Sin(a) * 0.22f;
                _channelRing.SetPosition(i, new Vector3(x, y, 0f));
            }
        }

        /// <summary>Renders a subtle diegetic stamina arc under the thief whenever stamina is below 98% or winded.</summary>
        public void UpdateStaminaArc(Vector3 worldPos, float stamina01, bool winded)
        {
            EnsureInitialized();
            if (_staminaArc == null)
            {
                return;
            }

            var show = winded || stamina01 < 0.98f;
            _staminaArc.enabled = show;
            if (!show)
            {
                return;
            }

            const int segments = 18;
            var frac = Mathf.Clamp01(stamina01);
            var count = Mathf.Clamp(Mathf.CeilToInt(segments * frac) + 1, 2, segments + 1);
            _staminaArc.positionCount = count;
            _staminaArc.widthMultiplier = winded ? 0.052f : 0.038f;
            var col = winded
                ? RisoPalette.Coral
                : (frac < 0.35f ? RisoPalette.Sun : RisoPalette.Teal);
            _staminaArc.startColor = col;
            _staminaArc.endColor = col;

            var cx = worldPos.x;
            var cy = worldPos.y - 0.02f;
            for (var i = 0; i < count; i++)
            {
                // Bottom-half isometric arc beneath feet
                var a = Mathf.PI + (i / (float)segments) * Mathf.PI;
                var x = cx + Mathf.Cos(a) * 0.34f;
                var y = cy + Mathf.Sin(a) * 0.16f;
                _staminaArc.SetPosition(i, new Vector3(x, y, 0f));
            }
        }

        /// <summary>Renders an expanding isometric acoustic wave ring when a sound or decoy coin is emitted.</summary>
        public void UpdateAcousticWave(Vector2? originTile, float radiusTiles, float maxRadiusTiles)
        {
            EnsureInitialized();
            if (_acousticRing == null)
            {
                return;
            }

            var show = originTile.HasValue && radiusTiles > 0.05f && maxRadiusTiles > 0.1f && radiusTiles <= maxRadiusTiles;
            _acousticRing.enabled = show;
            if (!show)
            {
                return;
            }

            const int segments = 36;
            _acousticRing.positionCount = segments + 1;
            var fade = Mathf.Clamp01(1f - (radiusTiles / Mathf.Max(0.1f, maxRadiusTiles)));
            var col = new Color(RisoPalette.Coral.r, RisoPalette.Coral.g, RisoPalette.Coral.b, 0.25f + 0.65f * fade);
            _acousticRing.startColor = col;
            _acousticRing.endColor = col;
            _acousticRing.widthMultiplier = 0.045f + 0.03f * fade;

            var orig = originTile.Value;
            for (var i = 0; i <= segments; i++)
            {
                var a = (i / (float)segments) * Mathf.PI * 2f;
                var ti = orig.x + Mathf.Cos(a) * radiusTiles;
                var tj = orig.y + Mathf.Sin(a) * radiusTiles;
                var wp = Iso2DMath.TileToWorld(ti, tj, 0.03f);
                _acousticRing.SetPosition(i, wp);
            }
        }

        /// <summary>Renders the guard's active investigation/chase/search target reticle and tether line on the floor.</summary>
        public void UpdateGuardTargetReticle(Vector2 guardTile, Vector2? targetTile, string guardState)
        {
            EnsureInitialized();
            if (_targetReticle == null)
            {
                return;
            }

            var active = targetTile.HasValue
                && (guardState == "investigate" || guardState == "chase" || guardState == "search");
            _targetReticle.enabled = active;
            if (!active)
            {
                return;
            }

            var t = targetTile.Value;
            var isChase = guardState == "chase";
            var baseCol = isChase ? RisoPalette.Coral : RisoPalette.Sun;
            var col = new Color(baseCol.r, baseCol.g, baseCol.b, 0.82f);
            _targetReticle.startColor = col;
            _targetReticle.endColor = col;
            _targetReticle.widthMultiplier = 0.042f;

            // Draw tether from guard -> target center -> isometric diamond around target tile
            const float r = 0.42f;
            var gWorld = Iso2DMath.TileToWorld(guardTile.x, guardTile.y, 0.04f);
            var c0 = Iso2DMath.TileToWorld(t.x - r, t.y, 0.04f);
            var c1 = Iso2DMath.TileToWorld(t.x, t.y - r, 0.04f);
            var c2 = Iso2DMath.TileToWorld(t.x + r, t.y, 0.04f);
            var c3 = Iso2DMath.TileToWorld(t.x, t.y + r, 0.04f);

            _targetReticle.positionCount = 6;
            _targetReticle.SetPosition(0, gWorld);
            _targetReticle.SetPosition(1, c0);
            _targetReticle.SetPosition(2, c1);
            _targetReticle.SetPosition(3, c2);
            _targetReticle.SetPosition(4, c3);
            _targetReticle.SetPosition(5, c0);
        }

        /// <summary>Triggers a brief damped camera micro-shake.</summary>
        public void TriggerShake(float intensity, float duration = 0.22f)
        {
            _shakeIntensity = Mathf.Max(_shakeIntensity, intensity);
            _shakeDuration = Mathf.Max(0.05f, duration);
            _shakeTimer = _shakeDuration;
        }

        /// <summary>Samples and advances the current camera shake offset.</summary>
        public Vector3 SampleShakeOffset(float dt)
        {
            if (_shakeTimer <= 0f)
            {
                return Vector3.zero;
            }

            _shakeTimer = Mathf.Max(0f, _shakeTimer - dt);
            var falloff = _shakeTimer / _shakeDuration;
            var amp = _shakeIntensity * falloff * falloff;
            if (_shakeTimer <= 0f)
            {
                _shakeIntensity = 0f;
            }

            var t = Time.unscaledTime * 55f;
            return new Vector3(
                Mathf.Sin(t * 1.3f) * amp,
                Mathf.Cos(t * 1.7f) * amp * 0.7f,
                0f);
        }

        private void SpawnParticle(Vector2 pos, Vector2 vel, float gravity, float size, float lifetime, Color color)
        {
            var slot = -1;
            var oldest = -1f;
            for (var i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].Active)
                {
                    slot = i;
                    break;
                }

                if (_particles[i].Age > oldest)
                {
                    oldest = _particles[i].Age;
                    slot = i;
                }
            }

            if (slot < 0)
            {
                return;
            }

            _particles[slot] = new RisoParticle
            {
                Active = true,
                Pos = pos,
                Vel = vel,
                Gravity = gravity,
                Size = size,
                Age = 0f,
                Lifetime = lifetime,
                Angle = Random.Range(0f, Mathf.PI * 2f),
                Spin = Random.Range(-8f, 8f),
                Color = color,
            };
        }

        private void SeedAmbientMotes()
        {
            for (var i = 0; i < _motes.Length; i++)
            {
                ResetMote(i, randomAge: true);
            }
        }

        private void ResetMote(int index, bool randomAge)
        {
            var life = Random.Range(4.2f, 7.2f);
            var warm = (index % 2 == 0)
                ? new Color(RisoPalette.Sun.r, RisoPalette.Sun.g, RisoPalette.Sun.b, 0.28f)
                : new Color(RisoPalette.InkBlue2.r, RisoPalette.InkBlue2.g, RisoPalette.InkBlue2.b, 0.20f);
            _motes[index] = new RisoParticle
            {
                Active = true,
                Pos = new Vector2(_ambientCenter.x + Random.Range(-5.5f, 5.5f), _ambientCenter.y + Random.Range(-3.2f, 3.2f)),
                Vel = new Vector2(Random.Range(0.06f, 0.16f), Random.Range(-0.08f, 0.05f)),
                Gravity = 0f,
                Size = Random.Range(0.032f, 0.054f),
                Age = randomAge ? Random.Range(0f, life) : 0f,
                Lifetime = life,
                Angle = Random.Range(0f, Mathf.PI),
                Spin = Random.Range(-0.6f, 0.6f),
                Color = warm,
            };
        }

        private void StepParticles(float dt)
        {
            for (var i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].Active)
                {
                    continue;
                }

                _particles[i].Age += dt;
                if (_particles[i].Age >= _particles[i].Lifetime)
                {
                    _particles[i].Active = false;
                    continue;
                }

                _particles[i].Vel.y -= _particles[i].Gravity * dt;
                _particles[i].Pos += _particles[i].Vel * dt;
                _particles[i].Angle += _particles[i].Spin * dt;
            }

            for (var i = 0; i < _motes.Length; i++)
            {
                _motes[i].Age += dt;
                if (_motes[i].Age >= _motes[i].Lifetime)
                {
                    ResetMote(i, randomAge: false);
                    continue;
                }

                _motes[i].Pos += _motes[i].Vel * dt;
                _motes[i].Angle += _motes[i].Spin * dt;
            }
        }

        private void RebuildParticleMesh()
        {
            _verts.Clear();
            _cols.Clear();
            _tris.Clear();

            for (var i = 0; i < _motes.Length; i++)
            {
                var m = _motes[i];
                var u = Mathf.Clamp01(m.Age / Mathf.Max(0.01f, m.Lifetime));
                var fade = Mathf.Sin(u * Mathf.PI);
                var col = new Color(m.Color.r, m.Color.g, m.Color.b, m.Color.a * fade);
                AppendQuad(m.Pos, m.Size, m.Angle, col);
            }

            for (var i = 0; i < _particles.Length; i++)
            {
                var p = _particles[i];
                if (!p.Active)
                {
                    continue;
                }

                var u = Mathf.Clamp01(p.Age / Mathf.Max(0.01f, p.Lifetime));
                var scale = 1f - u * u;
                var col = new Color(p.Color.r, p.Color.g, p.Color.b, p.Color.a * (1f - u));
                AppendQuad(p.Pos, p.Size * scale, p.Angle, col);
            }

            _particleMesh.Clear();
            _particleMesh.SetVertices(_verts);
            _particleMesh.SetColors(_cols);
            _particleMesh.SetTriangles(_tris, 0);
            _particleMesh.RecalculateBounds();
        }

        private void AppendQuad(Vector2 center, float halfSize, float angle, Color col)
        {
            if (halfSize <= 0.001f || col.a <= 0.005f)
            {
                return;
            }

            var c = Mathf.Cos(angle) * halfSize;
            var s = Mathf.Sin(angle) * halfSize;
            var idx = _verts.Count;

            _verts.Add(new Vector3(center.x + (-c - -s), center.y + (-s + -c), 0f));
            _verts.Add(new Vector3(center.x + (c - -s), center.y + (s + -c), 0f));
            _verts.Add(new Vector3(center.x + (c - s), center.y + (s + c), 0f));
            _verts.Add(new Vector3(center.x + (-c - s), center.y + (-s + c), 0f));

            _cols.Add(col);
            _cols.Add(col);
            _cols.Add(col);
            _cols.Add(col);

            _tris.Add(idx);
            _tris.Add(idx + 1);
            _tris.Add(idx + 2);
            _tris.Add(idx);
            _tris.Add(idx + 2);
            _tris.Add(idx + 3);
        }

        private LineRenderer CreateChannelRing(string goName, int sortingOrder)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = false;
            lr.widthMultiplier = 0.055f;
            lr.numCapVertices = 4;
            lr.material = _vfxMat;
            lr.sortingOrder = sortingOrder;
            lr.enabled = false;
            return lr;
        }
    }
}
