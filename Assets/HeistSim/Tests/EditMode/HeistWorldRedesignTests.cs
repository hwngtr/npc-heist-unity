using System.Collections.Generic;
using System.IO;
using System.Linq;
using HeistSim;
using HeistSim.Market;
using HeistSim.Market.Editor;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeistSim.Tests
{
    /// <summary>
    /// Locks the world redesign: twelve distinct department identities, a doorway gap on every room seam,
    /// fixture product bounds, stocked product part counts, clean materials and zero world space text.
    /// </summary>
    public sealed class HeistWorldRedesignTests
    {
        /// <summary>Product part total before the world redesign workstream.</summary>
        private const int ProductPartBaseline = 121;

        /// <summary>Slack for black backing shapes that sit a few hundredths past an authored shell edge.</summary>
        private const float EdgeTolerance = 0.1f;

        [Test]
        public void TwelveRoomSignaturesAreDistinct()
        {
            Assert.That(WorldArtSpec.Signatures.Length, Is.EqualTo(MarketData.Rooms.Length), "one signature per room");
            var patterns = new HashSet<FloorPattern>();
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var room in MarketData.Rooms)
            {
                var signature = WorldArtSpec.For(room.Id);
                Assert.That(signature.Room, Is.EqualTo(room.Id), "signature room id");
                patterns.Add(signature.Pattern);
                var key = signature.Pattern + "|" + signature.Wall + "|" + signature.Floor + "|" + signature.Accent;
                Assert.That(seen.Add(key), Is.True, room.Id + " has a unique ink and pattern mix");
            }

            Assert.That(patterns.Count, Is.EqualTo(4), "all four web floor patterns are used");
        }

        [Test]
        public void BuiltScenePaintsEverySignatureAndSignIcon()
        {
            RequireBuiltScene();
            var scene = OpenScene();
            try
            {
                var roots = scene.GetRootGameObjects();
                foreach (var room in MarketData.Rooms)
                {
                    var signature = WorldArtSpec.For(room.Id);
                    var wash = FindDeep(roots, "Wall Wash " + room.Id);
                    Assert.That(wash, Is.Not.Null, "wall wash for " + room.Id);
                    AssertColor(wash, signature.Wall, room.Id + " wall wash");

                    var accent = FindDeep(roots, "Wall Accent " + room.Id);
                    Assert.That(accent, Is.Not.Null, "wall accent for " + room.Id);
                    AssertColor(accent, signature.Accent, room.Id + " wall accent");

                    Assert.That(FindDeep(roots, "Floor Pattern " + room.Id), Is.Not.Null, "floor pattern for " + room.Id);
                    Assert.That(FindDeep(roots, "Aisle Sign " + room.Id), Is.Not.Null, "aisle sign for " + room.Id);

                    var icon = FindDeep(roots, "Icon " + room.Id);
                    Assert.That(icon, Is.Not.Null, "aisle icon for " + room.Id);
                    AssertColor(icon, signature.Accent, room.Id + " aisle icon");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void EverySeamHasAKeyInkThresholdAndADoorwayGap()
        {
            RequireBuiltScene();
            var scene = OpenScene();
            try
            {
                var roots = scene.GetRootGameObjects();
                var gaps = FindDeep(roots, "Door Gaps");
                Assert.That(gaps, Is.Not.Null, "door gaps group missing");
                var expected = (MarketData.Columns - 1) * MarketData.Rows + (MarketData.Rows - 1) * MarketData.Columns;
                Assert.That(gaps.childCount, Is.EqualTo(expected), "one gap per room seam");

                var low = (double)MarketData.DoorwayBandMin;
                var high = (double)MarketData.DoorwayBandMax;
                foreach (Transform gap in gaps)
                {
                    var vertical = gap.localScale.x < gap.localScale.z;
                    var along = vertical ? gap.position.z : gap.position.x;
                    var origin = Mathf.Floor(along / MarketData.RoomTiles) * MarketData.RoomTiles;
                    var local = along - origin;
                    Assert.That(local, Is.InRange(low, high), gap.name + " sits in the walkable band");
                    AssertColor(gap, InkRole.Sun, gap.name + " gap marker");
                }

                var dividers = FindDeep(roots, "Room Dividers");
                Assert.That(dividers, Is.Not.Null, "room dividers group missing");
                foreach (Transform stub in dividers)
                {
                    AssertColor(stub, InkRole.Key, stub.name + " threshold ink");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void BuiltSceneHasTheLandmarkGroups()
        {
            RequireBuiltScene();
            var scene = OpenScene();
            try
            {
                var roots = scene.GetRootGameObjects();
                Assert.That(FindDeep(roots, "Entrance Glow"), Is.Not.Null, "entrance glow");
                Assert.That(FindDeep(roots, "Checkout Canopy"), Is.Not.Null, "checkout canopy");
                Assert.That(FindDeep(roots, "Back Room Props"), Is.Not.Null, "back room pallets and crates");
                Assert.That(FindDeep(roots, "EAS Silhouette"), Is.Not.Null, "EAS gate silhouette");
                Assert.That(FindDeep(roots, "Window r1 Frame"), Is.Not.Null, "entrance window");
                Assert.That(FindDeep(roots, "Window r6 Frame"), Is.Not.Null, "checkout window");

                var bulbs = new List<Transform>();
                CollectNamed(roots, "Bulb r", bulbs);
                Assert.That(bulbs.Count, Is.EqualTo(MarketData.Rooms.Length), "one bulb per department");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [TestCase("m_gondola1")]
        [TestCase("m_chill1")]
        [TestCase("m_produce1")]
        [TestCase("m_bakery")]
        [TestCase("d_cart1")]
        [TestCase("d_bs1")]
        [TestCase("d_freezer1")]
        [TestCase("m_checkout1")]
        [TestCase("m_eas1")]
        [TestCase("m_pallet1")]
        public void NamedFixtureVisualPartsStayInsideTheBlockedFootprint(string id)
        {
            var fixture = MarketData.Fixture(id);
            var bounds = RenderBounds(fixture);
            var rect = TileRect(MarketGrid.FootprintTiles(fixture));
            Assert.That(bounds.min.x, Is.GreaterThanOrEqualTo(rect.xMin - EdgeTolerance), id + " min x");
            Assert.That(bounds.max.x, Is.LessThanOrEqualTo(rect.xMax + EdgeTolerance), id + " max x");
            Assert.That(bounds.min.z, Is.GreaterThanOrEqualTo(rect.yMin - EdgeTolerance), id + " min z");
            Assert.That(bounds.max.z, Is.LessThanOrEqualTo(rect.yMax + EdgeTolerance), id + " max z");
            Assert.That(MarketGrid.FootprintTiles(fixture).Count, Is.GreaterThan(0), id + " blocks at least one tile");
        }

        [Test]
        public void StockedProductPartsExceedTheRedesignBaseline()
        {
            var total = CountProductParts();
            Assert.That(total, Is.GreaterThan(ProductPartBaseline), "total camera facing product parts");

            Assert.That(ProductPartsFor("gondola-shelf"), Is.GreaterThanOrEqualTo(34), "gondola cartons and cans");
            Assert.That(ProductPartsFor("chilled-case"), Is.GreaterThanOrEqualTo(30), "chilled gable packs and tubs");
            Assert.That(ProductPartsFor("produce-island"), Is.GreaterThanOrEqualTo(20), "heaped fruit piles");
            Assert.That(ProductPartsFor("apple-crate"), Is.GreaterThanOrEqualTo(15), "apple piles");
            Assert.That(ProductPartsFor("shopping-cart"), Is.GreaterThanOrEqualTo(14), "cart groceries");
            Assert.That(ProductPartsFor("freezer-chest"), Is.GreaterThanOrEqualTo(7), "frozen boxes and tubs");
            Assert.That(ProductPartsFor("beverage-shelf"), Is.GreaterThanOrEqualTo(5), "bottles and cartons");
        }

        [Test]
        public void RedesignedSceneHasNoWorldTextAndNoMissingMaterials()
        {
            RequireBuiltScene();
            var scene = OpenScene();
            try
            {
                var roots = scene.GetRootGameObjects();
                var hud = roots.SelectMany(root => root.GetComponentsInChildren<MarketHUD>(true)).FirstOrDefault();
                Assert.That(hud, Is.Not.Null, "hud root missing");

                var worldText = roots.SelectMany(root => root.GetComponentsInChildren<TextMeshPro>(true)).ToArray();
                Assert.That(worldText, Is.Empty, "no world space TextMeshPro components");

                var allText = roots.SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true)).ToArray();
                foreach (var text in allText)
                {
                    Assert.That(IsDescendantOf(text.transform, hud.transform), Is.True, text.name + " stays inside the HUD");
                }

                var renderers = roots.SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true)).ToArray();
                Assert.That(renderers.Length, Is.GreaterThan(300), "built scene renderer count");
                foreach (var renderer in renderers)
                {
                    Assert.That(renderer.sharedMaterial, Is.Not.Null, renderer.name + " has a material");
                    Assert.That(renderer.sharedMaterial.shader, Is.Not.Null, renderer.name + " has a shader");
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static int CountProductParts()
        {
            var total = 0;
            foreach (var entry in FixtureShapeData.Fixtures)
            {
                total += ProductPartsFor(entry.Key);
            }

            return total;
        }

        private static int ProductPartsFor(string kind)
        {
            var count = 0;
            foreach (var part in FixtureShapeData.Fixture(kind))
            {
                if (part.Front)
                {
                    count++;
                }
            }

            return count;
        }

        private static void AssertColor(Transform node, InkRole role, string label)
        {
            var renderer = node.GetComponent<MeshRenderer>();
            Assert.That(renderer, Is.Not.Null, label + " renderer");
            var actual = renderer.sharedMaterial.GetColor("_Color");
            var expected = WorldArtSpec.InkColor(role);
            Assert.That(actual, Is.EqualTo(expected).Within(0.002f), label + " ink");
        }

        private static Bounds RenderBounds(Fixture fixture)
        {
            var root = DetailFactory.BuildFixture(fixture.Kind, fixture.Face);
            try
            {
                var position = MarketLayout.ItemPosition(fixture);
                root.transform.position = new Vector3(position.x, 0f, position.y);
                var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers.Length, Is.GreaterThan(0), fixture.Id + " renderers");
                var bounds = renderers[0].bounds;
                for (var i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }

                return bounds;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Rect TileRect(List<GridPos> tiles)
        {
            var minX = int.MaxValue;
            var maxX = int.MinValue;
            var minY = int.MaxValue;
            var maxY = int.MinValue;
            foreach (var tile in tiles)
            {
                minX = Mathf.Min(minX, tile.X);
                maxX = Mathf.Max(maxX, tile.X);
                minY = Mathf.Min(minY, tile.Y);
                maxY = Mathf.Max(maxY, tile.Y);
            }

            return Rect.MinMaxRect(minX, minY, maxX + 1, maxY + 1);
        }

        private static Transform FindDeep(GameObject[] roots, string name)
        {
            foreach (var root in roots)
            {
                var found = FindDeep(root.transform, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void CollectNamed(GameObject[] roots, string prefix, List<Transform> found)
        {
            foreach (var root in roots)
            {
                CollectNamed(root.transform, prefix, found);
            }
        }

        private static void CollectNamed(Transform root, string prefix, List<Transform> found)
        {
            if (root.name.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                found.Add(root);
            }

            for (var i = 0; i < root.childCount; i++)
            {
                CollectNamed(root.GetChild(i), prefix, found);
            }
        }

        private static bool IsDescendantOf(Transform candidate, Transform ancestor)
        {
            for (var node = candidate; node != null; node = node.parent)
            {
                if (node == ancestor)
                {
                    return true;
                }
            }

            return false;
        }

        private static Scene OpenScene()
        {
            return EditorSceneManager.OpenScene(MarketSceneSpec.ScenePath, OpenSceneMode.Additive);
        }

        private static void RequireBuiltScene()
        {
            if (!File.Exists(MarketSceneSpec.ScenePath))
            {
                Assert.Ignore("Market scene not built yet, run Heist/Market/Build Level Scene.");
            }
        }
    }
}
