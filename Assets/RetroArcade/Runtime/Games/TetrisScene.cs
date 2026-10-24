using System.Collections.Generic;
using RetroArcade.Logic;
using UnityEngine;

namespace RetroArcade.Games
{
    /// <summary>
    /// Tetris "guideline": porting della parte grafica di <c>tetris.py</c>. Le regole stanno in
    /// <see cref="TetrisLogic"/>; qui ci sono disegno (blocchi smussati, pezzo fantasma, animazione delle righe),
    /// input con auto-shift DAS/ARR, suoni ed effetti.
    /// </summary>
    public sealed class TetrisScene : ArcadeScene
    {
        private const int Cell = 26;
        private const int Columns = TetrisLogic.Columns, RowsN = TetrisLogic.RowsCount;
        private const int BoardW = Columns * Cell, BoardH = RowsN * Cell;
        private const int OffX = ((int)W - BoardW) / 2, OffY = 46;
        private const float Das = 0.16f, Arr = 0.045f;
        private const int PreviewCount = 3;

        private static readonly string[] Commands =
        {
            "←/→ muovi   ↓ soft drop   SPAZIO hard drop",
            "↑ o X ruota   Z ruota indietro   C/SHIFT hold",
        };

        private static readonly string[] HelpLines = { "← → muovi", "↑ X / Z ruota", "↓ soft drop", "SPAZIO hard drop", "ESC pausa" };

        private TetrisLogic _game;
        private Particles _particles;
        private FloatingTexts _popups;
        private ScreenShake _shake;
        private int _best;
        private float _heldLeft, _heldRight;
        private float _overDelay = -1f;

        public override void Create()
        {
            _particles = new Particles(App.Rng);
            _popups = new FloatingTexts();
            _shake = new ScreenShake(App.Rng, 6f);
            _best = ScoreStore.GetInt("tetris");
            NewGame();
        }

        private void NewGame()
        {
            _game = new TetrisLogic(new System.Random(App.Rng.Next()));
            _particles.Clear();
            _popups.Clear();
            _heldLeft = _heldRight = 0f;
            _overDelay = -1f;
        }

        protected override void Restart() => NewGame();

        protected override void HandleMenuResult(string result)
        {
            if (result == "menu" || result == "quit") ScoreStore.RecordBest("tetris", _game.Score);
            base.HandleMenuResult(result);
        }

        private static Vector2 BoardXY(int x, int y) => new Vector2(OffX + x * Cell, OffY + y * Cell);

        // ───────────────────────────── Logica ─────────────────────────────

        public override void Update(float timeMs, float deltaMs)
        {
            float dt = deltaMs / 1000f;

            if (_overDelay >= 0f)
            {
                // Piccola pausa (350 ms) sul campo pieno prima della schermata finale.
                _overDelay += dt;
                _particles.Update(dt);
                _popups.Update(dt);
                _shake.Update(dt);
                if (_overDelay >= 0.35f) ShowResults();
                return;
            }

            if (PausePressed()) { OpenPauseMenu("Tetris", Commands); return; }
            if (In.AnyPressed(ArcadeKey.Left, ArcadeKey.A))
            {
                _game.Move(-1);
                _heldLeft = Das;
                _heldRight = 0f;
            }
            else if (In.AnyPressed(ArcadeKey.Right, ArcadeKey.D))
            {
                _game.Move(1);
                _heldRight = Das;
                _heldLeft = 0f;
            }
            else if (In.AnyPressed(ArcadeKey.Up, ArcadeKey.X, ArcadeKey.W)) _game.Rotate(1);
            else if (In.AnyPressed(ArcadeKey.Z, ArcadeKey.Ctrl)) _game.Rotate(-1);
            else if (In.Pressed(ArcadeKey.Space)) _game.HardDrop();
            else if (In.AnyPressed(ArcadeKey.C, ArcadeKey.Shift)) _game.HoldPiece();

            if (!_game.GameOver)
            {
                AutoShift(dt);
                bool soft = In.AnyHeld(ArcadeKey.Down, ArcadeKey.S);
                _game.Update(dt, soft);
            }
            React(_game.PopEvents());
            _particles.Update(dt);
            _popups.Update(dt);
            _shake.Update(dt);

            if (_game.GameOver) _overDelay = 0f;
        }

        /// <summary>DAS/ARR: tenendo premuto ←/→ il pezzo parte dopo 0,16 s e poi scorre ogni 0,045 s.</summary>
        private void AutoShift(float dt)
        {
            Shift(ref _heldLeft, -1, In.AnyHeld(ArcadeKey.Left, ArcadeKey.A), dt);
            Shift(ref _heldRight, 1, In.AnyHeld(ArcadeKey.Right, ArcadeKey.D), dt);
        }

        private void Shift(ref float held, int dx, bool down, float dt)
        {
            if (!down)
            {
                held = 0f;
                return;
            }
            held -= dt;
            while (held <= 0f)
            {
                if (!_game.Move(dx))
                {
                    held = Arr;
                    break;
                }
                held += Arr;
            }
        }

