using System.Collections.Generic;
using UnityEngine;

namespace HeistSim.Market2D
{
    /// <summary>
    /// Fake-3D pixel-art Sprite Stacking renderer implementing SpriteCook Technique 20.
    /// Renders a horizontal slice sheet (ground slice on left, top slice on right) as stacked
    /// rotated layers lifted by round(z * spacing) with vertical camera squash and whole-stack
    /// ground-point sorting order.
    /// </summary>
    public sealed class SpriteStack2D : MonoBehaviour
    {
        /// <summary>Horizontal slice strip texture (width = sliceWidth * sliceCount, height = sliceHeight).</summary>
        public Texture2D sliceSheet;

        /// <summary>Number of vertical layers packed left-to-right in sliceSheet.</summary>
        public int sliceCount = 12;

        /// <summary>Pixels per Unity world unit for the slice sheet.</summary>
        public float pixelsPerUnit = 32f;

        /// <summary>Vertical lift between adjacent slices in art pixels (default 1.0 px).</summary>
        public float spacingPx = 1.0f;

        /// <summary>Vertical camera-angle squash factor (1.0 = top-down, 0.75 = angled, 0.5 = 2:1 iso).</summary>
        [Range(0.25f, 1.0f)]
        public float squash = 0.65f;

        /// <summary>Current facing rotation in degrees (0 = sheet default facing down/+y).</summary>
        public float turnDegrees;

        /// <summary>Base sorting order for the ground slice; slices use baseSortingOrder + z.</summary>
        public int baseSortingOrder = 4000;

        /// <summary>Optional material shared by all slice renderers.</summary>
        public Material sliceMaterial;

        private readonly List<SpriteRenderer> _sliceRenderers = new();
        private readonly List<Sprite> _sliceSprites = new();
        private Transform _stackRoot;
        private bool _built;

        /// <summary>Width of one individual slice in pixels.</summary>
        public int SliceWidth => sliceSheet != null && sliceCount > 0 ? sliceSheet.width / sliceCount : 0;

        /// <summary>Height of one individual slice in pixels.</summary>
        public int SliceHeight => sliceSheet != null ? sliceSheet.height : 0;

        /// <summary>Number of active child slice SpriteRenderers.</summary>
        public int ActiveSliceCount => _sliceRenderers.Count;

        private void Awake()
        {
            if (sliceSheet != null)
            {
                RebuildSlices();
                ApplyTransform();
            }
        }

