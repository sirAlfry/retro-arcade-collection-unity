using System.Collections.Generic;
using RetroArcade.Logic;
using UnityEngine;

namespace RetroArcade.Games
{
    /// <summary>
    /// Maze Runner: porting di <c>maze_runner.py</c>.
    /// <list type="bullet">
    /// <item>Labirinti "perfetti" (un solo percorso tra due celle) scavati con una DFS iterativa.</item>
    /// <item>Quattro difficoltà, compresa quella <b>al buio</b> dove vedi solo ciò che illumina la torcia.</item>
    /// <item><b>H</b> = aiuto: una ricerca in ampiezza (BFS) mostra per qualche secondo la strada più breve,
    /// al costo di +10 secondi.</item>
    /// <item>Movimento fluido, tieni premuto per correre, briciole sulle celle visitate, record di tempo
    /// per ogni difficoltà.</item>
    /// </list>
    /// </summary>
    public sealed class MazeRunnerScene : ArcadeScene
    {
        private const int HudH = 44;
        private const float RepeatDelay = 0.17f, RepeatRate = 0.075f;
        private const float HintPenalty = 10f, HintTime = 3f;
        private const int WinFrames = 40;
        private const float TorchRadius = 120f;

        private static readonly string[] Commands =
        {
            "Frecce o WASD: muovi (tieni premuto per correre)",
            "H: suggerimento con BFS (+10 secondi)",
        };

        private static readonly (ArcadeKey key, Int2 dir)[] KeyDirs =
        {
            (ArcadeKey.Up, Int2.Up), (ArcadeKey.W, Int2.Up), (ArcadeKey.Down, Int2.Down), (ArcadeKey.S, Int2.Down),
            (ArcadeKey.Left, Int2.Left), (ArcadeKey.A, Int2.Left), (ArcadeKey.Right, Int2.Right), (ArcadeKey.D, Int2.Right),
        };

        /// <summary>Una partita (labirinto, posizione, cronometro...).</summary>
        private sealed class Run
        {
            public int[,] Grid;
            public int Rows, Cols, Cell, Ox, Oy;
            public Int2 Goal, Pos;
            public float VisX, VisY;
            public int Moves, Hints;
            public List<Int2> HintPath = new List<Int2>();
            public float HintTimer;
            public readonly HashSet<Int2> Visited = new HashSet<Int2> { Int2.Zero };
            public readonly Stopwatch Timer = new Stopwatch { Running = false };
            public float Repeat;
            /// <summary>Muri raggruppati in strisce orizzontali (riga, x iniziale, lunghezza): pochi quad da disegnare.</summary>
            public readonly List<(int row, int x, int len)> WallRuns = new List<(int, int, int)>();

            public Vector2 Center(Int2 c) => new Vector2(Ox + c.X * Cell + Cell / 2, Oy + c.Y * Cell + Cell / 2);
        }

        private MazeLogic.Difficulty _diff;
        private bool _started;
        private Run _run;
        private Particles _particles;
        private double? _bestTime;
        private float _t;
        private float _winT = -1f;

        private string RecordKey => "maze_time_" + _diff.Key;

        public override void Create()
        {
            _particles = new Particles(App.Rng);
            _diff = MazeLogic.Difficulties[1];
            var labels = new string[MazeLogic.Difficulties.Length];
            for (int i = 0; i < labels.Length; i++) labels[i] = MazeLogic.Difficulties[i].Label;
            OpenSelectMenu("MAZE RUNNER", labels, new[] { "Scegli la dimensione del labirinto" }, 1, Palette.NeonCyan, choice =>
            {
                _diff = MazeLogic.Difficulties[choice];
                _bestTime = ScoreStore.Get(RecordKey);
                _run = NewRun();
                _started = true;
            });
            _run = NewRun(); // disegnato (coperto) mentre il menu è aperto
        }

