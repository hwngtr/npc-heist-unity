using UnityEngine;

namespace HeistSim.Market2D
{
    /// <summary>
    /// Floor-projected 2D isometric vision fan for the store detective and CCTV cameras.
    /// Uses Shadowcast2D ray-wall clipping against NavGrid plus a crisp high-contrast perimeter
    /// outline so exact sight ranges read unmistakably against every supermarket floor tile.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class Iso2DVisionCone : MonoBehaviour
    {
        private const int Segments = 44;

        private Mesh _mesh;
        private MeshRenderer _renderer;
        private LineRenderer _outline;
        private Vector3[] _vertices;
        private Color[] _colors;
        private int[] _triangles;
        private Vector3[] _outlinePts;

        private void Awake()
        {
            EnsureMesh();
        }

        /// <summary>Initializes the shared mesh, material, and crisp perimeter outline.</summary>
        public void EnsureMesh(Material material = null)
        {
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "Iso2D Vision Cone" };
                _vertices = new Vector3[Segments + 2];
                _colors = new Color[Segments + 2];
                _triangles = new int[Segments * 3];
                _outlinePts = new Vector3[Segments + 3];
                for (var i = 0; i < Segments; i++)
                {
                    _triangles[i * 3 + 0] = 0;
                    _triangles[i * 3 + 1] = i + 1;
                    _triangles[i * 3 + 2] = i + 2;
                }

                _mesh.vertices = _vertices;
                _mesh.colors = _colors;
                _mesh.triangles = _triangles;
                GetComponent<MeshFilter>().sharedMesh = _mesh;
            }

            _renderer = GetComponent<MeshRenderer>();
            if (material != null)
            {
                _renderer.sharedMaterial = material;
            }
            else if (_renderer.sharedMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    _renderer.sharedMaterial = new Material(shader);
                }
            }

            _renderer.sortingOrder = Iso2DMath.ConeSortingOrder;

            if (_outline == null)
            {
                _outline = GetComponent<LineRenderer>();
                if (_outline == null)
                {
                    _outline = gameObject.AddComponent<LineRenderer>();
                }
            }

            _outline.useWorldSpace = false;
            _outline.loop = false;
            _outline.widthMultiplier = 0.042f;
            _outline.numCornerVertices = 2;
            _outline.numCapVertices = 2;
            _outline.sharedMaterial = _renderer.sharedMaterial;
            _outline.sortingOrder = Iso2DMath.ConeSortingOrder + 1;
        }

        /// <summary>
        /// Updates the fan geometry, translucent vertex tint, and crisp perimeter outline around (originI, originJ).
        /// When grid is provided, every ray is shadow-clipped against vision-blocking walls and fixtures.
        /// </summary>
        public void SetFan(
            float originI,
            float originJ,
            float facingAngle,
            float halfAngle,
            float range,
            Color color,
            NavGrid grid = null,
            Color? outlineColor = null,
            bool ignoreOriginWall = false)
        {
            EnsureMesh();
            var originWorld = Iso2DMath.TileToWorld(originI, originJ, 0f);
            transform.position = originWorld;

            _vertices[0] = Vector3.zero;
            _colors[0] = color;
            _outlinePts[0] = Vector3.zero;

            for (var s = 0; s <= Segments; s++)
            {
                var t = (float)s / Segments;
                var a = facingAngle - halfAngle + halfAngle * 2f * t;
                var hitRange = ignoreOriginWall
                    ? Shadowcast2D.CastCctvRayDistance(originI, originJ, a, range)
                    : (grid != null
                        ? Shadowcast2D.CastRayDistance(grid, originI, originJ, a, range, false)
                        : range);

                var tipI = originI + Mathf.Cos(a) * hitRange;
                var tipJ = originJ + Mathf.Sin(a) * hitRange;
                var localPt = Iso2DMath.TileToWorld(tipI, tipJ, 0f) - originWorld;
                _vertices[s + 1] = localPt;
                _outlinePts[s + 1] = localPt;

                var frac = range > 0.01f ? Mathf.Clamp01(hitRange / range) : 1f;
                var alphaScale = Mathf.Lerp(0.95f, 0.58f, frac);
                _colors[s + 1] = new Color(color.r, color.g, color.b, color.a * alphaScale);
            }

            _outlinePts[Segments + 2] = Vector3.zero;

            _mesh.vertices = _vertices;
            _mesh.colors = _colors;
            _mesh.RecalculateBounds();

            if (_outline != null)
            {
                var borderCol = outlineColor ?? new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * 2.4f + 0.35f));
                _outline.positionCount = _outlinePts.Length;
                _outline.SetPositions(_outlinePts);
                _outline.startColor = borderCol;
                _outline.endColor = borderCol;
            }
        }
    }
}
