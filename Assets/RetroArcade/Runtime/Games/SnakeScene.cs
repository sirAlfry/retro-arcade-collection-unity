using RetroArcade.Logic;
using UnityEngine;

namespace RetroArcade.Games
{
    /// <summary>
    /// Snake: porting di <c>snake.py</c>. La logica (griglia, buffer dei comandi, velocità, frutto bonus)
    /// sta in <see cref="SnakeLogic"/>; qui c'è solo la parte visiva: scacchiera, serpente continuo con
    /// sfumatura e occhi, cibo pulsante con alone, particelle, punteggi a comparsa, tremolio alla morte.
    /// </summary>
    public sealed class SnakeScene : ArcadeScene
    {
        private const int Grid = 20, HudH = 40;
        private const int Cols = (int)W / Grid, Rows = ((int)H - HudH) / Grid;
        private const float DeathFrames = 45;

        private static readonly string[] Commands =
        {
            "Frecce o WASD: cambia direzione",
            "Mela rossa +2   Frutto blu +5 (scade!)",
        };

        private SnakeLogic _game;
        private Particles _particles;
        private FloatingTexts _popups;
        private ScreenShake _shake;
        private int _best;
        private float _t;
        private float _deathT = -1f; // >= 0 durante l'animazione di morte (secondi)
        private bool _flash;

        public override void Create()
        {
            _particles = new Particles(App.Rng);
            _popups = new FloatingTexts();
            _shake = new ScreenShake(App.Rng, 8f);
            _best = ScoreStore.GetInt("snake");
            NewGame();
        }

        private void NewGame()
        {
            _game = new SnakeLogic(new System.Random(App.Rng.Next()), Cols, Rows);
            _particles.Clear();
            _popups.Clear();
            _deathT = -1f;
            _flash = false;
        }

        protected override void Restart() => NewGame();

        protected override void HandleMenuResult(string result)
        {
            if (result == "menu" || result == "quit") ScoreStore.RecordBest("snake", _game.Score);
            base.HandleMenuResult(result);
        }

        private static Rect CellRect(Int2 c) => new Rect(c.X * Grid, HudH + c.Y * Grid, Grid, Grid);

        // ───────────────────────────── Logica ─────────────────────────────

        public override void Update(float timeMs, float deltaMs)
        {
            float dt = deltaMs / 1000f;
            _t += dt;

            if (_deathT >= 0f)
            {
                // Breve animazione di morte (45 frame) prima della schermata dei risultati.
                _deathT += dt;
                _particles.Update(dt);
                _shake.Update(dt);
                int frame = (int)(_deathT * 60f);
                _flash = (frame / 6) % 2 == 0;
                if (frame >= DeathFrames) ShowResults();
                return;
            }

            if (PausePressed()) { OpenPauseMenu("Snake", Commands); return; }
            if (In.AnyPressed(ArcadeKey.Up, ArcadeKey.W)) _game.Turn(Int2.Up);
            if (In.AnyPressed(ArcadeKey.Down, ArcadeKey.S)) _game.Turn(Int2.Down);
            if (In.AnyPressed(ArcadeKey.Left, ArcadeKey.A)) _game.Turn(Int2.Left);
            if (In.AnyPressed(ArcadeKey.Right, ArcadeKey.D)) _game.Turn(Int2.Right);

            _game.Update(dt);
            foreach (var (kind, cell) in _game.PopEvents())
            {
                var r = CellRect(cell);
                float cx = r.x + Grid / 2, cy = r.y + Grid / 2;
                switch (kind)
                {
                    case SnakeLogic.EventKind.Eat:
                        Sfx.Play(SfxId.Eat);
                        _particles.Burst(cx, cy, new[] { Palette.NeonRed, Palette.NeonOrange, Palette.NeonYellow }, 14,
                                         60f, 200f, 0.25f, 0.6f);
                        _popups.Add("+" + SnakeLogic.RedPoints, cx, cy - 10, Palette.NeonYellow);
                        break;
                    case SnakeLogic.EventKind.Bonus:
                        Sfx.Play(SfxId.Bonus);
                        _particles.Burst(cx, cy, new[] { Palette.NeonBlue, Palette.NeonCyan, Palette.White }, 24,
                                         80f, 260f, 0.3f, 0.8f);
                        _popups.Add("+" + SnakeLogic.BluePoints, cx, cy - 10, Palette.NeonCyan, 1.0f, 3f);
                        break;
                    case SnakeLogic.EventKind.Die:
                        Sfx.Play(SfxId.Hurt);
                        _shake.Add(0.7f);
                        for (int i = 0; i < _game.Body.Count; i += 2)
                        {
                            var s = CellRect(_game.Body[i]);
                            _particles.Burst(s.x + Grid / 2, s.y + Grid / 2, new[] { Palette.NeonGreen, Palette.White }, 5,
                                             40f, 180f, 0.4f, 0.9f);
                        }
                        break;
                }
            }
            _particles.Update(dt);
            _popups.Update(dt);
            _shake.Update(dt);

            if (!_game.Alive) _deathT = 0f;
        }

        private void ShowResults()
        {
            _deathT = -1f;
            _flash = false;
            bool newRecord = ScoreStore.RecordBest("snake", _game.Score);
            _best = Mathf.Max(_best, _game.Score);
            OpenEndScreen("GAME OVER", new[]
            {
                $"Punteggio: {_game.Score}   Lunghezza: {_game.Body.Count}",
                $"Record: {_best}",
            }, newRecord);
        }