        private Run NewRun()
        {
            var r = new Run { Grid = MazeLogic.Generate(_diff.Cols, _diff.Rows, new System.Random(App.Rng.Next())) };
            r.Rows = r.Grid.GetLength(0);
            r.Cols = r.Grid.GetLength(1);
            r.Cell = Mathf.Min(((int)W - 24) / r.Cols, ((int)H - HudH - 16) / r.Rows);
            r.Ox = ((int)W - r.Cols * r.Cell) / 2;
            r.Oy = HudH + ((int)H - HudH - r.Rows * r.Cell) / 2;
            r.Goal = new Int2(r.Cols - 1, r.Rows - 1);
            r.Pos = Int2.Zero;
            for (int y = 0; y < r.Rows; y++)
                for (int x = 0; x < r.Cols; x++)
                {
                    if (r.Grid[y, x] == 0) continue;
                    int start = x;
                    while (x + 1 < r.Cols && r.Grid[y, x + 1] != 0) x++;
                    r.WallRuns.Add((y, start, x - start + 1));
                }
            return r;
        }

        protected override void Restart()
        {
            _run = NewRun();
            _particles.Clear();
            _winT = -1f;
        }

        private bool TryMove(Int2 d)
        {
            var r = _run;
            int x = r.Pos.X + d.X, y = r.Pos.Y + d.Y;
            if (x < 0 || x >= r.Cols || y < 0 || y >= r.Rows || r.Grid[y, x] != 0) return false;
            r.Pos = new Int2(x, y);
            r.Moves++;
            r.Visited.Add(r.Pos);
            r.Timer.Running = true;
            Sfx.Play(SfxId.Move, 0.25f);
            return true;
        }

        // ───────────────────────────── Logica ─────────────────────────────

        public override void Update(float timeMs, float deltaMs)
        {
            if (!_started) return;
            float dt = deltaMs / 1000f;
            _t += dt;
            var r = _run;

            if (_winT >= 0f)
            {
                // 40 frame di festa prima della schermata finale.
                _winT += dt;
                _particles.Update(dt);
                if (_winT * 60f >= WinFrames) ShowResults();
                return;
            }

            if (PausePressed()) { OpenPauseMenu("Maze Runner", Commands); return; }
            foreach (var (key, dir) in KeyDirs)
            {
                if (!In.Pressed(key)) continue;
                TryMove(dir);
                r.Repeat = RepeatDelay;
            }
            if (In.Pressed(ArcadeKey.H))
            {
                r.HintPath = MazeLogic.ShortestPath(r.Grid, r.Pos, r.Goal);
                r.HintTimer = HintTime;
                r.Hints++;
                r.Timer.Elapsed += HintPenalty;
                Sfx.Play(SfxId.Powerup);
                App.Toast("+" + HintPenalty.ToString("0") + " SECONDI");
            }

            // Tieni premuta una freccia per correre
            Int2? held = null;
            foreach (var (key, dir) in KeyDirs)
                if (In.Held(key)) { held = dir; break; }
            if (held.HasValue)
            {
                r.Repeat -= dt;
                if (r.Repeat <= 0f)
                {
                    r.Repeat += RepeatRate;
                    TryMove(held.Value);
                }
            }

            // Movimento visivo fluido verso la cella logica
            float follow = Mathf.Min(1f, dt * 22f);
            r.VisX += (r.Pos.X - r.VisX) * follow;
            r.VisY += (r.Pos.Y - r.VisY) * follow;
            r.Timer.Update(dt);
            r.HintTimer = Mathf.Max(0f, r.HintTimer - dt);
            _particles.Update(dt);

            if (r.Pos == r.Goal)
            {
                r.Timer.Stop();
                var g = r.Center(r.Goal);
                _particles.Burst(g.x, g.y, new[] { Palette.NeonGreen, Palette.NeonYellow, Palette.White }, 60,
                                 80f, 320f, 0.5f, 1.2f);
                _winT = 0f;
            }
        }

        private void ShowResults()
        {
            _winT = -1f;
            var r = _run;
            double elapsed = System.Math.Round(r.Timer.Elapsed, 1);
            bool newRecord = ScoreStore.RecordBest(RecordKey, elapsed, lowerIsBetter: true);
            _bestTime = ScoreStore.Get(RecordKey) ?? elapsed;
            string size = _diff.Label.Split(' ')[0].ToLowerInvariant();
            OpenEndScreen("USCITA TROVATA!", new[]
            {
                $"Tempo: {Ui.FormatTime(elapsed)}   Mosse: {r.Moves}   Aiuti: {r.Hints}",
                $"Record ({size}): {Ui.FormatTime(_bestTime.Value)}",
            }, newRecord, win: true);
        }

