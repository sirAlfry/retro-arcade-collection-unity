using System.Collections.Generic;
using RetroArcade.Logic;
using UnityEngine;

namespace RetroArcade.Games
{
    /// <summary>
    /// Tic Tac Toe (Tris): porting di <c>tris.py</c>.
    /// <list type="bullet">
    /// <item>Modalità: contro la CPU (facile / medio / impossibile) o 2 giocatori.</item>
    /// <item>La CPU "impossibile" usa il minimax completo con tabella di trasposizione: non perde mai
    /// (lo dimostra un test esaustivo in GameAITests).</item>
    /// <item>Chi inizia si alterna a ogni partita, così anche la CPU apre.</item>
    /// <item>Segni al neon disegnati con un tratto animato, linea vincente animata, anteprima al passaggio
    /// del mouse, cursore da tastiera (frecce + INVIO).</item>
    /// <item>Vittorie e pareggi salvati (<c>tris_x</c>, <c>tris_o</c>, <c>tris_draw</c>).</item>
    /// </list>
    /// </summary>
    public sealed class TicTacToeScene : ArcadeScene
    {
        private const float Cell = 150, Board = Cell * 3;
        private const float OffX = (int)((W - Board) / 2), OffY = 52;
        private const float CpuDelay = 0.45f, StrokeTime = 0.18f;

        private static readonly string[] Commands =
        {
            "Clic o frecce + INVIO: piazza il segno",
            "TAB: CPU / 2 giocatori   1-2-3: difficoltà CPU",
        };

        private static readonly string[] Levels = { "easy", "medium", "hard" };
        private static readonly Dictionary<string, string> LevelNames = new Dictionary<string, string>
        {
            ["easy"] = "FACILE", ["medium"] = "MEDIO", ["hard"] = "IMPOSSIBILE",
        };
        private static readonly string[] Modes = { "VS CPU · FACILE", "VS CPU · MEDIO", "VS CPU · IMPOSSIBILE", "2 GIOCATORI" };

        private bool _started;
        private bool _vsCpu = true;
        private string _level = "medium";
        private GameAI<Logic.Cell> _ai;

        private int _tallyX, _tallyO, _tallyDraw;
        private Particles _particles;

        private readonly int[,] _board = new int[3, 3];
        private int _turn = 1, _starter = 1, _winner;
        private int[] _line;
        private bool _over;
        private float _cpuTimer, _lineT;
        private readonly Dictionary<int, float> _placed = new Dictionary<int, float>();
        private Vector2Int _cursor = new Vector2Int(1, 1); // (riga, colonna)
        private Vector2Int? _hover;
        private float _t;

        public override void Create()
        {
            _particles = new Particles(App.Rng);
            _tallyX = ScoreStore.GetInt("tris_x");
            _tallyO = ScoreStore.GetInt("tris_o");
            _tallyDraw = ScoreStore.GetInt("tris_draw");
            NewRound(false);
            OpenSelectMenu("TIC TAC TOE", Modes, new[] { "X sei tu, O è la CPU o il tuo amico" }, 1, Palette.NeonCyan, choice =>
            {
                _vsCpu = choice < 3;
                _level = _vsCpu ? Levels[Mathf.Min(choice, 2)] : "medium";
                _ai = GameAI<Logic.Cell>.ForLevel(_level, -1, new System.Random(App.Rng.Next()));
                _started = true;
                NewRound(false);
            });
        }

        protected override void Restart() => NewRound(false);

        private void NewRound(bool alternate = true)
        {
            if (alternate) _starter = -_starter;
            System.Array.Clear(_board, 0, _board.Length);
            _turn = _starter;
            _winner = 0;
            _line = null;
            _over = false;
            _cpuTimer = CpuDelay;
            _placed.Clear();
            _lineT = 0f;
        }

        private static Rect CellRect(int r, int c) => new Rect(OffX + c * Cell, OffY + r * Cell, Cell, Cell);

        private bool HumanTurn() => !_over && (!_vsCpu || _turn == 1);

        private bool Full()
        {
            foreach (int v in _board) if (v == 0) return false;
            return true;
        }

