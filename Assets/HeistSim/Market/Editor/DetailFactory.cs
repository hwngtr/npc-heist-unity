using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace HeistSim.Market.Editor
{
    /// <summary>
    /// Builds the composed market geometry from FixtureShapeData and the existing flat material assets.
    /// Every call returns a fresh root object with one child per authored part, so the builder stays idempotent.
    /// </summary>
    public static class DetailFactory
    {
        /// <summary>Height of the CCTV body above the floor, matching the mounted camera in the level builder.</summary>
        public const float CameraMountHeight = 2.62f;

        private const float SurfaceBase = 0.95f;
        private const float LootScale = 0.5f;

        private static readonly Dictionary<InkRole, Material> Materials = new Dictionary<InkRole, Material>();

        /// <summary>Creates a fixture root whose children are the web game part list for the kind.</summary>
        public static GameObject BuildFixture(string kindId, string face = "ne")
        {
            var kind = MarketData.Kind(kindId);
            var parts = FixtureShapeData.Fixture(kindId);
            MarketLayout.Footprint(kind, face, out var widthI, out var depthJ);

            var alongX = MarketLayout.SwapsAxes(face);
            var scaleU = alongX ? (float)kind.SizeI : (float)kind.SizeJ;
            var scaleV = alongX ? (float)kind.SizeJ : (float)kind.SizeI;
            var scaleX = widthI / Mathf.Max(0.001f, scaleU);
            var scaleZ = depthJ / Mathf.Max(0.001f, scaleV);
            var baseY = kind.Place == "surface" ? SurfaceBase : 0f;

            var root = new GameObject(kindId + " Detail");
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                var u = part.Offset.x * scaleX;
                var v = part.Offset.z * scaleZ;
                var position = alongX
                    ? new Vector3(u, part.Offset.y + baseY, v)
                    : new Vector3(v, part.Offset.y + baseY, u);
                var size = alongX
                    ? new Vector3(part.Size.x * scaleX, part.Size.y, part.Size.z * scaleZ)
                    : new Vector3(part.Size.z * scaleX, part.Size.y, part.Size.x * scaleZ);
                AddPart(root.transform, i, part, position, size, alongX);
            }

            return root;
        }

        /// <summary>Creates an actor root from the drawBot parts plus the role cap and mark.</summary>
        public static GameObject BuildActor(string role)
        {
            var parts = FixtureShapeData.Actor(role);
            var root = new GameObject(role + " Detail");
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                AddPart(root.transform, i, part, part.Offset, part.Size, true);
            }

            return root;
        }

        /// <summary>Creates a small loot item root for the kind that carries the loot.</summary>
        public static GameObject BuildLoot(string kindId)
        {
            var parts = FixtureShapeData.LootShape(FixtureShapeData.LootKindFor(kindId));
            var root = new GameObject(kindId + " Loot Detail");
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                AddPart(root.transform, i, part, part.Offset * LootScale, part.Size * LootScale, true);
            }

            return root;
        }

        /// <summary>Creates a CCTV rig with a ceiling plate, mount post, body and lens.</summary>
        public static GameObject BuildCamera()
        {
            var root = new GameObject("CCTV Detail");
            var h = CameraMountHeight;
            var parts = new[]
            {
                // web/js/market-app.js line 1029: mount line below the housing, kept as a floor post.
                new DetailPart(DetailPrimitive.Box, new Vector3(0f, h * 0.5f, 0f), new Vector3(0.08f, h, 0.08f), InkRole.Black, false),
                // web/js/market-app.js lines 1017-1018: blue rounded camera body.
                new DetailPart(DetailPrimitive.Box, new Vector3(0f, h, 0f), new Vector3(0.42f, 0.26f, 0.36f), InkRole.Key, false),
                // web/js/market-app.js line 1020: sun coloured lens disc.
                new DetailPart(DetailPrimitive.Disc, new Vector3(0f, h - 0.04f, 0.22f), new Vector3(0.12f, 0.02f, 0.12f), InkRole.Sun, false),
                // web/js/market-app.js line 1030: charcoal ceiling plate.
                new DetailPart(DetailPrimitive.Box, new Vector3(0f, h + 0.14f, 0f), new Vector3(0.5f, 0.04f, 0.3f), InkRole.Black, false),
            };

            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                AddPart(root.transform, i, part, part.Offset, part.Size, true);
            }

            return root;
        }

        private static GameObject AddPart(Transform parent, int index, DetailPart part, Vector3 position, Vector3 size, bool alongX)
        {
            var type = part.Primitive == DetailPrimitive.Box ? PrimitiveType.Cube
                : part.Primitive == DetailPrimitive.Quad ? PrimitiveType.Quad
                : PrimitiveType.Cylinder;

            var child = GameObject.CreatePrimitive(type);
            child.name = index.ToString("00") + " " + part.Primitive + (part.Tiny ? " Detail" : "");
            var collider = child.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            child.transform.SetParent(parent, false);
            child.transform.localPosition = position;
            child.transform.localScale = PartScale(part, size);

            if (type == PrimitiveType.Quad)
            {
                child.transform.localRotation = Quaternion.Euler(0f, alongX ? 0f : -90f, 0f);
                var mesh = child.GetComponent<MeshFilter>().sharedMesh;
                if (mesh != null && mesh.normals.Length > 0 && mesh.normals[0].z < 0f)
                {
                    child.transform.localRotation *= Quaternion.Euler(0f, 180f, 0f);
                }
            }

            child.GetComponent<MeshRenderer>().sharedMaterial = InkMaterial(part.Ink);
            return child;
        }

        private static Vector3 PartScale(DetailPart part, Vector3 size)
        {
            if (part.Primitive == DetailPrimitive.Quad)
            {
                return new Vector3(Mathf.Max(0.04f, size.x), Mathf.Max(0.04f, size.y), 1f);
            }

            var x = Mathf.Max(0.02f, size.x);
            var y = Mathf.Max(0.01f, size.y);
            var z = Mathf.Max(0.02f, size.z);
            if (part.Tiny)
            {
                x = Mathf.Max(x, 0.04f);
                z = Mathf.Max(z, 0.04f);
            }

            return new Vector3(x, y, z);
        }

        private static Material InkMaterial(InkRole role)
        {
            EnsureMaterialFolders();
            if (!Materials.TryGetValue(role, out var material) || material == null)
            {
                Materials.Clear();
                foreach (InkRole value in Enum.GetValues(typeof(InkRole)))
                {
                    Materials[value] = MarketLevelBuilder.FlatMaterial(MaterialName(value), InkColor(value));
                }

                material = Materials[role];
            }

            return material;
        }

        private static void EnsureMaterialFolders()
        {
            if (!AssetDatabase.IsValidFolder(MarketSceneSpec.GeneratedDir))
            {
                AssetDatabase.CreateFolder(MarketSceneSpec.MarketDir, "Generated");
            }

            if (!AssetDatabase.IsValidFolder(MarketSceneSpec.MaterialsDir))
            {
                AssetDatabase.CreateFolder(MarketSceneSpec.GeneratedDir, "Materials");
            }
        }

        private static string MaterialName(InkRole role)
        {
            return role switch
            {
                InkRole.Paper => "MarketWall",
                InkRole.Card => "MarketFloor",
                InkRole.Key => "MarketInkBlue",
                InkRole.Teal => "MarketTeal",
                InkRole.Sun => "MarketSun",
                InkRole.Coral => "MarketCoral",
                InkRole.Black => "MarketBlack",
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown ink role"),
            };
        }

        private static Color InkColor(InkRole role)
        {
            return role switch
            {
                InkRole.Paper => MarketSceneSpec.Paper,
                InkRole.Card => MarketSceneSpec.Card,
                InkRole.Key => MarketSceneSpec.InkBlue,
                InkRole.Teal => MarketSceneSpec.Teal,
                InkRole.Sun => MarketSceneSpec.Sun,
                InkRole.Coral => MarketSceneSpec.Coral,
                InkRole.Black => MarketSceneSpec.Black,
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown ink role"),
            };
        }
    }
}
