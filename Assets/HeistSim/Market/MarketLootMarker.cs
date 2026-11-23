using System.Collections.Generic;
using HeistSim.Arena;
using UnityEngine;

namespace HeistSim.Market
{
    /// <summary>Marks a loot disc so the runner can hide it once the good is in the bag.</summary>
    public sealed class MarketLootMarker : MonoBehaviour
    {
        /// <summary>Fixture id of the loot carried by this marker.</summary>
        public string lootId;

        private const int DotSegments = 14;
        private GameObject _tagDot;

        private void Awake()
        {
            EnsureTagDot();
        }

        /// <summary>Shows or hides the small coral dot that marks an active RF tag.</summary>
        public void SetTagVisible(bool visible)
        {
            EnsureTagDot();
            if (_tagDot != null && _tagDot.activeSelf != visible)
            {
                _tagDot.SetActive(visible);
            }
        }

        private void EnsureTagDot()
        {
            if (_tagDot != null)
            {
                return;
            }

            var dot = new GameObject("RF Tag Dot");
            dot.transform.SetParent(transform, false);
            dot.transform.localPosition = new Vector3(0f, 0.09f, 0f);
            dot.transform.localScale = new Vector3(0.17f, 1f, 0.17f);

            var filter = dot.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildDisc();
            var renderer = dot.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var material = new Material(Shader.Find("Sprites/Default"))
            {
                name = "RF Tag Dot (Instance)",
                color = ArenaSpec.Coral,
            };
            renderer.sharedMaterial = material;
            dot.SetActive(false);
            _tagDot = dot;
        }

        private static Mesh BuildDisc()
        {
            var vertices = new List<Vector3>(DotSegments + 1) { Vector3.zero };
            var triangles = new List<int>(DotSegments * 3);
            for (var i = 0; i < DotSegments; i++)
            {
                var angle = i / (float)DotSegments * Mathf.PI * 2f;
                vertices.Add(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            }

            for (var i = 1; i <= DotSegments; i++)
            {
                triangles.Add(0);
                triangles.Add(i == DotSegments ? 1 : i + 1);
                triangles.Add(i);
            }

            var mesh = new Mesh { name = "RF Tag Dot" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
