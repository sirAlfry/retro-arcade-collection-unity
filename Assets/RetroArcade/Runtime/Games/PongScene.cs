using System.Collections.Generic;
using RetroArcade.Logic;
using UnityEngine;

namespace RetroArcade.Games
{
    /// <summary>
    /// Pong, il classico del 1972: porting di <c>pong.py</c>.
    /// <list type="bullet">
    /// <item>Modalità: 1 giocatore contro la CPU (facile / medio / difficile) o 2 giocatori.</item>
    /// <item>La CPU PREVEDE dove arriverà la palla (rimbalzi compresi), con un ritardo di reazione e un errore
    /// di mira che dipendono dalla difficoltà.</item>
    /// <item>Fisica in pixel al secondo, collisioni tra rettangoli, la palla accelera a ogni colpo e l'angolo
    /// di rimbalzo dipende dal punto in cui tocca la racchetta.</item>
    /// <item>Conto alla rovescia del servizio dopo ogni punto; vince chi arriva a 11.</item>
    /// </list>
    /// </summary>
    public sealed class PongScene : ArcadeScene
    {
        private const int Top = 14, Bottom = (int)H - 14;
        private const int PaddleW = 12, PaddleH = 90, PaddleXMargin = 34;
        private const float PaddleSpeed = 470f;
        private const int BallR = 8;
        private const float BallStartSpeed = 380f, BallMaxSpeed = 950f, Speedup = 1.06f;
        private const float MaxBounceAngle = 60f * Mathf.Deg2Rad;
        private const int WinScore = 11;
        private const float ServeDelay = 1.0f;

        private static readonly string[] Commands =
        {
            "Giocatore 1: W / S   (in 1P anche le frecce)",
            "Giocatore 2: frecce ↑ / ↓   Vince chi arriva a 11",
        };

        private static readonly string[] Modes = { "1 GIOCATORE · FACILE", "1 GIOCATORE · MEDIO", "1 GIOCATORE · DIFFICILE", "2 GIOCATORI" };
        private static readonly string[] LevelNames = { "FACILE", "MEDIO", "DIFFICILE" };

        /// <summary>Velocità massima (px/s), errore di mira (px), secondi tra due "occhiate" alla palla.</summary>
        private static readonly (float speed, float error, float reaction)[] AiLevels =
        {
            (260f, 70f, 0.28f), (390f, 34f, 0.12f), (560f, 10f, 0.04f),
        };

        private sealed class Paddle
        {
            public PRect Rect;
            public float Y;
            public float Flash;

            public Paddle(int x)
            {
                Rect = PRect.FromCenter(x, (int)H / 2, PaddleW, PaddleH);
                Y = Rect.CenterY;
            }

            public void Move(float dy)
            {
                Y = Mathf.Max(Top + PaddleH / 2f, Mathf.Min(Bottom - PaddleH / 2f, Y + dy));
                Rect.CenterY = (int)Y;
            }
        }

        private sealed class Ball
        {
            public float X = W / 2, Y = H / 2, Vx, Vy, Speed = BallStartSpeed;
            public readonly List<Vector2> Trail = new List<Vector2>(12);
            public PRect Rect => new PRect((int)(X - BallR), (int)(Y - BallR), BallR * 2, BallR * 2);
        }

        private bool _started;
        private int _mode;
        private bool _vsCpu;
        // CPU
        private float _cpuTarget = H / 2, _cpuLook, _cpuError;

        private Particles _particles;
        private ScreenShake _shake;
        private Paddle _left, _right;
        private Ball _ball;
        private readonly int[] _score = new int[2];
        private float _serveTimer = ServeDelay;
        private int _serveDir;

        public override void Create()
        {
            _particles = new Particles(App.Rng);
            _shake = new ScreenShake(App.Rng, 7f);
            Reset();
            OpenSelectMenu("PONG", Modes, new[] { "Scegli la modalità di gioco" }, 1, Palette.NeonCyan, choice =>
            {
                _mode = choice;
                _vsCpu = _mode < 3;
                _started = true;
                Reset();
                _serveDir = Choice(-1, 1);
                if (_vsCpu) NewRally();
            });
        }

        private int Choice(int a, int b) => RandInt(2) == 0 ? a : b;

        private void Reset()
        {
            _ball = new Ball();
            Serve(_ball, Choice(-1, 1));
            _ball.Vx = _ball.Vy = 0f;
            _left = new Paddle(PaddleXMargin);
            _right = new Paddle((int)W - PaddleXMargin);
            _score[0] = _score[1] = 0;
            _serveTimer = ServeDelay;
        }

        protected override void Restart() => Reset();

