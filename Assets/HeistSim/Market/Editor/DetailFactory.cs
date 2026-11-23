using System;
using System.Collections.Generic;
using HeistSim.Arena;
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

        /// <summary>Outward nudge for product parts so they clear the fixture shell on the camera facing side.</summary>
        private const float FrontNudge = 0.02f;

        private static readonly Dictionary<InkRole, Material> Materials = new Dictionary<InkRole, Material>();
        private static Mesh _coneMesh;

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
                // Shape data stores offsets from the back corner, while MarketLayout anchors the root at the
                // footprint centre. Centre each part before scaling so it renders inside its blocked tiles.
                var u = (part.Offset.x - (float)kind.SizeI * 0.5f) * scaleX;
                var v = (part.Offset.z - (float)kind.SizeJ * 0.5f) * scaleZ;
                var position = alongX
                    ? new Vector3(u, part.Offset.y + baseY, v)
                    : new Vector3(v, part.Offset.y + baseY, u);
                if (part.Front)
                {
                    // The front of a fixture is +Z for axis keeping faces and +X for axis swapping faces.
                    if (alongX)
                    {
                        position.z += FrontNudge;
                    }
                    else
                    {
                        position.x += FrontNudge;
                    }

                    position.y += FrontNudge * 0.5f;
                }

                var size = alongX
                    ? new Vector3(part.Size.x * scaleX, part.Size.y, part.Size.z * scaleZ)
                    : new Vector3(part.Size.z * scaleX, part.Size.y, part.Size.x * scaleZ);
                AddPart(root.transform, i, part, position, size, alongX, true);
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

        private static GameObject AddPart(
            Transform parent,
            int index,
            DetailPart part,
            Vector3 position,
            Vector3 size,
            bool alongX,
            bool outline = false)
        {
            var type = part.Primitive switch
            {
                DetailPrimitive.Box => PrimitiveType.Cube,
                DetailPrimitive.Quad => PrimitiveType.Quad,
                DetailPrimitive.Sphere => PrimitiveType.Sphere,
                _ => PrimitiveType.Cylinder,
            };

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

            if (part.Primitive == DetailPrimitive.Cone)
            {
                child.GetComponent<MeshFilter>().sharedMesh = ConeMesh();
            }

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

            if (outline && part.Primitive == DetailPrimitive.Box && !part.Front
                && part.Ink != InkRole.Black
                && size.x >= 0.7f && size.y >= 0.08f && size.z >= 0.25f)
            {
                // Web outlines fixture bodies with black strokes (market-app.js lines 1019, 1042 and 1049).
                // Every broad shell, including paper white cases, gets a slightly larger black backing shape
                // so the body ink reads as a silhouette at gameplay zoom. Backings stay inside the footprint.
                var backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
                backing.name = index.ToString("00") + " Outline";
                var backingCollider = backing.GetComponent<Collider>();
                if (backingCollider != null)
                {
                    UnityEngine.Object.DestroyImmediate(backingCollider);
                }

                backing.transform.SetParent(parent, false);
                backing.transform.localPosition = position;
                backing.transform.localScale = new Vector3(size.x * 1.03f, size.y * 1.04f, size.z * 1.03f);
                backing.GetComponent<MeshRenderer>().sharedMaterial = InkMaterial(InkRole.Black);
            }

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

        /// <summary>
        /// Builds one shared unit cone, one unit tall with its apex at y = 0.5. The cone stands in for gable
        /// tops, bottle shoulders and carrot tips while the seven flat material assets stay unchanged.
        /// </summary>
        private static Mesh ConeMesh()
        {
            if (_coneMesh != null)
            {
                return _coneMesh;
            }

            const int segments = 12;
            var vertices = new Vector3[segments + 2];
            var triangles = new int[segments * 6];
            vertices[0] = new Vector3(0f, 0.5f, 0f);
            vertices[1] = new Vector3(0f, -0.5f, 0f);
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                vertices[i + 2] = new Vector3(Mathf.Cos(angle) * 0.5f, -0.5f, Mathf.Sin(angle) * 0.5f);
            }

            var t = 0;
            for (var i = 0; i < segments; i++)
            {
                var next = (i + 1) % segments;
                triangles[t++] = 0;
                triangles[t++] = next + 2;
                triangles[t++] = i + 2;
                triangles[t++] = 1;
                triangles[t++] = i + 2;
                triangles[t++] = next + 2;
            }

            var mesh = new Mesh { name = "Market Product Cone" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _coneMesh = mesh;
            return mesh;
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
                InkRole.CoralInk => "MarketCoralInk",
                InkRole.InkBlue2 => "MarketInkBlue2",
                InkRole.Black => "MarketBlack",
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown ink role"),
            };
        }

        private static Color InkColor(InkRole role)
        {
            return role switch
            {
                InkRole.Paper => ArenaSpec.Paper,
                InkRole.Card => ArenaSpec.Card,
                InkRole.Key => ArenaSpec.InkBlue,
                InkRole.InkBlue2 => ArenaSpec.InkBlue2,
                InkRole.Teal => ArenaSpec.Teal,
                InkRole.Sun => ArenaSpec.Sun,
                InkRole.Coral => ArenaSpec.Coral,
                InkRole.CoralInk => ArenaSpec.CoralInk,
                InkRole.Black => ArenaSpec.Black,
                _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown ink role"),
            };
        }
    }
}
