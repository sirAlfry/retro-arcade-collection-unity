using System.Collections.Generic;
using RetroArcade.Logic;
using UnityEngine;

namespace RetroArcade.Games
{
    /// <summary>
    /// Breakout: porting di <c>breakout.py</c>.
    /// <list type="bullet">
    /// <item>5 livelli fatti a mano (poi ricominciano, più veloci), mattoni da 1-3 colpi.</item>
    /// <item>Collisioni a sotto-passi e per assi separati: la palla non attraversa i mattoni e rimbalza
    /// dal lato giusto.</item>
    /// <item>Angolo di rimbalzo controllato dal punto in cui la palla tocca la racchetta.</item>
    /// <item>Power-up: L = racchetta larga, M = multi-palla, S = palla lenta, ♥ = +1 vita.</item>
    /// <item>La palla aspetta sulla racchetta finché non la lanci (SPAZIO / clic); mouse O tastiera
    /// (il mouse prende il comando solo quando lo muovi davvero).</item>
    /// </list>
    /// </summary>
    public sealed class BreakoutScene : ArcadeScene
    {
        private const int HudH = BreakoutLevels.HudH;
        private static readonly PRect Field = BreakoutLevels.Field;
        private const int PaddleWNormal = 104, PaddleWWide = 156, PaddleH = 14;
        private const int PaddleY = (int)H - 48;
        private const float PaddleSpeed = 620f;
        private const int BallR = 7;
        private const float BallSpeed = 340f, BallSpeedMax = 640f;
        private const float MaxAngle = 65f * Mathf.Deg2Rad;
        private const int StartLives = 3, MaxLives = 5;
        private const float PowerupChance = 0.12f, PowerupSpeed = 150f;
        private const int MaxBalls = 8;
        private const int LevelClearFrames = 70;

        private static readonly uint[] RowColors =
        {
            Palette.NeonRed, Palette.NeonOrange, Palette.NeonYellow, Palette.NeonGreen,
            Palette.NeonCyan, Palette.NeonBlue, Palette.NeonPurple,
        };

        private static readonly string[] Commands =
        {
            "Mouse o frecce / A-D: muovi   SPAZIO o clic: lancia",
            "Power-up: L larga  M multi-palla  S lenta  ♥ vita",
        };

        private static readonly string[] PowerupKinds = { "L", "M", "S", "♥" };
        private static readonly Dictionary<string, uint> PowerupColors = new Dictionary<string, uint>
        {
            ["L"] = Palette.NeonGreen, ["M"] = Palette.NeonCyan, ["S"] = Palette.NeonYellow, ["♥"] = Palette.NeonRed,
        };

        private sealed class Ball
        {
            public float X, Y, Vx, Vy;
            public bool Stuck = true;
            public Ball(float x, float y) { X = x; Y = y; }
            public float Speed => Mathf.Sqrt(Vx * Vx + Vy * Vy);
        }

        private sealed class PowerUp
        {
            public string Kind;
            public float X, Y;
        }

        private sealed class State
        {
            public int Level = 1, Score, Lives = StartLives;
            public float PaddleX = W / 2;
            public List<Ball> Balls = new List<Ball>();
            public List<Brick> Bricks = new List<Brick>();
            public List<PowerUp> Powerups = new List<PowerUp>();
            public float WideTimer, SlowTimer, BannerTimer = 1.4f;

            public int PaddleW => WideTimer > 0 ? PaddleWWide : PaddleWNormal;
            public PRect PaddleRect => PRect.FromCenter((int)PaddleX, PaddleY, PaddleW, PaddleH);

            public float BaseSpeed
            {
                get
                {
                    int loop = (Level - 1) / BreakoutLevels.Levels.Length;
                    return Mathf.Min(BallSpeedMax, BallSpeed * (1 + 0.06f * (Level - 1) + 0.15f * loop));
                }
            }
        }

