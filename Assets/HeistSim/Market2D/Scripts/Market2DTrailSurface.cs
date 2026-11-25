using System;
using System.Collections.Generic;
using UnityEngine;

namespace HeistSim.Market2D
{
    /// <summary>
    /// World-anchored 2D trail buffer and isometric floor footprint renderer (SpriteCook Technique 01).
    /// Maintains a scrolling 160x104 single-channel 8-bit trail canvas (fade 3/255 per 30 Hz tick,
    /// white -> black in 85 frames / 2.83 s) plus crisp isometric riso shoe-scuff decals on the
    /// supermarket floor that fade cleanly without ever dragging with the player.
    /// </summary>
    public sealed class Market2DTrailSurface : MonoBehaviour
    {
        /// <summary>Trail canvas width in world trail cells (T01 default: 160).</summary>
        public const int CanvasWidth = 160;

        /// <summary>Trail canvas height in world trail cells (T01 default: 104).</summary>
        public const int CanvasHeight = 104;

        /// <summary>Fade amount per 30 Hz tick out of 255 (3 -> 85 frames to black).</summary>
        public const byte FadePerTick = 3;

        private const int MaxMarks = 64;

        private struct FloorMark
        {
            public bool Active;
            public Vector2 WorldPos;
            public float Angle;
            public float Age;
            public float Lifetime;
            public float Size;
            public Color Color;
        }

        private byte[] _readBuf;
        private byte[] _writeBuf;
        private Vector2Int _origin;
        private bool _hasOrigin;
        private float _canvasAccum;

        private readonly FloorMark[] _marks = new FloorMark[MaxMarks];
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Material _mat;
        private readonly List<Vector3> _verts = new(MaxMarks * 4);
        private readonly List<Color> _cols = new(MaxMarks * 4);
        private readonly List<int> _tris = new(MaxMarks * 6);
        private bool _initialized;
        private float _meshAccum;

        /// <summary>Enables the software 8-bit trail simulation during runtime steps (off by default when only floor quads are rendered).</summary>
        public bool runSoftwareTrailBuffer;

        /// <summary>Current scrolling canvas top-left origin in trail cells.</summary>
        public Vector2Int Origin => _origin;

