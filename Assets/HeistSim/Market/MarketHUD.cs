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

        private bool _built;

        private void Awake()
        {
            if (!_built && heatText == null)
            {
                var font = Resources.Load<TMP_FontAsset>("MarketSans");
                CreateWidgets(font);
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
            var panel = CreateImage("Panel", transform, panelColor, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -8f), new Vector2(392f, 384f));
            panel.rectTransform.pivot = new Vector2(0f, 1f);

            var ink = MarketSceneSpec.InkBlue2;
            CreateText("Title", panel.rectTransform, font, "Frischemarkt", 26f, MarketSceneSpec.Black, TextAlignmentOptions.TopLeft, new Vector2(16f, -12f), new Vector2(340f, 32f));
            heatText = CreateText("Heat", panel.rectTransform, font, "Heat: Calm", 20f, MarketSceneSpec.Black, TextAlignmentOptions.TopLeft, new Vector2(16f, -48f), new Vector2(340f, 26f));

            var barBack = CreateImage("Heat Bar", panel.rectTransform, new Color(ink.r, ink.g, ink.b, 0.25f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -78f), new Vector2(340f, 10f));
            var fill = CreateImage("Heat Fill", barBack.rectTransform, MarketSceneSpec.Coral, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            fill.rectTransform.offsetMin = Vector2.zero;
            fill.rectTransform.offsetMax = Vector2.zero;
            heatFill = fill.rectTransform;
            heatFill.anchorMax = new Vector2(0.12f, 1f);

            lootText = CreateText("Loot", panel.rectTransform, font, "Loot: 0 / 190", 20f, MarketSceneSpec.InkBlue, TextAlignmentOptions.TopLeft, new Vector2(16f, -96f), new Vector2(340f, 26f));
            bagText = CreateText("Bag", panel.rectTransform, font, "Bag: 0 / 6", 20f, MarketSceneSpec.InkBlue, TextAlignmentOptions.TopLeft, new Vector2(16f, -124f), new Vector2(340f, 26f));
            CreateText("Objectives Header", panel.rectTransform, font, "Objectives", 18f, MarketSceneSpec.CoralInk, TextAlignmentOptions.TopLeft, new Vector2(16f, -156f), new Vector2(340f, 22f));

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
                    new Vector2(16f, -180f - i * 24f),
                    new Vector2(340f, 22f));
            }

            guardText = CreateText("Guard", panel.rectTransform, font, "Guard: patrol  [FSM]", 19f, MarketSceneSpec.Black, TextAlignmentOptions.TopLeft, new Vector2(16f, -306f), new Vector2(360f, 24f));
            backupText = CreateText("Backup", panel.rectTransform, font, "Backup: quiet", 18f, ink, TextAlignmentOptions.TopLeft, new Vector2(16f, -332f), new Vector2(360f, 22f));
            actionText = CreateText("Action", panel.rectTransform, font, string.Empty, 18f, MarketSceneSpec.CoralInk, TextAlignmentOptions.TopLeft, new Vector2(16f, -358f), new Vector2(360f, 22f));
            actionText.gameObject.SetActive(false);

            bannerText = CreateText("Banner", transform, font, "BUSTED", 64f, MarketSceneSpec.Coral, TextAlignmentOptions.Center, new Vector2(0f, -96f), new Vector2(900f, 96f));
            var bannerRect = bannerText.rectTransform;
            bannerRect.anchorMin = new Vector2(0.5f, 1f);
            bannerRect.anchorMax = new Vector2(0.5f, 1f);
            bannerRect.pivot = new Vector2(0.5f, 1f);
            bannerRect.anchoredPosition = new Vector2(0f, -96f);
            bannerText.fontStyle = FontStyles.Bold;
            bannerText.gameObject.SetActive(false);

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

        /// <summary>Updates every HUD line from the snapshot and the local action hold.</summary>
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

            if (hint.Found && actionProgress > 0.02f)
            {
                actionText.gameObject.SetActive(true);
                actionText.text = hint.Label + " " + Mathf.RoundToInt(actionProgress * 100f) + "%";
            }
            else
            {
                actionText.gameObject.SetActive(false);
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
