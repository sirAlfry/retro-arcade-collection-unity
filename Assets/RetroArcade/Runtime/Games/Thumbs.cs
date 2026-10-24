using UnityEngine;

namespace RetroArcade.Games
{
    /// <summary>
    /// Le miniature delle tessere del launcher (le funzioni <c>_thumb_*</c> di Python).
    /// <c>t</c> = tempo dell'animazione in secondi; 0 = miniatura ferma (solo la tessera selezionata si anima).
    /// </summary>
    internal static class Thumbs
    {
        public static void Draw(int index, RetroCanvas g, Rect r, float t)
        {
            switch (index)
            {
                case 0: Tris(g, r, t); break;
                case 1: Snake(g, r, t); break;
                case 2: Tetris(g, r, t); break;
                case 3: Pong(g, r, t); break;
                case 4: Breakout(g, r, t); break;
                case 5: Invaders(g, r, t); break;
                case 6: Maze(g, r, t); break;
                case 7: Memory(g, r, t); break;
                case 8: Puzzle(g, r, t); break;
            }
        }

        // Comodità "alla pygame": centro e bordi interi come pygame.Rect.
        private static int Cx(Rect r) => (int)r.x + (int)r.width / 2;
        private static int Cy(Rect r) => (int)r.y + (int)r.height / 2;
        private static int Right(Rect r) => (int)(r.x + r.width);
        private static int Bottom(Rect r) => (int)(r.y + r.height);

        private static void Tris(RetroCanvas g, Rect r, float t)
        {
            int cw = (int)r.width / 3, ch = (int)r.height / 3;
            uint grid = Palette.Rgb(60, 110, 200);
            for (int i = 1; i <= 2; i++)
            {
                g.FillRect(r.x + i * cw - 1, r.y + 4, 2, r.height - 8, grid);
                g.FillRect(r.x + 6, r.y + i * ch - 1, r.width - 12, 2, grid);
            }
            var marks = new (int col, int row, char m)[] { (0, 0, 'X'), (1, 1, 'O'), (2, 0, 'X'), (0, 2, 'O'), (2, 2, 'X') };
            int shown = t == 0 ? marks.Length : (int)(t * 2) % (marks.Length + 3);
            for (int k = 0; k < shown && k < marks.Length; k++)
            {
                var (col, row, m) = marks[k];
                // Rect(cella).inflate(-cw // 2, -8)
                float cx0 = r.x + col * cw + cw / 4, cy0 = r.y + row * ch + 4;
                float w = cw - cw / 2, h = ch - 8;
                if (m == 'X')
                {
                    g.Line(cx0, cy0, cx0 + w, cy0 + h, 3, Palette.NeonRed);
                    g.Line(cx0 + w, cy0, cx0, cy0 + h, 3, Palette.NeonRed);
                }
                else
                {
                    float rad = Mathf.Min(w, h) / 2f;
                    g.StrokeCircle(cx0 + w / 2, cy0 + h / 2, rad - 1.5f, 3, Palette.NeonBlue);
                }
            }
        }

        private static void Snake(RetroCanvas g, Rect r, float t)
        {
            const int seg = 10;
            int cols = (int)r.width / seg;
            int head = t != 0 ? (int)(t * 8) % cols : cols / 2;
            int y = Cy(r);
            for (int i = 0; i < 7; i++)
            {
                int x = (int)r.x + (((head - i) % cols) + cols) % cols * seg;
                uint color = Palette.Lerp(Palette.Rgb(120, 255, 120), Palette.Rgb(20, 110, 40), i / 7f);
                g.FillRoundedRect(x, y - seg / 2, seg - 1, seg - 1, 3, color);
            }
            g.FillCircle(Right(r) - 26, r.y + 14, 5, Palette.NeonRed);
            g.FillCircle(r.x + 24, Bottom(r) - 12, 4, Palette.NeonBlue);
        }

        private static void Tetris(RetroCanvas g, Rect r, float t)
        {
            const int c = 10;
            var stack = new (int x, int y, uint col)[]
            {
                (0, 5, Palette.NeonRed), (1, 5, Palette.NeonRed), (2, 5, Palette.NeonYellow), (3, 5, Palette.NeonYellow),
                (5, 5, Palette.NeonGreen), (6, 5, Palette.NeonGreen), (7, 5, Palette.NeonBlue), (8, 5, Palette.NeonBlue),
                (0, 4, Palette.NeonPurple), (1, 4, Palette.NeonPurple), (6, 4, Palette.NeonOrange), (7, 4, Palette.NeonOrange),
            };
            int ox = Cx(r) - 5 * c, oy = Bottom(r) - 6 * c - 2;
            foreach (var (x, y, col) in stack) g.FillRect(ox + x * c, oy + y * c, c - 1, c - 1, col);
            int fall = t != 0 ? (int)(t * 4) % 6 : 2;
            for (int y = 0; y < 4; y++) g.FillRect(ox + 4 * c, oy + (y + fall - 4) * c, c - 1, c - 1, Palette.NeonCyan);
        }

