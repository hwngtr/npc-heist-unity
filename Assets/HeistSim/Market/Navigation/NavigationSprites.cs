using UnityEngine;

namespace HeistSim.Market
{
    /// <summary>Builds the small geometric sprites the navigation canvases use at runtime.</summary>
    public static class NavigationSprites
    {
        private static Sprite _disc;
        private static Sprite _triangle;

        /// <summary>Returns a cached white disc used for actor and camera pips.</summary>
        public static Sprite Disc => _disc ??= BuildDisc();

        /// <summary>Returns a cached white right pointing triangle used for the objective arrow.</summary>
        public static Sprite Triangle => _triangle ??= BuildTriangle();

        private static Sprite BuildDisc()
        {
            const int size = 32;
            const float radius = 15f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Navigation Disc (Runtime)",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size * 0.5f, size * 0.5f));
                    var alpha = Mathf.Clamp01(radius - distance + 0.5f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite BuildTriangle()
        {
            const int width = 32;
            const int height = 24;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Navigation Arrow (Runtime)",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var u = (x + 0.5f) / width;
                    var v = Mathf.Abs((y + 0.5f) / height - 0.5f) * 2f;
                    var inside = u >= v * 0.94f && u <= 0.98f;
                    pixels[y * width + x] = inside ? Color.white : Color.clear;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
        }
    }
}
