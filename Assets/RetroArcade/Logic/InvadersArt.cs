using System;
using System.Collections.Generic;

namespace RetroArcade.Logic
{
    /// <summary>Gli sprite pixel-art di Space Invaders ('#' = pixel acceso), come in <c>space_invaders.py</c>.</summary>
    public static class InvadersArt
    {
        /// <summary>Due frame di animazione per ogni tipo di alieno.</summary>
        public static readonly Dictionary<string, string[][]> Sprites = new Dictionary<string, string[][]>
        {
            ["squid"] = new[]
            {
                new[] { "...##...", "..####..", ".######.", "##.##.##", "########", "..#..#..", ".#.##.#.", "#.#..#.#" },
                new[] { "...##...", "..####..", ".######.", "##.##.##", "########", ".#.##.#.", "#......#", ".#....#." },
            },
            ["crab"] = new[]
            {
                new[] { "..#.....#..", "...#...#...", "..#######..", ".##.###.##.", "###########", "#.#######.#", "#.#.....#.#", "...##.##..." },
                new[] { "..#.....#..", "#..#...#..#", "#.#######.#", "###.###.###", "###########", ".#########.", "..#.....#..", ".#.......#." },
            },
            ["octopus"] = new[]
            {
                new[] { "....####....", ".##########.", "############", "###..##..###", "############", "...##..##...", "..##.##.##..", "##........##" },
                new[] { "....####....", ".##########.", "############", "###..##..###", "############", "..###..###..", ".##..##..##.", "..##....##.." },
            },
        };

        public static readonly string[] Player =
        {
            "......#......", ".....###.....", ".....###.....", ".###########.",
            "#############", "#############", "#############", "#############",
        };

        public static readonly string[] Ufo =
        {
            ".....######.....", "...##########...", "..############..", ".##.##.##.##.##.",
            "################", "..###..##..###..", "...#........#...",
        };

        public static readonly string[] Explosion =
        {
            "#...#...#", ".#..#..#.", "..#...#..", "##.....##", "..#...#..", ".#..#..#.", "#...#...#",
        };

        public static readonly string[] Bunker =
        {
            "....############....", "...##############...", "..################..",
            ".##################.", "####################", "####################",
            "####################", "####################", "####################",
            "######........######", "#####..........#####", "####............####",
            "####............####",
        };

        public static readonly string[] RowTypes = { "squid", "crab", "crab", "octopus", "octopus" };
        public static readonly Dictionary<string, int> Points = new Dictionary<string, int> { ["squid"] = 30, ["crab"] = 20, ["octopus"] = 10 };
    }

    /// <summary>Uno scudo distruttibile fatto di celle quadrate che si sgretolano colpo dopo colpo.</summary>
    public sealed class Bunker
    {
        public const int Cell = 4;
        public readonly int X, Y;
        public readonly HashSet<Int2> Cells = new HashSet<Int2>();
        private readonly Random _rng;

        public Bunker(int x, int y, Random rng)
        {
            X = x;
            Y = y;
            _rng = rng ?? new Random();
            var art = InvadersArt.Bunker;
            for (int cy = 0; cy < art.Length; cy++)
                for (int cx = 0; cx < art[cy].Length; cx++)
                    if (art[cy][cx] == '#') Cells.Add(new Int2(cx, cy));
        }

        public PRect CellRect(Int2 c) => new PRect(X + c.X * Cell, Y + c.Y * Cell, Cell, Cell);

        public PRect Bounds => new PRect(X, Y, InvadersArt.Bunker[0].Length * Cell, InvadersArt.Bunker.Length * Cell);

        /// <summary>Erode le celle toccate da <paramref name="rect"/> (più qualche vicina). True se ha colpito qualcosa.</summary>
        public bool Hit(PRect rect, int splash = 2)
        {
            if (Cells.Count == 0 || !rect.Collides(Bounds)) return false;
            var touched = new List<Int2>();
            foreach (var c in Cells) if (CellRect(c).Collides(rect)) touched.Add(c);
            if (touched.Count == 0) return false;
            foreach (var c in touched)
            {
                for (int dx = -splash; dx <= splash; dx++)
                    for (int dy = -splash; dy <= splash; dy++)
                        if (Math.Abs(dx) + Math.Abs(dy) <= splash && _rng.NextDouble() < 0.65)
                            Cells.Remove(new Int2(c.X + dx, c.Y + dy));
                Cells.Remove(c);
            }
            return true;
        }
    }
}