        private void Place(int r, int c)
        {
            if (_over || _board[r, c] != 0) return;
            int mark = _turn;
            _board[r, c] = mark;
            _placed[r * 3 + c] = _t;
            Sfx.Play(mark == 1 ? SfxId.PlaceX : SfxId.PlaceO);
            int w = new TicTacToeState(_board, -mark).Winner(out int[] line);
            if (w != 0)
            {
                _winner = mark;
                _line = line;
                _over = true;
                _lineT = _t;
            }
            else if (Full())
            {
                _winner = 0;
                _over = true;
                _lineT = _t;
            }
            else
            {
                _turn = -mark;
                _cpuTimer = CpuDelay;
                return;
            }

            // Partita finita: aggiorna e salva il conteggio.
            if (_winner == 1) _tallyX++;
            else if (_winner == -1) _tallyO++;
            else _tallyDraw++;
            ScoreStore.Set("tris_x", _tallyX);
            ScoreStore.Set("tris_o", _tallyO);
            ScoreStore.Set("tris_draw", _tallyDraw);
            if (_winner == 0)
            {
                Sfx.Play(SfxId.Blip);
            }
            else if (_vsCpu && _winner == -1)
            {
                Sfx.Play(SfxId.Lose);
            }
            else
            {
                Sfx.Play(SfxId.Win);
                if (_line != null)
                    foreach (int idx in _line)
                    {
                        var rect = CellRect(idx / 3, idx % 3);
                        _particles.Burst(rect.x + Cell / 2, rect.y + Cell / 2,
                            new[] { mark == 1 ? Palette.NeonRed : Palette.NeonBlue, Palette.White }, 20,
                            60f, 260f, 0.4f, 1.0f);
                    }
            }
        }

        private Vector2Int? CellAt(Vector2 p)
        {
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    if (CellRect(r, c).Contains(p)) return new Vector2Int(r, c);
            return null;
        }

        // ───────────────────────────── Logica ─────────────────────────────

        public override void Update(float timeMs, float deltaMs)
        {
            if (!_started) return;
            float dt = deltaMs / 1000f;
            _t += dt;

            if (PausePressed()) { OpenPauseMenu("Tic Tac Toe", Commands); return; }
            if (In.Pressed(ArcadeKey.R)) NewRound(false);
            else if (In.Pressed(ArcadeKey.Tab))
            {
                _vsCpu = !_vsCpu;
                App.Toast(_vsCpu ? "MODALITÀ: VS CPU" : "MODALITÀ: 2 GIOCATORI");
                NewRound(false);
            }
            else if (In.AnyPressed(ArcadeKey.One, ArcadeKey.Two, ArcadeKey.Three))
            {
                int k = In.Pressed(ArcadeKey.One) ? 0 : In.Pressed(ArcadeKey.Two) ? 1 : 2;
                _level = Levels[k];
                _ai = GameAI<Logic.Cell>.ForLevel(_level, -1, new System.Random(App.Rng.Next()));
                App.Toast("CPU: " + LevelNames[_level]);
            }
            else if (_over && In.AnyPressed(ArcadeKey.Enter, ArcadeKey.Space))
            {
                NewRound();
            }
            else if (In.AnyPressed(ArcadeKey.Up, ArcadeKey.Down, ArcadeKey.Left, ArcadeKey.Right,
                                   ArcadeKey.W, ArcadeKey.S, ArcadeKey.A, ArcadeKey.D))
            {
                int dr = In.AnyPressed(ArcadeKey.Up, ArcadeKey.W) ? -1 : In.AnyPressed(ArcadeKey.Down, ArcadeKey.S) ? 1 : 0;
                int dc = In.AnyPressed(ArcadeKey.Left, ArcadeKey.A) ? -1 : In.AnyPressed(ArcadeKey.Right, ArcadeKey.D) ? 1 : 0;
                _cursor = new Vector2Int((_cursor.x + dr + 3) % 3, (_cursor.y + dc + 3) % 3);
                _hover = null;
            }
            else if (In.AnyPressed(ArcadeKey.Enter, ArcadeKey.Space) && HumanTurn())
            {
                Place(_cursor.x, _cursor.y);
            }

            if (In.PointerMoved)
            {
                _hover = In.PointerInside ? CellAt(In.Pointer) : null;
                if (_hover.HasValue) _cursor = _hover.Value;
            }
            if (In.PointerPressed)
            {
                if (_over) NewRound();
                else if (HumanTurn())
                {
                    var cell = CellAt(In.Pointer);
                    if (cell.HasValue) Place(cell.Value.x, cell.Value.y);
                }
            }

            // Mossa della CPU dopo una breve pausa "di riflessione" (non bloccante).
            if (_vsCpu && !_over && _turn == -1)
            {
                _cpuTimer -= dt;
                if (_cpuTimer <= 0f)
                {
                    _ai ??= GameAI<Logic.Cell>.ForLevel(_level, -1, new System.Random(App.Rng.Next()));
                    var move = _ai.BestMove(new TicTacToeState(_board, -1), out bool found);
                    if (found) Place(move.Row, move.Col);
                }
            }

            _particles.Update(dt);
        }

