using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeistSim.Market
{
    /// <summary>Screen space HUD for the market shift: heat, loot, objectives, guard, backup, action and outcome.</summary>
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
        private const float RefusalSeconds = 0.9f;
        private const float DetectionFlashSeconds = 0.25f;

        /// <summary>Heat tier name and current tier readout.</summary>
        public TMP_Text heatText;

        /// <summary>Fill bar that follows the heat value.</summary>
        public RectTransform heatFill;

        /// <summary>Stolen value against the quota.</summary>
        public TMP_Text lootText;

        /// <summary>Bag slots used.</summary>
        public TMP_Text bagText;

        /// <summary>One line per objective, with a done marker.</summary>
        public TMP_Text[] objectiveLines = new TMP_Text[0];

        /// <summary>Guard state with the controller badge.</summary>
        public TMP_Text guardText;

        /// <summary>Staff radio backup status.</summary>
        public TMP_Text backupText;

        /// <summary>Timed action progress line, hidden when idle.</summary>
        public TMP_Text actionText;

        /// <summary>Outcome banner for busted or escaped.</summary>
        public TMP_Text bannerText;

        /// <summary>Control hint along the bottom of the view.</summary>
        public TMP_Text hintText;

        /// <summary>Current department name for the player tile, shown as one line in the HUD.</summary>
        public TMP_Text zoneText;

        /// <summary>World anchored prompt panel, positioned from a screen point by the runner.</summary>
        public RectTransform promptRect;

        /// <summary>Prompt label beside the E badge.</summary>
        public TMP_Text promptText;

        /// <summary>One line refusal message shown after a real interaction failure.</summary>
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

        /// <summary>Full screen coral overlay for the first detection tick.</summary>
        public Image detectionFlash;

        /// <summary>Four edge strips shown while the guard chases or attacks.</summary>
        public Image[] chaseVignette = new Image[0];

        /// <summary>Badge labels of the compact feed, newest first.</summary>
        public TMP_Text[] feedBadges = new TMP_Text[0];

        /// <summary>Message lines of the compact feed, newest first.</summary>
        public TMP_Text[] feedMessages = new TMP_Text[0];

        private readonly List<KeyValuePair<string, string>> _feed = new();
        private bool _built;
        private float _refusalTimer;
        private float _flashTimer;

        private void Awake()
        {
            var font = Resources.Load<TMP_FontAsset>("MarketSans");
            if (heatText == null)
            {
                CreateWidgets(font);
            }
            else if (NeedsFeedbackWidgets())
            {
                var panel = heatText.transform.parent as RectTransform;
                CreateFeedbackWidgets(font, panel != null ? panel : (RectTransform)transform);
                _built = true;
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
            if (_built || heatText != null)
            {
                return false;
            }

            var panelColor = new Color(MarketSceneSpec.Card.r, MarketSceneSpec.Card.g, MarketSceneSpec.Card.b, 0.9f);
            var ink = MarketSceneSpec.InkBlue2;

            var panel = CreateImage("Panel", transform, panelColor, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(392f, 460f));
            panel.rectTransform.pivot = new Vector2(0f, 1f);

            CreateText("Title", panel.rectTransform, font, "Frischemarkt", 26f, MarketSceneSpec.Black, TextAlignmentOptions.TopLeft, new Vector2(16f, -12f), new Vector2(340f, 32f));
            heatText = CreateText("Heat", panel.rectTransform, font, "Heat: Calm", 20f, MarketSceneSpec.Black, TextAlignmentOptions.TopLeft, new Vector2(16f, -48f), new Vector2(340f, 26f));

            var barBack = CreateImage("Heat Bar", panel.rectTransform, new Color(ink.r, ink.g, ink.b, 0.25f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -78f), new Vector2(340f, 10f));
            var fill = CreateImage("Heat Fill", barBack.rectTransform, MarketSceneSpec.Coral, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            heatFill = fill.rectTransform;
            heatFill.anchorMax = new Vector2(0.12f, 1f);

            lootText = CreateText("Loot", panel.rectTransform, font, "Loot: 0 / 190", 20f, MarketSceneSpec.InkBlue, TextAlignmentOptions.TopLeft, new Vector2(16f, -106f), new Vector2(340f, 26f));
            bagText = CreateText("Bag", panel.rectTransform, font, "Bag: 0 / 6", 20f, MarketSceneSpec.InkBlue, TextAlignmentOptions.TopLeft, new Vector2(16f, -134f), new Vector2(340f, 26f));
            CreateText("Objectives Header", panel.rectTransform, font, "Objectives", 18f, MarketSceneSpec.CoralInk, TextAlignmentOptions.TopLeft, new Vector2(16f, -166f), new Vector2(340f, 22f));

            objectiveLines = new TMP_Text[ObjectiveLabels.Length];
            for (var i = 0; i < ObjectiveLabels.Length; i++)
            {
                objectiveLines[i] = CreateText(
                    "Objective " + (i + 1),
                    panel.rectTransform,
                    font,
                    "[ ] " + ObjectiveLabels[i],
                    18f,
                    MarketSceneSpec.Black,
                    TextAlignmentOptions.TopLeft,
                    new Vector2(16f, -190f - i * 24f),
                    new Vector2(340f, 22f));
            }

            guardText = CreateText("Guard", panel.rectTransform, font, "Guard: patrol  [FSM]", 19f, MarketSceneSpec.Black, TextAlignmentOptions.TopLeft, new Vector2(16f, -316f), new Vector2(310f, 24f));

            backupText = CreateText("Backup", panel.rectTransform, font, "Backup: quiet", 18f, ink, TextAlignmentOptions.TopLeft, new Vector2(16f, -342f), new Vector2(360f, 22f));
            actionText = CreateText("Action", panel.rectTransform, font, string.Empty, 18f, MarketSceneSpec.CoralInk, TextAlignmentOptions.TopLeft, new Vector2(16f, -368f), new Vector2(360f, 22f));
            actionText.gameObject.SetActive(false);

            bannerText = CreateText("Banner", transform, font, "BUSTED", 64f, MarketSceneSpec.Coral, TextAlignmentOptions.Center, new Vector2(0f, -96f), new Vector2(900f, 96f));
            var bannerRect = bannerText.rectTransform;
            bannerRect.anchorMin = new Vector2(0.5f, 1f);
            bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0f, -96f);
            bannerText.fontStyle = FontStyles.Bold;
            bannerText.gameObject.SetActive(false);

            CreateFeedbackWidgets(font, panel.rectTransform);

            hintText = CreateText("Hint", transform, font, "Click the Game view first | WASD or arrows sneak | hold E act | C crouch | Shift run | R new shift | Tab guard", 16f, ink, TextAlignmentOptions.BottomLeft, new Vector2(24f, 20f), new Vector2(900f, 24f));
            var hintRect = hintText.rectTransform;
            hintRect.anchorMin = new Vector2(0f, 0f);
            hintRect.anchorMax = new Vector2(0f, 0f);
            hintRect.pivot = new Vector2(0f, 0f);
            hintRect.anchoredPosition = new Vector2(24f, 20f);

            zoneText = CreateText("Zone", transform, font, "Zone: " + MarketData.Rooms[0].Name, 18f, ink, TextAlignmentOptions.BottomLeft, new Vector2(24f, 48f), new Vector2(900f, 24f));
            var zoneRect = zoneText.rectTransform;
            zoneRect.anchorMin = new Vector2(0f, 0f);
            zoneRect.anchorMax = new Vector2(0f, 0f);
            zoneRect.pivot = new Vector2(0f, 0f);
            zoneRect.anchoredPosition = new Vector2(24f, 48f);

            _built = true;
            return true;
        }

        /// <summary>Updates every HUD line from the snapshot and the live channel.</summary>
        public void Refresh(in HeistSnapshot snapshot, in MarketActionHint hint, float actionProgress)
        {
            if (!_built && heatText == null)
            {
                return;
            }

            var tier = Mathf.Clamp(snapshot.Tier, 0, HeistState.Tiers.Length - 1);
            heatText.text = "Heat: " + HeistState.Tiers[tier].Label;
            if (heatFill != null)
            {
                heatFill.anchorMax = new Vector2(Mathf.Clamp01(snapshot.Heat), 1f);
            }

            for (var i = 0; i < heatNotches.Length; i++)
            {
                var notch = heatNotches[i];
                if (notch == null)
                {
                    continue;
                }

                var tint = HeatNotchColor(i);
                notch.color = i <= tier
                    ? tint
                    : new Color(tint.r, tint.g, tint.b, 0.25f);
            }

            lootText.text = "Loot: " + Mathf.FloorToInt(snapshot.StolenValue) + " / " + MarketData.Quota;
            bagText.text = "Bag: " + snapshot.BagCount + " / " + MarketData.BagCapacity;

            if (zoneText != null)
            {
                zoneText.text = "Zone: " + ZoneName(snapshot.Player);
            }

            if (snapshot.Objectives != null)
            {
                for (var i = 0; i < objectiveLines.Length && i < snapshot.Objectives.Count; i++)
                {
                    var objective = snapshot.Objectives[i];
                    objectiveLines[i].text = (objective.Done ? "[x] " : "[ ] ") + ObjectiveLabel(objective.Id);
                }
            }

            guardText.text = "Guard: " + snapshot.GuardState + "  [" + (snapshot.Arch == GuardArchitecture.Fsm ? "FSM" : "BT") + "]";
            backupText.text = "Backup: " + BackupLabel(snapshot.BackupPhase);

            if (tagText != null)
            {
                tagText.text = "RF tags: " + snapshot.ActiveTags;
                tagText.color = snapshot.ActiveTags > 0 ? MarketSceneSpec.Coral : MarketSceneSpec.InkBlue2;
            }

            if (glancePip != null)
            {
                var state = snapshot.GuardState;
                var glancing = snapshot.DetectedNow
                    || state == "investigate"
                    || state == "search"
                    || state == "chase"
                    || state == "attack";
                glancePip.gameObject.SetActive(glancing);
                var pipTint = GuardVisionFan.TintFor(state);
                glancePip.color = new Color(pipTint.r, pipTint.g, pipTint.b, 0.95f);
            }

            var alarmed = snapshot.GuardState == "chase" || snapshot.GuardState == "attack";
            for (var i = 0; i < chaseVignette.Length; i++)
            {
                if (chaseVignette[i] != null && chaseVignette[i].gameObject.activeSelf != alarmed)
                {
                    chaseVignette[i].gameObject.SetActive(alarmed);
                }
            }

            if (!string.IsNullOrEmpty(snapshot.ChannelKind) && snapshot.ChannelHold > 0f)
            {
                var progress = Mathf.Clamp01(snapshot.ChannelProgress / snapshot.ChannelHold);
                actionText.gameObject.SetActive(true);
                actionText.text = ChannelVerb(snapshot.ChannelKind) + " " + Mathf.RoundToInt(progress * 100f) + "% · hold still";
                if (actionBarRoot != null)
                {
                    actionBarRoot.SetActive(true);
                }

                if (actionFill != null)
                {
                    actionFill.anchorMax = new Vector2(Mathf.Max(0.02f, progress), 1f);
                }
            }
            else
            {
                actionText.gameObject.SetActive(false);
                if (actionBarRoot != null)
                {
                    actionBarRoot.SetActive(false);
                }
            }

            if (snapshot.Outcome == HeistOutcome.Busted)
            {
                bannerText.gameObject.SetActive(true);
                bannerText.color = MarketSceneSpec.Coral;
                bannerText.text = "BUSTED\nR for a new shift";
            }
            else if (snapshot.Outcome == HeistOutcome.Escaped)
            {
                bannerText.gameObject.SetActive(true);
                bannerText.color = MarketSceneSpec.Teal;
                bannerText.text = "ESCAPED\nShift complete, R to run again";
            }
            else
            {
                bannerText.gameObject.SetActive(false);
            }
        }

        /// <summary>Places the in-range prompt at a screen point from Camera.WorldToScreenPoint.</summary>
        public void ShowPrompt(Vector2 screenPoint, string label)
        {
            if (promptRect == null || string.IsNullOrEmpty(label))
            {
                HidePrompt();
                return;
            }

            var canvas = GetComponent<Canvas>();
            Camera worldCamera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                worldCamera = canvas.worldCamera;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screenPoint, worldCamera, out var local))
            {
                HidePrompt();
                return;
            }

            promptRect.anchoredPosition = local;
            promptText.text = label;
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

        /// <summary>Starts the short lived refusal flash for a real interaction failure.</summary>
        public void ShowRefusal(string reason)
        {
            if (refusalText == null || string.IsNullOrEmpty(reason))
            {
                return;
            }

            refusalText.text = reason;
            refusalText.gameObject.SetActive(true);
            _refusalTimer = RefusalSeconds;
        }

        /// <summary>Prepends one feed line, keeping the newest five entries.</summary>
        public void PushFeed(string badge, string message)
        {
            if (_feed.Count == FeedCapacity)
            {
                _feed.RemoveAt(_feed.Count - 1);
            }

            _feed.Insert(0, new KeyValuePair<string, string>(badge, message));
            RefreshFeed();
        }

        /// <summary>Clears the feed when a new shift starts.</summary>
        public void ClearFeed()
        {
            _feed.Clear();
            RefreshFeed();
        }

        /// <summary>Starts the first detection flash.</summary>
        public void FlashDetection()
        {
            if (detectionFlash == null)
            {
                return;
            }

            _flashTimer = DetectionFlashSeconds;
            detectionFlash.gameObject.SetActive(true);
        }

        private void Update()
        {
            if (_flashTimer > 0f)
            {
                _flashTimer = Mathf.Max(0f, _flashTimer - Time.deltaTime);
                var alpha = 0.55f * (_flashTimer / DetectionFlashSeconds);
                detectionFlash.color = new Color(MarketSceneSpec.Coral.r, MarketSceneSpec.Coral.g, MarketSceneSpec.Coral.b, alpha);
                if (_flashTimer <= 0f)
                {
                    detectionFlash.gameObject.SetActive(false);
                }
            }

            if (_refusalTimer > 0f)
            {
                _refusalTimer = Mathf.Max(0f, _refusalTimer - Time.deltaTime);
                var alpha = Mathf.Clamp01(_refusalTimer / 0.25f);
                refusalText.color = new Color(MarketSceneSpec.CoralInk.r, MarketSceneSpec.CoralInk.g, MarketSceneSpec.CoralInk.b, alpha);
                if (_refusalTimer <= 0f)
                {
                    refusalText.gameObject.SetActive(false);
                }
            }
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

        private bool NeedsFeedbackWidgets()
        {
            return detectionFlash == null
                || chaseVignette.Length == 0
                || heatNotches.Length == 0
                || glancePip == null
                || tagText == null
                || actionBarRoot == null
                || promptRect == null
                || refusalText == null
                || feedBadges.Length == 0
                || feedMessages.Length == 0;
        }

        private void CreateFeedbackWidgets(TMP_FontAsset font, RectTransform panel)
        {
            var ink = MarketSceneSpec.InkBlue2;

            if (detectionFlash == null)
            {
                detectionFlash = CreateImage("Detection Flash", transform, new Color(MarketSceneSpec.Coral.r, MarketSceneSpec.Coral.g, MarketSceneSpec.Coral.b, 0f), Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                detectionFlash.rectTransform.offsetMin = Vector2.zero;
                detectionFlash.rectTransform.offsetMax = Vector2.zero;
                detectionFlash.gameObject.SetActive(false);
                detectionFlash.transform.SetAsFirstSibling();
            }

            if (chaseVignette.Length == 0)
            {
                CreateVignette();
            }

            if (heatNotches.Length == 0)
            {
                heatNotches = new Image[HeistState.Tiers.Length];
                for (var i = 0; i < heatNotches.Length; i++)
                {
                    heatNotches[i] = CreateImage(
                        "Heat Notch " + i,
                        panel,
                        HeatNotchColor(i),
                        new Vector2(0f, 1f),
                        new Vector2(0f, 1f),
                        new Vector2(0f, 1f),
                        new Vector2(16f + i * 24f, -90f),
                        new Vector2(18f, 5f));
                }
            }

            if (glancePip == null)
            {
                glancePip = CreateImage("Glance Pip", panel, MarketSceneSpec.Teal, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(338f, -314f), new Vector2(14f, 14f));
                glancePip.gameObject.SetActive(false);
            }

            if (tagText == null)
            {
                tagText = CreateText("Tags", panel, font, "RF tags: 0", 18f, ink, TextAlignmentOptions.TopLeft, new Vector2(16f, -392f), new Vector2(360f, 22f));
            }

            if (actionBarRoot == null)
            {
                var actionBack = CreateImage("Action Bar", panel, new Color(ink.r, ink.g, ink.b, 0.25f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -418f), new Vector2(340f, 8f));
                var actionBarFill = CreateImage("Action Fill", actionBack.rectTransform, MarketSceneSpec.Sun, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
                actionBarFill.rectTransform.offsetMin = Vector2.zero;
                actionBarFill.rectTransform.offsetMax = Vector2.zero;
                actionFill = actionBarFill.rectTransform;
                actionFill.anchorMax = new Vector2(0f, 1f);
                actionBarRoot = actionBack.gameObject;
                actionBarRoot.SetActive(false);
            }

            if (promptRect == null || promptText == null)
            {
                CreatePrompt(font);
            }

            if (refusalText == null)
            {
                CreateRefusal(font);
            }

            if (feedBadges.Length == 0 || feedMessages.Length == 0)
            {
                CreateFeed(font);
            }
        }

        private void CreateVignette()
        {
            chaseVignette = new Image[4];
            chaseVignette[0] = CreateImage("Vignette Top", transform, new Color(MarketSceneSpec.Coral.r, MarketSceneSpec.Coral.g, MarketSceneSpec.Coral.b, 0.2f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 84f));
            chaseVignette[1] = CreateImage("Vignette Bottom", transform, new Color(MarketSceneSpec.Coral.r, MarketSceneSpec.Coral.g, MarketSceneSpec.Coral.b, 0.2f), new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 84f));
            chaseVignette[2] = CreateImage("Vignette Left", transform, new Color(MarketSceneSpec.Coral.r, MarketSceneSpec.Coral.g, MarketSceneSpec.Coral.b, 0.2f), new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0f), Vector2.zero, new Vector2(84f, 0f));
            chaseVignette[3] = CreateImage("Vignette Right", transform, new Color(MarketSceneSpec.Coral.r, MarketSceneSpec.Coral.g, MarketSceneSpec.Coral.b, 0.2f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0f), Vector2.zero, new Vector2(84f, 0f));
            foreach (var strip in chaseVignette)
            {
                strip.gameObject.SetActive(false);
            }
        }

        private void CreatePrompt(TMP_FontAsset font)
        {
            promptRect = CreateImage("Prompt", transform, new Color(MarketSceneSpec.Card.r, MarketSceneSpec.Card.g, MarketSceneSpec.Card.b, 0.94f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(420f, 42f)).rectTransform;
            var badge = CreateImage("Prompt Badge", promptRect, MarketSceneSpec.Sun, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(30f, 30f));
            var badgeText = CreateText("Prompt Key", badge.rectTransform, font, "E", 20f, MarketSceneSpec.Black, TextAlignmentOptions.Center, Vector2.zero, new Vector2(30f, 30f));
            badgeText.fontStyle = FontStyles.Bold;
            badgeText.rectTransform.anchorMin = Vector2.zero;
            badgeText.rectTransform.anchorMax = Vector2.one;
            badgeText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            badgeText.rectTransform.anchoredPosition = Vector2.zero;
            promptText = CreateText("Prompt Label", promptRect, font, string.Empty, 18f, MarketSceneSpec.Black, TextAlignmentOptions.MidlineLeft, new Vector2(46f, 0f), new Vector2(364f, 36f));
            promptText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            promptText.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            promptText.rectTransform.pivot = new Vector2(0f, 0.5f);
            promptRect.gameObject.SetActive(false);
        }

        private void CreateRefusal(TMP_FontAsset font)
        {
            refusalText = CreateText("Refusal", transform, font, string.Empty, 22f, MarketSceneSpec.CoralInk, TextAlignmentOptions.Center, Vector2.zero, new Vector2(800f, 30f));
            var rect = refusalText.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 84f);
            refusalText.fontStyle = FontStyles.Bold;
            refusalText.gameObject.SetActive(false);
        }

        private void CreateFeed(TMP_FontAsset font)
        {
            var feedPanel = CreateImage("Feed", transform, new Color(MarketSceneSpec.Card.r, MarketSceneSpec.Card.g, MarketSceneSpec.Card.b, 0.86f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 48f), new Vector2(400f, 150f));
            feedBadges = new TMP_Text[FeedCapacity];
            feedMessages = new TMP_Text[FeedCapacity];
            for (var i = 0; i < FeedCapacity; i++)
            {
                feedBadges[i] = CreateText("Feed Badge " + i, feedPanel.rectTransform, font, string.Empty, 15f, MarketSceneSpec.CoralInk, TextAlignmentOptions.TopLeft, new Vector2(10f, -10f - i * 26f), new Vector2(66f, 22f));
                feedMessages[i] = CreateText("Feed Line " + i, feedPanel.rectTransform, font, string.Empty, 16f, MarketSceneSpec.Black, TextAlignmentOptions.TopLeft, new Vector2(82f, -10f - i * 26f), new Vector2(306f, 22f));
            }
        }

        private void RefreshFeed()
        {
            for (var i = 0; i < feedBadges.Length; i++)
            {
                if (i < _feed.Count)
                {
                    var entry = _feed[i];
                    feedBadges[i].text = entry.Key;
                    feedBadges[i].color = BadgeColor(entry.Key);
                    feedMessages[i].text = entry.Value;
                }
                else
                {
                    feedBadges[i].text = string.Empty;
                    feedMessages[i].text = string.Empty;
                }
            }
        }

        private static TMP_Text CreateText(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string content,
            float size,
            Color color,
            TextAlignmentOptions alignment,
            Vector2 position,
            Vector2 box)
        {
            var textObject = new GameObject(name, typeof(RectTransform));
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }

            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = box;
            return text;
        }

        private static Image CreateImage(
            string name,
            Transform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            var imageObject = new GameObject(name, typeof(RectTransform));
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var rect = image.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return image;
        }
    }
}