        // ───────────────────────────── Disegno ─────────────────────────────

        public override void Draw(RetroCanvas g)
        {
            g.Offset = _shake.Offset();
            g.FillRect(-20, -20, W + 40, H + 40, Palette.Bg);

            // Scacchiera del campo
            g.FillRect(0, HudH, Cols * Grid, Rows * Grid, Palette.Rgb(16, 18, 36));
            uint light = Palette.Rgb(20, 22, 42);
            for (int y = 0; y < Rows; y++)
                for (int x = (y % 2 == 0) ? 1 : 0; x < Cols; x += 2)
                    g.FillRect(x * Grid, HudH + y * Grid, Grid, Grid, light);
            g.StrokeRect(1, HudH - 1, W - 2, H - HudH, 2, Palette.NeonGreen);

            DrawFood(g, _game.Red, Palette.NeonRed, true);
            if (_game.Blue.HasValue)
            {
                bool blinking = _game.BlueTimer < 2f && (int)(_t * 8) % 2 == 0;
                DrawFood(g, _game.Blue.Value, Palette.NeonBlue, !blinking);
            }
            DrawSnake(g, _flash);
            _particles.Draw(g);
            _popups.Draw(g);

            // HUD
            g.PixelText("PUNTEGGIO " + _game.Score, 12, HudH / 2, 2, Palette.NeonCyan, 0f, 0.5f);
            g.PixelText("RECORD " + Mathf.Max(_best, _game.Score), W / 2, HudH / 2, 2, Palette.NeonYellow, 0.5f, 0.5f);
            g.PixelText("LUNGHEZZA " + _game.Body.Count, W - 12, HudH / 2, 2, Palette.NeonGreen, 1f, 0.5f);
            if (_game.Blue.HasValue)
            {
                int bar = (int)(120 * Mathf.Max(0f, _game.BlueTimer) / SnakeLogic.BonusLifetime);
                g.FillRect(W / 2 - 60, HudH - 7, bar, 3, Palette.NeonBlue);
            }
            if (!_game.Started)
            {
                float bx = W / 2 - 230, by = H / 2 + 60 - 35;
                Ui.Panel(g, bx, by, 460, 70, Palette.NeonGreen, 200);
                g.PixelText("PREMI UNA FRECCIA", W / 2, by + 35 - 10, 3, Palette.White, 0.5f, 0.5f);
                g.PixelText("PER INIZIARE", W / 2, by + 35 + 18, 2, Palette.TextDim, 0.5f, 0.5f);
            }
            g.Offset = Vector2.zero;
        }

        /// <summary>Serpente continuo con sfumatura e occhi che guardano dove va (<c>draw_snake</c>).</summary>
        private void DrawSnake(RetroCanvas g, bool flash)
        {
            var body = _game.Body;
            int n = body.Count;
            for (int i = n - 1; i >= 0; i--)
            {
                uint color = flash
                    ? ((i / 2) % 2 == 0 ? Palette.White : Palette.NeonRed)
                    : Palette.Lerp(Palette.Rgb(120, 255, 140), Palette.Rgb(20, 120, 60), (float)i / Mathf.Max(1, n - 1));
                var rect = CellRect(body[i]);
                if (i < n - 1)
                {
                    // "Ponte" verso il segmento precedente: il corpo diventa un tubo continuo.
                    var next = CellRect(body[i + 1]);
                    float x0 = Mathf.Min(rect.x, next.x) + 3, y0 = Mathf.Min(rect.y, next.y) + 3;
                    float x1 = Mathf.Max(rect.x, next.x) + Grid - 3, y1 = Mathf.Max(rect.y, next.y) + Grid - 3;
                    g.FillRect(x0, y0, x1 - x0, y1 - y0, color);
                }
                float inset = i < n - 1 ? 1f : 3f;
                g.FillRoundedRect(rect.x + inset, rect.y + inset, Grid - 2 * inset, Grid - 2 * inset, 7, color);
                if (i % 3 == 1 && !flash)
                    g.FillCircle(rect.x + Grid / 2, rect.y + Grid / 2, 2, Palette.Lerp(color, Palette.White, 0.25f));
            }
            var head = CellRect(body[0]);
            int dx = _game.Direction.X, dy = _game.Direction.Y;
            float cx = head.x + Grid / 2, cy = head.y + Grid / 2;
            for (int side = -1; side <= 1; side += 2)
            {
                float ex = cx + dx * 4 + (-dy) * side * 5, ey = cy + dy * 4 + dx * side * 5;
                g.FillCircle(ex, ey, 3, Palette.White);
                g.FillCircle(ex + dx, ey + dy, 1, Palette.Black);
            }
        }

        /// <summary>Frutto pulsante con alone, riflesso e picciolo (<c>draw_food</c>).</summary>
        private void DrawFood(RetroCanvas g, Int2 cell, uint color, bool visible)
        {
            if (!visible) return;
            var rect = CellRect(cell);
            float cx = rect.x + Grid / 2, cy = rect.y + Grid / 2;
            float r = Grid / 2 - 2 + (int)(1.5f * Mathf.Sin(_t * 6));
            g.FillCircle(cx, cy, Grid - 2, color, 60f / 255f);
            g.FillCircle(cx, cy, r, color);
            g.FillCircle(cx - 3, cy - 3, 2, Palette.Lerp(color, Palette.White, 0.6f));
            g.Line(cx, rect.y + 2, cx + 3, rect.y - 2, 2, Palette.Rgb(90, 200, 90));
        }
    }
}