        // ───────────────────────────── Disegno ─────────────────────────────

        public override void Draw(RetroCanvas g)
        {
            g.FillRect(0, 0, W, H, Palette.Bg);

            // Tabellone dei punti
            string left = _vsCpu ? "X (TU)" : "X (G1)";
            string right = _vsCpu ? $"O (CPU {LevelNames[_level]})" : "O (G2)";
            g.PixelText($"{left}  {_tallyX}", 16, 22, 2, Palette.NeonRed, 0f, 0.5f);
            g.PixelText($"PARI {_tallyDraw}", W / 2, 22, 2, Palette.TextDim, 0.5f, 0.5f);
            g.PixelText($"{_tallyO}  {right}", W - 16, 22, 2, Palette.NeonBlue, 1f, 0.5f);

            // Griglia: alone scuro largo + linea neon sottile
            for (int i = 1; i <= 2; i++)
            {
                float x = OffX + i * Cell, y = OffY + i * Cell;
                foreach (var (color, width) in new[] { (Palette.Rgb(40, 60, 120), 14f), (Palette.NeonPurple, 5f) })
                {
                    float lo = Mathf.Floor((width - 1) / 2); // banda delle linee pygame
                    g.FillRect(x - lo, OffY + 8, width, Board - 16 + 1, color);
                    g.FillRect(OffX + 8, y - lo, Board - 16 + 1, width, color);
                }
            }

            var winning = new HashSet<int>();
            if (_line != null) foreach (int i in _line) winning.Add(i);
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    int mark = _board[r, c];
                    if (mark == 0) continue;
                    var rect = CellRect(r, c);
                    if (winning.Contains(r * 3 + c))
                    {
                        float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 8f);
                        g.FillRoundedRect(rect.x + 5, rect.y + 5, rect.width - 10, rect.height - 10, 16,
                                          Palette.Rgb(255, 230, 60), (40f + 50f * pulse) / 255f);
                    }
                    float progress = (_t - (_placed.TryGetValue(r * 3 + c, out float pt) ? pt : -1f)) / StrokeTime;
                    DrawMark(g, mark, rect, progress);
                }

            // Anteprima al passaggio del mouse / cursore da tastiera
            if (_started && HumanTurn())
            {
                var target = _hover ?? _cursor;
                if (_board[target.x, target.y] == 0) DrawMark(g, _turn, CellRect(target.x, target.y), 1f, 70f / 255f);
                var cr = CellRect(_cursor.x, _cursor.y);
                g.StrokeRoundedRect(cr.x + 8, cr.y + 8, cr.width - 16, cr.height - 16, 11, 2, Palette.NeonYellow);
            }

