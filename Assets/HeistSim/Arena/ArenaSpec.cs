using UnityEngine;

namespace HeistSim.Arena
{
    /// <summary>Palette, scene paths and root contract shared by the three arena look candidates.</summary>
    public static class ArenaSpec
    {
        /// <summary>Root objects every built scene contains: Main Camera, Content and Arena Settings.</summary>
        public const int RootObjectCount = 3;

        /// <summary>Orthographic half height used by the flat look cameras.</summary>
        public const float OrthographicSize = 6f;

        /// <summary>Orthographic half height used by the isometric look camera.</summary>
        public const float IsoOrthographicSize = 7.5f;

        /// <summary>Capture width in pixels.</summary>
        public const int CaptureWidth = 1600;

        /// <summary>Capture height in pixels.</summary>
        public const int CaptureHeight = 900;

        /// <summary>Scene asset paths in look order.</summary>
        public static readonly string[] ScenePaths =
        {
            "Assets/HeistSim/Arena/LookA-SpriteDither.unity",
            "Assets/HeistSim/Arena/LookB-FlatIso3D.unity",
            "Assets/HeistSim/Arena/LookC-TilemapPost.unity",
        };

        /// <summary>Capture file names in look order.</summary>
        public static readonly string[] CaptureFiles = { "LookA.png", "LookB.png", "LookC.png" };

        /// <summary>Root object names present in every built scene.</summary>
        public static readonly string[] RootNames = { "Main Camera", "Content", "Arena Settings" };

        /// <summary>Paper stock, #f3ebdd from web/style.css line 9.</summary>
        public static readonly Color Paper = Rgb(0xF3EBDD);

        /// <summary>Card stock, #fbf6ec from web/style.css line 10.</summary>
        public static readonly Color Card = Rgb(0xFBF6EC);

        /// <summary>Key plate ink, #344a80 from web/style.css line 11.</summary>
        public static readonly Color InkBlue = Rgb(0x344A80);

        /// <summary>Half shade ink, #56628a from web/style.css line 12.</summary>
        public static readonly Color InkBlue2 = Rgb(0x56628A);

        /// <summary>Coral drum, #ee6852 from web/style.css line 14.</summary>
        public static readonly Color Coral = Rgb(0xEE6852);

        /// <summary>Deep coral rule, #a8402d from web/style.css line 15.</summary>
        public static readonly Color CoralInk = Rgb(0xA8402D);

        /// <summary>Sun drum, #ffd428 from web/style.css line 16.</summary>
        public static readonly Color Sun = Rgb(0xFFD428);

        /// <summary>Teal drum, #008c8a from web/style.css line 17.</summary>
        public static readonly Color Teal = Rgb(0x008C8A);

        /// <summary>Bot black, #242226 from web/js/press.js line 118.</summary>
        public static readonly Color Black = Rgb(0x242226);

        /// <summary>Converts a packed 0xRRGGBB value to an opaque colour.</summary>
        public static Color Rgb(int hex)
        {
            return new Color(
                ((hex >> 16) & 0xFF) / 255f,
                ((hex >> 8) & 0xFF) / 255f,
                (hex & 0xFF) / 255f,
                1f);
        }
    }
}
