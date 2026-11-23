using System.IO;
using System.Linq;
using HeistSim.Arena;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace HeistSim.Tests
{
    /// <summary>Arena look scenes exist and keep the root contract the builder guarantees.</summary>
    public sealed class ArenaSmokeTests
    {
        [Test]
        public void SceneAssetsExist()
        {
            RequireBuiltScenes();

            Assert.That(ArenaSpec.ScenePaths.Length, Is.EqualTo(4), "arena look count");
            Assert.That(ArenaSpec.CaptureFiles.Length, Is.EqualTo(4), "arena capture count");

            foreach (var path in ArenaSpec.ScenePaths)
            {
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, path);
            }
        }

        [Test]
        public void RootObjectCountMatchesBuilderContract()
        {
            RequireBuiltScenes();

            foreach (var path in ArenaSpec.ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                try
                {
                    var roots = scene.GetRootGameObjects();
                    Assert.That(roots.Length, Is.EqualTo(ArenaSpec.RootObjectCount), path);
                    foreach (var name in ArenaSpec.RootNames)
                    {
                        Assert.That(roots.Any(root => root.name == name), Is.True, path + " misses root " + name);
                    }
                }
                finally
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void RequireBuiltScenes()
        {
            foreach (var path in ArenaSpec.ScenePaths)
            {
                if (!File.Exists(path))
                {
                    Assert.Ignore("Arena scenes not built yet, run Heist/Arena/Build All Looks.");
                }
            }
        }
    }
}