        /// <summary>Slices sliceSheet left-to-right into nearest-neighbor child SpriteRenderers.</summary>
        public void RebuildSlices()
        {
            ClearSlices();
            if (sliceSheet == null || sliceCount <= 0)
            {
                return;
            }

            sliceSheet.filterMode = FilterMode.Point;
            var w = sliceSheet.width / sliceCount;
            var h = sliceSheet.height;
            if (w <= 0 || h <= 0)
            {
                return;
            }

            if (_stackRoot == null)
            {
                var existing = transform.Find("Slices");
                if (existing != null)
                {
                    _stackRoot = existing;
                }
                else
                {
                    var rootGo = new GameObject("Slices");
                    rootGo.transform.SetParent(transform, false);
                    _stackRoot = rootGo.transform;
                }
            }

            for (var z = 0; z < sliceCount; z++)
            {
                var rect = new Rect(z * w, 0, w, h);
                var sp = Sprite.Create(sliceSheet, rect, new Vector2(0.5f, 0.5f), Mathf.Max(1f, pixelsPerUnit), 0, SpriteMeshType.FullRect);
                sp.name = "Slice_" + z;
                _sliceSprites.Add(sp);

                var go = new GameObject("Slice_" + z);
                go.transform.SetParent(_stackRoot, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sp;
                if (sliceMaterial != null)
                {
                    sr.sharedMaterial = sliceMaterial;
                }

                sr.sortingOrder = baseSortingOrder + z;
                _sliceRenderers.Add(sr);
            }

            _built = true;
            ApplyTransform();
        }

        /// <summary>Updates the stack's facing angle (degrees) and repositions all slices.</summary>
        public void SetTurn(float degrees)
        {
            turnDegrees = degrees;
            ApplyTransform();
        }

        /// <summary>Updates base ground-point sorting order for the entire stack as one unit.</summary>
        public void SetBaseSortingOrder(int order)
        {
            baseSortingOrder = order;
            for (var z = 0; z < _sliceRenderers.Count; z++)
            {
                if (_sliceRenderers[z] != null)
                {
                    _sliceRenderers[z].sortingOrder = baseSortingOrder + z;
                }
            }
        }

        /// <summary>Applies lift, squash, and shared rotation to every slice in the stack.</summary>
        public void ApplyTransform()
        {
            if (!_built)
            {
                return;
            }

            var invPpu = 1f / Mathf.Max(1f, pixelsPerUnit);
            var rot = Quaternion.Euler(0f, 0f, turnDegrees);

            for (var z = 0; z < _sliceRenderers.Count; z++)
            {
                var sr = _sliceRenderers[z];
                if (sr == null)
                {
                    continue;
                }

                var liftPx = ComputeSliceLiftPx(z, spacingPx);
                var t = sr.transform;
                t.localPosition = new Vector3(0f, liftPx * invPpu, 0f);
                // Squash vertically in screen-Y BEFORE lifting (parent squash container pattern approximated via rotated child or matrix)
                t.localRotation = rot;
                t.localScale = Vector3.one;
                sr.sortingOrder = baseSortingOrder + z;
            }

            if (_stackRoot != null)
            {
                // Apply camera-angle vertical squash on the stack container and counter-scale slice lift so lift stays exact in screen pixels
                _stackRoot.localScale = new Vector3(1f, squash, 1f);
                var safeSquash = Mathf.Max(0.1f, squash);
                for (var z = 0; z < _sliceRenderers.Count; z++)
                {
                    var sr = _sliceRenderers[z];
                    if (sr != null)
                    {
                        var liftPx = ComputeSliceLiftPx(z, spacingPx);
                        sr.transform.localPosition = new Vector3(0f, (liftPx * invPpu) / safeSquash, 0f);
                    }
                }
            }
        }

        /// <summary>Exact T20 lift rule: round(z * spacing) in art pixels.</summary>
        public static float ComputeSliceLiftPx(int z, float spacing)
        {
            return Mathf.Round(z * spacing);
        }

        /// <summary>
        /// Computes the expected screen bounding box (width, height) in art pixels for a sprite stack
        /// of sliceWidth x sliceHeight with count slices, given turnDeg, squash, and spacing.
        /// Matches T20 checks: squash=1, turn=0 -> (w, h + (count-1)*spacing); turn=90 -> width = h.
        /// </summary>
        public static Vector2 ComputeFootprintPx(int sliceWidth, int sliceHeight, int count, float turnDeg, float squash, float spacing)
        {
            var rad = turnDeg * Mathf.Deg2Rad;
            var c = Mathf.Abs(Mathf.Cos(rad));
            var s = Mathf.Abs(Mathf.Sin(rad));
            var rotW = sliceWidth * c + sliceHeight * s;
            var rotH = sliceWidth * s + sliceHeight * c;
            var liftTotal = count > 1 ? ComputeSliceLiftPx(count - 1, spacing) : 0f;
            return new Vector2(rotW, rotH * squash + liftTotal);
        }

        /// <summary>Exact T20 velocity-to-turn formula: atan2(vy, vx) - 90 deg (sheet faces +y/down).</summary>
        public static float VelocityToTurnDegrees(Vector2 velocity)
        {
            return Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f;
        }

        /// <summary>
        /// Procedurally builds a crisp 12-slice risograph CCTV security camera turret strip
        /// (24x28 per slice, 288x28 total) facing down (+y) in the sheet for live 360-degree swiveling.
        /// </summary>
        public static Texture2D BuildRisoCctvSliceSheet(out int outSliceCount)
        {
            const int count = 12;
            outSliceCount = count;
            const int w = 24;
            const int h = 28;
            var tex = new Texture2D(w * count, h, TextureFormat.RGBA32, mipChain: false)
            {
                name = "RisoCctvStackSheet",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

            var clear = new Color(0f, 0f, 0f, 0f);
            var pixels = new Color[w * count * h];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = clear;

            var ink = RisoPalette.InkBlue;
            var body = RisoPalette.Paper;
            var stripe = RisoPalette.Sun;
            var led = RisoPalette.Coral;
            var mount = RisoPalette.InkBlue2;

            void SetPx(int z, int x, int y, Color col)
            {
                if (x < 0 || x >= w || y < 0 || y >= h) return;
                pixels[y * (w * count) + (z * w + x)] = col;
            }

            void FillRect(int z, int x0, int y0, int x1, int y1, Color fill, Color border)
            {
                for (var y = y0; y <= y1; y++)
                {
                    for (var x = x0; x <= x1; x++)
                    {
                        var isEdge = x == x0 || x == x1 || y == y0 || y == y1;
                        SetPx(z, x, y, isEdge ? border : fill);
                    }
                }
            }

            for (var z = 0; z < outSliceCount; z++)
            {
                if (z <= 2)
                {
                    // Base swivel bracket stem
                    FillRect(z, 9, 10, 14, 15, mount, ink);
                }
                else if (z <= 9)
                {
                    // Main boxy camera housing pointing toward +y (lens at top/bottom depending on UV)
                    var fill = (z == 6) ? stripe : body;
                    FillRect(z, 6, 6, 17, 20, fill, ink);
                    // Lens barrel protruding forward (+y -> low y in Texture2D bottom-left? Unity y=0 is bottom, so front = y=2..6)
                    if (z >= 4 && z <= 8)
                    {
                        FillRect(z, 9, 2, 14, 6, mount, ink);
                    }
                }
                else
                {
                    // Top visor hood + coral recording LED
                    FillRect(z, 6, 4, 17, 19, body, ink);
                    if (z == 11)
                    {
                        FillRect(z, 10, 8, 13, 11, led, ink);
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return tex;
        }

        private void ClearSlices()
        {
            for (var i = 0; i < _sliceRenderers.Count; i++)
            {
                if (_sliceRenderers[i] != null)
                {
                    if (Application.isPlaying) Destroy(_sliceRenderers[i].gameObject);
                    else DestroyImmediate(_sliceRenderers[i].gameObject);
                }
            }

            for (var i = 0; i < _sliceSprites.Count; i++)
            {
                if (_sliceSprites[i] != null)
                {
                    if (Application.isPlaying) Destroy(_sliceSprites[i]);
                    else DestroyImmediate(_sliceSprites[i]);
                }
            }

            _sliceRenderers.Clear();
            _sliceSprites.Clear();
            _built = false;
        }
    }
}