        private Particles _particles;
        private FloatingTexts _popups;
        private ScreenShake _shake;
        private int _best;
        private bool _mouseMode;
        private State _s;
        private float _clearT = -1f; // >= 0 durante la festa di fine livello

        public override void Create()
        {
            _particles = new Particles(App.Rng);
            _popups = new FloatingTexts();
            _shake = new ScreenShake(App.Rng, 8f);
            _best = ScoreStore.GetInt("breakout");
            _s = NewState();
        }

        private static State NewState(int level = 1, State keep = null)
        {
            var st = new State { Level = level };
            if (keep != null)
            {
                st.Score = keep.Score;
                st.Lives = keep.Lives;
            }
            st.Bricks = BreakoutLevels.Build(level);
            st.Balls.Add(new Ball(st.PaddleX, PaddleY - PaddleH / 2 - BallR));
            return st;
        }

        protected override void Restart()
        {
            _s = NewState();
            _particles.Clear();
            _popups.Clear();
            _clearT = -1f;
        }

        protected override void HandleMenuResult(string result)
        {
            if (result == "menu" || result == "quit") ScoreStore.RecordBest("breakout", _s.Score);
            base.HandleMenuResult(result);
        }

        // ───────────────────────────── Regole ─────────────────────────────

        private void Launch(Ball ball)
        {
            float angle = Uniform(-0.35f, 0.35f);
            float speed = _s.BaseSpeed;
            ball.Vx = speed * Mathf.Sin(angle);
            ball.Vy = -speed * Mathf.Cos(angle);
            ball.Stuck = false;
            Sfx.Play(SfxId.Blip);
        }

        private void Damage(Brick brick)
        {
            brick.Hp--;
            _s.Score += 5;
            float cx = brick.Rect.CenterX, cy = brick.Rect.CenterY;
            uint color = RowColors[brick.Row % RowColors.Length];
            if (brick.Hp <= 0)
            {
                _s.Bricks.Remove(brick);
                _s.Score += 10 * brick.MaxHp;
                Sfx.Play(SfxId.Brick);
                _particles.Burst(cx, cy, new[] { color, Palette.White }, 16, 60f, 240f, 0.3f, 0.7f, gravity: 420f);
                _popups.Add("+" + (5 + 10 * brick.MaxHp), cx, cy, color, 0.6f);
                if (Rand() < PowerupChance)
                    _s.Powerups.Add(new PowerUp { Kind = PowerupKinds[RandInt(PowerupKinds.Length)], X = cx, Y = cy });
            }
            else
            {
                Sfx.Play(SfxId.Hit, 0.7f);
                _particles.Burst(cx, cy, Palette.White, 5, 40f, 120f, 0.15f, 0.3f);
            }
        }

        private bool CollideBricks(Ball ball)
        {
            foreach (var brick in _s.Bricks)
            {
                if (!ArcadePhysics.CircleHitsRect(ball.X, ball.Y, BallR, brick.Rect)) continue;
                Damage(brick);
                return true;
            }
            return false;
        }

