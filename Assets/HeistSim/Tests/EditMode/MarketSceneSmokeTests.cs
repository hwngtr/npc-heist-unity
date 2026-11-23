using System.IO;
using System.Linq;
using HeistSim.Arena;
using HeistSim.Market;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeistSim.Tests
{
    /// <summary>The built market scene keeps the contract the level builder promises.</summary>
    public sealed class MarketSceneSmokeTests
    {
        [Test]
        public void SceneAssetExists()
        {
            RequireBuiltScene();

            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(MarketSceneSpec.ScenePath), Is.Not.Null);
            Assert.That(File.Exists(MarketSceneSpec.ScenePath), Is.True);
        }

        [Test]
        public void FixtureRootCountMatchesMarketData()
        {
            RequireBuiltScene();

            var scene = EditorSceneManager.OpenScene(MarketSceneSpec.ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var fixtures = FindDeep(roots, MarketSceneSpec.FixturesGroup);
                Assert.That(fixtures, Is.Not.Null, "fixture group missing");
                Assert.That(fixtures.childCount, Is.EqualTo(MarketData.Fixtures.Length), "fixture count");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void RunnerAndHudPresent()
        {
            RequireBuiltScene();

            var scene = EditorSceneManager.OpenScene(MarketSceneSpec.ScenePath, OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects();
                var runner = roots.SelectMany(root => root.GetComponentsInChildren<MarketRunner>(true)).FirstOrDefault();
                var hud = roots.SelectMany(root => root.GetComponentsInChildren<MarketHUD>(true)).FirstOrDefault();
                var fan = roots.SelectMany(root => root.GetComponentsInChildren<GuardVisionFan>(true)).FirstOrDefault();

                Assert.That(runner, Is.Not.Null, "runner missing");
                Assert.That(hud, Is.Not.Null, "hud missing");
                Assert.That(fan, Is.Not.Null, "guard vision fan missing");
                Assert.That(runner.playerRoot, Is.Not.Null, "player binding");
                Assert.That(runner.guardRoot, Is.Not.Null, "guard binding");
                Assert.That(runner.staffRoot, Is.Not.Null, "staff binding");
                Assert.That(runner.cameraCones.Count(cone => cone != null), Is.EqualTo(MarketData.Cameras.Length), "camera cone bindings");
                Assert.That(hud.objectiveLines.Count(line => line != null), Is.EqualTo(MarketData.ObjectiveIds.Length), "objective lines");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void CameraCarriesLookDPost()
        {
            RequireBuiltScene();

            var scene = EditorSceneManager.OpenScene(MarketSceneSpec.ScenePath, OpenSceneMode.Additive);
            try
            {
                var cameraObject = scene.GetRootGameObjects().FirstOrDefault(root => root.name == MarketSceneSpec.RootCamera);
                Assert.That(cameraObject, Is.Not.Null, "camera root missing");
                var camera = cameraObject.GetComponent<Camera>();
                Assert.That(camera, Is.Not.Null);
                Assert.That(camera.orthographic, Is.True);

                var post = cameraObject.GetComponent<IsoDitherPost>();
                Assert.That(post, Is.Not.Null, "IsoDitherPost missing");
                Assert.That(post.postMaterial, Is.Not.Null, "post material missing");
                Assert.That(post.bayerScale, Is.EqualTo(2f));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
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
