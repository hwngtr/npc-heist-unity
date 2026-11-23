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
    /// <summary>Checks that the built market scene paints and labels every MarketData.Rooms department.</summary>
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
        public void RoomLabelCountMatchesRoomData()
        {
            RequireBuiltScene();

            var scene = OpenScene();
            try
            {
                var labels = FindDeep(scene.GetRootGameObjects(), "Room Labels");
                Assert.That(labels, Is.Not.Null, "room labels group missing");
                Assert.That(labels.childCount, Is.EqualTo(MarketData.Rooms.Length), "room label object count");

                var texts = labels.GetComponentsInChildren<TextMeshPro>(true);
                Assert.That(texts.Length, Is.EqualTo(MarketData.Rooms.Length), "room label component count");
                foreach (var room in MarketData.Rooms)
                {
                    Assert.That(texts.Count(text => text.text == room.Name), Is.EqualTo(1), "label for " + room.Id);
                }
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
