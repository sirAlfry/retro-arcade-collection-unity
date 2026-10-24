using System;
using System.Collections.Generic;

namespace RetroArcade.Logic
{
    /// <summary>Una carta del Memory: icona, posizione e stato (animazioni comprese).</summary>
    public sealed class MemoryCard
    {
        public readonly string Icon;
        public readonly PRect Rect;
        public bool FaceUp, Matched, Wrong;
        /// <summary>0 = dorso, 1 = faccia (animato).</summary>
        public float Flip;
        /// <summary>Timer del bagliore verde dopo una coppia trovata.</summary>
        public float Glow;

        public MemoryCard(string icon, PRect rect)
        {
            Icon = icon;
            Rect = rect;
        }
    }

    /// <summary>Regole e dati del Memory (porting di <c>memory_game.py</c>): griglie, icone, distribuzione delle carte.</summary>
    public static class MemoryLogic
    {
        public const int ScreenW = 800, ScreenH = 600, HudH = 44, Gap = 12;

        public sealed class BoardSize
        {
            public readonly string Label, Key;
            public readonly int Cols, Rows;
            public BoardSize(string label, string key, int cols, int rows) { Label = label; Key = key; Cols = cols; Rows = rows; }
        }

        public static readonly BoardSize[] Sizes =
        {
            new BoardSize("FACILE  4 X 3", "easy", 4, 3),
            new BoardSize("NORMALE  6 X 3", "normal", 6, 3),
            new BoardSize("DIFFICILE  6 X 4", "hard", 6, 4),
        };

        /// <summary>Le 12 icone pixel-art 7×7, ognuna col suo colore (0xRRGGBB), nello stesso ordine di Python.</summary>
        public static readonly (string name, string[] art, uint color)[] Icons =
        {
            ("heart", new[] { ".##.##.", "#######", "#######", "#######", ".#####.", "..###..", "...#..." }, 0xff465a),
            ("star", new[] { "...#...", "...#...", "#######", ".#####.", "..###..", ".##.##.", "##...##" }, 0xffe63c),
            ("diamond", new[] { "...#...", "..###..", ".#####.", "#######", ".#####.", "..###..", "...#..." }, 0x00f0ff),
            ("club", new[] { "..###..", "..###..", "##.#.##", "#######", "##.#.##", "...#...", "..###.." }, 0x3cff78),
            ("spade", new[] { "...#...", "..###..", ".#####.", "#######", "#######", "...#...", "..###.." }, 0x9646ff),
            ("moon", new[] { "..###..", ".##....", "##.....", "##.....", "##.....", ".##....", "..###.." }, 0xb4c8ff),
            ("note", new[] { "...###.", "...#.##", "...#...", "...#...", ".###...", "####...", ".##...." }, 0xff3cc8),
            ("bolt", new[] { "...###.", "..###..", ".###...", "#######", "...###.", "..###..", ".##...." }, 0xff9628),
            ("smile", new[] { ".#####.", "#######", "##.#.##", "#######", "#.###.#", "##...##", ".#####." }, 0xc8ff3c),
            ("mushroom", new[] { "..###..", ".#.#.#.", "#######", "#.###.#", "..#.#..", "..#.#..", "..###.." }, 0xff78aa),
            ("crown", new[] { "#..#..#", "##.#.##", "#######", "#######", "#######", ".......", "#######" }, 0xffc828),
            ("ghost", new[] { "..###..", ".#####.", "##.#.##", "#######", "#######", "#######", "#.#.#.#" }, 0x3c82ff),
        };

        public static (string[] art, uint color) Icon(string name)
        {
            foreach (var i in Icons) if (i.name == name) return (i.art, i.color);
            throw new ArgumentException("Icona sconosciuta: " + name);
        }

        /// <summary>Mescola le icone e dispone le carte in una griglia centrata.</summary>
        public static List<MemoryCard> Deal(BoardSize size, Random rng)
        {
            rng ??= new Random();
            int pairs = size.Cols * size.Rows / 2;
            // random.sample(list(ICONS), pairs) * 2, poi shuffle
            var pool = new List<string>();
            foreach (var i in Icons) pool.Add(i.name);
            var chosen = new List<string>();
            for (int k = 0; k < pairs; k++)
            {
                int j = rng.Next(pool.Count);
                chosen.Add(pool[j]);
                pool.RemoveAt(j);
            }
            var names = new List<string>(chosen);
            names.AddRange(chosen);
            for (int i = names.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (names[i], names[j]) = (names[j], names[i]);
            }
            int card = Math.Min(Math.Min((ScreenW - 80) / size.Cols - Gap, (ScreenH - HudH - 70) / size.Rows - Gap), 130);
            int totalW = size.Cols * card + (size.Cols - 1) * Gap;
            int totalH = size.Rows * card + (size.Rows - 1) * Gap;
            int ox = (ScreenW - totalW) / 2, oy = HudH + (ScreenH - HudH - totalH) / 2;
            var cards = new List<MemoryCard>();
            for (int r = 0; r < size.Rows; r++)
                for (int c = 0; c < size.Cols; c++)
                    cards.Add(new MemoryCard(names[r * size.Cols + c], new PRect(ox + c * (card + Gap), oy + r * (card + Gap), card, card)));
            return cards;
        }
    }
}
