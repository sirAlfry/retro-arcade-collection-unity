using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using RetroArcade.Logic;
using UnityEngine;

namespace RetroArcade.Preview
{
    /// <summary>Host finto: niente MonoBehaviour, niente audio, input iniettabile.</summary>
    internal sealed class FakeHost : IArcadeHost
    {
        public ArcadeInput Controls { get; } = new ArcadeInput();
        public Sfx Sfx { get; } = Sfx.CreateSilent();
        public System.Random Rng { get; }
        public bool CanQuit => true;
        public float Fps => 60f;
        public bool ShowFps => true;
        public bool SoundOn { get; private set; } = true;
        public bool CrtOn { get; private set; } = true;
        public string RequestedScene;
        public bool QuitRequested;
        public string LastToast;
        public FakeHost(int seed) => Rng = new System.Random(seed);
        public void StartScene(string key) => RequestedScene = key;
        public void Quit() => QuitRequested = true;
        public void ToggleSound() { SoundOn = !SoundOn; LastToast = SoundOn ? "AUDIO ON" : "AUDIO OFF"; }
        public void ToggleCrt() { CrtOn = !CrtOn; LastToast = CrtOn ? "FILTRO CRT ON" : "FILTRO CRT OFF"; }
        public void Toast(string message, float seconds = 1.6f) => LastToast = message;
    }

    /// <summary>Piccolo "regista": fa avanzare la scena a 60 fps simulati e preme tasti/click.</summary>
    internal sealed class Sim
    {
        public const float Dt = 1000f / 60f;
        public readonly FakeHost Host;
        public readonly ArcadeScene Scene;

        public Sim(FakeHost host, ArcadeScene scene) { Host = host; Scene = scene; }

        public void Run(float ms, Action perFrame = null)
        {
            for (float t = 0; t < ms; t += Dt)
            {
                perFrame?.Invoke();
                Scene.Tick(Dt);
                Host.Controls.EndInjectedFrame(Dt);
            }
        }

        public void Press(ArcadeKey k)
        {
            Host.Controls.Inject(k, true, true);
            Scene.Tick(Dt);
            Host.Controls.EndInjectedFrame(Dt);
            Host.Controls.Inject(k, false, false);
        }

        public void Click(float x, float y)
        {
            Host.Controls.InjectPointer(new Vector2(x, y), true);
            Scene.Tick(Dt);
            Host.Controls.EndInjectedFrame(Dt);
        }