        /// <summary>Movimento a sotto-passi (mai più di 0,7 raggi per passo), un asse alla volta.</summary>
        private void StepBall(Ball ball, float dt)
        {
            if (ball.Stuck)
            {
                ball.X = _s.PaddleX + 18;
                ball.Y = PaddleY - PaddleH / 2 - BallR;
                return;
            }
            float slow = _s.SlowTimer > 0 ? 0.7f : 1f;
            float speed = ball.Speed * slow;
            int steps = Mathf.Max(1, Mathf.CeilToInt(speed * dt / (BallR * 0.7f)));
            float factor = slow * dt / steps;
            for (int i = 0; i < steps; i++)
            {
                // Asse X
                ball.X += ball.Vx * factor;
                if (ball.X - BallR < Field.Left)
                {
                    ball.X = Field.Left + BallR;
                    ball.Vx = Mathf.Abs(ball.Vx);
                    Sfx.Play(SfxId.Wall, 0.6f);
                }
                else if (ball.X + BallR > Field.Right)
                {
                    ball.X = Field.Right - BallR;
                    ball.Vx = -Mathf.Abs(ball.Vx);
                    Sfx.Play(SfxId.Wall, 0.6f);
                }
                else if (CollideBricks(ball))
                {
                    ball.X -= ball.Vx * factor;
                    ball.Vx = -ball.Vx;
                }
                // Asse Y
                ball.Y += ball.Vy * factor;
                if (ball.Y - BallR < Field.Top)
                {
                    ball.Y = Field.Top + BallR;
                    ball.Vy = Mathf.Abs(ball.Vy);
                    Sfx.Play(SfxId.Wall, 0.6f);
                }
                else if (CollideBricks(ball))
                {
                    ball.Y -= ball.Vy * factor;
                    ball.Vy = -ball.Vy;
                }
                // Racchetta
                var pr = _s.PaddleRect;
                if (ball.Vy > 0 && ArcadePhysics.CircleHitsRect(ball.X, ball.Y, BallR, pr) && ball.Y < pr.CenterY)
                {
                    float offset = Mathf.Clamp((ball.X - pr.CenterX) / (pr.W / 2f), -1f, 1f);
                    float angle = offset * MaxAngle;
                    float newSpeed = Mathf.Min(BallSpeedMax, ball.Speed * 1.015f);
                    ball.Vx = newSpeed * Mathf.Sin(angle);
                    ball.Vy = -newSpeed * Mathf.Cos(angle);
                    ball.Y = pr.Top - BallR;
                    Sfx.Play(SfxId.Hit);
                    _particles.Burst(ball.X, pr.Top, Palette.NeonCyan, 6, 40f, 140f, 0.15f, 0.3f,
                                     angle: -Mathf.PI / 2, spread: 2f);
                }
            }
        }

        private void ApplyPowerup(string kind)
        {
            Sfx.Play(SfxId.Powerup);
            float px = _s.PaddleX;
            switch (kind)
            {
                case "L":
                    _s.WideTimer = 12f;
                    _popups.Add("PADDLE LARGO!", px, PaddleY - 30, Palette.NeonGreen, 1f);
                    break;
                case "S":
                    _s.SlowTimer = 9f;
                    _popups.Add("RALLENTATORE!", px, PaddleY - 30, Palette.NeonYellow, 1f);
                    break;
                case "♥":
                    _s.Lives = Mathf.Min(MaxLives, _s.Lives + 1);
                    _popups.Add("+1 VITA!", px, PaddleY - 30, Palette.NeonRed, 1f);
                    break;
                case "M":
                    var extra = new List<Ball>();
                    foreach (var b in _s.Balls)
                    {
                        if (b.Stuck) continue;
                        foreach (float da in new[] { -0.45f, 0.45f })
                        {
                            float ang = Mathf.Atan2(b.Vx, -b.Vy) + da;
                            extra.Add(new Ball(b.X, b.Y)
                            {
                                Stuck = false,
                                Vx = b.Speed * Mathf.Sin(ang),
                                Vy = -Mathf.Abs(b.Speed * Mathf.Cos(ang)),
                            });
                        }
                    }
                    int room = Mathf.Max(0, MaxBalls - _s.Balls.Count);
                    _s.Balls.AddRange(extra.GetRange(0, Mathf.Min(room, extra.Count)));
                    _popups.Add("MULTI-PALLA!", px, PaddleY - 30, Palette.NeonCyan, 1f);
                    break;
            }
        }

        // ───────────────────────────── Logica del frame ─────────────────────────────

