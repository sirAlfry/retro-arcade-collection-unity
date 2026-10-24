using System.Collections.Generic;
using UnityEngine;

namespace RetroArcade
{
    /// <summary>I due font bitmap inclusi nel progetto (entrambi di pubblico dominio).</summary>
    public enum FontFace
    {
        /// <summary>X11 misc-fixed 6×10: compatto e leggibile, per HUD e testi lunghi (≈ "monospace 11px" di Phaser).</summary>
        Body,
        /// <summary>font8x8 di Daniel Hepper: grosso e "arcade", per titoli e banner.</summary>
        Display,
    }

    /// <summary>Un font bitmap a cella fissa, già impacchettato nell'atlante.</summary>
    internal sealed class PixelFont
    {
        public readonly int CellW;
        public readonly int CellH;
        /// <summary>Spazio extra tra righe in un testo multilinea (in pixel del font).</summary>
        public readonly int LineGap;

        private readonly Dictionary<int, Rect> _uv = new Dictionary<int, Rect>(256);

        public PixelFont(int cellW, int cellH, int lineGap)
        {
            CellW = cellW;
            CellH = cellH;
            LineGap = lineGap;
        }

        internal void Add(int codepoint, Rect uv) => _uv[codepoint] = uv;

        public bool TryGet(int codepoint, out Rect uv)
        {
            if (_uv.TryGetValue(codepoint, out uv)) return true;
            return _uv.TryGetValue('?', out uv);
        }
    }

    /// <summary>
    /// Costruisce a runtime una texture-atlante 256×256 con:
    /// un blocco bianco (usato da tutte le forme piene) + i glifi dei due font.
    /// Così l'intero frame si disegna con UNA sola texture e UNA sola draw call.
    /// </summary>
    internal static class FontAtlas
    {
        private const int Size = 256;

        public const int AtlasSize = Size;

        /// <summary>Pixel dell'atlante (righe dal basso, come le texture Unity). Puro C#: niente GPU.</summary>
        public static Color32[] Pixels { get; private set; }
        public static Vector2 WhiteUV { get; private set; }
        public static PixelFont Body { get; private set; }
        public static PixelFont Display { get; private set; }

        public static void EnsureBuilt()
        {
            if (Pixels != null) return;

            var pixels = new Color32[Size * Size]; // tutto trasparente
            var white = new Color32(255, 255, 255, 255);

            // Blocco bianco 6×6 in alto a sinistra: le forme campionano il suo centro.
            for (int y = 0; y < 6; y++)
                for (int x = 0; x < 6; x++)
                    Put(pixels, x, y, white);
            WhiteUV = new Vector2(3f / Size, 1f - 3f / Size);

            Body = new PixelFont(PixelFontData.BodyCellW, PixelFontData.BodyCellH, 3);
            Display = new PixelFont(PixelFontData.DisplayCellW, PixelFontData.DisplayCellH, 3);

            int cursorY = 8;
            cursorY = Pack(pixels, Body, PixelFontData.BodyCodes, PixelFontData.BodyBits, cursorY);
            Pack(pixels, Display, PixelFontData.DisplayCodes, PixelFontData.DisplayBits, cursorY + 2);

            Pixels = pixels;
        }

        /// <summary>Carica l'atlante sulla GPU come <see cref="Texture2D"/> (filtro Point, niente mipmap).</summary>
        public static Texture2D CreateTexture()
        {
            EnsureBuilt();
            // linear = true: la texture contiene solo bianco/alpha, nessuna conversione sRGB desiderata.
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false, true)
            {
                name = "RetroArcade_FontAtlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            tex.SetPixels32(Pixels);
            tex.Apply(false, true); // true = libera la copia in RAM
            return tex;
        }

        private static int Pack(Color32[] pixels, PixelFont font, int[] codes, string bits, int startY)
        {
            int padW = font.CellW + 2, padH = font.CellH + 2; // 1 px di bordo vuoto attorno a ogni glifo
            int perRow = Size / padW;
            int hexPerGlyph = font.CellH * 2;
            var white = new Color32(255, 255, 255, 255);

            for (int i = 0; i < codes.Length; i++)
            {
                int gx = (i % perRow) * padW + 1;
                int gy = startY + (i / perRow) * padH + 1;

                for (int row = 0; row < font.CellH; row++)
                {
                    int v = ParseHexByte(bits, i * hexPerGlyph + row * 2);
                    for (int col = 0; col < font.CellW; col++)
                        if ((v & (0x80 >> col)) != 0)
                            Put(pixels, gx + col, gy + row, white);
                }

                // UV: in Unity l'origine delle texture è in basso a sinistra, noi impacchettiamo dall'alto.
                float uMin = (float)gx / Size;
                float vMax = 1f - (float)gy / Size;
                font.Add(codes[i], new Rect(uMin, vMax, (float)font.CellW / Size, -(float)font.CellH / Size));
            }

            int rows = (codes.Length + perRow - 1) / perRow;
            return startY + rows * padH;
        }

        private static void Put(Color32[] pixels, int x, int yFromTop, Color32 c)
        {
            if (x < 0 || x >= Size || yFromTop < 0 || yFromTop >= Size) return;
            pixels[(Size - 1 - yFromTop) * Size + x] = c;
        }

        private static int ParseHexByte(string s, int index) => (Hex(s[index]) << 4) | Hex(s[index + 1]);

        private static int Hex(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            return 0;
        }
    }
}