        private void React(List<string> events)
        {
            foreach (string ev in events)
            {
                switch (ev)
                {
                    case "move": Sfx.Play(SfxId.Move, 0.35f); break;
                    case "rotate": Sfx.Play(SfxId.Rotate); break;
                    case "hold": Sfx.Play(SfxId.Blip); break;
                    case "harddrop":
                        Sfx.Play(SfxId.Drop);
                        _shake.Add(0.25f);
                        break;
                    case "lock": Sfx.Play(SfxId.Drop, 0.5f); break;
                    case "levelup":
                        Sfx.Play(SfxId.Levelup);
                        _popups.Add("LIVELLO " + _game.Level, W / 2, OffY + BoardH / 3, Palette.NeonGreen, 1.4f, 3f);
                        break;
                    default:
                        if (!ev.StartsWith("lines")) break;
                        int n = int.Parse(ev.Substring(5));
                        var rows = new List<int>(_game.ClearingRows);
                        Sfx.Play(n >= 4 ? SfxId.Tetris : SfxId.Line);
                        _shake.Add(0.15f * n);
                        float sum = 0;
                        foreach (int r in rows) sum += r;
                        int midY = OffY + (int)(sum / rows.Count * Cell) + Cell / 2;
                        _popups.Add(TetrisLogic.LineNames[Mathf.Min(n, 4)], W / 2, midY,
                                    n >= 4 ? Palette.NeonMagenta : Palette.NeonYellow, 1.0f, 3f);
                        foreach (int row in rows)
                            for (int c = 0; c < Columns; c++)
                            {
                                var p = BoardXY(c, row);
                                _particles.Burst(p.x + Cell / 2, p.y + Cell / 2,
                                    new[] { Palette.White, Palette.NeonCyan, Palette.NeonYellow }, 3,
                                    40f, 200f, 0.3f, 0.7f, gravity: 300f);
                            }
                        break;
                }
            }
        }

        private void ShowResults()
        {
            _overDelay = -1f;
            bool newRecord = ScoreStore.RecordBest("tetris", _game.Score);
            _best = Mathf.Max(_best, _game.Score);
            OpenEndScreen("GAME OVER", new[]
            {
                $"Punteggio: {_game.Score}   Linee: {_game.Lines}   Livello: {_game.Level}",
                $"Record: {_best}",
            }, newRecord);
        }

        // ───────────────────────────── Disegno ─────────────────────────────

