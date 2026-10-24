using System.Collections.Generic;

namespace RetroArcade.Logic
{
    /// <summary>Un mattone: rettangolo, punti vita (1-3) e riga (per il colore).</summary>
    public sealed class Brick
    {
        public PRect Rect;
        public int Hp;
        public readonly int MaxHp;
        public readonly int Row;

        public Brick(PRect rect, int hp, int row)
        {
            Rect = rect;
            Hp = hp;
            MaxHp = hp;
            Row = row;
        }
    }

    /// <summary>I 5 livelli di Breakout fatti a mano (poi ricominciano, più veloci): porting di <c>breakout.LEVELS</c>.</summary>
    public static class BreakoutLevels
    {
        public const int ScreenW = 800, ScreenH = 600, HudH = 44;
        /// <summary>Il campo di gioco (muri sinistro, destro e superiore).</summary>
        public static readonly PRect Field = new PRect(16, HudH + 4, ScreenW - 32, ScreenH - HudH - 4);
        public const int BrickCols = 10, BrickW = 68, BrickH = 22, BrickGap = 4;
        public const int BricksX = (ScreenW - (BrickCols * BrickW - BrickGap)) / 2;
        public const int BricksY = HudH + 40;

        /// <summary>'.' = vuoto, cifra = punti vita.</summary>
        public static readonly string[][] Levels =
        {
            new[] { "2222222222", "2222222222", "1111111111", "1111111111", "1111111111" },
            new[] { "...1111...", "..122221..", ".12333321.", "..122221..", "...1111..." },
            new[] { "1.1.1.1.1.", ".2.2.2.2.2", "3.3.3.3.3.", ".2.2.2.2.2", "1.1.1.1.1." },
            new[] { "3333333333", "1........1", "1.222222.1", "1.2.33.2.1", "1.222222.1", "1........1" },
            new[] { "1........1", "21......12", "321....123", "2321..1232", "1232112321", "3333333333" },
        };

        /// <summary>I mattoni del livello (da 1; gli schemi si ripetono).</summary>
        public static List<Brick> Build(int level)
        {
            var layout = Levels[(level - 1) % Levels.Length];
            var bricks = new List<Brick>();
            for (int row = 0; row < layout.Length; row++)
            {
                string line = layout[row];
                for (int col = 0; col < line.Length && col < BrickCols; col++)
                {
                    char ch = line[col];
                    if (ch < '1' || ch > '9') continue;
                    var rect = new PRect(BricksX + col * BrickW, BricksY + row * (BrickH + BrickGap), BrickW - BrickGap, BrickH);
                    bricks.Add(new Brick(rect, ch - '0', row));
                }
            }
            return bricks;
        }
    }
}