        private static void Pong(RetroCanvas g, Rect r, float t)
        {
            for (int y = (int)r.y + 4; y < Bottom(r) - 4; y += 10) g.FillRect(Cx(r) - 1, y, 2, 5, Palette.Rgb(90, 90, 130));
            int bx, by;
            if (t != 0)
            {
                bx = Cx(r) + (int)(Mathf.Sin(t * 2.5f) * ((int)r.width / 2 - 22));
                by = Cy(r) + (int)(Mathf.Sin(t * 3.7f) * ((int)r.height / 2 - 8));
            }
            else
            {
                bx = Cx(r) + 30;
                by = Cy(r) - 10;
            }
            int leftY = Mathf.Clamp(bx < Cx(r) ? by : Cy(r), (int)r.y + 12, Bottom(r) - 12);
            int rightY = Mathf.Clamp(bx >= Cx(r) ? by : Cy(r), (int)r.y + 12, Bottom(r) - 12);
            g.FillRect(r.x + 8, leftY - 12, 4, 24, Palette.White);
            g.FillRect(Right(r) - 12, rightY - 12, 4, 24, Palette.White);
            g.FillCircle(bx, by, 4, Palette.White);
        }

        private static void Breakout(RetroCanvas g, Rect r, float t)
        {
            var colors = new[] { Palette.NeonRed, Palette.NeonOrange, Palette.NeonYellow, Palette.NeonGreen };
            int bw = ((int)r.width - 8) / 8;
            for (int row = 0; row < colors.Length; row++)
                for (int i = 0; i < 8; i++)
                {
                    if (t != 0 && (i + row * 3 + (int)(t * 3)) % 11 == 0) continue;
                    g.FillRect(r.x + 4 + i * bw, r.y + 4 + row * 8, bw - 2, 6, colors[row]);
                }
            int px = Cx(r) + (t != 0 ? (int)(Mathf.Sin(t * 2) * 50) : 0);
            g.FillRoundedRect(px - 18, Bottom(r) - 8, 36, 5, 2, Palette.Text);
            int bounce = t != 0 ? (int)(Mathf.Abs(Mathf.Sin(t * 4)) * 14) : 0;
            g.FillCircle(px + 10, Bottom(r) - 22 - bounce, 3, Palette.White);
        }

        /// <summary>L'alieno 11×8 usato anche per l'icona del gioco.</summary>
        public static readonly string[] Alien =
        {
            "..#.....#..", "...#...#...", "..#######..", ".##.###.##.",
            "###########", "#.#######.#", "#.#.....#.#", "...##.##...",
        };

        public static void DrawPixelArt(RetroCanvas g, string[] art, float ox, float oy, float px, uint color)
        {
            for (int y = 0; y < art.Length; y++)
            {
                string row = art[y];
                for (int x = 0; x < row.Length; x++)
                {
                    if (row[x] != '#') continue;
                    int start = x;
                    while (x + 1 < row.Length && row[x + 1] == '#') x++;
                    g.FillRect(ox + start * px, oy + y * px, (x - start + 1) * px, px, color);
                }
            }
        }

        private static void Invaders(RetroCanvas g, Rect r, float t)
        {
            int sway = t != 0 ? (int)(Mathf.Sin(t * 2) * 12) : 0;
            for (int k = 0; k < 3; k++)
                DrawPixelArt(g, Alien, r.x + 26 + k * 52 + sway, r.y + 8, 3, k != 1 ? Palette.NeonGreen : Palette.NeonMagenta);
            int sx = Cx(r) + (t != 0 ? (int)(Mathf.Sin(t * 1.3f) * 60) : 0);
            int b = Bottom(r);
            g.FillTriangle(sx, b - 16, sx - 9, b - 4, sx + 9, b - 4, Palette.NeonCyan);
            if (t != 0)
            {
                int by = b - 20 - (int)((t * 120) % 40);
                g.FillRect(sx - 1, by, 2, 6, Palette.NeonYellow);
            }
        }