        public override void Update(float timeMs, float deltaMs)
        {
            float dt = deltaMs / 1000f;

            if (_clearT >= 0f)
            {
                // Festa di fine livello (70 frame), poi il livello successivo.
                _clearT += dt;
                _popups.Update(dt);
                _particles.Update(dt);
                if (_clearT * 60f >= LevelClearFrames)
                {
                    _s = NewState(_s.Level + 1, _s);
                    _clearT = -1f;
                }
                return;
            }

            if (PausePressed()) { OpenPauseMenu("Breakout", Commands); return; }
            if (In.AnyPressed(ArcadeKey.Space, ArcadeKey.Up, ArcadeKey.W) || In.PointerPressed)
                foreach (var ball in _s.Balls)
                    if (ball.Stuck) Launch(ball);
            if (In.AnyPressed(ArcadeKey.Left, ArcadeKey.Right, ArcadeKey.A, ArcadeKey.D)) _mouseMode = false;
            if (In.PointerMoved) _mouseMode = true;

            // Racchetta
            if (In.AnyHeld(ArcadeKey.Left, ArcadeKey.A)) _s.PaddleX -= PaddleSpeed * dt;
            if (In.AnyHeld(ArcadeKey.Right, ArcadeKey.D)) _s.PaddleX += PaddleSpeed * dt;
            if (_mouseMode && In.PointerInside) _s.PaddleX = In.Pointer.x;
            float half = _s.PaddleW / 2f;
            _s.PaddleX = Mathf.Clamp(_s.PaddleX, Field.Left + half, Field.Right - half);

            // Timer
            _s.BannerTimer = Mathf.Max(0f, _s.BannerTimer - dt);
            _s.WideTimer = Mathf.Max(0f, _s.WideTimer - dt);
            _s.SlowTimer = Mathf.Max(0f, _s.SlowTimer - dt);

            // Palle
            foreach (var ball in _s.Balls.ToArray())
            {
                StepBall(ball, dt);
                if (ball.Y - BallR > H) _s.Balls.Remove(ball);
            }
            if (_s.Balls.Count == 0)
            {
                _s.Lives--;
                Sfx.Play(SfxId.Hurt);
                _shake.Add(0.6f);
                _s.WideTimer = _s.SlowTimer = 0f;
                _s.Powerups.Clear();
                if (_s.Lives > 0) _s.Balls.Add(new Ball(_s.PaddleX, PaddleY));
            }

            // Power-up che cadono
            var catcher = _s.PaddleRect.Inflate(10, 6);
            foreach (var p in _s.Powerups.ToArray())
            {
                p.Y += PowerupSpeed * dt;
                if (catcher.Contains((int)p.X, (int)p.Y))
                {
                    _s.Powerups.Remove(p);
                    ApplyPowerup(p.Kind);
                }
                else if (p.Y > H)
                {
                    _s.Powerups.Remove(p);
                }
            }

            _particles.Update(dt);
            _popups.Update(dt);
            _shake.Update(dt);

            // Livello completato / game over
            if (_s.Bricks.Count == 0)
            {
                int bonus = 250 * _s.Level;
                _s.Score += bonus;
                Sfx.Play(SfxId.Win);
                _popups.Add("LIVELLO COMPLETATO! +" + bonus, W / 2, H / 2, Palette.NeonGreen, 1.6f, 3f);
                _clearT = 0f;
            }
            else if (_s.Lives <= 0)
            {
                bool newRecord = ScoreStore.RecordBest("breakout", _s.Score);
                _best = Mathf.Max(_best, _s.Score);
                OpenEndScreen("GAME OVER", new[]
                {
                    $"Punteggio: {_s.Score}   Livello raggiunto: {_s.Level}",
                    $"Record: {_best}",
                }, newRecord);
            }
        }

        // ───────────────────────────── Disegno ─────────────────────────────