        public object Field(string name) => Scene.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(Scene);
        public void SetField(string name, object value) => Scene.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(Scene, value);
        public void Call(string method) => Scene.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Scene, null);
    }

    internal static class Program
    {
        private const int Scale = 2; // come renderScale = 2 in ArcadeApp

        private static int Main(string[] args)
        {
            ScoreStore.Storage = new MemoryScoreStorage();
            if (args.Length > 0 && args[0] == "--fuzz") return Fuzz(args.Length > 1 ? int.Parse(args[1]) : 120);

            string outDir = args.Length > 0 ? args[0] : "screenshots";
            Directory.CreateDirectory(outDir);

            ScoreStore.Set("snake", 46); ScoreStore.Set("tetris", 1380); ScoreStore.Set("breakout", 410);
            ScoreStore.Set("ttt.X", 3); ScoreStore.Set("ttt.O", 5); ScoreStore.Set("ttt.draws", 2);

            var canvas = new RetroCanvas(Scale, false);
            var shots = BuildShots();
            string filter = args.Length > 1 ? args[1] : null; // facoltativo: solo gli screenshot che contengono questo testo
            foreach (var (name, key, setup) in shots)
            {
                if (filter != null && !name.Contains(filter)) continue;
                var host = new FakeHost(2026);
                var scene = SceneRegistry.Factories[key]();
                scene.Key = key;
                scene.Attach(host);
                scene.Create();
                var sim = new Sim(host, scene);
                setup(sim);

                canvas.Begin();
                scene.Render(canvas);
                var rgb = SoftwareRaster.Render(canvas, Scale, 0x14142a);
                string path = Path.Combine(outDir, name + ".png");
                SoftwareRaster.SavePng(path, rgb, 800 * Scale, 600 * Scale);
                Console.WriteLine($"{path}  ({canvas.VertexCount} vertici, {canvas.Triangles.Count / 3} triangoli)");
            }
            return 0;
        }

        private static List<(string, string, Action<Sim>)> BuildShots() => new List<(string, string, Action<Sim>)>
        {
            ("01_launcher", SceneKeys.Launcher, s => s.Run(1300)),
            ("02_launcher_crediti", SceneKeys.Launcher, s => { s.Press(ArcadeKey.C); s.Run(2600); }),
            ("03_tris_menu", SceneKeys.TicTacToe, s => s.Press(ArcadeKey.Down)),
            ("04_tris_vittoria", SceneKeys.TicTacToe, s =>
            {
                s.Press(ArcadeKey.Down); s.Press(ArcadeKey.Down); s.Press(ArcadeKey.Enter); // 2 giocatori
                foreach (int idx in new[] { 0, 3, 4, 5, 8 }) { ClickTtt(s, idx); s.Run(330); }
                s.Run(500);
            }),
            ("04b_tris_cpu", SceneKeys.TicTacToe, s =>
            {
                s.Press(ArcadeKey.Down); s.Press(ArcadeKey.Enter); // CPU impossibile
                ClickTtt(s, 0); s.Run(200);
            }),
            ("05_snake_inizio", SceneKeys.Snake, s => s.Run(30)),
            ("05_snake", SceneKeys.Snake, s => { s.Run(30); s.Press(ArcadeKey.Up); PlaySnake(s, 9); s.Run(120); }),
            ("06_snake_game_over", SceneKeys.Snake, s => { s.Press(ArcadeKey.Up); s.Run(5000); }),
            ("07_snake_pausa", SceneKeys.Snake, s => { s.Press(ArcadeKey.Up); s.Run(500); s.Press(ArcadeKey.Escape); s.Run(100); }),
            ("08_tetris", SceneKeys.Tetris, s => { PlayTetris(s, 26); s.Run(450); s.Press(ArcadeKey.C); s.Run(300); }),
            ("09_tetris_pausa", SceneKeys.Tetris, s => { PlayTetris(s, 12); s.Press(ArcadeKey.Escape); s.Run(50); }),
            ("10_pong_servizio", SceneKeys.Pong, s => { s.Press(ArcadeKey.Enter); s.Run(480); }),
            ("10_pong", SceneKeys.Pong, s =>
            {
                s.Press(ArcadeKey.Enter);
                int f = 0;
                s.Run(1650, () => { f++; s.Host.Controls.Inject(ArcadeKey.W, f > 40 && f < 70, f == 41); });
            }),
            ("11_pong_vittoria", SceneKeys.Pong, s =>
            {
                s.Press(ArcadeKey.Enter); s.Run(300);
                var score = (int[])s.Field("_score"); score[0] = 10; score[1] = 7;
                s.SetField("_ball", null); // forza la fine: la prossima palla persa dalla CPU dà l'11
                s.SetField("_ball", MakePongBall(s)); s.Run(4000);
            }),
            ("12_breakout_inizio", SceneKeys.Breakout, s => s.Run(600)),
            ("12_breakout", SceneKeys.Breakout, s => { s.Run(1500); s.Press(ArcadeKey.Space); s.Run(1400); }),
            ("13_space_invaders", SceneKeys.SpaceInvaders, s =>
            {
                int frame = 0;
                s.Run(5200, () =>
                {
                    frame++;
                    s.Host.Controls.Inject(ArcadeKey.Left, frame % 160 < 80, frame % 160 == 0);
                    s.Host.Controls.Inject(ArcadeKey.Right, frame % 160 >= 80, frame % 160 == 80);
                    s.Host.Controls.Inject(ArcadeKey.Space, frame % 20 == 0, frame % 20 == 0);
                });
            }),
            ("14_maze_medio", SceneKeys.MazeRunner, s => { s.Press(ArcadeKey.Enter); WalkMaze(s, 0.55f); }),
            ("15_maze_buio", SceneKeys.MazeRunner, s =>
            {
                s.Press(ArcadeKey.Down); s.Press(ArcadeKey.Down); s.Press(ArcadeKey.Enter);
                WalkMaze(s, 0.35f); s.Press(ArcadeKey.H); s.Run(300);
            }),
            ("16_memory", SceneKeys.MemoryGame, s =>
            {
                s.Press(ArcadeKey.Enter);
                var pairs = MemoryPairs(s);
                for (int p = 0; p < 3; p++) { ClickCard(s, pairs[p].a); ClickCard(s, pairs[p].b); s.Run(700); }
                ClickCard(s, pairs[4].a); s.Run(250); ClickCard(s, pairs[5].a); s.Run(60); // seconda carta a metà giro
            }),
            ("17_memory_vittoria", SceneKeys.MemoryGame, s =>
            {
                s.Press(ArcadeKey.Enter);
                foreach (var (a, b) in MemoryPairs(s)) { ClickCard(s, a); ClickCard(s, b); s.Run(700); }
                s.Run(1000);
            }),
            ("18_puzzle15", SceneKeys.Puzzle15, s =>
            {
                // Stessa scacchiera dello screenshot di riferimento Python; poi una tessera a metà scorrimento
                s.SetField("_tiles", new[] { 1, 2, 3, 4, 5, 6, 11, 7, 9, 10, 0, 8, 13, 14, 15, 12 });
                s.Run(600);
                s.Press(ArcadeKey.Left); s.Run(33);
            }),
            ("19_puzzle15_risolto", SceneKeys.Puzzle15, s =>
            {
                s.SetField("_tiles", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 0, 15 });
                s.Run(600);
                s.Press(ArcadeKey.Left);
                s.Run(400);
            }),
        };

        /// <summary>
        /// "Scimmia" che preme tasti e clicca a caso su ogni scena per <paramref name="seconds"/> secondi simulati
        /// (5 semi diversi), disegnando ogni frame: se qualcosa lancia un'eccezione, lo segnala.
        /// </summary>
        private static int Fuzz(int seconds)
        {
            var keys = (ArcadeKey[])Enum.GetValues(typeof(ArcadeKey));
            var canvas = new RetroCanvas(Scale, false);
            int failures = 0;
            foreach (var key in SceneRegistry.Factories.Keys)
            {
                long frames = 0;
                for (int seed = 1; seed <= 5; seed++)
                {
                    var rnd = new System.Random(seed * 7919);
                    var host = new FakeHost(seed);
                    var scene = SceneRegistry.Factories[key]();
                    scene.Key = key;
                    try
                    {
                        scene.Attach(host);
                        scene.Create();
                        var held = new bool[keys.Length];
                        for (float t = 0; t < seconds * 1000f; t += Sim.Dt)
                        {
                            for (int k = 0; k < keys.Length; k++)
                            {
                                bool press = rnd.NextDouble() < 0.012;
                                if (press) held[k] = true;
                                else if (held[k] && rnd.NextDouble() < 0.05) held[k] = false;
                                host.Controls.Inject(keys[k], held[k], press);
                            }
                            if (rnd.NextDouble() < 0.05)
                                host.Controls.InjectPointer(new Vector2((float)rnd.NextDouble() * 800f, (float)rnd.NextDouble() * 600f), rnd.NextDouble() < 0.5);
                            // ogni tanto un frame lento, come una finestra trascinata
                            float dt = rnd.NextDouble() < 0.002 ? 100f : Sim.Dt;
                            scene.Tick(dt);
                            host.Controls.EndInjectedFrame(dt);
                            canvas.Begin();
                            scene.Render(canvas);
                            frames++;
                        }
                    }
                    catch (Exception e)
                    {
                        failures++;
                        Console.WriteLine($"✗ {key} seed {seed}: {e}");
                    }
                }
                Console.WriteLine($"{key,-20} {frames,8} frame simulati");
            }
            Console.WriteLine(failures == 0 ? "OK: nessuna eccezione." : $"{failures} errori.");
            return failures == 0 ? 0 : 1;
        }

        // ─────────── helper per i singoli giochi ───────────

        /// <summary>Una palla di Pong già lanciata verso la porta della CPU, per forzare un punto.</summary>
        private static object MakePongBall(Sim s)
        {
            var ballType = s.Scene.GetType().GetNestedType("Ball", System.Reflection.BindingFlags.NonPublic);
            var ball = Activator.CreateInstance(ballType, true);
            ballType.GetField("X").SetValue(ball, 700f);
            ballType.GetField("Y").SetValue(ball, 40f);
            ballType.GetField("Vx").SetValue(ball, 900f);
            ballType.GetField("Vy").SetValue(ball, 0f);
            s.SetField("_serveTimer", 0f);
            return ball;
        }

        private static void ClickTtt(Sim s, int idx) =>
            s.Click(175 + 75 + idx % 3 * 150, 52 + 75 + idx / 3 * 150);

        private static void ClickCard(Sim s, int i)
        {
            var r = ((List<MemoryCard>)s.Field("_cards"))[i].Rect;
            s.Click(r.CenterX, r.CenterY);
        }

        private static List<(int a, int b)> MemoryPairs(Sim s)
        {
            var cards = (List<MemoryCard>)s.Field("_cards");
            return Enumerable.Range(0, cards.Count)
                .GroupBy(i => cards[i].Icon)
                .Select(g => (g.First(), g.Last()))
                .ToList();
        }

        private static void WalkMaze(Sim s, float fraction)
        {
            var run = s.Field("_run");
            var maze = (int[,])run.GetType().GetField("Grid").GetValue(run);
            int rows = maze.GetLength(0), cols = maze.GetLength(1);
            var path = BfsPath(maze, cols - 1, rows - 1);
            int steps = (int)(path.Count * fraction);
            s.Run(1500); // un po' di cronometro
            for (int i = 1; i <= steps; i++)
            {
                var d = path[i] - path[i - 1];
                s.Press(d.x < 0 ? ArcadeKey.Left : d.x > 0 ? ArcadeKey.Right : d.y < 0 ? ArcadeKey.Up : ArcadeKey.Down);
                s.Run(50);
            }
            s.Run(300);
        }

        private static List<Vector2Int> BfsPath(int[,] g, int gx, int gy)
        {
            int rows = g.GetLength(0), cols = g.GetLength(1);
            var prev = new Dictionary<Vector2Int, Vector2Int>();
            var q = new Queue<Vector2Int>();
            var start = Vector2Int.zero;
            q.Enqueue(start); prev[start] = start;
            var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            while (q.Count > 0)
            {
                var c = q.Dequeue();
                if (c.x == gx && c.y == gy) break;
                foreach (var d in dirs)
                {
                    var n = c + d;
                    if (n.x < 0 || n.y < 0 || n.x >= cols || n.y >= rows || g[n.y, n.x] == 1 || prev.ContainsKey(n)) continue;
                    prev[n] = c; q.Enqueue(n);
                }
            }
            var path = new List<Vector2Int>();
            for (var p = new Vector2Int(gx, gy); ; p = prev[p]) { path.Add(p); if (p == start) break; }
            path.Reverse();
            return path;
        }

        /// <summary>Un piccolo bot (altezza + buchi) che gioca a Tetris tramite i tasti della scena.</summary>
        /// <summary>Bot di Snake: va verso la mela rossa col percorso più breve (BFS) evitando il corpo.</summary>
        private static void PlaySnake(Sim s, int apples)
        {
            var dirs = new[] { (Int2.Up, ArcadeKey.Up), (Int2.Down, ArcadeKey.Down), (Int2.Left, ArcadeKey.Left), (Int2.Right, ArcadeKey.Right) };
            for (int frame = 0; frame < 60 * 60; frame++)
            {
                var game = (SnakeLogic)s.Field("_game");
                if (!game.Alive || game.Body.Count >= 3 + apples) return;
                var head = game.Body[0];
                var blocked = new HashSet<Int2>(game.Body.GetRange(0, game.Body.Count - 1));
                // BFS dalla mela verso la testa: il primo passo è la casella vicina alla testa più vicina alla mela
                var dist = new Dictionary<Int2, int> { [game.Red] = 0 };
                var queue = new Queue<Int2>();
                queue.Enqueue(game.Red);
                while (queue.Count > 0)
                {
                    var c = queue.Dequeue();
                    foreach (var (d, _) in dirs)
                    {
                        var n = c + d;
                        if (n.X < 0 || n.Y < 0 || n.X >= game.Cols || n.Y >= game.Rows || dist.ContainsKey(n) || blocked.Contains(n)) continue;
                        dist[n] = dist[c] + 1;
                        queue.Enqueue(n);
                    }
                }
                ArcadeKey? best = null;
                int bestDist = int.MaxValue;
                foreach (var (d, key) in dirs)
                {
                    if (d == -game.Direction) continue;
                    if (dist.TryGetValue(head + d, out int nd) && nd < bestDist) { bestDist = nd; best = key; }
                }
                var want = best ?? ArcadeKey.Up;
                bool sameDir = dirs.First(x => x.Item2 == want).Item1 == game.Direction;
                if (!sameDir && game.Queue.Count == 0) s.Press(want); else s.Run(Sim.Dt);
            }
        }

        private static void PlayTetris(Sim s, int pieces)
        {
            for (int n = 0; n < pieces; n++)
            {
                var game = (TetrisLogic)s.Field("_game");
                if (game.GameOver || game.Piece == null) return;
                var (rot, tx) = BestPlacement(game);
                for (int r = 0; r < rot; r++) s.Press(ArcadeKey.Up);
                for (int guard = 0; guard < 12 && game.Piece != null && game.Piece.X != tx; guard++)
                    s.Press(game.Piece.X < tx ? ArcadeKey.Right : ArcadeKey.Left);
                s.Press(ArcadeKey.Space);
                s.Run(400); // lascia finire l'animazione delle righe
            }
        }

        /// <summary>Piccolo bot (altezza, buchi, irregolarità, righe) per avere screenshot "giocati".</summary>
        private static (int rot, int x) BestPlacement(TetrisLogic g)
        {
            double best = double.MaxValue; (int, int) choice = (0, g.Piece.X);
            for (int rot = 0; rot < 4; rot++)
            {
                var shape = TetrisLogic.Rotated(g.Piece.Kind, g.Piece.Rotation + rot);
                for (int x = -3; x < g.Cols; x++)
                {
                    if (g.Collides(shape, x, g.Piece.Y)) continue;
                    int y = g.Piece.Y;
                    while (!g.Collides(shape, x, y + 1)) y++;
                    var b = new int[g.Rows, g.Cols];
                    for (int r = 0; r < g.Rows; r++) for (int c = 0; c < g.Cols; c++) b[r, c] = g.Board[r, c] != '\0' ? 1 : 0;
                    for (int r = 0; r < shape.Length; r++)
                        for (int c = 0; c < shape[r].Length; c++)
                            if (shape[r][c] != 0 && y + r >= 0) b[y + r, x + c] = 1;
                    double score = Evaluate(b, g.Rows, g.Cols);
                    if (score < best) { best = score; choice = (rot, x); }
                }
            }
            return choice;
        }

        private static double Evaluate(int[,] b, int rows, int cols)
        {
            int holes = 0, agg = 0, lines = 0, bump = 0, prevH = -1;
            for (int r = 0; r < rows; r++)
            {
                bool full = true;
                for (int c = 0; c < cols; c++) full &= b[r, c] != 0;
                if (full) lines++;
            }
            for (int c = 0; c < cols; c++)
            {
                int h = 0; bool seen = false;
                for (int r = 0; r < rows; r++)
                {
                    if (b[r, c] != 0) { if (!seen) { h = rows - r; seen = true; } }
                    else if (seen) holes++;
                }
                agg += h;
                if (prevH >= 0) bump += Math.Abs(h - prevH);
                prevH = h;
            }
            return agg * 0.51 + holes * 3.6 + bump * 0.18 - lines * 7.6;
        }
    }
}