        private static readonly string[] MazeArt =
        {
            "#############", "#...#.......#", "#.#.#.#####.#", "#.#...#...#.#",
            "#.#####.#.#.#", "#.......#..E#", "#############",
        };

        private static readonly Vector2Int[] MazePath =
        {
            new Vector2Int(1, 1), new Vector2Int(1, 2), new Vector2Int(1, 3), new Vector2Int(1, 4), new Vector2Int(1, 5),
            new Vector2Int(2, 5), new Vector2Int(3, 5), new Vector2Int(4, 5), new Vector2Int(5, 5), new Vector2Int(6, 5),
            new Vector2Int(7, 5), new Vector2Int(7, 4), new Vector2Int(7, 3), new Vector2Int(8, 3), new Vector2Int(9, 3),
            new Vector2Int(9, 4), new Vector2Int(9, 5), new Vector2Int(10, 5),
        };

        private static void Maze(RetroCanvas g, Rect r, float t)
        {
            uint wall = Palette.Rgb(40, 110, 230);
            const int c = 9;
            int ox = Cx(r) - MazeArt[0].Length * c / 2, oy = Cy(r) - MazeArt.Length * c / 2;
            for (int y = 0; y < MazeArt.Length; y++)
                for (int x = 0; x < MazeArt[y].Length; x++)
                {
                    char ch = MazeArt[y][x];
                    if (ch == '#') g.FillRect(ox + x * c, oy + y * c, c - 1, c - 1, wall);
                    else if (ch == 'E') g.FillRect(ox + x * c + 1, oy + y * c + 1, c - 3, c - 3, Palette.NeonGreen);
                }
            int step = t != 0 ? (int)(t * 6) % MazePath.Length : 0;
            for (int k = 0; k < step; k++)
                g.FillRect(ox + MazePath[k].x * c + 3, oy + MazePath[k].y * c + 3, c - 7, c - 7, Palette.Rgb(70, 70, 120));
            var p = MazePath[step];
            g.FillRect(ox + p.x * c + 1, oy + p.y * c + 1, c - 3, c - 3, Palette.NeonYellow);
        }

        private static void Memory(RetroCanvas g, Rect r, float t)
        {
            var colors = new[] { Palette.NeonRed, Palette.NeonBlue, Palette.NeonGreen, Palette.NeonYellow };
            const int size = 26, gap = 8;
            int ox = Cx(r) - (4 * size + 3 * gap) / 2;
            for (int row = 0; row < 2; row++)
                for (int i = 0; i < 4; i++)
                {
                    int x = ox + i * (size + gap);
                    int y = Cy(r) - size - gap / 2 + row * (size + gap);
                    float phase = Mathf.Cos(t * 2 + i + row * 2);
                    float flip = t != 0 ? Mathf.Abs(phase) : 1f;
                    int w = Mathf.Max(2, (int)(size * flip));
                    float rx = x + (size - w) / 2;
                    bool faceUp = t == 0 ? (i + row) % 2 == 0 : phase > 0;
                    if (faceUp)
                    {
                        g.FillRoundedRect(rx, y, w, size, 3, Palette.Rgb(230, 230, 240));
                        g.FillCircle(rx + w / 2, y + size / 2, Mathf.Max(1, w / 4), colors[(i + row) % 4]);
                    }
                    else
                    {
                        g.FillRoundedRect(rx, y, w, size, 3, Palette.Rgb(90, 50, 150));
                        g.StrokeRoundedRect(rx + 0.5f, y + 0.5f, w - 1, size - 1, 2.5f, 1, Palette.NeonMagenta);
                    }
                }
        }

        private static void Puzzle(RetroCanvas g, Rect r, float t)
        {
            const int tile = 15, gap = 2;
            int ox = Cx(r) - (4 * tile + 3 * gap) / 2, oy = Cy(r) - (4 * tile + 3 * gap) / 2;
            float slide = t != 0 ? Mathf.Sin(t * 2) * 0.5f + 0.5f : 0f;
            for (int n = 0; n < 15; n++)
            {
                int row = n / 4, col = n % 4;
                int x = ox + col * (tile + gap), y = oy + row * (tile + gap);
                if (n == 14) x += (int)(slide * (tile + gap));
                g.FillRoundedRect(x, y, tile, tile, 2, Palette.Rgb(70, 70, 110));
                g.PixelText((n + 1).ToString(), x + tile / 2f, y + tile / 2f, 1, Palette.NeonYellow, 0.5f, 0.5f);
            }
        }
    }
}
