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
            Assert.That(runner.guard2Sprite, Is.Not.Null);
            Assert.That(runner.guard2BubbleSprite, Is.Not.Null);
            Assert.That(runner.guard2VisionCone, Is.Not.Null);
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

        [Test]
        public void SpriteCookAssetsManifestAndCustomSpritesAreWired()
        {
            const string manifestPath = "Assets/HeistSim/Market2D/Sprites/spritecook-assets.json";
            Assert.That(File.Exists(manifestPath), Is.True, "spritecook-assets.json missing");
            var manifestJson = File.ReadAllText(manifestPath);
            Assert.That(manifestJson, Does.Contain("\"asset_id\""), "spritecook-assets.json missing asset_id entries");

            Assert.That(File.Exists(Market2DBuilder.ScenePath), Is.True, "Run Heist/Market 2D/Build Prototype Scene");
            EditorSceneManager.OpenScene(Market2DBuilder.ScenePath, OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<Market2DRunner>();
            Assert.That(runner, Is.Not.Null);
            Assert.That(runner.guard2StandSprite, Is.Not.Null, "Distinct Guard 2 sprite missing");
            Assert.That(runner.guard2StandSprite, Is.Not.SameAs(runner.guardStandSprite), "Guard 2 must not reuse Guard 1 sprite");
            Assert.That(runner.guard2Sprite, Is.Not.Null);
            Assert.That(runner.guard2Sprite.color, Is.EqualTo(Color.white), "Guard 2 should use natural SpriteCook colors rather than blue tint");

            Assert.That(runner.guard2WalkFrames, Is.Not.Null);
            Assert.That(runner.guard2WalkFrames.Length, Is.GreaterThanOrEqualTo(4), "Guard 2 walk frames missing");
            Assert.That(runner.playerWalkFrames, Is.Not.Null);
            Assert.That(runner.playerWalkFrames.Length, Is.GreaterThanOrEqualTo(4), "Player walk frames missing");
            Assert.That(runner.playerCrouchWalkFrames, Is.Not.Null);
            Assert.That(runner.playerCrouchWalkFrames.Length, Is.GreaterThanOrEqualTo(4), "Player crouch walk frames missing");
            Assert.That(runner.guardWalkFrames, Is.Not.Null);
            Assert.That(runner.guardWalkFrames.Length, Is.GreaterThanOrEqualTo(4), "Guard 1 walk frames missing");
            Assert.That(runner.staffWalkFrames, Is.Not.Null);
            Assert.That(runner.staffWalkFrames.Length, Is.GreaterThanOrEqualTo(4), "Staff walk frames missing");

            Assert.That(runner.detaggerPadSprite, Is.Not.Null, "Custom detagger pad prop missing");
            Assert.That(runner.decoyCoinSprite, Is.Not.Null, "Custom decoy coin prop missing");
            Assert.That(runner.cctvPanelSprite, Is.Not.Null, "Custom CCTV panel prop missing");

            Assert.That(runner.hud, Is.Not.Null, "HUD controller missing");
            Assert.That(runner.hud.rolePatrolIcon, Is.Not.Null);
            Assert.That(runner.hud.rolePursuerIcon, Is.Not.Null);
            Assert.That(runner.hud.roleInterceptorIcon, Is.Not.Null);
            Assert.That(runner.hud.roleSabotageIcon, Is.Not.Null);
            Assert.That(runner.hud.roleSweeperIcon, Is.Not.Null);
            Assert.That(runner.hud.spiritsTrophyIcon, Is.Not.Null);
            Assert.That(runner.hud.dossierIcon, Is.Not.Null, "SpriteCook dossier emblem missing");
            Assert.That(runner.hud.lootBagIcon, Is.Not.Null, "SpriteCook loot bag emblem missing");
            Assert.That(manifestJson, Does.Contain("\"icon_dossier\""), "spritecook-assets.json missing icon_dossier");
            Assert.That(manifestJson, Does.Contain("\"icon_loot_bag\""), "spritecook-assets.json missing icon_loot_bag");

            runner.hud.BindElements();
            Assert.That(runner.hud.IsDrawerOpen, Is.False, "Shift Dossier drawer must start hidden by default");
            Assert.That(runner.hud.ActiveRightPanelFrac, Is.EqualTo(0f));
            runner.hud.SetDrawerOpen(true);
            Assert.That(runner.hud.IsDrawerOpen, Is.True);
            Assert.That(runner.hud.ActiveRightPanelFrac, Is.GreaterThan(0.2f));
            runner.hud.ToggleDrawer();
            Assert.That(runner.hud.IsDrawerOpen, Is.False);

            const string risoManifestPath = "Assets/HeistSim/Market2D/Sprites/sprite-manifest.json";
            Assert.That(File.Exists(risoManifestPath), Is.True, "Riso sprite-manifest.json missing");
            var risoJson = File.ReadAllText(risoManifestPath);
            Assert.That(risoJson, Does.Contain("\"guard2\""), "Unified Riso manifest must export guard2");
            Assert.That(risoJson, Does.Contain("\"prop_cctv_panel\""), "Unified Riso manifest must export props");
            Assert.That(risoJson, Does.Contain("\"role_patrol\""), "Unified Riso manifest must export HUD role badges");

            Assert.That(GameObject.Find("StackedShoppingCart"), Is.Null, "Duplicate StackedShoppingCart should be removed");
            Assert.That(runner.cctvStacks == null || runner.cctvStacks.Length == 0, Is.True,
                "Cameras should use unified Riso SpriteRenderers instead of SpriteStack2D slices");
            Assert.That(runner.cctvRigs, Is.Not.Null, "runner.cctvRigs missing");
            Assert.That(runner.cctvRigs.Length, Is.EqualTo(MarketData.Cameras.Length), "Each camera must be wired to a Market2DCctvRig");
            foreach (var cam in MarketData.Cameras)
            {
                var camGo = GameObject.Find(cam.Id);
                Assert.That(camGo, Is.Not.Null, cam.Id + " camera object missing");
                Assert.That(camGo.GetComponent<SpriteStack2D>(), Is.Null, cam.Id + " should not use non-Riso SpriteStack2D");
                var rig = camGo.GetComponent<Market2DCctvRig>();
                Assert.That(rig, Is.Not.Null, cam.Id + " Market2DCctvRig missing");
                Assert.That(rig.headFrames, Is.Not.Null.And.Length.EqualTo(8), cam.Id + " must hold 8 directional turret head frames");
                Assert.That(rig.mountRenderer, Is.Not.Null, cam.Id + " mountRenderer missing");
                Assert.That(rig.mountRenderer.sprite, Is.Not.Null, cam.Id + " mount sprite missing");
                Assert.That(rig.headRenderer, Is.Not.Null, cam.Id + " headRenderer missing");
                Assert.That(rig.headRenderer.sprite, Is.Not.Null, cam.Id + " turret head sprite missing");
                Assert.That(rig.beamTether, Is.Not.Null, cam.Id + " optical beam tether missing");
                Assert.That(rig.visionCone, Is.Not.Null, cam.Id + " visionCone missing");
                Assert.That(rig.visionCone.transform.parent, Is.EqualTo(camGo.transform), cam.Id + " visionCone must be a direct child of the camera rig");
            }
        }

        [Test]
        public void ResetFeedImmediatelyClearsStaleLootAndHeatText()
        {
            Assert.That(File.Exists(Market2DBuilder.ScenePath), Is.True, "Run Heist/Market 2D/Build Prototype Scene");
            EditorSceneManager.OpenScene(Market2DBuilder.ScenePath, OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<Market2DRunner>();
            Assert.That(runner, Is.Not.Null);
            runner.hud.BindElements();

            var root = runner.hud.Root;
            var lootBig = root.Q<Label>("loot-big");
            var hudLootPill = root.Q<Label>("hud-loot-pill");
            var hudBagPill = root.Q<Label>("hud-bag-pill");
            var hudHeatFill = root.Q<VisualElement>("hud-heat-fill");
            Assert.That(lootBig, Is.Not.Null);
            Assert.That(hudLootPill, Is.Not.Null);

            var stolenGame = new HeistGame(GuardProfile.CreateDefault("standard"), GuardArchitecture.StateTree, guardCount: 2);
            var dirtySnap = new HeistSnapshot(
                120,
                new Vector2(20f, 13f),
                HeistGame.GuardStart,
                HeistGame.StaffStart,
                "patrol",
                GuardArchitecture.StateTree,
                0.72f,
                4,
                "execute",
                bagCount: 2,
                stolenValue: 100f,
                new[] { "m_spirits", "m_gondola2" },
                stolenGame.Snapshot().Objectives,
                BackupPhase.None,
                null,
                HeistOutcome.None,
                false,
                0,
                0,
                camerasDisabled: false,
                vitrineOpen: true,
                activeTags: 1,
                easAlarmTicks: 0,
                channelKind: null,
                channelProgress: 0f,
                channelHold: 0f,
                ChannelResult.None,
                0,
                null,
                false,
                false,
                false,
                false,
                false,
                false,
                InteractOutcome.None);

            runner.hud.Refresh(dirtySnap, MarketActionHint.None);
            Assert.That(lootBig.text, Is.EqualTo("€100"));
            Assert.That(hudLootPill.text, Is.EqualTo("€100 / €" + MarketData.Quota));

            runner.hud.ResetFeed();
            Assert.That(lootBig.text, Is.EqualTo("€0"), "ResetFeed must clear drawer loot immediately before stealing any new item");
            Assert.That(hudLootPill.text, Is.EqualTo("€0 / €" + MarketData.Quota), "ResetFeed must clear top HUD loot pill immediately");
            Assert.That(hudBagPill.text, Is.EqualTo("0/" + MarketData.BagCapacity));
            Assert.That(hudHeatFill.style.width, Is.EqualTo(new StyleLength(Length.Percent(12f))));

            var freshGame = new HeistGame(GuardProfile.CreateDefault("standard"), GuardArchitecture.StateTree, guardCount: 2);
            runner.hud.Refresh(freshGame.Snapshot(), MarketActionHint.None);
            Assert.That(lootBig.text, Is.EqualTo("€0"), "Refreshing with zero loot after reset must stay €0");
            Assert.That(hudLootPill.text, Is.EqualTo("€0 / €" + MarketData.Quota), "Refreshing top HUD pill with zero loot must stay €0 / €190");
        }

        [Test]
        public void AllMapObjectsAreGroundedAtFloorPlaneWithManifestPivots()
        {
            Assert.That(File.Exists(Market2DBuilder.ScenePath), Is.True, "Run Heist/Market 2D/Build Prototype Scene");
            EditorSceneManager.OpenScene(Market2DBuilder.ScenePath, OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<Market2DRunner>();
            Assert.That(runner, Is.Not.Null);
            Assert.That(runner.cctvPanelSprite.sprite.pixelsPerUnit, Is.EqualTo(128f).Within(0.1f));
            Assert.That(runner.detaggerPadSprite.sprite.pixelsPerUnit, Is.EqualTo(128f).Within(0.1f));
            Assert.That(runner.decoyCoinSprite.sprite.pixelsPerUnit, Is.EqualTo(128f).Within(0.1f));
            Assert.That(runner.cctvPanelSprite.transform.localScale, Is.EqualTo(Vector3.one));
            Assert.That(runner.detaggerPadSprite.transform.localScale, Is.EqualTo(Vector3.one));

            var panelExpected = Iso2DMath.TileToWorld((float)MarketData.Panel.I, (float)MarketData.Panel.J, 0f);
            Assert.That(Vector3.Distance(runner.cctvPanelSprite.transform.position, panelExpected), Is.LessThan(0.01f));

            var detaggerExpected = Iso2DMath.TileToWorld((float)MarketData.Detagger.I, (float)MarketData.Detagger.J, 0f);
            Assert.That(Vector3.Distance(runner.detaggerPadSprite.transform.position, detaggerExpected), Is.LessThan(0.01f));

            foreach (var lf in runner.lootFixtures)
            {
                Assert.That(Mathf.Abs(lf.transform.position.z), Is.LessThan(0.001f), lf.fixtureId + " must rest on floor z=0");
            }

            foreach (var rig in runner.cctvRigs)
            {
                Assert.That(Mathf.Abs(rig.transform.position.z), Is.LessThan(0.001f), rig.name + " must rest on floor z=0");
                Assert.That(rig.mountRenderer.transform.localPosition, Is.EqualTo(Vector3.zero), rig.name + " stanchion mount must rest at local zero");
            }

            Assert.That(Mathf.Abs(runner.playerSprite.transform.position.z), Is.LessThan(0.001f));
            Assert.That(Mathf.Abs(runner.guardSprite.transform.position.z), Is.LessThan(0.001f));
            Assert.That(Mathf.Abs(runner.guard2Sprite.transform.position.z), Is.LessThan(0.001f));
            Assert.That(Mathf.Abs(runner.staffSprite.transform.position.z), Is.LessThan(0.001f));

            var manifestJson = File.ReadAllText(Market2DBuilder.ManifestPath);
            Assert.That(manifestJson, Does.Contain("\"prop_cctv_panel\"").And.Contain("\"pivotY\""));
            Assert.That(manifestJson, Does.Contain("\"prop_decoy_coin\""));
        }

        [Test]
        public void LowWallsAreSegmentedWithHighlightMaterialAndMultiActorSeeThrough()
        {
            Assert.That(File.Exists(Market2DBuilder.ScenePath), Is.True, "Run Heist/Market 2D/Build Prototype Scene");
            EditorSceneManager.OpenScene(Market2DBuilder.ScenePath, OpenSceneMode.Single);

            var runner = Object.FindAnyObjectByType<Market2DRunner>();
            Assert.That(runner, Is.Not.Null);
            var lowWalls = runner.transform.Find("LowWalls");
            Assert.That(lowWalls, Is.Not.Null);
            Assert.That(lowWalls.childCount, Is.GreaterThanOrEqualTo(20), "Interior LowWalls must be split into 0.71-tile segments");

            Iso2DOccluderBounds sampleBounds = null;
            SpriteRenderer sampleWallSr = null;
            for (var i = 0; i < lowWalls.childCount; i++)
            {
                var child = lowWalls.GetChild(i);
                var sr = child.GetComponent<SpriteRenderer>();
                var ob = child.GetComponent<Iso2DOccluderBounds>();
                Assert.That(sr, Is.Not.Null, child.name + " requires SpriteRenderer");
                Assert.That(sr.sharedMaterial, Is.Not.Null);
                Assert.That(sr.sharedMaterial.shader.name, Is.EqualTo("HeistSim/RisoSpriteHighlight"), child.name + " must use RisoSpriteHighlight");
                Assert.That(sr.sharedMaterial.HasProperty("_SeeThroughTarget1"), Is.True);
                Assert.That(sr.sharedMaterial.HasProperty("_SeeThroughTarget2"), Is.True);
                Assert.That(sr.sharedMaterial.HasProperty("_SeeThroughTarget3"), Is.True);
                Assert.That(ob, Is.Not.Null, child.name + " requires Iso2DOccluderBounds");
                Assert.That(ob.isLowWall, Is.True);
                var span = Mathf.Max(ob.maxI - ob.minI, ob.maxJ - ob.minJ);
                Assert.That(span, Is.LessThanOrEqualTo(0.75f), child.name + " segment length must stay narrow for accurate depth sorting");
                if (sampleBounds == null && (ob.maxI - ob.minI) > 0.5f)
                {
                    sampleBounds = ob;
                    sampleWallSr = sr;
                }
            }

            Assert.That(sampleBounds, Is.Not.Null);
            Assert.That(sampleWallSr, Is.Not.Null);

            runner.seeThrough.BindOccluders(runner.transform.Find("Fixtures"), lowWalls);
            Assert.That(runner.seeThrough.TrackedCount, Is.GreaterThan(lowWalls.childCount));

            // Guard (slot 1) standing right behind the far end of the LowWall must sort behind the wall and receive a Bayer cutout
            var behindTile = new Vector2(sampleBounds.maxI - 0.12f, sampleBounds.minJ - 0.18f);
            var behindOrder = Iso2DMath.DepthOrder(behindTile.x, behindTile.y, 2);
            var actors = new[]
            {
                new Market2DSeeThrough.SeeThroughActor(new Vector2(2f, 2f), Iso2DMath.TileToWorld(new Vector2(2f, 2f)), Iso2DMath.DepthOrder(2f, 2f, 1)),
                new Market2DSeeThrough.SeeThroughActor(behindTile, Iso2DMath.TileToWorld(behindTile), behindOrder),
            };
            for (var f = 0; f < 15; f++)
            {
                runner.seeThrough.StepActors(actors, actors.Length, 1f / 30f);
            }

            Assert.That(sampleWallSr.sortingOrder, Is.GreaterThan(behindOrder), "Wall must sort in front of NPC standing behind it");
            var mpb = new MaterialPropertyBlock();
            sampleWallSr.GetPropertyBlock(mpb);
            Assert.That(mpb.GetVector("_SeeThroughTarget1").z, Is.GreaterThan(0.8f), "NPC behind LowWall must activate multi-target Bayer cutout slot 1");

            // Stepping in front of the same LowWall restores sorting in front and clears cutout amount
            var frontTile = new Vector2(sampleBounds.minI + 0.15f, sampleBounds.minJ + 0.25f);
            var frontOrder = Iso2DMath.DepthOrder(frontTile.x, frontTile.y, 2);
            actors[1] = new Market2DSeeThrough.SeeThroughActor(frontTile, Iso2DMath.TileToWorld(frontTile), frontOrder);
            for (var f = 0; f < 15; f++)
            {
                runner.seeThrough.StepActors(actors, actors.Length, 1f / 30f);
            }

            Assert.That(sampleWallSr.sortingOrder, Is.LessThan(frontOrder), "Wall must sort behind NPC walking in front of it");
            sampleWallSr.GetPropertyBlock(mpb);
            Assert.That(mpb.GetVector("_SeeThroughTarget1").z, Is.LessThan(0.05f), "Wall cutout for NPC must fade off when walking in front");
        }
    }
}
