using UnityEngine;

namespace HeistSim.Market.UI
{
    /// <summary>Named screen regions owned by the redesigned HUD.</summary>
    public enum HudPanel
    {
        /// <summary>Top-left status card.</summary>
        Status,
        /// <summary>Top-right guard glance card.</summary>
        Glance,
        /// <summary>Bottom-left activity feed card.</summary>
        Feed,
        /// <summary>Bottom-center interaction prompt.</summary>
        Prompt,
        /// <summary>Bottom-center refusal line above the prompt.</summary>
        Refusal,
        /// <summary>Bottom-center timed channel bar.</summary>
        Channel,
        /// <summary>Bottom-right control hint bar.</summary>
        Hint,
        /// <summary>Bottom-right wheel zoom note.</summary>
        Zoom,
        /// <summary>Full screen detection flash.</summary>
        Flash,
        /// <summary>Top edge of the chase vignette.</summary>
        VignetteTop,
        /// <summary>Bottom edge of the chase vignette.</summary>
        VignetteBottom,
        /// <summary>Left edge of the chase vignette.</summary>
        VignetteLeft,
        /// <summary>Right edge of the chase vignette.</summary>
        VignetteRight,
    }

    /// <summary>
    /// Normalized anchor table for every HUD panel. Margins and sizes are reference pixels at
    /// 1600 by 900; the canvas scaler shrinks them with the screen, so corners never collide.
    /// </summary>
    public static class HudLayout
    {
        /// <summary>Reference width used by the market canvas scaler.</summary>
        public const float ReferenceWidth = 1600f;

        /// <summary>Reference height used by the market canvas scaler.</summary>
        public const float ReferenceHeight = 900f;

        private readonly struct Region
        {
            public Region(Vector2 anchor, Vector2 pivot, Vector2 margin, Vector2 size, Vector2? stretchMax = null)
            {
                Anchor = anchor;
                Pivot = pivot;
                Margin = margin;
                Size = size;
                StretchMax = stretchMax;
            }

            public Vector2 Anchor { get; }

            public Vector2 Pivot { get; }

            public Vector2 Margin { get; }

            public Vector2 Size { get; }

            public Vector2? StretchMax { get; }
        }

        private static readonly Region[] Regions =
        {
            new Region(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, -20f), new Vector2(360f, 470f)),
            new Region(new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(320f, 168f)),
            new Region(new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(20f, 160f), new Vector2(380f, 156f)),
            new Region(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(440f, 52f)),
            new Region(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 202f), new Vector2(760f, 30f)),
            new Region(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 156f), new Vector2(440f, 14f)),
            new Region(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 20f), new Vector2(470f, 22f)),
            new Region(new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 46f), new Vector2(470f, 20f)),
            new Region(Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, Vector2.one),
            new Region(new Vector2(0f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 64f), new Vector2(1f, 1f)),
            new Region(Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 64f), new Vector2(1f, 0f)),
            new Region(Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(64f, 0f), new Vector2(0f, 1f)),
            new Region(new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(64f, 0f), new Vector2(1f, 1f)),
        };

        /// <summary>Applies the anchor, pivot, margin and size of one panel to a rect transform.</summary>
        public static void Place(RectTransform rect, HudPanel panel)
        {
            if (rect == null)
            {
                return;
            }

            var region = Regions[(int)panel];
            rect.anchorMin = region.Anchor;
            rect.anchorMax = region.StretchMax ?? region.Anchor;
            rect.pivot = region.Pivot;
            rect.anchoredPosition = region.Margin;
            rect.sizeDelta = region.Size;
        }

        /// <summary>
        /// Returns the screen rect of a panel at a given resolution, in bottom-left screen pixels.
        /// Mirrors the geometric mean scaling of a match-width-or-height canvas scaler at 0.5.
        /// </summary>
        public static Rect Bounds(HudPanel panel, float width, float height)
        {
            var region = Regions[(int)panel];
            var scale = ScaleFor(width, height);
            var left = region.Anchor.x * width + region.Margin.x * scale - region.Pivot.x * region.Size.x * scale;
            var bottom = region.Anchor.y * height + region.Margin.y * scale - region.Pivot.y * region.Size.y * scale;
            return Rect.MinMaxRect(
                left,
                bottom,
                left + region.Size.x * scale,
                bottom + region.Size.y * scale);
        }

        /// <summary>Scale applied by the market canvas scaler at a given resolution.</summary>
        public static float ScaleFor(float width, float height)
        {
            if (width <= 0f || height <= 0f)
            {
                return 1f;
            }

            var scaleWidth = width / ReferenceWidth;
            var scaleHeight = height / ReferenceHeight;
            return Mathf.Sqrt(scaleWidth * scaleHeight);
        }
    }
}