        // ───────────────────────────── Disegno ─────────────────────────────

        public override void Draw(RetroCanvas g)
        {
            g.FillRect(0, 0, W, H, Palette.Bg);
            var r = _run;
            int c = r.Cell;

            // Muri (la "superficie" del labirinto in Python)
            g.FillRect(r.Ox, r.Oy, r.Cols * c, r.Rows * c, Palette.Rgb(10, 10, 26));
            uint wall = Palette.Rgb(40, 90, 210), top = Palette.Rgb(90, 150, 255);
            int topH = Mathf.Max(2, c / 6);
            foreach (var (row, x, len) in r.WallRuns)
            {
                g.FillRect(r.Ox + x * c, r.Oy + row * c, len * c, c, wall);
                g.FillRect(r.Ox + x * c, r.Oy + row * c, len * c, topH, top);
            }
            g.StrokeRect(r.Ox + 1, r.Oy + 1, r.Cols * c - 2, r.Rows * c - 2, 2, Palette.NeonCyan);

            uint crumb = Palette.Rgb(90, 80, 150);
            foreach (var v in r.Visited)
            {
                var p = r.Center(v);
                g.FillRect(p.x - 1, p.y - 1, 3, 3, crumb);
            }
            var goal = r.Center(r.Goal);
            float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 5f);
            g.FillRoundedRect(goal.x - c / 2 + 1, goal.y - c / 2 + 1, c - 2, c - 2, 3,
                              Palette.Lerp(Palette.Rgb(20, 120, 60), Palette.NeonGreen, pulse));
            float px = (int)(r.Ox + r.VisX * c + c / 2), py = (int)(r.Oy + r.VisY * c + c / 2);

            if (_diff.Dark && _started)
            {
                // Oscurità quasi totale con un "buco" sfumato attorno alla torcia (maschera radiale ^2.2).
                g.FillAlphaField(Palette.Black, (x, y) =>
                {
                    float d = Mathf.Sqrt((x - px) * (x - px) + (y - py) * (y - py));
                    float a = d >= TorchRadius ? 1f : Mathf.Pow(d / TorchRadius, 2.2f);
                    return Mathf.Min(252f / 255f, a);
                }, 12f);
                float beacon = 80f + 80f * pulse;
                g.FillCircle(goal.x, goal.y, c, Palette.NeonGreen, beacon / 255f);
            }
            if (r.HintTimer > 0f) // disegnato dopo l'oscurità: l'aiuto funziona anche al buio
            {
                for (int i = 0; i < r.HintPath.Count; i++)
                {
                    if ((i + (int)(_t * 12)) % 3 != 0) continue;
                    var h = r.Center(r.HintPath[i]);
                    g.FillCircle(h.x, h.y, Mathf.Max(2, c / 5), Palette.NeonYellow);
                }
            }
            g.FillCircle(px, py, Mathf.Max(3, c / 2 - 2), Palette.NeonYellow);
            g.FillCircle(px - 2, py - 2, Mathf.Max(1, c / 7), Palette.White);
            _particles.Draw(g);

            // HUD
            if (!_started) return;
            g.PixelText("TEMPO " + Ui.FormatTime(r.Timer.Elapsed), 14, HudH / 2, 2, Palette.NeonCyan, 0f, 0.5f);
            g.PixelText("MOSSE " + r.Moves, 230, HudH / 2, 2, Palette.NeonGreen, 0f, 0.5f);
            string rec = _bestTime.HasValue && _bestTime.Value != 0 ? Ui.FormatTime(_bestTime.Value) : "--:--";
            g.PixelText("RECORD " + rec, 400, HudH / 2, 2, Palette.NeonYellow, 0f, 0.5f);
            g.PixelText("H AIUTO", W - 14, HudH / 2, 2, Palette.TextDim, 1f, 0.5f);
            if (!r.Timer.Running && r.Moves == 0)
                g.PixelText("RAGGIUNGI L'USCITA VERDE!", W / 2, H - 14, 2, Palette.White, 0.5f, 0.5f);
        }
    }
}