        private void Serve(Ball b, int direction)
        {
            b.X = W / 2;
            b.Y = Uniform(H * 0.3f, H * 0.7f);
            b.Speed = BallStartSpeed;
            float angle = Uniform(-0.45f, 0.45f);
            b.Vx = direction * b.Speed * Mathf.Cos(angle);
            b.Vy = b.Speed * Mathf.Sin(angle);
            b.Trail.Clear();
        }

        private void NewRally()
        {
            float err = AiLevels[Mathf.Min(_mode, 2)].error;
            _cpuError = Uniform(-err, err);
        }

        private void CpuUpdate(float dt, Paddle paddle)
        {
            var profile = AiLevels[Mathf.Min(_mode, 2)];
            _cpuLook -= dt;
            if (_cpuLook <= 0f)
            {
                _cpuLook = profile.reaction;
                _cpuTarget = _ball.Vx > 0
                    ? ArcadePhysics.PredictY(_ball.X, _ball.Y, _ball.Vx, _ball.Vy, paddle.Rect.Left, Top + BallR, Bottom - BallR) + _cpuError
                    : H / 2; // torna verso il centro
            }
            float diff = _cpuTarget - paddle.Y;
            float step = profile.speed * dt;
            if (Mathf.Abs(diff) > 4f) paddle.Move(Mathf.Max(-step, Mathf.Min(step, diff)));
        }

        // ───────────────────────────── Logica ─────────────────────────────

        public override void Update(float timeMs, float deltaMs)
        {
            if (!_started) return;
            float dt = deltaMs / 1000f;

            if (In.AnyPressed(ArcadeKey.Escape, ArcadeKey.P, ArcadeKey.Space))
            {
                OpenPauseMenu("Pong", Commands);
                return;
            }

            // Input
            float step = PaddleSpeed * dt;
            bool up1 = In.Held(ArcadeKey.W) || (_vsCpu && In.Held(ArcadeKey.Up));
            bool down1 = In.Held(ArcadeKey.S) || (_vsCpu && In.Held(ArcadeKey.Down));
            if (up1) _left.Move(-step);
            if (down1) _left.Move(step);
            if (_vsCpu) CpuUpdate(dt, _right);
            else
            {
                if (In.Held(ArcadeKey.Up)) _right.Move(-step);
                if (In.Held(ArcadeKey.Down)) _right.Move(step);
            }

            // Servizio
            if (_serveTimer > 0f)
            {
                _serveTimer -= dt;
                if (_serveTimer <= 0f)
                {
                    Serve(_ball, _serveDir);
                    if (_vsCpu) NewRally();
                    Sfx.Play(SfxId.Blip);
                }
            }

            // Fisica della palla
            if (_serveTimer <= 0f) UpdateBall(dt);

            _left.Flash = Mathf.Max(0f, _left.Flash - dt);
            _right.Flash = Mathf.Max(0f, _right.Flash - dt);
            _particles.Update(dt);
            _shake.Update(dt);

            // Vittoria
            if (Mathf.Max(_score[0], _score[1]) >= WinScore) ShowResults();
        }

        private void UpdateBall(float dt)
        {
            var b = _ball;
            if (b.Trail.Count == 12) b.Trail.RemoveAt(0);
            b.Trail.Add(new Vector2(b.X, b.Y));
            b.X += b.Vx * dt;
            b.Y += b.Vy * dt;
            if (b.Y - BallR < Top)
            {
                b.Y = Top + BallR;
                b.Vy = Mathf.Abs(b.Vy);
                Sfx.Play(SfxId.Wall);
            }
            else if (b.Y + BallR > Bottom)
            {
                b.Y = Bottom - BallR;
                b.Vy = -Mathf.Abs(b.Vy);
                Sfx.Play(SfxId.Wall);
            }

            foreach (var (paddle, direction) in new[] { (_left, 1), (_right, -1) })
            {
                bool towards = direction == 1 ? b.Vx < 0 : b.Vx > 0;
                if (!towards || !b.Rect.Collides(paddle.Rect)) continue;
                float offset = Mathf.Clamp((b.Y - paddle.Y) / (PaddleH / 2f), -1f, 1f);
                float angle = offset * MaxBounceAngle;
                b.Speed = Mathf.Min(BallMaxSpeed, b.Speed * Speedup);
                b.Vx = direction * b.Speed * Mathf.Cos(angle);
                b.Vy = b.Speed * Mathf.Sin(angle);
                b.X = direction == 1 ? paddle.Rect.Right + BallR : paddle.Rect.Left - BallR;
                paddle.Flash = 0.08f;
                uint color = direction == 1 ? Palette.NeonCyan : Palette.NeonMagenta;
                _particles.Burst(b.X, b.Y, new[] { color, Palette.White }, 10, 80f, 260f, 0.15f, 0.4f,
                                 angle: direction == 1 ? 0f : Mathf.PI, spread: 2.2f);
                Sfx.Play(SfxId.Hit);
                if (_vsCpu && direction == 1) NewRally();
            }

            int scorer = -1;
            if (b.X < -BallR * 2) scorer = 1;
            else if (b.X > W + BallR * 2) scorer = 0;
            if (scorer < 0) return;
            _score[scorer]++;
            Sfx.Play(SfxId.Score);
            _shake.Add(0.45f);
            _particles.Burst(W / 2 + (scorer == 1 ? 60 : -60), 60, scorer == 1 ? Palette.NeonMagenta : Palette.NeonCyan, 30,
                             60f, 220f, 0.4f, 0.9f);
            _serveDir = scorer == 1 ? -1 : 1; // il servizio va verso chi ha subito il punto
            b.Vx = b.Vy = 0f;
            b.X = W / 2;
            b.Y = H / 2;
            b.Trail.Clear();
            _serveTimer = ServeDelay;
        }

