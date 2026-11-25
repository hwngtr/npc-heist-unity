using UnityEngine;

namespace HeistSim.Market2D
{
    /// <summary>Riso print ink palette shared by Market2D runtime visuals, shaders, and builders.</summary>
    public static class RisoPalette
    {
        /// <summary>Paper stock, #F3EBDD.</summary>
        public static readonly Color Paper = Rgb(0xF3EBDD);

        /// <summary>Card stock, #FBF6EC.</summary>
        public static readonly Color Card = Rgb(0xFBF6EC);

        /// <summary>Key plate ink, #344A80.</summary>
        public static readonly Color InkBlue = Rgb(0x344A80);

        /// <summary>Half shade ink, #56628A.</summary>
        public static readonly Color InkBlue2 = Rgb(0x56628A);

        /// <summary>Coral drum, #EE6852.</summary>
        public static readonly Color Coral = Rgb(0xEE6852);

        /// <summary>Deep coral rule, #A8402D.</summary>
        public static readonly Color CoralInk = Rgb(0xA8402D);

        /// <summary>Sun drum, #FFD428.</summary>
        public static readonly Color Sun = Rgb(0xFFD428);

        /// <summary>Teal drum, #008C8A.</summary>
        public static readonly Color Teal = Rgb(0x008C8A);

        /// <summary>Bot black, #242226.</summary>
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
