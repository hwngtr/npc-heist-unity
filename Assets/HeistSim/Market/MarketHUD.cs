using System;
using System.Collections.Generic;
using HeistSim.Market.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeistSim.Market
{
    /// <summary>
    /// Screen space HUD for the market shift. Reads HeistSnapshot only and never mutates gameplay.
    /// The whole widget tree is built at runtime, so the scene needs no new serialized bindings.
    /// </summary>
    public sealed class MarketHUD : MonoBehaviour
    {
        private static readonly string[] ObjectiveLabels =
        {
            "Cut the power",
            "Open the vitrine",
            "Bag 190 in goods",
            "Kill the tags",
            "Reach the gate",
        };

        private const int FeedCapacity = 5;
        private const float FeedSeconds = 14f;
        private const float FeedFadeSeconds = 2.5f;
        private const float RefusalSeconds = 1.1f;
        private const float DetectionFlashSeconds = 0.35f;
        private const string RootName = "Redesign Root";

        /// <summary>Heat tier name, shown beside the six notch spine.</summary>
        public TMP_Text heatText;

        /// <summary>Back of the heat spine, kept for the established binding.</summary>
        public RectTransform heatFill;

        /// <summary>Stolen value against the quota.</summary>
        public TMP_Text lootText;

        /// <summary>Bag slots used.</summary>
        public TMP_Text bagText;

        /// <summary>One body line per objective.</summary>
        public TMP_Text[] objectiveLines = new TMP_Text[0];

        /// <summary>Guard state in the top right glance card.</summary>
        public TMP_Text guardText;

        /// <summary>Staff radio backup status.</summary>
        public TMP_Text backupText;

        /// <summary>Real channel progress label above the Sun bar.</summary>
        public TMP_Text actionText;

        /// <summary>Outcome banner for busted or escaped.</summary>
        public TMP_Text bannerText;

        /// <summary>Bottom right control hint.</summary>
        public TMP_Text hintText;

        /// <summary>Current department name, with the Zone prefix the scene tests require.</summary>
        public TMP_Text zoneText;

        /// <summary>Bottom center prompt card, shown only for an in range target.</summary>
        public RectTransform promptRect;

        /// <summary>Short prompt line beside the Sun E badge.</summary>
        public TMP_Text promptText;

        /// <summary>Short lived refusal line in CoralInk above the prompt.</summary>
        public TMP_Text refusalText;

        /// <summary>Fill of the real channel progress bar.</summary>
        public RectTransform actionFill;

        /// <summary>Root of the channel progress bar.</summary>
        public GameObject actionBarRoot;

        /// <summary>Six colour notches that mirror the heat tier.</summary>
        public Image[] heatNotches = new Image[0];

        /// <summary>Active RF tag count line.</summary>
        public TMP_Text tagText;

        /// <summary>Coloured pip beside the guard readout.</summary>
        public Image glancePip;

        /// <summary>Translucent full screen detection pulse.</summary>
        public Image detectionFlash;

        /// <summary>Four pulsing edge strips shown while the guard chases.</summary>
        public Image[] chaseVignette = new Image[0];

        /// <summary>Badges of the compact feed, newest first.</summary>
        public TMP_Text[] feedBadges = new TMP_Text[0];

        /// <summary>Message lines of the compact feed, newest first.</summary>
        public TMP_Text[] feedMessages = new TMP_Text[0];

        private readonly struct FeedEntry
        {
            public FeedEntry(string badge, string message, float life)
            {
                Badge = badge;
                Message = message;
                Life = life;
            }

            public string Badge { get; }

            public string Message { get; }

            public float Life { get; }
        }

        private readonly List<FeedEntry> _feed = new();

        private RectTransform _redesignRoot;
        private TMP_Text _headline;
        private TMP_Text _controllerBadge;
        private TMP_Text _zoomText;
        private Image _refusalBack;
        private GameObject _bannerRoot;
        private Image[] _objectiveMarks = new Image[0];

        private float _refusalTimer;
        private float _flashTimer;
        private float _pulse;
        private bool _chasing;

        private void Awake()
        {
            var font = Resources.Load<TMP_FontAsset>("MarketSans");
            var existing = transform.Find(RootName) as RectTransform;
            if (existing == null)
            {
                RemoveLegacyChildren();
                CreateWidgets(font);
            }
            else
            {
                _redesignRoot = existing;
                RebindRuntimeReferences();
                LayoutPanels();
            }

            var canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.worldCamera == null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                canvas.worldCamera = Camera.main;
            }
        }

        /// <summary>Builds the full widget tree once and returns false when it already exists.</summary>
        public bool CreateWidgets(TMP_FontAsset font)
        {
            if (_redesignRoot != null || transform.Find(RootName) != null)
            {
                return false;
            }

            font ??= Resources.Load<TMP_FontAsset>("MarketSans");

            var root = HudWidgets.Rect(RootName, transform);
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.pivot = new Vector2(0.5f, 0.5f);
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            _redesignRoot = root;

            CreateAtmosphere(root);
            CreateStatusCard(font, root);
            CreateGlanceCard(font, root);
            CreateFeed(font, root);
            CreatePrompt(font, root);
            CreateChannel(font, root);
            CreateRefusal(font, root);
            CreateHints(font, root);
            CreateBanner(font, root);

            LayoutPanels();
            return true;
        }

        /// <summary>Reapplies every normalized anchor and pivot. Safe to call on resize or re-init.</summary>
        public void LayoutPanels()
        {
            if (_redesignRoot == null)
            {
                return;
            }

            PlaceNamed("Status Card", HudPanel.Status);
            PlaceNamed("Glance Card", HudPanel.Glance);
            PlaceNamed("Feed Card", HudPanel.Feed);
            PlaceNamed("Prompt", HudPanel.Prompt);
            PlaceNamed("Refusal Back", HudPanel.Refusal);
            PlaceNamed("Channel Bar", HudPanel.Channel);
            PlaceNamed("Hint Bar", HudPanel.Hint);
            PlaceNamed("Zoom Bar", HudPanel.Zoom);

            if (detectionFlash != null)
            {
                HudLayout.Place(detectionFlash.rectTransform, HudPanel.Flash);
            }

            if (chaseVignette.Length == 4)
            {
                HudLayout.Place(chaseVignette[0].rectTransform, HudPanel.VignetteTop);
                HudLayout.Place(chaseVignette[1].rectTransform, HudPanel.VignetteBottom);
                HudLayout.Place(chaseVignette[2].rectTransform, HudPanel.VignetteLeft);
                HudLayout.Place(chaseVignette[3].rectTransform, HudPanel.VignetteRight);
            }
        }

        /// <summary>Updates every HUD widget from the snapshot and the probed action hint.</summary>
        public void Refresh(in HeistSnapshot snapshot, in MarketActionHint hint, float actionProgress)
        {
            if (_redesignRoot == null)
            {
                return;
            }

            var tier = Mathf.Clamp(snapshot.Tier, 0, HeistState.Tiers.Length - 1);
            heatText.text = HeistState.Tiers[tier].Label;
            for (var i = 0; i < heatNotches.Length; i++)
            {
                var notch = heatNotches[i];
                if (notch == null)
                {
                    continue;
                }

                var ink = HeatNotchColor(i);
                var alpha = i <= tier ? 1f : 0.24f;
                notch.color = new Color(ink.r, ink.g, ink.b, alpha);
            }

            lootText.text = "Loot: " + Mathf.FloorToInt(snapshot.StolenValue) + " / " + MarketData.Quota;
            bagText.text = "Bag: " + snapshot.BagCount + " / " + MarketData.BagCapacity;
            zoneText.text = "Zone: " + ZoneName(snapshot.Player);

            if (snapshot.Objectives != null)
            {
                for (var i = 0; i < objectiveLines.Length && i < snapshot.Objectives.Count; i++)
                {
                    var objective = snapshot.Objectives[i];
                    objectiveLines[i].text = ObjectiveLabel(objective.Id);
                    if (i < _objectiveMarks.Length && _objectiveMarks[i] != null)
                    {
                        _objectiveMarks[i].color = objective.Done ? MarketSceneSpec.Teal : MarketSceneSpec.Card;
                    }
                }
            }

            guardText.text = StateLabel(snapshot.GuardState);
            _controllerBadge.text = snapshot.Arch == GuardArchitecture.Fsm ? "FSM" : "BT";
            backupText.text = "Backup: " + BackupLabel(snapshot.BackupPhase);
            tagText.text = "RF tags: " + snapshot.ActiveTags;
            tagText.color = snapshot.ActiveTags > 0 ? MarketSceneSpec.Coral : MarketSceneSpec.InkBlue2;

            var glancing = snapshot.DetectedNow
                || snapshot.GuardState == "investigate"
                || snapshot.GuardState == "search"
                || snapshot.GuardState == "chase"
                || snapshot.GuardState == "attack";
            glancePip.gameObject.SetActive(glancing);
            var pipInk = GuardVisionFan.TintFor(snapshot.GuardState);
            glancePip.color = new Color(pipInk.r, pipInk.g, pipInk.b, 0.95f);

            _chasing = snapshot.GuardState == "chase" || snapshot.GuardState == "attack";
            foreach (var strip in chaseVignette)
            {
                if (strip != null && strip.gameObject.activeSelf != _chasing)
                {
                    strip.gameObject.SetActive(_chasing);
                }
            }

            var channelActive = !string.IsNullOrEmpty(snapshot.ChannelKind) && snapshot.ChannelHold > 0f;
            actionBarRoot.SetActive(channelActive);
            if (channelActive)
            {
                var progress = Mathf.Clamp01(snapshot.ChannelProgress / snapshot.ChannelHold);
                actionText.text = ChannelVerb(snapshot.ChannelKind) + " " + Mathf.RoundToInt(progress * 100f) + "%";
                actionFill.anchorMax = new Vector2(Mathf.Max(0.02f, progress), 1f);
            }

            if (hint.Found && !string.IsNullOrEmpty(hint.Prompt))
            {
                promptText.text = ShortPrompt(hint.Prompt);
                promptRect.gameObject.SetActive(true);
            }
            else
            {
                HidePrompt();
            }

            _bannerRoot.SetActive(snapshot.Outcome != HeistOutcome.None);
            _headline.gameObject.SetActive(snapshot.Outcome == HeistOutcome.None);
            if (snapshot.Outcome == HeistOutcome.Busted)
            {
                bannerText.color = MarketSceneSpec.Coral;
                bannerText.text = "BUSTED\nR for a new shift";
            }
            else if (snapshot.Outcome == HeistOutcome.Escaped)
            {
                bannerText.color = MarketSceneSpec.Teal;
                bannerText.text = "ESCAPED\nShift complete, R to run again";
            }

            // actionProgress is accepted for the established runner signature; the real bar
            // reads ChannelProgress and ChannelHold from the snapshot only.
        }

        /// <summary>Places the in-range prompt in its fixed bottom center slot.</summary>
        public void ShowPrompt(Vector2 screenPoint, string label)
        {
            if (string.IsNullOrEmpty(label))
            {
                HidePrompt();
                return;
            }

            promptText.text = ShortPrompt(label);
            promptRect.gameObject.SetActive(true);
        }

        /// <summary>Hides the in-range prompt.</summary>
        public void HidePrompt()
        {
            if (promptRect != null)
            {
                promptRect.gameObject.SetActive(false);
            }
        }

        /// <summary>Starts the short lived CoralInk refusal flash for a real interaction failure.</summary>
        public void ShowRefusal(string reason)
        {
            if (refusalText == null || string.IsNullOrEmpty(reason))
            {
                return;
            }

            refusalText.text = reason;
            refusalText.color = MarketSceneSpec.CoralInk;
            refusalText.gameObject.SetActive(true);
            _refusalBack.gameObject.SetActive(true);
            _refusalTimer = RefusalSeconds;
        }

        /// <summary>Prepends one feed line, keeping the newest five entries.</summary>
        public void PushFeed(string badge, string message)
        {
            if (_feed.Count == FeedCapacity)
            {
                _feed.RemoveAt(_feed.Count - 1);
            }

            _feed.Insert(0, new FeedEntry(badge ?? string.Empty, message ?? string.Empty, FeedSeconds));
            RefreshFeed();
        }

        /// <summary>Clears the feed when a new shift starts.</summary>
        public void ClearFeed()
        {
            _feed.Clear();
            RefreshFeed();
        }

        /// <summary>Ages feed entries so the list decays instead of accumulating forever.</summary>
        public void TickFeed(float seconds)
        {
            if (seconds <= 0f || _feed.Count == 0)
            {
                return;
            }

            for (var i = _feed.Count - 1; i >= 0; i--)
            {
                var entry = _feed[i];
                var life = entry.Life - seconds;
                if (life <= 0f)
                {
                    _feed.RemoveAt(i);
                    continue;
                }

                _feed[i] = new FeedEntry(entry.Badge, entry.Message, life);
            }

            RefreshFeed();
        }

        /// <summary>Starts the brief translucent detection pulse.</summary>
        public void FlashDetection()
        {
            if (detectionFlash == null)
            {
                return;
            }

            _flashTimer = DetectionFlashSeconds;
            detectionFlash.color = HudWidgets.DangerColor(0.14f);
            detectionFlash.gameObject.SetActive(true);
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            _pulse += dt * 6f;
            var wave = 0.5f + 0.5f * Mathf.Sin(_pulse);

            if (_flashTimer > 0f)
            {
                _flashTimer = Mathf.Max(0f, _flashTimer - dt);
                var remaining = _flashTimer / DetectionFlashSeconds;
                var alpha = (0.10f + 0.14f * wave) * remaining;
                detectionFlash.color = HudWidgets.DangerColor(alpha);
                if (_flashTimer <= 0f)
                {
                    detectionFlash.gameObject.SetActive(false);
                }
            }

            if (_chasing && chaseVignette.Length == 4)
            {
                chaseVignette[0].color = HudWidgets.DangerColor(0.10f + 0.08f * wave);
                chaseVignette[1].color = HudWidgets.DangerColor(0.10f + 0.08f * wave);
                chaseVignette[2].color = HudWidgets.ShadowColor(0.12f + 0.10f * (1f - wave));
                chaseVignette[3].color = HudWidgets.ShadowColor(0.12f + 0.10f * (1f - wave));
            }

            if (_refusalTimer > 0f)
            {
                _refusalTimer = Mathf.Max(0f, _refusalTimer - dt);
                var alpha = Mathf.Clamp01(_refusalTimer / 0.3f) * (0.75f + 0.25f * wave);
                var ink = MarketSceneSpec.CoralInk;
                refusalText.color = new Color(ink.r, ink.g, ink.b, alpha);
                var back = HudWidgets.CardColor();
                _refusalBack.color = new Color(back.r, back.g, back.b, HudWidgets.PanelAlpha * alpha);
                if (_refusalTimer <= 0f)
                {
                    refusalText.gameObject.SetActive(false);
                    _refusalBack.gameObject.SetActive(false);
                }
            }

            TickFeed(dt);
        }

        private void CreateAtmosphere(Transform root)
        {
            detectionFlash = HudWidgets.Shape("Detection Flash", root, HudWidgets.DangerColor(0f),
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            detectionFlash.gameObject.SetActive(false);

            var names = new[] { "Vignette Top", "Vignette Bottom", "Vignette Left", "Vignette Right" };
            chaseVignette = new Image[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                var alpha = i < 2 ? HudWidgets.DangerColor(0.12f) : HudWidgets.ShadowColor(0.16f);
                chaseVignette[i] = HudWidgets.Shape(names[i], root, alpha,
                    Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);
                chaseVignette[i].gameObject.SetActive(false);
            }
        }

        private void CreateStatusCard(TMP_FontAsset font, Transform root)
        {
            var panel = HudWidgets.Card("Status Card", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            var panelRect = panel.rectTransform;
            HudWidgets.Kicker("Status Kicker", panelRect, font, "Shift brief", new Vector2(16f, -12f), new Vector2(320f, 16f));
            _headline = HudWidgets.Headline("Headline", panelRect, font, "Frischemarkt", new Vector2(16f, -30f), new Vector2(320f, 36f));

            HudWidgets.Kicker("Heat Kicker", panelRect, font, "Heat", new Vector2(16f, -74f), new Vector2(120f, 16f));
            heatText = HudWidgets.Body("Heat Tier", panelRect, font, HeistState.Tiers[0].Label, new Vector2(196f, -74f), new Vector2(148f, 20f));
            heatText.alignment = TextAlignmentOptions.TopRight;

            var spine = HudWidgets.Shape("Heat Spine Back", panelRect, new Color(MarketSceneSpec.InkBlue2.r, MarketSceneSpec.InkBlue2.g, MarketSceneSpec.InkBlue2.b, 0.25f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -98f), new Vector2(338f, 8f));
            heatFill = spine.rectTransform;
            heatNotches = new Image[HeistState.Tiers.Length];
            for (var i = 0; i < heatNotches.Length; i++)
            {
                heatNotches[i] = HudWidgets.Shape("Heat Notch " + i, panelRect, HeatNotchColor(i),
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(16f + i * 56f, -98f), new Vector2(50f, 8f));
            }

            HudWidgets.Kicker("Loot Kicker", panelRect, font, "Loot / quota", new Vector2(16f, -124f), new Vector2(150f, 16f));
            lootText = HudWidgets.Body("Loot", panelRect, font, "Loot: 0 / " + MarketData.Quota, new Vector2(166f, -122f), new Vector2(178f, 22f));
            lootText.alignment = TextAlignmentOptions.TopRight;

            HudWidgets.Kicker("Bag Kicker", panelRect, font, "Bag", new Vector2(16f, -156f), new Vector2(150f, 16f));
            bagText = HudWidgets.Body("Bag", panelRect, font, "Bag: 0 / " + MarketData.BagCapacity, new Vector2(166f, -154f), new Vector2(178f, 22f));
            bagText.alignment = TextAlignmentOptions.TopRight;

            HudWidgets.Kicker("Objectives Kicker", panelRect, font, "Objectives", new Vector2(16f, -188f), new Vector2(320f, 16f));
            objectiveLines = new TMP_Text[ObjectiveLabels.Length];
            _objectiveMarks = new Image[ObjectiveLabels.Length];
            for (var i = 0; i < ObjectiveLabels.Length; i++)
            {
                var top = -212f - i * 30f;
                HudWidgets.Shape("Objective Mark Back " + (i + 1), panelRect, MarketSceneSpec.InkBlue2,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(16f, top), new Vector2(16f, 16f));
                _objectiveMarks[i] = HudWidgets.Shape("Objective Mark " + (i + 1), panelRect, MarketSceneSpec.Card,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(19f, top - 3f), new Vector2(10f, 10f));
                objectiveLines[i] = HudWidgets.Body("Objective " + (i + 1), panelRect, font, ObjectiveLabels[i],
                    new Vector2(40f, top - 1f), new Vector2(304f, 22f));
            }

            HudWidgets.Kicker("Radio Kicker", panelRect, font, "Radio", new Vector2(16f, -376f), new Vector2(150f, 16f));
            backupText = HudWidgets.Micro("Backup", panelRect, font, "Backup: quiet", new Vector2(150f, -375f), new Vector2(194f, 20f));
            backupText.alignment = TextAlignmentOptions.TopRight;

            HudWidgets.Kicker("Tags Kicker", panelRect, font, "RF tags", new Vector2(16f, -408f), new Vector2(150f, 16f));
            tagText = HudWidgets.Micro("Tags", panelRect, font, "RF tags: 0", new Vector2(150f, -407f), new Vector2(194f, 20f));
            tagText.alignment = TextAlignmentOptions.TopRight;
        }

        private void CreateGlanceCard(TMP_FontAsset font, Transform root)
        {
            var panel = HudWidgets.Card("Glance Card", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            var panelRect = panel.rectTransform;
            HudWidgets.Kicker("Glance Kicker", panelRect, font, "Guard glance", new Vector2(16f, -12f), new Vector2(288f, 16f));
            guardText = HudWidgets.Body("Guard", panelRect, font, "Patrol", new Vector2(16f, -36f), new Vector2(200f, 24f));

            HudWidgets.Shape("Controller Badge Back", panelRect, MarketSceneSpec.InkBlue2,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(250f, -34f), new Vector2(54f, 20f));
            _controllerBadge = HudWidgets.Text("Controller Badge Label", panelRect, font, "FSM", HudWidgets.KickerSize,
                MarketSceneSpec.Card, TextAlignmentOptions.Center, new Vector2(250f, -34f), new Vector2(54f, 20f),
                HudWidgets.KickerTracking, true, true);

            glancePip = HudWidgets.Shape("Glance Pip", panelRect, MarketSceneSpec.Teal,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(224f, -38f), new Vector2(12f, 12f));
            glancePip.gameObject.SetActive(false);

            HudWidgets.Shape("Glance Rule", panelRect, new Color(MarketSceneSpec.InkBlue2.r, MarketSceneSpec.InkBlue2.g, MarketSceneSpec.InkBlue2.b, 0.35f),
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(16f, -68f), new Vector2(-32f, 2f));
            HudWidgets.Kicker("Zone Kicker", panelRect, font, "Department", new Vector2(16f, -84f), new Vector2(288f, 16f));
            zoneText = HudWidgets.Micro("Zone", panelRect, font, "Zone: " + MarketData.Rooms[0].Name, new Vector2(16f, -106f), new Vector2(288f, 20f));
        }

        private void CreateFeed(TMP_FontAsset font, Transform root)
        {
            var panel = HudWidgets.Card("Feed Card", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            var panelRect = panel.rectTransform;
            HudWidgets.Kicker("Feed Kicker", panelRect, font, "Activity feed", new Vector2(12f, -10f), new Vector2(300f, 16f));
            feedBadges = new TMP_Text[FeedCapacity];
            feedMessages = new TMP_Text[FeedCapacity];
            for (var i = 0; i < FeedCapacity; i++)
            {
                var top = -34f - i * 24f;
                feedBadges[i] = HudWidgets.Kicker("Feed Badge " + i, panelRect, font, string.Empty, new Vector2(12f, top), new Vector2(72f, 18f));
                feedBadges[i].color = MarketSceneSpec.CoralInk;
                feedMessages[i] = HudWidgets.Micro("Feed Line " + i, panelRect, font, string.Empty, new Vector2(88f, top), new Vector2(282f, 18f));
            }
        }

        private void CreatePrompt(TMP_FontAsset font, Transform root)
        {
            var panel = HudWidgets.Card("Prompt", root, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            promptRect = panel.rectTransform;
            var badge = HudWidgets.Shape("Prompt Badge", panel.rectTransform, MarketSceneSpec.Sun,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -11f), new Vector2(30f, 30f));
            HudWidgets.Text("Prompt Key", badge.rectTransform, font, "E", 20f, MarketSceneSpec.Black,
                TextAlignmentOptions.Center, Vector2.zero, new Vector2(30f, 30f), 0f, false, true);
            var keyRect = badge.rectTransform.GetChild(0) as RectTransform;
            if (keyRect != null)
            {
                keyRect.anchorMin = Vector2.zero;
                keyRect.anchorMax = Vector2.one;
                keyRect.pivot = new Vector2(0.5f, 0.5f);
                keyRect.anchoredPosition = Vector2.zero;
                keyRect.sizeDelta = Vector2.zero;
            }

            promptText = HudWidgets.Text("Prompt Label", panel.rectTransform, font, string.Empty, HudWidgets.BodySize,
                MarketSceneSpec.InkBlue, TextAlignmentOptions.MidlineLeft, new Vector2(50f, -13f), new Vector2(378f, 26f));
        }

        private void CreateChannel(TMP_FontAsset font, Transform root)
        {
            var bar = HudWidgets.Shape("Channel Bar", root,
                new Color(MarketSceneSpec.InkBlue2.r, MarketSceneSpec.InkBlue2.g, MarketSceneSpec.InkBlue2.b, 0.28f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);
            actionBarRoot = bar.gameObject;
            var fill = HudWidgets.Shape("Channel Fill", bar.rectTransform, MarketSceneSpec.Sun,
                Vector2.zero, new Vector2(0f, 1f), Vector2.zero, Vector2.zero, Vector2.zero);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            actionFill = fill.rectTransform;
            actionFill.anchorMax = new Vector2(0f, 1f);

            actionText = HudWidgets.Micro("Action", bar.rectTransform, font, string.Empty, new Vector2(0f, 18f), new Vector2(440f, 18f));
            actionBarRoot.SetActive(false);
        }

        private void CreateRefusal(TMP_FontAsset font, Transform root)
        {
            _refusalBack = HudWidgets.Shape("Refusal Back", root, HudWidgets.CardColor(),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);
            refusalText = HudWidgets.Text("Refusal", _refusalBack.rectTransform, font, string.Empty, HudWidgets.BodySize,
                MarketSceneSpec.CoralInk, TextAlignmentOptions.Center, new Vector2(10f, -2f), new Vector2(740f, 26f),
                0f, false, true);
            refusalText.color = MarketSceneSpec.CoralInk;
            _refusalBack.gameObject.SetActive(false);
            refusalText.gameObject.SetActive(false);
        }

        private void CreateHints(TMP_FontAsset font, Transform root)
        {
            var hintBar = HudWidgets.Shape("Hint Bar", root, HudWidgets.CardColor(),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);
            hintText = HudWidgets.Micro("Hint", hintBar.rectTransform, font,
                "Click Game view | WASD move | E act | C crouch | Shift run | R reset | Tab guard",
                new Vector2(10f, -3f), new Vector2(450f, 20f));

            var zoomBar = HudWidgets.Shape("Zoom Bar", root, HudWidgets.CardColor(),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Vector2.zero);
            _zoomText = HudWidgets.Micro("Zoom Note", zoomBar.rectTransform, font,
                "Wheel zoom 8 to 24 tiles", new Vector2(10f, -3f), new Vector2(450f, 20f));
        }

        private void CreateBanner(TMP_FontAsset font, Transform root)
        {
            _bannerRoot = HudWidgets.Shape("Outcome Banner", root, HudWidgets.CardColor(),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, 90f), new Vector2(700f, 130f)).gameObject;
            bannerText = HudWidgets.Text("Banner", _bannerRoot.transform, font, string.Empty, HudWidgets.HeadlineSize,
                MarketSceneSpec.Coral, TextAlignmentOptions.Center, new Vector2(20f, -22f), new Vector2(660f, 96f),
                0f, false, true);
            _bannerRoot.SetActive(false);
        }

        private void RefreshFeed()
        {
            for (var i = 0; i < feedBadges.Length; i++)
            {
                if (i >= _feed.Count)
                {
                    feedBadges[i].text = string.Empty;
                    feedMessages[i].text = string.Empty;
                    continue;
                }

                var entry = _feed[i];
                var alpha = Mathf.Clamp01(entry.Life / FeedFadeSeconds);
                var badgeInk = BadgeColor(entry.Badge);
                feedBadges[i].text = entry.Badge;
                feedBadges[i].color = new Color(badgeInk.r, badgeInk.g, badgeInk.b, alpha);
                feedMessages[i].text = entry.Message;
                feedMessages[i].color = new Color(MarketSceneSpec.InkBlue.r, MarketSceneSpec.InkBlue.g, MarketSceneSpec.InkBlue.b, alpha);
            }
        }

        private void RemoveLegacyChildren()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }
        }

        private void RebindRuntimeReferences()
        {
            var root = _redesignRoot;
            _headline = FindText(root, "Headline");
            _controllerBadge = FindText(root, "Controller Badge Label");
            _zoomText = FindText(root, "Zoom Note");
            _refusalBack = FindImage(root, "Refusal Back");
            var banner = Find(root, "Outcome Banner");
            _bannerRoot = banner != null ? banner.gameObject : null;

            heatText = FindText(root, "Heat Tier");
            heatFill = FindRect(root, "Heat Spine Back");
            lootText = FindText(root, "Loot");
            bagText = FindText(root, "Bag");
            guardText = FindText(root, "Guard");
            backupText = FindText(root, "Backup");
            actionText = FindText(root, "Action");
            bannerText = FindText(root, "Banner");
            hintText = FindText(root, "Hint");
            zoneText = FindText(root, "Zone");
            promptRect = FindRect(root, "Prompt");
            promptText = FindText(root, "Prompt Label");
            refusalText = FindText(root, "Refusal");
            tagText = FindText(root, "Tags");
            glancePip = FindImage(root, "Glance Pip");
            detectionFlash = FindImage(root, "Detection Flash");

            var bar = Find(root, "Channel Bar");
            actionBarRoot = bar != null ? bar.gameObject : null;
            actionFill = FindRect(root, "Channel Fill");

            heatNotches = new Image[HeistState.Tiers.Length];
            for (var i = 0; i < heatNotches.Length; i++)
            {
                heatNotches[i] = FindImage(root, "Heat Notch " + i);
            }

            var stripNames = new[] { "Vignette Top", "Vignette Bottom", "Vignette Left", "Vignette Right" };
            var strips = new Image[stripNames.Length];
            for (var i = 0; i < stripNames.Length; i++)
            {
                strips[i] = FindImage(root, stripNames[i]);
            }

            chaseVignette = strips;

            objectiveLines = new TMP_Text[ObjectiveLabels.Length];
            _objectiveMarks = new Image[ObjectiveLabels.Length];
            for (var i = 0; i < ObjectiveLabels.Length; i++)
            {
                objectiveLines[i] = FindText(root, "Objective " + (i + 1));
                _objectiveMarks[i] = FindImage(root, "Objective Mark " + (i + 1));
            }

            feedBadges = new TMP_Text[FeedCapacity];
            feedMessages = new TMP_Text[FeedCapacity];
            for (var i = 0; i < FeedCapacity; i++)
            {
                feedBadges[i] = FindText(root, "Feed Badge " + i);
                feedMessages[i] = FindText(root, "Feed Line " + i);
            }
        }

        private void PlaceNamed(string name, HudPanel panel)
        {
            var found = Find(_redesignRoot, name);
            if (found is RectTransform rect)
            {
                HudLayout.Place(rect, panel);
            }
        }

        private static Transform Find(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (var i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static TMP_Text FindText(Transform root, string name)
        {
            var found = Find(root, name);
            return found != null ? found.GetComponent<TMP_Text>() : null;
        }

        private static Image FindImage(Transform root, string name)
        {
            var found = Find(root, name);
            return found != null ? found.GetComponent<Image>() : null;
        }

        private static RectTransform FindRect(Transform root, string name)
        {
            var found = Find(root, name);
            return found as RectTransform;
        }

        /// <summary>Maps a tile position to the department that owns it, clamping outside points into the floorplan.</summary>
        public static string ZoneName(Vector2 player)
        {
            var i = Mathf.Clamp(player.x, 0f, MarketData.WidthTiles - 0.001f);
            var j = Mathf.Clamp(player.y, 0f, MarketData.HeightTiles - 0.001f);
            foreach (var room in MarketData.Rooms)
            {
                if (i >= room.OriginI && i < room.OriginI + MarketData.RoomTiles
                    && j >= room.OriginJ && j < room.OriginJ + MarketData.RoomTiles)
                {
                    return room.Name;
                }
            }

            return "Frischemarkt";
        }

        /// <summary>Colour of a heat spine notch for a tier index.</summary>
        public static Color HeatNotchColor(int tier)
        {
            return tier switch
            {
                0 => MarketSceneSpec.InkBlue2,
                1 => MarketSceneSpec.Teal,
                2 => MarketSceneSpec.Sun,
                3 => MarketSceneSpec.Sun,
                4 => MarketSceneSpec.Coral,
                5 => MarketSceneSpec.CoralInk,
                _ => MarketSceneSpec.InkBlue2,
            };
        }

        private static string ObjectiveLabel(string id)
        {
            return id switch
            {
                "power" => ObjectiveLabels[0],
                "vitrine" => ObjectiveLabels[1],
                "quota" => ObjectiveLabels[2],
                "detag" => ObjectiveLabels[3],
                "escape" => ObjectiveLabels[4],
                _ => id,
            };
        }

        private static string StateLabel(string state)
        {
            if (string.IsNullOrEmpty(state))
            {
                return "Patrol";
            }

            return char.ToUpperInvariant(state[0]) + state.Substring(1);
        }

        private static string BackupLabel(BackupPhase phase)
        {
            return phase switch
            {
                BackupPhase.Respond => "on the way",
                BackupPhase.Sweep => "sweeping",
                _ => "quiet",
            };
        }

        private static string ChannelVerb(string kind)
        {
            return kind switch
            {
                "steal" => "Stealing",
                "power" => "Cutting power",
                "detag" => "De-tagging",
                "vitrine" => "Opening vitrine",
                _ => "Working",
            };
        }

        private static Color BadgeColor(string badge)
        {
            return badge switch
            {
                "ALARM" => MarketSceneSpec.Coral,
                "CCTV" => MarketSceneSpec.Sun,
                "BACKUP" => MarketSceneSpec.InkBlue2,
                "STEAL" => MarketSceneSpec.Teal,
                _ => MarketSceneSpec.CoralInk,
            };
        }

        private static string ShortPrompt(string prompt)
        {
            const string prefix = "Hold E: ";
            var line = prompt.StartsWith(prefix, StringComparison.Ordinal) ? prompt.Substring(prefix.Length) : prompt;
            return line.Length <= 44 ? line : line.Substring(0, 43) + "...";
        }
    }
}