        private void ShowResults()
        {
            bool leftWon = _score[0] > _score[1];
            string title;
            string[] lines;
            if (_vsCpu)
            {
                title = leftWon ? "HAI VINTO!" : "HAI PERSO!";
                if (leftWon) ScoreStore.Set("pong_wins", ScoreStore.GetInt("pong_wins") + 1);
                lines = new[] { $"Punteggio finale: {_score[0]} - {_score[1]}", $"Vittorie contro la CPU: {ScoreStore.GetInt("pong_wins")}" };
            }
            else
            {
                title = leftWon ? "VINCE G1!" : "VINCE G2!";
                lines = new[] { $"Punteggio finale: {_score[0]} - {_score[1]}" };
            }
            OpenEndScreen(title, lines, win: leftWon || !_vsCpu);
        }

        // ───────────────────────────── Disegno ─────────────────────────────

        public override void Draw(RetroCanvas g)
        {
            g.Offset = _shake.Offset();
            g.FillRect(-20, -20, W + 40, H + 40, Palette.Bg);
            uint wall = Palette.Rgb(70, 70, 120);
            g.FillRect(0, Top - 2 - 1, W, 4, wall); // linea pygame larga 4 su y = TOP - 2
            g.FillRect(0, Bottom + 2 - 1, W, 4, wall);
            for (int y = Top + 6; y < Bottom; y += 26) g.FillRect(W / 2 - 3, y, 6, 14, Palette.Rgb(60, 60, 105));
            g.PixelText(_score[0].ToString(), W / 2 - 60, 30, 6, Palette.NeonCyan, 0.5f, 0f);
            g.PixelText(_score[1].ToString(), W / 2 + 60, 30, 6, Palette.NeonMagenta, 0.5f, 0f);
            string leftLabel = _vsCpu ? "TU" : "G1";
            string rightLabel = _vsCpu ? "CPU " + LevelNames[Mathf.Min(_mode, 2)] : "G2";
            if (_started)
            {
                g.PixelText(leftLabel, W / 2 - 110, 40, 2, Palette.TextDim, 1f, 0f);
                g.PixelText(rightLabel, W / 2 + 110, 40, 2, Palette.TextDim, 0f, 0f);
            }
            foreach (var (paddle, color) in new[] { (_left, Palette.NeonCyan), (_right, Palette.NeonMagenta) })
            {
                var r = paddle.Rect;
                g.FillRoundedRect(r.X - 8, r.Y - 8, r.W + 16, r.H + 16, 8, color, 50f / 255f);
                g.FillRoundedRect(r.X, r.Y, r.W, r.H, 4, paddle.Flash > 0 ? Palette.White : color);
            }
            uint trailColor = Palette.Rgb(120, 220, 255);
            for (int i = 0; i < _ball.Trail.Count; i++)
            {
                var t = _ball.Trail[i];
                int k = Mathf.Min(i, 11);
                g.FillCircle((int)t.x, (int)t.y, Mathf.Max(2, BallR * (k + 4) / 16), trailColor, 150f * (k + 1) / 12f / 255f);
            }
            if (_serveTimer <= 0f || (int)(_serveTimer * 8) % 2 == 0)
                g.FillCircle((int)_ball.X, (int)_ball.Y, BallR, Palette.White);
            if (_serveTimer > 0f && _started)
                g.PixelText("PRONTI!", W / 2, H / 2 + 60, 3, Palette.NeonYellow, 0.5f, 0.5f);
            _particles.Draw(g);
            g.Offset = Vector2.zero;
        }
    }
}
