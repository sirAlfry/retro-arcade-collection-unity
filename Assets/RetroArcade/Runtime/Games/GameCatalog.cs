using System;

namespace RetroArcade.Games
{
    /// <summary>
    /// Il catalogo unico dei giochi (la tupla <c>GAMES</c> di <c>retro_arcade_interface.py</c>): da qui derivano
    /// tessere, pannello informativo, record e scena da avviare. Aggiungere un gioco = aggiungere una riga.
    /// </summary>
    public static class GameCatalog
    {
        public sealed class Info
        {
            public readonly string Name;
            public readonly string SceneKey;
            public readonly string Description;
            public readonly string[] Controls;
            public readonly Func<string> Record;
            public readonly uint Accent;

            public Info(string name, string sceneKey, string description, string[] controls, Func<string> record, uint accent)
            {
                Name = name; SceneKey = sceneKey; Description = description; Controls = controls; Record = record; Accent = accent;
            }
        }

        // ── Formattazione dei record (stesse chiavi di scores.json della versione Python) ──

        private static Func<string> Rec(string key, string label = "RECORD") => () =>
        {
            double? v = ScoreStore.Get(key);
            return v.HasValue && v.Value != 0 ? label + " " + Math.Round(v.Value) : null;
        };

        private static string RecTris()
        {
            int x = ScoreStore.GetInt("tris_x"), o = ScoreStore.GetInt("tris_o"), d = ScoreStore.GetInt("tris_draw");
            return x != 0 || o != 0 || d != 0 ? $"X {x}  O {o}  PARI {d}" : null;
        }

        private static string RecMaze()
        {
            foreach (var (key, label) in new[] { ("maze_time_medium", "MEDIO"), ("maze_time_small", "PICCOLO"),
                                                 ("maze_time_large", "GRANDE"), ("maze_time_dark", "BUIO") })
            {
                double? v = ScoreStore.Get(key);
                if (v.HasValue && v.Value != 0) return $"RECORD {Ui.FormatTime(v.Value)} ({label})";
            }
            return null;
        }

        private static string RecMemory()
        {
            foreach (var key in new[] { "memory_moves_normal", "memory_moves_easy", "memory_moves_hard" })
            {
                double? v = ScoreStore.Get(key);
                if (v.HasValue && v.Value != 0) return "MIN. MOSSE " + Math.Round(v.Value);
            }
            return null;
        }

        private static string RecPuzzle()
        {
            double? moves = ScoreStore.Get("puzzle15_moves");
            if (!moves.HasValue || moves.Value == 0) return null;
            return $"MOSSE {Math.Round(moves.Value)}  TEMPO {Ui.FormatTime(ScoreStore.Get("puzzle15_time") ?? 0)}";
        }

        public static readonly Info[] Games =
        {
            new Info("Tic Tac Toe", SceneKeys.TicTacToe,
                "Tris contro un amico o una CPU imbattibile (Minimax + Alpha-Beta).",
                new[] { "Mouse o frecce + INVIO: piazza il segno",
                        "TAB: cambia modalità   1/2/3: difficoltà CPU   ESC: pausa" },
                RecTris, Palette.NeonBlue),
            new Info("Snake", SceneKeys.Snake,
                "Mangia, cresci, non morderti la coda. Il frutto blu vale +5 ma scade!",
                new[] { "Frecce o WASD: muovi il serpente", "ESC o P: pausa" },
                Rec("snake"), Palette.NeonGreen),
            new Info("Tetris", SceneKeys.Tetris,
                "Rotazione SRS con wall kick, hold, ghost piece, 3 pezzi in anteprima.",
                new[] { "←/→ muovi  ↓ soft drop  SPAZIO hard drop  ↑/X ruota  Z ruota indietro",
                        "C o SHIFT: hold   ESC o P: pausa" },
                Rec("tetris"), Palette.NeonPurple),
            new Info("Pong", SceneKeys.Pong,
                "Il primo videogioco da bar: contro la CPU (3 livelli) o in due.",
                new[] { "Giocatore 1: W/S   Giocatore 2: frecce ↑/↓",
                        "ESC o P: pausa   Vince chi arriva a 11" },
                Rec("pong_wins", "VITTORIE VS CPU"), Palette.NeonCyan),
            new Info("Breakout", SceneKeys.Breakout,
                "Distruggi i mattoni, raccogli i power-up, sopravvivi ai livelli.",
                new[] { "Mouse o frecce/A-D: muovi   SPAZIO/clic: lancia la palla",
                        "Power-up: L larga  M multi-palla  S lenta  ♥ vita   ESC: pausa" },
                Rec("breakout"), Palette.NeonOrange),
            new Info("Space Invaders", SceneKeys.SpaceInvaders,
                "Ondate di alieni, scudi distruttibili e il misterioso UFO bonus.",
                new[] { "Frecce o A/D: muovi   SPAZIO: spara", "ESC o P: pausa" },
                Rec("space_invaders"), Palette.NeonGreen),
            new Info("Maze Runner", SceneKeys.MazeRunner,
                "Labirinti generati con DFS, 4 difficoltà (anche al buio) e aiuto BFS.",
                new[] { "Frecce o WASD: muovi (tieni premuto per correre)",
                        "H: suggerimento (+10 s)   ESC: pausa" },
                RecMaze, Palette.NeonBlue),
            new Info("Memory Game", SceneKeys.MemoryGame,
                "Trova le coppie con meno mosse possibili. Tre dimensioni di griglia.",
                new[] { "Mouse: gira le carte   Frecce + INVIO: gioca da tastiera",
                        "ESC: pausa" },
                RecMemory, Palette.NeonMagenta),
            new Info("Puzzle 15", SceneKeys.Puzzle15,
                "Il gioco del quindici: sempre risolvibile, con mosse multiple a clic.",
                new[] { "Clic su una tessera (anche più tessere in fila) o frecce",
                        "Le tessere al posto giusto si illuminano   ESC: pausa" },
                RecPuzzle, Palette.NeonYellow),
        };

        public static Info ByKey(string sceneKey)
        {
            foreach (var g in Games) if (g.SceneKey == sceneKey) return g;
            return null;
        }
    }
}
