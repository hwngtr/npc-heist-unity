using System.Collections.Generic;
using System.Reflection;
using HeistSim.Market;
using HeistSim.Market.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeistSim.Tests
{
    /// <summary>Locks the HUD text pipeline: baked glyphs, overlay canvas and safe panel bounds.</summary>
    public sealed class HeistTextRenderTests
    {
        private static readonly Vector2[] CheckResolutions =
        {
            new Vector2(1121f, 630f),
            new Vector2(1280f, 720f),
            new Vector2(1600f, 900f),
        };

        private static readonly HudPanel[] CornerPanels =
        {
            HudPanel.Status, HudPanel.Glance, HudPanel.Feed, HudPanel.Prompt,
            HudPanel.Refusal, HudPanel.Channel, HudPanel.Hint, HudPanel.Zoom,
        };

        private readonly List<Object> _owned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var owned in _owned)
            {
                if (owned != null)
                {
                    Object.DestroyImmediate(owned);
                }
            }

            _owned.Clear();
        }

        [Test]
        public void BakedFontAssetHasStaticAsciiAtlas()
        {
            var font = Resources.Load<TMP_FontAsset>("MarketSans");
            Assert.That(font, Is.Not.Null, "MarketSans loads from Resources");
            Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static), "atlas is baked");
            Assert.That(font.characterTable.Count, Is.GreaterThanOrEqualTo(95), "space through tilde are baked");
            Assert.That(font.glyphTable.Count, Is.GreaterThanOrEqualTo(95), "glyphs are baked into the atlas");
            Assert.That(font.atlasTextures, Is.Not.Empty, "atlas texture subasset exists");
            Assert.That(font.atlasTextures[0].width, Is.EqualTo(1024), "atlas width");
            Assert.That(font.atlasTextures[0].height, Is.EqualTo(1024), "atlas height");
            Assert.That(font.HasCharacter('A'), Is.True, "uppercase letters render");
            Assert.That(font.HasCharacter('0'), Is.True, "digits render");
            Assert.That(font.HasCharacter(' '), Is.True, "space renders");
        }

        [Test]
        public void HudCanvasIsOverlayAfterAwake()
        {
            var hud = NewHud(ScreenSpaceBadState: true);
            var canvas = hud.GetComponent<Canvas>();

            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay), "HUD bypasses the camera post pass");
            Assert.That(canvas.worldCamera, Is.Null, "overlay HUD keeps no world camera");
            Assert.That(canvas.sortingOrder, Is.GreaterThan(50), "HUD sorts above the navigation overlay canvas");
        }

        [Test]
        public void NoHudTextReferencesAnEmptyFontAfterAwake()
        {
            var hud = NewHud(ScreenSpaceBadState: true);
            var texts = hud.GetComponentsInChildren<TMP_Text>(true);
            Assert.That(texts.Length, Is.GreaterThan(30), "full widget tree exists");

            foreach (var text in texts)
            {
                Assert.That(text.font, Is.Not.Null, text.name + " has a font");
                Assert.That(text.font.characterTable.Count, Is.GreaterThan(0), text.name + " font has glyphs");
            }
        }

        [Test]
        public void CornerPanelsFitAllCheckResolutions()
        {
            foreach (var resolution in CheckResolutions)
            {
                foreach (var panel in CornerPanels)
                {
                    var bounds = HudLayout.Bounds(panel, resolution.x, resolution.y);
                    Assert.That(HudLayout.FitsScreen(panel, resolution.x, resolution.y), Is.True,
                        panel + " fits at " + resolution.x + " by " + resolution.y
                        + " with bounds " + bounds);
                }

                for (var i = 0; i < CornerPanels.Length; i++)
                {
                    for (var j = i + 1; j < CornerPanels.Length; j++)
                    {
                        var a = HudLayout.Bounds(CornerPanels[i], resolution.x, resolution.y);
                        var b = HudLayout.Bounds(CornerPanels[j], resolution.x, resolution.y);
                        Assert.That(a.Overlaps(b), Is.False,
                            CornerPanels[i] + " overlaps " + CornerPanels[j]
                            + " at " + resolution.x + " by " + resolution.y);
                    }
                }
            }
        }

        private MarketHUD NewHud(bool ScreenSpaceBadState)
        {
            var go = new GameObject("Text Render HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _owned.Add(go);
            var canvas = go.GetComponent<Canvas>();
            if (ScreenSpaceBadState)
            {
                var cameraObject = new GameObject("HUD Camera", typeof(Camera));
                _owned.Add(cameraObject);
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cameraObject.GetComponent<Camera>();
                canvas.sortingOrder = 10;
            }

            var hud = go.AddComponent<MarketHUD>();
            var awake = typeof(MarketHUD).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null, "Awake exists");
            awake.Invoke(hud, null);
            return hud;
        }
    }
}
