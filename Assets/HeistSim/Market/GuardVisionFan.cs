using System.Collections.Generic;
using HeistSim.Arena;
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

        /// <summary>Half angle of the fan in radians before state widening.</summary>
        public float halfAngle = 0.6f;

        /// <summary>Sight range of the fan in tiles.</summary>
        public float range = 5.4f;

        /// <summary>Minimum alpha of the fan fill, kept for Inspector tuning.</summary>
        public float alpha = 0.44f;

        private const int Segments = 24;
        private Mesh _mesh;
        private Material _runtimeMaterial;
        private float _builtHalfAngle = -1f;

        /// <summary>Maps a guard state name to its shared palette ink.</summary>
        public static Color TintFor(string guardState)
        {
            return guardState switch
            {
                "attack" => ArenaSpec.CoralInk,
                "chase" => ArenaSpec.Coral,
                "investigate" => ArenaSpec.Sun,
                "search" => ArenaSpec.Sun,
                "idle" => ArenaSpec.InkBlue2,
                _ => ArenaSpec.Teal,
            };
        }

        /// <summary>Fill alpha for a guard state, always stronger than the old flat 0.34.</summary>
        public static float AlphaFor(string guardState)
        {
            return guardState switch
            {
                "attack" => 0.60f,
                "chase" => 0.54f,
                "investigate" => 0.48f,
                "search" => 0.48f,
                "idle" => 0.42f,
                _ => 0.44f,
            };
        }

        /// <summary>Fan width multiplier for a guard state, widening on alert states.</summary>
        public static float WidthFor(string guardState)
        {
            return guardState switch
            {
                "attack" => 1.20f,
                "chase" => 1.18f,
                "investigate" => 1.08f,
                "search" => 1.08f,
                _ => 1f,
            };
        }

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

        /// <summary>Places and tints the fan for the current guard facing and state name.</summary>
        public void Apply(float angleRadians, float sightHalfAngle, float sightRange, string guardState)
        {
            Ensure();
            var effectiveHalf = Mathf.Clamp(sightHalfAngle * WidthFor(guardState), 0.08f, Mathf.PI * 0.9f);
            if (!Mathf.Approximately(effectiveHalf, _builtHalfAngle))
            {
                Rebuild(effectiveHalf);
            }

            range = Mathf.Max(0.5f, sightRange);
            transform.localScale = new Vector3(range, 1f, range);
            transform.rotation = Quaternion.Euler(0f, -angleRadians * Mathf.Rad2Deg, 0f);
            var tint = TintFor(guardState);
            var fill = Mathf.Max(alpha, AlphaFor(guardState));
            if (_runtimeMaterial != null)
            {
                _runtimeMaterial.color = new Color(tint.r, tint.g, tint.b, fill);
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
