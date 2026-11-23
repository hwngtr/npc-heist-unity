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
    /// <summary>Locks the riso HUD redesign to the shared visual system and the snapshot contract.</summary>
    public sealed class HeistHudRedesignTests
    {
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
        public void EveryHeatTierUsesItsMappedInk()
        {
            var expected = new[]
            {
                MarketSceneSpec.InkBlue2,
                MarketSceneSpec.Teal,
                MarketSceneSpec.Sun,
                MarketSceneSpec.Sun,
                MarketSceneSpec.Coral,
                MarketSceneSpec.CoralInk,
            };

            Assert.That(HeistState.Tiers.Length, Is.EqualTo(expected.Length), "six heat tiers");
            for (var i = 0; i < expected.Length; i++)
            {
                AssertColor(HeatNotchColorFor(i), expected[i], "static notch ink for tier " + i);
            }

            var hud = NewHud();
            for (var tier = 0; tier < expected.Length; tier++)
            {
                var snapshot = SnapshotAtTier(tier);
                hud.Refresh(snapshot, MarketActionHint.None, 0f);
                for (var notch = 0; notch < hud.heatNotches.Length; notch++)
                {
                    var ink = HeatNotchColorFor(notch);
                    var alpha = notch <= tier ? 1f : 0.24f;
                    var actual = hud.heatNotches[notch].color;
                    Assert.That(actual.a, Is.EqualTo(alpha).Within(0.001f), "notch " + notch + " alpha at tier " + tier);
                    AssertColor(actual, ink, "notch " + notch + " ink at tier " + tier);
                }
            }
        }

        [Test]
        public void PromptShowsOnlyForInRangeTargets()
        {
            var hud = NewHud();
            var snapshot = SnapshotAtTier(0);
            var reachable = new MarketActionHint(true, "Stealing", 1.4f, Vector2.zero, "Hold E: steal the goods");

            hud.Refresh(snapshot, MarketActionHint.None, 0f);
            Assert.That(hud.promptRect.gameObject.activeSelf, Is.False, "no prompt without a target in range");

            hud.Refresh(snapshot, reachable, 0f);
            Assert.That(hud.promptRect.gameObject.activeSelf, Is.True, "prompt appears for a target in range");
            Assert.That(hud.promptText.text, Is.EqualTo("steal the goods"), "prompt drops the Hold E prefix");

            hud.Refresh(snapshot, MarketActionHint.None, 0f);
            Assert.That(hud.promptRect.gameObject.activeSelf, Is.False, "prompt hides when the target leaves range");

            hud.ShowPrompt(Vector2.zero, string.Empty);
            Assert.That(hud.promptRect.gameObject.activeSelf, Is.False, "an empty label never shows the prompt");
        }

        [Test]
        public void RefusalUsesCoralInkAboveThePrompt()
        {
            var hud = NewHud();
            hud.ShowRefusal("BAG FULL");

            Assert.That(hud.refusalText.gameObject.activeSelf, Is.True, "refusal is visible");
            Assert.That(hud.refusalText.text, Is.EqualTo("BAG FULL"), "refusal carries the reason");
            AssertColor(hud.refusalText.color, MarketSceneSpec.CoralInk, "refusal ink");
            Assert.That(hud.refusalText.rectTransform.parent.name, Is.EqualTo("Refusal Back"), "refusal sits on a card");

            var prompt = HudLayout.Bounds(HudPanel.Prompt, HudLayout.ReferenceWidth, HudLayout.ReferenceHeight);
            var refusal = HudLayout.Bounds(HudPanel.Refusal, HudLayout.ReferenceWidth, HudLayout.ReferenceHeight);
            Assert.That(refusal.yMin, Is.GreaterThan(prompt.yMax), "refusal line sits above the prompt");
        }

        [Test]
        public void FeedNeverExceedsCapAndDecays()
        {
            var hud = NewHud();
            for (var i = 0; i < 12; i++)
            {
                hud.PushFeed(i % 2 == 0 ? "ALARM" : "STEAL", "entry " + i);
            }

            Assert.That(hud.feedBadges.Length, Is.EqualTo(5), "five badge rows");
            Assert.That(hud.feedMessages.Length, Is.EqualTo(5), "five message rows");
            Assert.That(hud.feedMessages[0].text, Is.EqualTo("entry 11"), "newest entry first");
            Assert.That(hud.feedMessages[4].text, Is.EqualTo("entry 7"), "oldest kept entry at the cap");

            var filled = 0;
            foreach (var line in hud.feedMessages)
            {
                if (!string.IsNullOrEmpty(line.text))
                {
                    filled++;
                }
            }

            Assert.That(filled, Is.EqualTo(5), "no more than five visible feed lines");

            hud.TickFeed(20f);
            foreach (var line in hud.feedMessages)
            {
                Assert.That(line.text, Is.Empty, "aged feed lines decay away");
            }

            hud.PushFeed("CCTV", "Camera sighting");
            Assert.That(hud.feedMessages[0].text, Is.EqualTo("Camera sighting"), "feed accepts new lines after decay");
        }

        [Test]
        public void ReinitializationDoesNotDuplicateWidgets()
        {
            var hud = NewHud();
            hud.CreateWidgets(Resources.Load<TMP_FontAsset>("MarketSans"));
            Assert.That(hud.transform.Find("Redesign Root"), Is.Not.Null, "design root exists");

            var textCount = hud.GetComponentsInChildren<TMP_Text>(true).Length;
            var imageCount = hud.GetComponentsInChildren<Image>(true).Length;
            Assert.That(textCount, Is.GreaterThan(30), "full widget tree built");

            Assert.That(hud.CreateWidgets(null), Is.False, "second build is refused");
            var awake = typeof(MarketHUD).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(awake, Is.Not.Null, "Awake exists");
            awake.Invoke(hud, null);

            Assert.That(hud.GetComponentsInChildren<TMP_Text>(true).Length, Is.EqualTo(textCount), "no duplicate text widgets");
            Assert.That(hud.GetComponentsInChildren<Image>(true).Length, Is.EqualTo(imageCount), "no duplicate ink widgets");
            Assert.That(hud.heatNotches.Length, Is.EqualTo(6), "six notches after re-init");
            Assert.That(hud.objectiveLines.Length, Is.EqualTo(5), "five objectives after re-init");
        }

        [Test]
        public void PanelsDoNotOverlapAtBothReferenceResolutions()
        {
            var resolutions = new[]
            {
                new Vector2(1600f, 900f),
                new Vector2(1280f, 720f),
            };

            var panels = new[]
            {
                HudPanel.Status, HudPanel.Glance, HudPanel.Feed, HudPanel.Prompt,
                HudPanel.Refusal, HudPanel.Channel, HudPanel.Hint, HudPanel.Zoom,
            };

            foreach (var resolution in resolutions)
            {
                for (var i = 0; i < panels.Length; i++)
                {
                    for (var j = i + 1; j < panels.Length; j++)
                    {
                        var a = HudLayout.Bounds(panels[i], resolution.x, resolution.y);
                        var b = HudLayout.Bounds(panels[j], resolution.x, resolution.y);
                        Assert.That(a.Overlaps(b), Is.False,
                            panels[i] + " overlaps " + panels[j] + " at " + resolution.x + " by " + resolution.y);
                    }
                }
            }
        }

        [Test]
        public void AnchorsAndTypeRolesFollowTheVisualSystem()
        {
            var hud = NewHud();
            var status = hud.transform.Find("Redesign Root/Status Card") as RectTransform;
            Assert.That(status, Is.Not.Null, "status card exists");
            Assert.That(status.anchorMin, Is.EqualTo(new Vector2(0f, 1f)), "status anchors to the top left");
            Assert.That(status.pivot, Is.EqualTo(new Vector2(0f, 1f)), "status pivot at its top left");
            Assert.That(status.sizeDelta.x, Is.EqualTo(360f), "status card is 360 reference pixels wide");

            var glance = hud.transform.Find("Redesign Root/Glance Card") as RectTransform;
            Assert.That(glance.anchorMin, Is.EqualTo(new Vector2(1f, 1f)), "glance anchors to the top right");
            Assert.That(glance.pivot, Is.EqualTo(new Vector2(1f, 1f)), "glance pivot at its top right");

            AssertTextRole(hud, "Status Kicker", HudWidgets.KickerSize, MarketSceneSpec.InkBlue2);
            AssertTextRole(hud, "Headline", HudWidgets.HeadlineSize, MarketSceneSpec.InkBlue);
            AssertTextRole(hud, "Loot", HudWidgets.BodySize, MarketSceneSpec.InkBlue);
            AssertTextRole(hud, "Zone", HudWidgets.MicroSize, MarketSceneSpec.InkBlue2);
        }

        [Test]
        public void ChannelBarReadsRealSnapshotProgress()
        {
            var hud = NewHud();
            var game = NewGame();
            game.SetGuard(1f, 16f);
            game.SetStaff(1f, 16f);
            game.FrozenPatrol = true;
            game.TeleportPlayer(11f, 7.5f);

            var hold = new HeistInput(0f, 0f, action: true, crouch: true);
            for (var i = 0; i < 200 && game.Snapshot().ChannelKind == null; i++)
            {
                game.Step(1f / 60f, hold);
            }

            game.Step(1f / 60f, hold);
            var snapshot = game.Snapshot();
            Assert.That(snapshot.ChannelKind, Is.EqualTo("steal"), "a real channel is running");

            hud.Refresh(snapshot, MarketActionHint.None, 0f);
            var progress = Mathf.Clamp01(snapshot.ChannelProgress / snapshot.ChannelHold);
            var expected = Mathf.Max(0.02f, progress);
            Assert.That(hud.actionBarRoot.activeSelf, Is.True, "channel bar visible while working");
            Assert.That(hud.actionFill.anchorMax.x, Is.EqualTo(expected).Within(0.001f), "bar follows snapshot progress");
            Assert.That(hud.actionText.text, Does.Contain("%"), "channel label reports a percentage");
            AssertColor(hud.actionFill.GetComponent<Image>().color, MarketSceneSpec.Sun, "channel fill ink");
        }

        private MarketHUD NewHud()
        {
            var go = new GameObject("Hud Under Test", typeof(RectTransform), typeof(Canvas));
            _owned.Add(go);
            var hud = go.AddComponent<MarketHUD>();
            hud.CreateWidgets(Resources.Load<TMP_FontAsset>("MarketSans"));
            return hud;
        }

        private HeistGame NewGame()
        {
            var profile = GuardProfile.CreateDefault("standard");
            _owned.Add(profile);
            return new HeistGame(profile);
        }

        private static HeistSnapshot SnapshotAtTier(int tier)
        {
            var profile = GuardProfile.CreateDefault("standard");
            try
            {
                var game = new HeistGame(profile);
                game.SetHeat(tier == 0 ? 0f : HeistState.Tiers[tier].Min + 0.01f);
                return game.Snapshot();
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        private static Color HeatNotchColorFor(int tier)
        {
            return MarketHUD.HeatNotchColor(tier);
        }

        private static void AssertColor(Color actual, Color expected, string message)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.001f), message + " red");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.001f), message + " green");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.001f), message + " blue");
        }

        private static void AssertTextRole(MarketHUD hud, string name, float size, Color color)
        {
            var text = FindDeep(hud.transform, name)?.GetComponent<TMP_Text>();
            Assert.That(text, Is.Not.Null, name + " exists");
            Assert.That(text.fontSize, Is.EqualTo(size), name + " size");
            AssertColor(text.color, color, name + " ink");
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
    }
}
