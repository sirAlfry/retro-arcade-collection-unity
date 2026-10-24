using System.Collections.Generic;
using UnityEngine;

namespace RetroArcade
{
    /// <summary>
    /// Il font bitmap 5×7 disegnato a mano della versione Python (<c>pixel_font.py</c>), con le stesse regole:
    /// glifi a larghezza variabile, 1 colonna vuota tra le lettere, minuscole e lettere accentate convertite
    /// in maiuscole senza accento ("è" → "E"), un quadratino per i caratteri sconosciuti.
    /// <para>Ogni glifo viene precalcolato come pochi rettangoli (le "strisce" di pixel accesi unite tra loro),
    /// così una scritta costa pochissimi triangoli e resta nitida a qualsiasi scala.</para>
    /// </summary>
    public static class PixelFont5x7
    {
        public const int GlyphHeight = 7;
        public const int LetterSpacing = 1;

        /// <summary>Un glifo pronto da disegnare: larghezza in pixel del font + rettangoli (x, y, w, h).</summary>
        public sealed class Glyph
        {
            public readonly int Width;
            public readonly RectInt[] Runs;
            public Glyph(int width, RectInt[] runs) { Width = width; Runs = runs; }
        }

        private static readonly Dictionary<char, string[]> Rows = new Dictionary<char, string[]>
        {
            ['A'] = new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['B'] = new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." },
            ['C'] = new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." },
            ['D'] = new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." },
            ['E'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" },
            ['F'] = new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." },
            ['G'] = new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".####" },
            ['H'] = new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" },
            ['I'] = new[] { ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['J'] = new[] { "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.." },
            ['K'] = new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" },
            ['L'] = new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" },
            ['M'] = new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" },
            ['N'] = new[] { "#...#", "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#" },
            ['O'] = new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['P'] = new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." },
            ['Q'] = new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" },
            ['R'] = new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" },
            ['S'] = new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." },
            ['T'] = new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." },
            ['U'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." },
            ['V'] = new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." },
            ['W'] = new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "#.#.#", ".#.#." },
            ['X'] = new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" },
            ['Y'] = new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." },
            ['Z'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" },
            ['0'] = new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." },
            ['1'] = new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." },
            ['2'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####" },
            ['3'] = new[] { "#####", "...#.", "..#..", "...#.", "....#", "#...#", ".###." },
            ['4'] = new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." },
            ['5'] = new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." },
            ['6'] = new[] { "..##.", ".#...", "#....", "####.", "#...#", "#...#", ".###." },
            ['7'] = new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." },
            ['8'] = new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." },
            ['9'] = new[] { ".###.", "#...#", "#...#", ".####", "....#", "...#.", ".##.." },
            [' '] = new[] { "...", "...", "...", "...", "...", "...", "..." },
            ['.'] = new[] { "..", "..", "..", "..", "..", "##", "##" },
            [','] = new[] { "..", "..", "..", "..", "##", ".#", "#." },
            [':'] = new[] { "..", "##", "##", "..", "##", "##", ".." },
            [';'] = new[] { "..", "##", "##", "..", "##", ".#", "#." },
            ['!'] = new[] { "#", "#", "#", "#", "#", ".", "#" },
            ['?'] = new[] { ".###.", "#...#", "....#", "...#.", "..#..", ".....", "..#.." },
            ['-'] = new[] { "....", "....", "....", "####", "....", "....", "...." },
            ['+'] = new[] { ".....", "..#..", "..#..", "#####", "..#..", "..#..", "....." },
            ['='] = new[] { ".....", ".....", "#####", ".....", "#####", ".....", "....." },
            ['/'] = new[] { "....#", "....#", "...#.", "..#..", ".#...", "#....", "#...." },
            ['\''] = new[] { "#", "#", ".", ".", ".", ".", "." },
            ['"'] = new[] { "#.#", "#.#", "...", "...", "...", "...", "..." },
            ['('] = new[] { ".#", "#.", "#.", "#.", "#.", "#.", ".#" },
            [')'] = new[] { "#.", ".#", ".#", ".#", ".#", ".#", "#." },
            ['['] = new[] { "##", "#.", "#.", "#.", "#.", "#.", "##" },
            [']'] = new[] { "##", ".#", ".#", ".#", ".#", ".#", "##" },
            ['<'] = new[] { "...#", "..#.", ".#..", "#...", ".#..", "..#.", "...#" },
            ['>'] = new[] { "#...", ".#..", "..#.", "...#", "..#.", ".#..", "#..." },
            ['_'] = new[] { ".....", ".....", ".....", ".....", ".....", ".....", "#####" },
            ['%'] = new[] { "##..#", "##..#", "...#.", "..#..", ".#...", "#..##", "#..##" },
            ['#'] = new[] { ".#.#.", ".#.#.", "#####", ".#.#.", "#####", ".#.#.", ".#.#." },
            ['*'] = new[] { ".....", "#.#.#", ".###.", "#####", ".###.", "#.#.#", "....." },
            ['|'] = new[] { "#", "#", "#", "#", "#", "#", "#" },
            ['·'] = new[] { "..", "..", "..", "##", "##", "..", ".." },
            ['♥'] = new[] { ".....", ".#.#.", "#####", "#####", ".###.", "..#..", "....." },
            ['★'] = new[] { "..#..", "..#..", "#####", ".###.", ".###.", "##.##", "#...#" },
            ['→'] = new[] { ".....", "...#.", "....#", "#####", "....#", "...#.", "....." },
            ['←'] = new[] { ".....", ".#...", "#....", "#####", "#....", ".#...", "....." },
            ['↑'] = new[] { "..#..", ".###.", "#.#.#", "..#..", "..#..", "..#..", "..#.." },
            ['↓'] = new[] { "..#..", "..#..", "..#..", "..#..", "#.#.#", ".###.", "..#.." },
        };

        private static readonly string[] FallbackRows = { "###", "#.#", "#.#", "#.#", "#.#", "#.#", "###" };

        private static readonly Dictionary<char, Glyph> Cache = new Dictionary<char, Glyph>();
        private static Glyph _fallback;

        /// <summary>Il glifo usato per <paramref name="ch"/> (normalizzato come in Python).</summary>
        public static Glyph Get(char ch)
        {
            if (Cache.TryGetValue(ch, out var g)) return g;
            char key = Normalize(ch);
            g = Rows.TryGetValue(key, out var rows) ? Build(rows) : (_fallback ??= Build(FallbackRows));
            Cache[ch] = g;
            return g;
        }

        /// <summary>Larghezza e altezza in pixel del font (senza scala né effetti).</summary>
        public static Vector2Int Measure(string text)
        {
            if (string.IsNullOrEmpty(text)) return new Vector2Int(0, GlyphHeight);
            int w = 0;
            foreach (char c in text) w += Get(c).Width;
            w += LetterSpacing * (text.Length - 1);
            return new Vector2Int(w, GlyphHeight);
        }

        /// <summary>Maiuscola senza accento: stessa idea di unicodedata.normalize("NFKD") in Python.</summary>
        public static char Normalize(char ch)
        {
            if (Rows.ContainsKey(ch)) return ch;
            char up = char.ToUpperInvariant(ch);
            if (Rows.ContainsKey(up)) return up;
            switch (up)
            {
                case 'À': case 'Á': case 'Â': case 'Ã': case 'Ä': case 'Å': return 'A';
                case 'È': case 'É': case 'Ê': case 'Ë': return 'E';
                case 'Ì': case 'Í': case 'Î': case 'Ï': return 'I';
                case 'Ò': case 'Ó': case 'Ô': case 'Õ': case 'Ö': return 'O';
                case 'Ù': case 'Ú': case 'Û': case 'Ü': return 'U';
                case 'Ç': return 'C';
                case 'Ñ': return 'N';
                case 'Ý': return 'Y';
                default: return '\0';
            }
        }

        /// <summary>Converte le righe "#..#." in rettangoli: strisce orizzontali, poi unite in verticale se identiche.</summary>
        private static Glyph Build(string[] rows)
        {
            int width = rows[0].Length;
            var open = new List<RectInt>();   // strisce ancora estendibili verso il basso
            var done = new List<RectInt>();
            for (int y = 0; y < rows.Length; y++)
            {
                var current = new List<RectInt>();
                string row = rows[y];
                for (int x = 0; x < row.Length; x++)
                {
                    if (row[x] != '#') continue;
                    int start = x;
                    while (x + 1 < row.Length && row[x + 1] == '#') x++;
                    current.Add(new RectInt(start, y, x - start + 1, 1));
                }
                var next = new List<RectInt>();
                foreach (var r in current)
                {
                    int match = open.FindIndex(o => o.x == r.x && o.width == r.width && o.y + o.height == y);
                    if (match >= 0)
                    {
                        var o = open[match];
                        open.RemoveAt(match);
                        next.Add(new RectInt(o.x, o.y, o.width, o.height + 1));
                    }
                    else next.Add(r);
                }
                done.AddRange(open); // quelle non proseguite sono finite
                open = next;
            }
            done.AddRange(open);
            return new Glyph(width, done.ToArray());
        }
    }
}
