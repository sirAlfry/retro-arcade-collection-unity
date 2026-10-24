using UnityEngine;

namespace RetroArcade
{
    /// <summary>
    /// La palette della versione Python (<c>game_shared.py</c>): sfondo "spazio" scuro e colori neon
    /// ciano + magenta come il banner del README. Valori 0xRRGGBB, pronti per <see cref="RetroCanvas"/>.
    /// </summary>
    public static class Palette
    {
        public const uint Bg = 0x141428;          // (20, 20, 40)
        public const uint BgDark = 0x0a0818;      // (10, 8, 24)
        public const uint PanelBg = 0x0e0c20;     // (14, 12, 32)
        public const uint White = 0xffffff;
        public const uint Black = 0x000000;
        public const uint GrayLight = 0xdcdcdc;
        public const uint GrayMedium = 0xb4b4b4;
        public const uint GrayDark = 0x646464;
        public const uint Text = 0xdcdcff;        // (220, 220, 255)
        public const uint TextDim = 0x8c8cb9;     // (140, 140, 185)

        public const uint NeonCyan = 0x00f0ff;
        public const uint NeonMagenta = 0xff3cc8;
        public const uint NeonPurple = 0x9646ff;
        public const uint NeonGreen = 0x3cff78;
        public const uint NeonYellow = 0xffe63c;
        public const uint NeonOrange = 0xff9628;
        public const uint NeonRed = 0xff465a;
        public const uint NeonBlue = 0x3c82ff;
        public const uint NeonPink = NeonMagenta;

        public const uint ScoreColor = NeonCyan;
        public const uint HudTextColor = NeonGreen;
        public const uint WarningColor = NeonMagenta;

        /// <summary>Interpolazione lineare tra due colori (t in 0..1), come <c>lerp_color</c>.</summary>
        public static uint Lerp(uint a, uint b, float t)
        {
            t = Mathf.Clamp01(t);
            int ar = (int)((a >> 16) & 255), ag = (int)((a >> 8) & 255), ab = (int)(a & 255);
            int br = (int)((b >> 16) & 255), bg = (int)((b >> 8) & 255), bb = (int)(b & 255);
            return RetroCanvas.Rgb((int)(ar + (br - ar) * t), (int)(ag + (bg - ag) * t), (int)(ab + (bb - ab) * t));
        }

        public static uint Rgb(int r, int g, int b) => RetroCanvas.Rgb(r, g, b);
    }
}
