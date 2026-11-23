using System.IO;
using System.Linq;
using HeistSim.Market;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeistSim.Tests
{
    /// <summary>Checks that the built market scene paints every department floor panel and keeps all text in the HUD.</summary>
    public sealed class SectionTests
    {
        [Test]
        public void RoomFloorPanelsMatchMarketData()
        {
            RequireBuiltScene();

            var scene = OpenScene();
            try
            {
                var panels = FindDeep(scene.GetRootGameObjects(), "Room Floors");
                Assert.That(panels, Is.Not.Null, "room floors group missing");
                Assert.That(panels.childCount, Is.EqualTo(MarketData.Rooms.Length), "room floor panel count");

                for (var i = 0; i < MarketData.Rooms.Length; i++)
                {
                    var room = MarketData.Rooms[i];
                    var panel = panels.GetChild(i);
                    var expectedX = room.OriginI + MarketData.RoomTiles * 0.5f;
                    var expectedZ = room.OriginJ + MarketData.RoomTiles * 0.5f;
                    Assert.That(panel.name, Is.EqualTo("Room Panel " + room.Id), "panel name at index " + i);
                    Assert.That(panel.position.x, Is.EqualTo(expectedX).Within(0.01f), "panel x for " + room.Id);
                    Assert.That(panel.position.z, Is.EqualTo(expectedZ).Within(0.01f), "panel z for " + room.Id);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void ZoneLineMapsPlayerTileToDepartment()
        {
            foreach (var room in MarketData.Rooms)
            {
                var centre = new Vector2(
                    room.OriginI + MarketData.RoomTiles * 0.5f,
                    room.OriginJ + MarketData.RoomTiles * 0.5f);
                Assert.That(MarketHUD.ZoneName(centre), Is.EqualTo(room.Name), "centre of " + room.Id);

                var nearCorner = new Vector2(room.OriginI + 0.25f, room.OriginJ + 0.25f);
                Assert.That(MarketHUD.ZoneName(nearCorner), Is.EqualTo(room.Name), "corner of " + room.Id);
            }

            Assert.That(
                MarketHUD.ZoneName(new Vector2(-5f, -5f)),
                Is.EqualTo(MarketData.Rooms[0].Name),
                "point outside the floorplan clamps to r1");
            Assert.That(
                MarketHUD.ZoneName(new Vector2(MarketData.WidthTiles + 5f, MarketData.HeightTiles + 5f)),
                Is.EqualTo(MarketHUD.ZoneName(new Vector2(MarketData.WidthTiles - 0.5f, MarketData.HeightTiles - 0.5f))),
                "point outside the floorplan clamps to the last room");
        }

        [Test]
        public void NoWorldSpaceTextOutsideTheHudCanvas()
        {
            RequireBuiltScene();

            var scene = OpenScene();
            try
            {
                var roots = scene.GetRootGameObjects();
                var hud = roots.SelectMany(root => root.GetComponentsInChildren<MarketHUD>(true)).FirstOrDefault();
                Assert.That(hud, Is.Not.Null, "hud missing");
                Assert.That(hud.zoneText, Is.Not.Null, "zone line missing");
                Assert.That(hud.zoneText.text, Does.StartWith("Zone: "), "zone line prefix");

                var worldText = roots.SelectMany(root => root.GetComponentsInChildren<TextMeshPro>(true)).ToArray();
                Assert.That(worldText, Is.Empty, "no world-space TMP labels may remain in the scene");

                var strayText = roots.SelectMany(root => root.GetComponentsInChildren<TMP_Text>(true))
                    .Where(text => !IsDescendantOf(text.transform, hud.transform))
                    .ToArray();
                Assert.That(strayText, Is.Empty, "all TMP text must live under the HUD canvas");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void NoDividerStubInsideDoorwayBand()
        {
            RequireBuiltScene();

            var scene = OpenScene();
            try
            {
                var stubs = FindDeep(scene.GetRootGameObjects(), "Room Dividers");
                Assert.That(stubs, Is.Not.Null, "room dividers group missing");
                var expected = (MarketData.Columns - 1) * MarketData.Rows * 2
                    + (MarketData.Rows - 1) * MarketData.Columns * 2;
                Assert.That(stubs.childCount, Is.EqualTo(expected), "divider stub count");

                var low = (float)MarketData.DoorwayBandMin;
                var high = (float)MarketData.DoorwayBandMax;
                foreach (Transform stub in stubs)
                {
                    var vertical = stub.localScale.x < stub.localScale.z;
                    var half = (vertical ? stub.localScale.z : stub.localScale.x) * 0.5f;
                    var centre = vertical ? stub.position.z : stub.position.x;
                    var origin = Mathf.Floor(centre / MarketData.RoomTiles) * MarketData.RoomTiles;
                    var localFrom = centre - half - origin;
                    var localTo = centre + half - origin;

                    Assert.That(
                        localTo <= low + 0.01f || localFrom >= high - 0.01f,
                        Is.True,
                        stub.name + " sits inside the doorway band " + low + ".." + high);
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
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

        private static void RequireBuiltScene()
        {
            if (!File.Exists(MarketSceneSpec.ScenePath))
            {
                Assert.Ignore("Market scene not built yet, run Heist/Market/Build Level Scene.");
            }
        }
    }
}