        public override void Draw(RetroCanvas g)
        {
            g.Offset = _shake.Offset();
            g.FillRect(-20, -20, W + 40, H + 40, Palette.Bg);

            // Campo
            g.StrokeRoundedRect(OffX - 6 + 1.5f, OffY - 6 + 1.5f, BoardW + 12 - 3, BoardH + 12 - 3, 4.5f, 3, Palette.NeonPurple);
            g.FillRect(OffX, OffY, BoardW, BoardH, Palette.Rgb(8, 6, 20));
            uint gridColor = Palette.Rgb(22, 18, 44);
            for (int c = 1; c < Columns; c++) g.FillRect(OffX + c * Cell, OffY, 1, BoardH, gridColor);
            for (int r = 1; r < RowsN; r++) g.FillRect(OffX, OffY + r * Cell, BoardW, 1, gridColor);

            for (int y = 0; y < RowsN; y++)
                for (int x = 0; x < Columns; x++)
                {
                    char kind = _game.Board[y, x];
                    if (kind != '\0') Block(g, TetrisLogic.Colors[kind], Cell, BoardXY(x, y));
                }

            // Righe completate: lampeggiano e si stringono verso il centro
            if (_game.ClearingRows.Count > 0)
            {
                float progress = 1f - Mathf.Max(0f, _game.ClearTimer) / TetrisLogic.ClearAnimTime;
                foreach (int row in _game.ClearingRows)
                {
                    var p = BoardXY(0, row);
                    int width = (int)(BoardW * (1f - progress));
                    uint flash = (int)(progress * 8) % 2 == 0 ? Palette.White : Palette.NeonCyan;
                    g.FillRect(p.x, p.y, BoardW, Cell, Palette.Rgb(8, 6, 20));
                    g.FillRect(p.x + (BoardW - width) / 2, p.y + 2, width, Cell - 4, flash);
                }
            }

            // Pezzo fantasma e pezzo attivo
            var piece = _game.Piece;
            if (piece != null && !_game.GameOver)
            {
                uint color = TetrisLogic.Colors[piece.Kind];
                var ghost = new TetrisPiece(piece.Kind, piece.X, _game.GhostY(), piece.Rotation);
                foreach (var c in ghost.Cells())
                {
                    if (c.Y < 0) continue;
                    var p = BoardXY(c.X, c.Y);
                    g.FillRect(p.x, p.y, Cell, Cell, color, 45f / 255f);
                    g.StrokeRect(p.x + 1, p.y + 1, Cell - 2, Cell - 2, 2, color, 200f / 255f);
                }
                bool flashLock = _game.OnGround && (int)(_game.LockTimer * 20) % 2 == 1;
                foreach (var c in piece.Cells())
                {
                    if (c.Y < 0) continue;
                    var p = BoardXY(c.X, c.Y);
                    Block(g, color, Cell, p);
                    if (flashLock) g.StrokeRect(p.x + 0.5f, p.y + 0.5f, Cell - 1, Cell - 1, 1, Palette.White);
                }
            }

            // Pannello sinistro: statistiche + hold
            float lx = OffX - 230;
            var stats = new (string label, string value)[]
            {
                ("PUNTEGGIO", _game.Score.ToString()), ("RECORD", Mathf.Max(_best, _game.Score).ToString()),
                ("LIVELLO", _game.Level.ToString()), ("LINEE", _game.Lines.ToString()),
            };
            for (int i = 0; i < stats.Length; i++)
            {
                float y = OffY + i * 62;
                g.PixelText(stats[i].label, lx, y, 2, Palette.TextDim);
                g.PixelText(stats[i].value, lx, y + 22, 3, i != 1 ? Palette.NeonCyan : Palette.NeonYellow);
            }
            var hold = new Rect(lx, OffY + 270, 180, 110);
            Ui.Panel(g, hold.x, hold.y, hold.width, hold.height, _game.CanHold ? Palette.NeonMagenta : Palette.GrayDark, 200, 8);
            g.PixelText("HOLD (C)", hold.x + hold.width / 2, hold.y + 10, 2, Palette.TextDim, 0.5f, 0f);
            if (_game.Hold.HasValue)
                MiniPiece(g, _game.Hold.Value, hold.x + hold.width / 2, hold.y + hold.height / 2 + 12, 20);

            // Pannello destro: coda dei prossimi pezzi
            float rx = OffX + BoardW + 30;
            var next = new Rect(rx, OffY, 180, 300);
            Ui.Panel(g, next.x, next.y, next.width, next.height, Palette.NeonCyan, 200, 8);
            g.PixelText("PROSSIMI", next.x + next.width / 2, next.y + 10, 2, Palette.TextDim, 0.5f, 0f);
            for (int i = 0; i < PreviewCount && i < _game.Queue.Count; i++)
                MiniPiece(g, _game.Queue[i], next.x + next.width / 2, next.y + 75 + i * 80, i == 0 ? 22 : 16);
            float helpY = OffY + 330;
            for (int k = 0; k < HelpLines.Length; k++) Ui.Body(g, HelpLines[k], rx, helpY + k * 22, 15, Palette.TextDim);

            _particles.Draw(g);
            _popups.Draw(g);
            g.Offset = Vector2.zero;
        }

        /// <summary>Blocco "arcade" smussato (<c>block_surface</c>).</summary>
        private static void Block(RetroCanvas g, uint color, int size, Vector2 at)
        {
            uint light = Palette.Lerp(color, Palette.White, 0.45f);
            uint dark = Palette.Lerp(color, Palette.Black, 0.45f);
            float x = at.x, y = at.y;
            g.FillRect(x, y, size, size, dark);
            g.FillTriangle(x, y, x + size + 1, y, x, y + size + 1, light); // come il poligono di pygame, bordi inclusi
            int bevel = Mathf.Max(2, size / 7);
            g.FillRect(x + bevel, y + bevel, size - 2 * bevel, size - 2 * bevel, color);
            g.FillRect(x + bevel, y + bevel, size - 2 * bevel, 1, Palette.Lerp(color, Palette.White, 0.25f));
        }

        /// <summary>Un pezzo (orientamento di partenza) centrato in un riquadro di anteprima.</summary>
        private static void MiniPiece(RetroCanvas g, char kind, float cx, float cy, int size)
        {
            var shape = TetrisLogic.Shapes[kind];
            int minX = 99, maxX = -1, minY = 99, maxY = -1;
            for (int r = 0; r < shape.Length; r++)
                for (int c = 0; c < shape[r].Length; c++)
                {
                    if (shape[r][c] == 0) continue;
                    minX = Mathf.Min(minX, c); maxX = Mathf.Max(maxX, c);
                    minY = Mathf.Min(minY, r); maxY = Mathf.Max(maxY, r);
                }
            int w = (maxX - minX + 1) * size, h = (maxY - minY + 1) * size;
            float ox = (int)cx - w / 2, oy = (int)cy - h / 2;
            uint color = TetrisLogic.Colors[kind];
            for (int r = 0; r < shape.Length; r++)
                for (int c = 0; c < shape[r].Length; c++)
                    if (shape[r][c] != 0) Block(g, color, size, new Vector2(ox + (c - minX) * size, oy + (r - minY) * size));
        }
    }
}
