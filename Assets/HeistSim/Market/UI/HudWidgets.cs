using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeistSim.Market.UI
{
    /// <summary>
    /// Builds the four typography roles and the flat ink shapes of the riso HUD.
    /// Every colour comes from MarketSceneSpec, so no new ink enters the system.
    /// </summary>
    public static class HudWidgets
    {
        /// <summary>Kicker size in points: uppercase, wide tracking, InkBlue2.</summary>
        public const float KickerSize = 11f;

        /// <summary>Headline size in points: InkBlue, one instance per screen.</summary>
        public const float HeadlineSize = 26f;

        /// <summary>Body size in points: InkBlue, primary numbers and labels.</summary>
        public const float BodySize = 15f;

        /// <summary>Micro size in points: InkBlue2, hints and secondary facts.</summary>
        public const float MicroSize = 12f;

        /// <summary>Tracking applied to every kicker.</summary>
        public const float KickerTracking = 3f;

        /// <summary>Alpha of every panel that sits over the world, above the 0.9 floor in the spec.</summary>
        public const float PanelAlpha = 0.94f;

        /// <summary>Creates a bare rect transform below a parent.</summary>
        public static RectTransform Rect(string name, Transform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return (RectTransform)child.transform;
        }

        /// <summary>Creates a flat ink or card rectangle with no raycast target.</summary>
        public static Image Shape(
            string name,
            Transform parent,
            Color color,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            var rect = Rect(name, parent);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Creates a text widget in one of the four typography roles.</summary>
        public static TMP_Text Text(
            string name,
            Transform parent,
            TMP_FontAsset font,
            string content,
            float size,
            Color color,
            TextAlignmentOptions alignment,
            Vector2 position,
            Vector2 box,
            float tracking = 0f,
            bool upper = false,
            bool bold = false)
        {
            var rect = Rect(name, parent);
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = box;

            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null)
            {
                text.font = font;
            }

            text.text = upper ? content.ToUpperInvariant() : content;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.characterSpacing = tracking;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.raycastTarget = false;
            if (bold)
            {
                text.fontStyle = FontStyles.Bold;
            }

            return text;
        }

        /// <summary>Creates an 11 point uppercase kicker in InkBlue2.</summary>
        public static TMP_Text Kicker(string name, Transform parent, TMP_FontAsset font, string content, Vector2 position, Vector2 box)
        {
            return Text(name, parent, font, content, KickerSize, MarketSceneSpec.InkBlue2,
                TextAlignmentOptions.TopLeft, position, box, KickerTracking, true);
        }

        /// <summary>Creates the single 26 point InkBlue headline.</summary>
        public static TMP_Text Headline(string name, Transform parent, TMP_FontAsset font, string content, Vector2 position, Vector2 box)
        {
            return Text(name, parent, font, content, HeadlineSize, MarketSceneSpec.InkBlue,
                TextAlignmentOptions.TopLeft, position, box);
        }

        /// <summary>Creates a 15 point InkBlue body line.</summary>
        public static TMP_Text Body(string name, Transform parent, TMP_FontAsset font, string content, Vector2 position, Vector2 box)
        {
            return Text(name, parent, font, content, BodySize, MarketSceneSpec.InkBlue,
                TextAlignmentOptions.TopLeft, position, box);
        }

        /// <summary>Creates a 12 point InkBlue2 micro line.</summary>
        public static TMP_Text Micro(string name, Transform parent, TMP_FontAsset font, string content, Vector2 position, Vector2 box)
        {
            return Text(name, parent, font, content, MicroSize, MarketSceneSpec.InkBlue2,
                TextAlignmentOptions.TopLeft, position, box);
        }

        /// <summary>Creates a raised card surface at a corner of the screen.</summary>
        public static Image Card(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 margin, Vector2 size)
        {
            var panel = Shape(name, parent, CardColor(), anchor, anchor, pivot, margin, size);
            var rule = Shape(name + " Rule", panel.rectTransform, MarketSceneSpec.InkBlue,
                new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, 0f));
            rule.rectTransform.offsetMin = new Vector2(0f, 0f);
            rule.rectTransform.offsetMax = new Vector2(4f, 0f);
            return panel;
        }

        /// <summary>Card ink at the panel alpha required by the visual system.</summary>
        public static Color CardColor()
        {
            return new Color(MarketSceneSpec.Card.r, MarketSceneSpec.Card.g, MarketSceneSpec.Card.b, PanelAlpha);
        }

        /// <summary>Coral ink at a translucent alpha, for flashes and vignette edges.</summary>
        public static Color DangerColor(float alpha)
        {
            return new Color(MarketSceneSpec.Coral.r, MarketSceneSpec.Coral.g, MarketSceneSpec.Coral.b, alpha);
        }

        /// <summary>Deep coral ink at a translucent alpha, for the inner vignette edges.</summary>
        public static Color ShadowColor(float alpha)
        {
            return new Color(MarketSceneSpec.CoralInk.r, MarketSceneSpec.CoralInk.g, MarketSceneSpec.CoralInk.b, alpha);
        }
    }
}