            // Linea vincente animata
            if (_line != null)
            {
                var a = CellRect(_line[0] / 3, _line[0] % 3);
                var b = CellRect(_line[2] / 3, _line[2] % 3);
                float ax = a.x + Cell / 2, ay = a.y + Cell / 2, bx = b.x + Cell / 2, by = b.y + Cell / 2;
                float p = Mathf.Min(1f, (_t - _lineT) / 0.35f);
                float ex = ax + (bx - ax) * p, ey = ay + (by - ay) * p;
                g.Line(ax, ay, ex, ey, 18, Palette.Rgb(255, 230, 60));
                g.Line(ax, ay, ex, ey, 6, Palette.White);
            }
            _particles.Draw(g);

            // Riga di stato
            if (_over)
            {
                string text;
                uint color;
                if (_winner == 0) { text = "PAREGGIO!"; color = Palette.NeonYellow; }
                else if (_vsCpu && _winner == 1) { text = "HAI VINTO!"; color = Palette.NeonGreen; }
                else if (_vsCpu) { text = "VINCE LA CPU!"; color = Palette.NeonMagenta; }
                else if (_winner == 1) { text = "VINCE X!"; color = Palette.NeonRed; }
                else { text = "VINCE O!"; color = Palette.NeonBlue; }
                float bx = W / 2 - 260, by = H - 6 - 64;
                Ui.Panel(g, bx, by, 520, 64, color, 230);
                g.PixelText(text, W / 2, by + 20, 3, color, 0.5f, 0.5f);
                g.PixelText("INVIO / CLIC: NUOVA PARTITA   ESC: PAUSA", W / 2, by + 46, 1, Palette.TextDim, 0.5f, 0.5f);
            }
            else if (_started)
            {
                string msg;
                uint color;
                if (_vsCpu && _turn == -1)
                {
                    msg = "LA CPU STA PENSANDO" + new string('.', 1 + (int)(_t * 4) % 3);
                    color = Palette.NeonBlue;
                }
                else
                {
                    msg = _vsCpu ? "TOCCA A TE (X)" : "TOCCA A " + (_turn == 1 ? "X" : "O");
                    color = _turn == 1 ? Palette.NeonRed : Palette.NeonBlue;
                }
                g.PixelText(msg, W / 2, H - 22, 2, color, 0.5f, 0.5f);
            }
        }

        /// <summary>Una X (1) o una O (-1) con il tratto neon animato (<c>draw_mark</c>).</summary>
        private static void DrawMark(RetroCanvas g, int mark, Rect rect, float progress, float alpha = 1f)
        {
            progress = Mathf.Clamp01(progress);
            // inner = rect.inflate(-63, -63): 87×87 al centro della cella
            float ix = rect.x + 31, iy = rect.y + 31, iw = 87;
            if (mark == 1)
            {
                var segs = new[] { (ix, iy, ix + iw, iy + iw), (ix + iw, iy, ix, iy + iw) };
                for (int k = 0; k < 2; k++)
                {
                    float p = Mathf.Clamp01(progress * 2 - k);
                    if (p <= 0f) continue;
                    var (ax, ay, bx, by) = segs[k];
                    float ex = ax + (bx - ax) * p, ey = ay + (by - ay) * p;
                    g.Line(ax, ay, ex, ey, 22, Palette.NeonRed, alpha / 4f);
                    g.Line(ax, ay, ex, ey, 10, Palette.NeonRed, alpha);
                }
            }
            else
            {
                float radius = iw / 2 + 4; // 47.5 → come inner.w // 2 + 4
                radius = Mathf.Floor(radius);
                float cx = rect.x + Cell / 2, cy = rect.y + Cell / 2;
                if (progress >= 1f)
                {
                    g.StrokeCircle(cx, cy, radius + 6 - 10, 20, Palette.NeonBlue, alpha / 4f);
                    g.StrokeCircle(cx, cy, radius - 5, 10, Palette.NeonBlue, alpha);
                }
                else
                {
                    float start = Mathf.PI / 2, end = start + progress * Mathf.PI * 2f;
                    g.StrokeArc(cx, cy, radius + 6, 20, start, end, Palette.NeonBlue, alpha / 4f);
                    g.StrokeArc(cx, cy, radius, 10, start, end, Palette.NeonBlue, alpha);
                }
            }
        }
    }
}