        /// <summary>Number of currently visible floor footprint/scuff marks.</summary>
        public int ActiveMarkCount
        {
            get
            {
                var c = 0;
                for (var i = 0; i < _marks.Length; i++)
                {
                    if (_marks[i].Active)
                    {
                        c++;
                    }
                }

                return c;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        /// <summary>Initializes the ping-pong 8-bit trail canvas and pooled floor decal mesh.</summary>
        public void EnsureInitialized()
        {
            if (_readBuf == null)
            {
                _readBuf = new byte[CanvasWidth * CanvasHeight];
                _writeBuf = new byte[CanvasWidth * CanvasHeight];
            }

            if (_initialized)
            {
                return;
            }

            _initialized = true;
            var shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _mat = new Material(shader);
            }

            var go = new GameObject("VfxFloorTrailMarks");
            go.transform.SetParent(transform, false);
            var mf = go.AddComponent<MeshFilter>();
            _mesh = new Mesh { name = "RisoFloorTrailMesh" };
            _mesh.MarkDynamic();
            mf.sharedMesh = _mesh;

            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _mat;
            _renderer.sortingOrder = Iso2DMath.ConeSortingOrder - 15;
        }

        /// <summary>Clears both trail buffers and all floor scuff marks (e.g., on New Shift).</summary>
        public void ResetTrail()
        {
            EnsureInitialized();
            Array.Clear(_readBuf, 0, _readBuf.Length);
            Array.Clear(_writeBuf, 0, _writeBuf.Length);
            _hasOrigin = false;
            for (var i = 0; i < _marks.Length; i++)
            {
                _marks[i].Active = false;
            }

            RebuildMesh();
        }

        /// <summary>
        /// Steps one 30 Hz T01 trail canvas tick: shifts previous buffer by scroll delta,
        /// subtracts FadePerTick (3/255), paints faint body dab (peak 0.30 -> level 76) and optional
        /// boot contact dot (peak 1.3 -> flat white 255), then swaps ping-pong buffers.
        /// </summary>
        public void StepCanvasTick(Vector2Int feetCell, bool bootContact, Vector2Int bootOffset)
        {
            EnsureInitialized();
            var nextOrigin = new Vector2Int(feetCell.x - CanvasWidth / 2, feetCell.y - CanvasHeight / 2);
            var scroll = _hasOrigin ? (nextOrigin - _origin) : Vector2Int.zero;
            _hasOrigin = true;

            // 1. Shift + fade
            for (var y = 0; y < CanvasHeight; y++)
            {
                var sy = y + scroll.y;
                var rowOff = y * CanvasWidth;
                if (sy < 0 || sy >= CanvasHeight)
                {
                    Array.Clear(_writeBuf, rowOff, CanvasWidth);
                    continue;
                }

                var srcRow = sy * CanvasWidth;
                for (var x = 0; x < CanvasWidth; x++)
                {
                    var sx = x + scroll.x;
                    if (sx < 0 || sx >= CanvasWidth)
                    {
                        _writeBuf[rowOff + x] = 0;
                    }
                    else
                    {
                        var v = _readBuf[srcRow + sx];
                        _writeBuf[rowOff + x] = v > FadePerTick ? (byte)(v - FadePerTick) : (byte)0;
                    }
                }
            }

            // 2. Body dab every tick (sx 4.5, sy 4.2, peak 0.30 -> level 76)
            PaintDot(_writeBuf, nextOrigin, feetCell.x + 1, feetCell.y + 3, 4.5f, 4.2f, 0.30f);

            // 3. Strong boot contact dot when stride lands (sx 2.7, sy 1.8, peak 1.3 -> level 255)
            if (bootContact)
            {
                PaintDot(_writeBuf, nextOrigin, feetCell.x + bootOffset.x, feetCell.y + bootOffset.y, 2.7f, 1.8f, 1.3f);
            }

            // 4. Ping-pong swap
            (_readBuf, _writeBuf) = (_writeBuf, _readBuf);
            _origin = nextOrigin;
        }

        /// <summary>Reads the current 8-bit trail level (0..255) at a world cell coordinate.</summary>
        public byte SampleWorldCell(int worldX, int worldY)
        {
            if (_readBuf == null || !_hasOrigin)
            {
                return 0;
            }

            var lx = worldX - _origin.x;
            var ly = worldY - _origin.y;
            if (lx < 0 || lx >= CanvasWidth || ly < 0 || ly >= CanvasHeight)
            {
                return 0;
            }

            return _readBuf[ly * CanvasWidth + lx];
        }

        /// <summary>Stamps a crisp fading isometric floor footprint/scuff mark at worldPos.</summary>
        public void StampFootprint(Vector3 worldPos, float moveAngleRad, bool sprint, bool isGuard = false)
        {
            EnsureInitialized();
            var slot = -1;
            var oldest = -1f;
            for (var i = 0; i < _marks.Length; i++)
            {
                if (!_marks[i].Active)
                {
                    slot = i;
                    break;
                }

                if (_marks[i].Age > oldest)
                {
                    oldest = _marks[i].Age;
                    slot = i;
                }
            }

            if (slot < 0)
            {
                return;
            }

            var col = isGuard
                ? new Color(RisoPalette.Coral.r, RisoPalette.Coral.g, RisoPalette.Coral.b, 0.24f)
                : sprint
                    ? new Color(RisoPalette.InkBlue.r, RisoPalette.InkBlue.g, RisoPalette.InkBlue.b, 0.34f)
                    : new Color(RisoPalette.InkBlue2.r, RisoPalette.InkBlue2.g, RisoPalette.InkBlue2.b, 0.20f);

            _marks[slot] = new FloorMark
            {
                Active = true,
                WorldPos = new Vector2(worldPos.x, worldPos.y + 0.02f),
                Angle = moveAngleRad,
                Age = 0f,
                Lifetime = sprint ? 2.83f : 2.1f,
                Size = sprint ? 0.085f : 0.062f,
                Color = col,
            };
        }

        /// <summary>Advances floor footprint fade timers and, when enabled, the 30 Hz software trail buffer.</summary>
        public void Step(Vector2 playerTile, bool bootContact, float dt)
        {
            EnsureInitialized();
            if (runSoftwareTrailBuffer)
            {
                var feetCell = new Vector2Int(Mathf.RoundToInt(playerTile.x * 4f), Mathf.RoundToInt(playerTile.y * 4f));

                _canvasAccum += dt;
                const float canvasStep = 1f / 30f;
                var didBoot = false;
                while (_canvasAccum >= canvasStep)
                {
                    _canvasAccum -= canvasStep;
                    StepCanvasTick(feetCell, bootContact && !didBoot, new Vector2Int(2, 0));
                    didBoot = true;
                }

                if (bootContact && !didBoot)
                {
                    StepCanvasTick(feetCell, true, new Vector2Int(2, 0));
                }
            }

            var anyActive = false;
            for (var i = 0; i < _marks.Length; i++)
            {
                if (!_marks[i].Active)
                {
                    continue;
                }

                _marks[i].Age += dt;
                if (_marks[i].Age >= _marks[i].Lifetime)
                {
                    _marks[i].Active = false;
                }
                else
                {
                    anyActive = true;
                }
            }

            _meshAccum += dt;
            if (bootContact || (!anyActive && _verts.Count > 0) || (anyActive && _meshAccum >= 0.04f))
            {
                _meshAccum = 0f;
                RebuildMesh();
            }
        }

        private static void PaintDot(byte[] buf, Vector2Int origin, float worldX, float worldY, float sx, float sy, float peak)
        {
            var lx = worldX - origin.x;
            var ly = worldY - origin.y;
            var rx = Mathf.CeilToInt(sx * 2.5f);
            var ry = Mathf.CeilToInt(sy * 2.5f);
            var minX = Mathf.Max(0, Mathf.FloorToInt(lx) - rx);
            var maxX = Mathf.Min(CanvasWidth - 1, Mathf.CeilToInt(lx) + rx);
            var minY = Mathf.Max(0, Mathf.FloorToInt(ly) - ry);
            var maxY = Mathf.Min(CanvasHeight - 1, Mathf.CeilToInt(ly) + ry);

            var invSx = 1f / Mathf.Max(0.01f, sx);
            var invSy = 1f / Mathf.Max(0.01f, sy);

            for (var y = minY; y <= maxY; y++)
            {
                var dy = (y - ly) * invSy;
                var dy2 = dy * dy;
                var rowOff = y * CanvasWidth;
                for (var x = minX; x <= maxX; x++)
                {
                    var dx = (x - lx) * invSx;
                    var val = Mathf.Min(1f, peak * Mathf.Exp(-(dx * dx + dy2)));
                    var b = (byte)Mathf.RoundToInt(val * 255f);
                    if (b > buf[rowOff + x])
                    {
                        buf[rowOff + x] = b;
                    }
                }
            }
        }

        private void RebuildMesh()
        {
            if (_mesh == null)
            {
                return;
            }

            _verts.Clear();
            _cols.Clear();
            _tris.Clear();

            for (var i = 0; i < _marks.Length; i++)
            {
                var m = _marks[i];
                if (!m.Active)
                {
                    continue;
                }

                var u = Mathf.Clamp01(m.Age / Mathf.Max(0.01f, m.Lifetime));
                var alpha = m.Color.a * (1f - u);
                if (alpha <= 0.004f)
                {
                    continue;
                }

                var col = new Color(m.Color.r, m.Color.g, m.Color.b, alpha);
                var hx = m.Size;
                var hy = m.Size * 0.52f;
                var c = Mathf.Cos(m.Angle);
                var s = Mathf.Sin(m.Angle);
                var idx = _verts.Count;

                AppendRotated(m.WorldPos, -hx, -hy, c, s);
                AppendRotated(m.WorldPos,  hx, -hy, c, s);
                AppendRotated(m.WorldPos,  hx,  hy, c, s);
                AppendRotated(m.WorldPos, -hx,  hy, c, s);

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

            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetColors(_cols);
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateBounds();
        }

        private void AppendRotated(Vector2 center, float lx, float ly, float cos, float sin)
        {
            _verts.Add(new Vector3(center.x + lx * cos - ly * sin, center.y + lx * sin + ly * cos, 0f));
        }
    }
}
