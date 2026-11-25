using System.IO;
using HeistSim.Market2D;
using HeistSim.Market2D.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace HeistSim.Tests
{
    /// <summary>Verifies the 2D Riso Press projection math, baked sprite manifest, and split-screen UI Toolkit prototype.</summary>
    public sealed class Market2DPrototypeTests
    {
        [Test]
        public void ScreenInputToGridMapsCardinalKeysToScreenDirections()
        {
            var upGrid = Iso2DMath.ScreenInputToGrid(0f, 1f);
            var upWorld = Iso2DMath.TileToWorld(upGrid.x, upGrid.y);
            Assert.That(upWorld.y, Is.GreaterThan(0.1f));
            Assert.That(Mathf.Abs(upWorld.x), Is.LessThan(0.01f));

            var rightGrid = Iso2DMath.ScreenInputToGrid(1f, 0f);
            var rightWorld = Iso2DMath.TileToWorld(rightGrid.x, rightGrid.y);
            Assert.That(rightWorld.x, Is.GreaterThan(0.1f));
            Assert.That(Mathf.Abs(rightWorld.y), Is.LessThan(0.01f));
        }

        [Test]
        public void DepthOrderIncreasesFromBackToFront()
        {
            Assert.That(Iso2DMath.DepthOrder(5f, 5f), Is.GreaterThan(Iso2DMath.DepthOrder(2f, 2f)));
            Assert.That(Iso2DMath.RoomOrder(0, 0), Is.LessThan(Iso2DMath.ConeSortingOrder));
            Assert.That(Iso2DMath.ConeSortingOrder, Is.LessThan(Iso2DMath.DepthOrder(0f, 0f)));
        }

        [Test]
        public void BakedSpriteManifestAndUiAssetsExist()
        {
            Assert.That(File.Exists(Market2DBuilder.ManifestPath), Is.True, "sprite-manifest.json missing");
            Assert.That(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Market2DBuilder.UxmlPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Market2DBuilder.ThemePath), Is.Not.Null);
        }

        [Test]
        public void BuiltMarket2DSceneHasAllRoomsFixturesAndSplitScreenUi()
        {
            Assert.That(File.Exists(Market2DBuilder.ScenePath), Is.True, "Run Heist/Market 2D/Build Prototype Scene");
            EditorSceneManager.OpenScene(Market2DBuilder.ScenePath, OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<Market2DRunner>();
            Assert.That(runner, Is.Not.Null);
            Assert.That(runner.mainCamera, Is.Not.Null);
            Assert.That(runner.mainCamera.orthographic, Is.True);
            Assert.That(runner.mainCamera.rect.width, Is.EqualTo(1f).Within(0.01f), "Full-width camera prevents black seams beside the resolution-scaled editorial panel");
            Assert.That(runner.playerSprite, Is.Not.Null);
            Assert.That(runner.guardSprite, Is.Not.Null);
            Assert.That(runner.staffSprite, Is.Not.Null);
            Assert.That(runner.lootFixtures.Length, Is.EqualTo(MarketData.Loot.Length));
            Assert.That(GameObject.Find("LootMarkers"), Is.Null, "Detached floating LootMarkers must not exist");
            foreach (var lf in runner.lootFixtures)
            {
                Assert.That(lf, Is.Not.Null);
                Assert.That(lf.stockedSprite, Is.Not.Null, lf.fixtureId + " stocked sprite");
                Assert.That(lf.emptySprite, Is.Not.Null, lf.fixtureId + " empty/stolen sprite");
                Assert.That(lf.transform.parent.name, Is.EqualTo("Fixtures"), "LootableFixture2D must live directly on the real fixture");
            }

            Assert.That(runner.cctvCones.Length, Is.EqualTo(MarketData.Cameras.Length));
            Assert.That(File.Exists(Market2DSpriteAtlasPrebuild.FixturesAtlasPath), Is.True, "Fixtures SpriteAtlas V2 missing");
            Assert.That(File.Exists(Market2DSpriteAtlasPrebuild.ActorsAtlasPath), Is.True, "Actors SpriteAtlas V2 missing");

            var doc = Object.FindAnyObjectByType<UIDocument>();
            Assert.That(doc, Is.Not.Null);
            Assert.That(doc.panelSettings, Is.Not.Null);
            Assert.That(doc.visualTreeAsset, Is.Not.Null);
        }
    }
}