        public override void Draw(RetroCanvas g)
        {
            g.Offset = _shake.Offset();
            g.FillRect(-20, -20, W + 40, H + 40, Palette.Bg);
            var frame = Field.Inflate(6, 6);
            g.StrokeRoundedRect(frame.X + 1.5f, frame.Y + 1.5f, frame.W - 3, frame.H - 3, 2.5f, 3, Palette.NeonOrange);

            foreach (var b in _s.Bricks)
            {
                uint color = RowColors[b.Row % RowColors.Length];
                float ratio = (float)b.Hp / b.MaxHp;
                uint baseColor = Palette.Lerp(Palette.Black, color, 0.45f + 0.55f * ratio);
                var r = b.Rect;
                g.FillRoundedRect(r.X, r.Y, r.W, r.H, 3, baseColor);
                g.FillRect(r.X + 3, r.Y + 2, r.Right - 4 - (r.X + 3) + 1, 2, Palette.Lerp(baseColor, Palette.White, 0.5f));
                if (b.MaxHp > 1)
                    for (int k = 0; k < b.Hp; k++) g.FillRect(r.X + 6 + k * 7, r.Bottom - 7, 4, 3, Palette.White);
                if (b.MaxHp >= 3 && b.Hp == b.MaxHp)
                    g.StrokeRoundedRect(r.X + 0.5f, r.Y + 0.5f, r.W - 1, r.H - 1, 2.5f, 1, Palette.White);
            }

            foreach (var p in _s.Powerups)
            {
                var cap = PRect.FromCenter((int)p.X, (int)p.Y, 34, 16);
                g.FillRoundedRect(cap.X, cap.Y, cap.W, cap.H, 8, PowerupColors[p.Kind]);
                g.StrokeRoundedRect(cap.X + 0.5f, cap.Y + 0.5f, cap.W - 1, cap.H - 1, 7.5f, 1, Palette.White);
                g.PixelText(p.Kind, cap.CenterX, cap.CenterY, 2, Palette.Black, 0.5f, 0.5f);
            }

            var pr = _s.PaddleRect;
            g.FillRoundedRect(pr.X - 10, pr.Y - 10, pr.W + 20, pr.H + 20, 12, Palette.NeonCyan, 45f / 255f);
            g.FillRoundedRect(pr.X, pr.Y, pr.W, pr.H, 7, Palette.NeonCyan);
            var inner = pr.Inflate(-pr.W / 2, -8);
            g.FillRoundedRect(inner.X, inner.Y, inner.W, inner.H, 3, Palette.White);
            uint ballColor = _s.SlowTimer > 0 ? Palette.NeonYellow : Palette.White;
            foreach (var ball in _s.Balls) g.FillCircle((int)ball.X, (int)ball.Y, BallR, ballColor);

            _particles.Draw(g);
            _popups.Draw(g);

            // HUD
            g.PixelText("PUNTI " + _s.Score, 16, HudH / 2, 2, Palette.NeonCyan, 0f, 0.5f);
            g.PixelText("RECORD " + Mathf.Max(_best, _s.Score), 290, HudH / 2, 2, Palette.NeonYellow, 0f, 0.5f);
            g.PixelText("LIVELLO " + _s.Level, 540, HudH / 2, 2, Palette.NeonGreen, 0f, 0.5f);
            Ui.Hearts(g, _s.Lives, W - 16, HudH / 2 - 7);
            int k2 = 0;
            if (_s.WideTimer > 0) TimerLabel(g, "L", _s.WideTimer, Palette.NeonGreen, k2++);
            if (_s.SlowTimer > 0) TimerLabel(g, "S", _s.SlowTimer, Palette.NeonYellow, k2);
            if (_s.BannerTimer > 0)
                g.PixelText("LIVELLO " + _s.Level, W / 2, H / 2 + 20, 5, Palette.NeonOrange, 0.5f, 0.5f, glow: true);
            bool anyStuck = false;
            foreach (var b in _s.Balls) anyStuck |= b.Stuck;
            if (anyStuck && _s.BannerTimer <= 0)
                g.PixelText("SPAZIO O CLIC PER LANCIARE", W / 2, PaddleY - 60, 2, Palette.TextDim, 0.5f, 0.5f);
            g.Offset = Vector2.zero;
        }

        private static void TimerLabel(RetroCanvas g, string label, float value, uint color, int k) =>
            g.PixelText(label + " " + System.Math.Round(value), Field.Right - 8, Field.Bottom - 20 - k * 18, 2, color, 1f, 1f);
    }
}
