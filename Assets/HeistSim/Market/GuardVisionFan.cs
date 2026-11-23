using System.Collections.Generic;
using UnityEngine;

namespace HeistSim.Market
{
    /// <summary>Flat fan mesh that shows the field of view of the store detective on the floor.</summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class GuardVisionFan : MonoBehaviour
    {
        /// <summary>Base material for the fan, the component clones it so it can tint per frame.</summary>
        public Material fanMaterial;

        /// <summary>Half angle of the fan in radians.</summary>
        public float halfAngle = 0.6f;

        /// <summary>Sight range of the fan in tiles.</summary>
        public float range = 5.4f;

        /// <summary>Alpha of the fan fill.</summary>
        public float alpha = 0.34f;

        private const int Segments = 24;
        private Mesh _mesh;
        private Material _runtimeMaterial;
        private float _builtHalfAngle = -1f;

        private void Awake()
        {
            Ensure();
        }

        /// <summary>Rebuilds the fan geometry for a new half angle.</summary>
        public void Rebuild(float newHalfAngle)
        {
            halfAngle = Mathf.Clamp(newHalfAngle, 0.08f, Mathf.PI * 0.9f);
            _builtHalfAngle = halfAngle;
            _mesh = BuildMesh(halfAngle);
            var filter = GetComponent<MeshFilter>();
            filter.sharedMesh = _mesh;
        }

        /// <summary>Places and tints the fan for the current guard facing and state.</summary>
        public void Apply(float angleRadians, float sightHalfAngle, float sightRange, bool alarmed)
        {
            Ensure();
            if (!Mathf.Approximately(sightHalfAngle, _builtHalfAngle))
            {
                Rebuild(sightHalfAngle);
            }

            range = Mathf.Max(0.5f, sightRange);
            transform.localScale = new Vector3(range, 1f, range);
            transform.rotation = Quaternion.Euler(0f, -angleRadians * Mathf.Rad2Deg, 0f);
            var tint = alarmed ? MarketSceneSpec.Coral : MarketSceneSpec.Teal;
            if (_runtimeMaterial != null)
            {
                _runtimeMaterial.color = new Color(tint.r, tint.g, tint.b, alpha);
            }
        }

        private void Ensure()
        {
            if (_mesh == null || !Mathf.Approximately(halfAngle, _builtHalfAngle))
            {
                Rebuild(halfAngle);
            }

            var renderer = GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (_runtimeMaterial == null)
            {
                _runtimeMaterial = fanMaterial != null
                    ? new Material(fanMaterial) { name = "GuardVisionFan (Instance)" }
                    : new Material(Shader.Find("Sprites/Default")) { name = "GuardVisionFan (Instance)" };
                renderer.sharedMaterial = _runtimeMaterial;
            }
        }

        private static Mesh BuildMesh(float half)
        {
            var vertices = new List<Vector3>(Segments + 2);
            var triangles = new List<int>(Segments * 3);
            vertices.Add(Vector3.zero);
            for (var i = 0; i <= Segments; i++)
            {
                var angle = -half + (2f * half) * (i / (float)Segments);
                vertices.Add(new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            }

            for (var i = 1; i <= Segments; i++)
            {
                triangles.Add(0);
                triangles.Add(i);
                triangles.Add(i + 1);
            }

            var mesh = new Mesh { name = "GuardVisionFan" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
