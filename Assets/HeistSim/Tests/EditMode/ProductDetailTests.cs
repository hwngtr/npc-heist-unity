using System.Collections.Generic;
using System.IO;
using System.Linq;
using HeistSim.Market;
using HeistSim.Market.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeistSim.Tests
{
    /// <summary>
    /// Checks that the five product heavy kinds carry enough rendered parts, that at least one part stands
    /// above the tiny original scale, and that the gameplay camera starts framed on the player.
    /// </summary>
    public sealed class ProductDetailTests
    {
        /// <summary>Height a product must clear to read as a raised prop instead of a buried accent.</summary>
        private const float VisibleHeight = 0.3f;

        [TestCase("gondola-shelf", 28)]
        [TestCase("chilled-case", 30)]
        [TestCase("produce-island", 12)]
        [TestCase("apple-crate", 15)]
        [TestCase("shopping-cart", 29)]
        public void ProductKindsCarryEnoughParts(string kind, int minimum)
        {
            var root = DetailFactory.BuildFixture(kind);
            try
            {
                var children = new List<Transform>();
                foreach (Transform child in root.transform)
                {
                    children.Add(child);
                }

                Assert.That(children.Count, Is.GreaterThanOrEqualTo(minimum), kind + " part count");
                Assert.That(
                    children.Exists(child => child.localPosition.y > VisibleHeight),
                    Is.True,
                    kind + " raised product");
                Assert.That(
                    children.All(child => child.GetComponent<MeshRenderer>() != null),
                    Is.True,
                    kind + " product renderers");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FollowOffsetShowsProductFrontsAndClampCoversStartZoom()
        {
            var offset = MarketRunner.CameraOffset;
            Assert.That(offset.x, Is.GreaterThan(0f), "camera sits on the +X side");
            Assert.That(offset.z, Is.GreaterThan(0f), "camera sits on the +Z side");
            Assert.That(offset.y, Is.GreaterThan(offset.x), "camera looks down from above");
            Assert.That(
                MarketRunner.FollowOrthographicSize,
                Is.InRange(MarketRunner.MinOrthographicSize, MarketRunner.MaxOrthographicSize),
                "start zoom inside wheel clamp");
        }

        [Test]
        public void BuiltSceneCameraFramesPlayerAtGameplayZoom()
        {
            if (!File.Exists(MarketSceneSpec.ScenePath))
            {
                Assert.Ignore("Market scene not built yet, run Heist/Market/Build Level Scene.");
            }

            var scene = EditorSceneManager.OpenScene(MarketSceneSpec.ScenePath, OpenSceneMode.Additive);
            try
            {
                var cameraObject = scene.GetRootGameObjects()
                    .FirstOrDefault(root => root.name == MarketSceneSpec.RootCamera);
                Assert.That(cameraObject, Is.Not.Null, "camera root missing");
                var camera = cameraObject.GetComponent<Camera>();
                Assert.That(camera, Is.Not.Null, "camera missing");
                Assert.That(camera.orthographic, Is.True, "camera is orthographic");
                Assert.That(
                    camera.orthographicSize,
                    Is.EqualTo(MarketRunner.FollowOrthographicSize).Within(0.001f),
                    "gameplay zoom");

                var player = new Vector3(HeistGame.PlayerStart.x, 0f, HeistGame.PlayerStart.y);
                var expected = player + MarketRunner.CameraOffset;
                Assert.That((camera.transform.position - expected).magnitude, Is.LessThan(0.001f), "follow position");
                var toPlayer = (player - camera.transform.position).normalized;
                Assert.That(Vector3.Dot(camera.transform.forward, toPlayer), Is.GreaterThan(0.999f), "looks at player");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
