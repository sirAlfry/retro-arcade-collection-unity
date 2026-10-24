using System.Collections.Generic;
using RetroArcade.Logic;
using UnityEngine;

namespace RetroArcade.Games
{
    /// <summary>
    /// Space Invaders: porting di <c>space_invaders.py</c>.
    /// <list type="bullet">
    /// <item>Formazione classica 11 × 5 con tre tipi di alieni animati in pixel-art (30 / 20 / 10 punti).</item>
    /// <item>La marcia accelera man mano che gli alieni muoiono, con l'iconica linea di basso a 4 note.</item>
    /// <item>Quattro scudi distruttibili che si sgretolano colpo dopo colpo.</item>
    /// <item>Il misterioso UFO (50-300 punti), proiettili nemici che si possono abbattere.</item>
    /// <item>Vite con invulnerabilità temporanea, ondate che partono più in basso e più veloci.</item>
    /// </list>
    /// </summary>
    public sealed class SpaceInvadersScene : ArcadeScene
    {
        private const int HudH = 40;
        private const int GroundY = (int)H - 26;
        private const int Scale = 3;
        private const int PlayerY = GroundY - 34;
        private const float PlayerSpeed = 300f, PlayerBulletSpeed = 640f;
        private const int MaxPlayerBullets = 2;
        private const float FireCooldown = 0.28f, InvulnerableTime = 1.6f;
        private const int StartLives = 3;
        private const int Cols = 11, Rows = 5;
        private const int SpacingX = 46, SpacingY = 36, StepX = 10, StepDown = 18;
        private const float EnemyBulletSpeed = 250f;
        private const int BunkerY = PlayerY - 90;
        private const int LostFrames = 50;

        private static readonly uint BgColor = Palette.Rgb(8, 6, 20);
        private static readonly Dictionary<string, uint> TypeColors = new Dictionary<string, uint>
        {
            ["squid"] = Palette.NeonMagenta, ["crab"] = Palette.NeonCyan, ["octopus"] = Palette.NeonGreen,
        };
        private static readonly SfxId[] MarchNotes = { SfxId.Step, SfxId.Step2, SfxId.Step3, SfxId.Step4 };

        private static readonly string[] Commands =
        {
            "Frecce o A/D: muovi   SPAZIO: spara",
            "Gli scudi si consumano!  UFO: 50-300 punti",
        };

        private sealed class Alien
        {
            public int Col, Row;
            public string Kind;
            public bool Alive = true;

            public PRect Rect(float fx, float fy)
            {
                int w = InvadersArt.Sprites[Kind][0][0].Length * Scale, h = 8 * Scale;
                int cx = (int)(fx + Col * SpacingX + SpacingX / 2);
                return new PRect(cx - w / 2, (int)(fy + Row * SpacingY), w, h);
            }
        }

        private sealed class Bullet
        {
            public float X, Y, Vy;
            public bool Zigzag;
            public PRect Rect => new PRect((int)X - 2, (int)Y - 6, 4, 12);
        }

        /// <summary>Tutto lo stato di una partita (ricreato a ogni "ricomincia").</summary>
        private sealed class Wave
        {
            public int Score, Lives = StartLives, Level;
            public float PlayerX = W / 2f, Invulnerable;
            public List<Bunker> Bunkers = new List<Bunker>();
            public List<Alien> Aliens;
            public float Fx, Fy;
            public int Direction, Frame, March;
            public float StepTimer, ShootTimer, FireCooldown, UfoTimer, UfoSound, Banner;
            public List<Bullet> Bullets, EnemyBullets;
            public float? UfoX;
            public int UfoDir;
            public List<Vector3> Explosions; // x, y, tempo rimasto

            public IEnumerable<Alien> AliveAliens
            {
                get { foreach (var a in Aliens) if (a.Alive) yield return a; }
            }

            public int AliveCount
            {
                get { int n = 0; foreach (var a in Aliens) if (a.Alive) n++; return n; }
            }

            public float StepInterval()
            {
                float ratio = (float)AliveCount / (Cols * Rows);
                return Mathf.Max(0.035f, 0.05f + 0.65f * ratio) / (1 + 0.12f * (Level - 1));
            }
        }

        private Particles _particles;
        private FloatingTexts _popups;
        private ScreenShake _shake;
        private int _best;
        private (int x, int y, int s)[] _stars;
        private Wave _g;
        private float _lostT = -1f;

        public override void Create()
        {
            _particles = new Particles(App.Rng);
            _popups = new FloatingTexts();
            _shake = new ScreenShake(App.Rng, 9f);
            _best = ScoreStore.GetInt("space_invaders");
            _stars = new (int, int, int)[70];
            for (int i = 0; i < _stars.Length; i++)
                _stars[i] = (RandInt((int)W), RandInt(HudH, GroundY - 1), RandInt(3) == 2 ? 2 : 1);
            _g = NewWave();
        }

        private Wave NewWave()
        {
            var g = new Wave();
            for (int i = 0; i < 4; i++) g.Bunkers.Add(new Bunker((int)(W * (i + 1) / 5 - 40), BunkerY, new System.Random(App.Rng.Next())));
            NextWave(g);
            return g;
        }

        private void NextWave(Wave g)
        {
            g.Level++;
            g.Aliens = new List<Alien>();
            for (int r = 0; r < Rows; r++)
                for (int c = 0; c < Cols; c++)
                    g.Aliens.Add(new Alien { Col = c, Row = r, Kind = InvadersArt.RowTypes[r] });
            g.Fx = (W - Cols * SpacingX) / 2f;
            g.Fy = HudH + 56 + Mathf.Min(6, g.Level - 1) * 12;
            g.Direction = 1;
            g.StepTimer = 0f;
            g.Frame = 0;
            g.March = 0;
            g.ShootTimer = 1.5f;
            g.Bullets = new List<Bullet>();
            g.EnemyBullets = new List<Bullet>();
            g.FireCooldown = 0f;
            g.UfoX = null;
            g.UfoDir = 1;
            g.UfoTimer = Uniform(15f, 25f);
            g.UfoSound = 0f;
            g.Explosions = new List<Vector3>();
            g.Banner = 1.6f;
        }

        protected override void Restart()
        {
            _g = NewWave();
            _particles.Clear();
            _popups.Clear();
            _lostT = -1f;
        }

        protected override void HandleMenuResult(string result)
        {
            if (result == "menu" || result == "quit") ScoreStore.RecordBest("space_invaders", _g.Score);
            base.HandleMenuResult(result);
        }

        private static int ArtW(string[] art) => art[0].Length * Scale;
        private static int ArtH(string[] art) => art.Length * Scale;

        private PRect PlayerRect()
        {
            int w = ArtW(InvadersArt.Player);
            return new PRect((int)_g.PlayerX - w / 2, PlayerY, w, ArtH(InvadersArt.Player));
        }

        private static PRect UfoRect(float x) => PRect.FromCenter((int)x, HudH + 22, ArtW(InvadersArt.Ufo), ArtH(InvadersArt.Ufo));

        private void KillPlayer()
        {
            _g.Lives--;
            _g.Invulnerable = InvulnerableTime;
            _g.EnemyBullets.Clear();
            var pr = PlayerRect();
            Sfx.Play(SfxId.Explosion);
            _shake.Add(0.8f);
            _particles.Burst(pr.CenterX, pr.CenterY, new[] { Palette.NeonCyan, Palette.White, Palette.NeonYellow }, 40,
                             60f, 300f, 0.4f, 1.0f, gravity: 200f);
        }

        // ───────────────────────────── Logica ─────────────────────────────

        public override void Update(float timeMs, float deltaMs)
        {
            float dt = deltaMs / 1000f;

            if (_lostT >= 0f)
            {
                // 50 frame di esplosione prima della schermata finale.
                _lostT += dt;
                _particles.Update(dt);
                _shake.Update(dt);
                if (_lostT * 60f >= LostFrames) ShowResults();
                return;
            }

            if (PausePressed()) { OpenPauseMenu("Space Invaders", Commands); return; }
            var g = _g;
            if (In.AnyPressed(ArcadeKey.Space, ArcadeKey.Up, ArcadeKey.W)
                && g.FireCooldown <= 0f && g.Bullets.Count < MaxPlayerBullets && g.Lives > 0)
            {
                g.Bullets.Add(new Bullet { X = g.PlayerX, Y = PlayerY - 4, Vy = -PlayerBulletSpeed });
                g.FireCooldown = FireCooldown;
                Sfx.Play(SfxId.Shoot);
            }

            g.Banner = Mathf.Max(0f, g.Banner - dt);
            bool lost = g.Banner <= 0.8f && Simulate(dt);
            _particles.Update(dt);
            _popups.Update(dt);
            _shake.Update(dt);

            if (lost)
            {
                if (_g.Lives > 0) KillPlayer(); // invasione: il pianeta cade anche con vite di scorta
                _lostT = 0f;
            }
        }

        /// <summary>Avanza la simulazione. True quando la partita è persa.</summary>
        private bool Simulate(float dt)
        {
            var g = _g;
            if (In.AnyHeld(ArcadeKey.Left, ArcadeKey.A)) g.PlayerX -= PlayerSpeed * dt;
            if (In.AnyHeld(ArcadeKey.Right, ArcadeKey.D)) g.PlayerX += PlayerSpeed * dt;
            g.PlayerX = Mathf.Clamp(g.PlayerX, 30f, W - 30f);
            g.FireCooldown = Mathf.Max(0f, g.FireCooldown - dt);
            g.Invulnerable = Mathf.Max(0f, g.Invulnerable - dt);

            // Marcia della formazione
            g.StepTimer += dt;
            if (g.StepTimer >= g.StepInterval())
            {
                g.StepTimer = 0f;
                g.Frame ^= 1;
                Sfx.Play(MarchNotes[g.March % 4], 0.6f);
                g.March++;
                int left = int.MaxValue, right = int.MinValue;
                foreach (var a in g.AliveAliens)
                {
                    var r = a.Rect(g.Fx, g.Fy);
                    left = Mathf.Min(left, r.Left);
                    right = Mathf.Max(right, r.Right);
                }
                if (left != int.MaxValue)
                {
                    if ((g.Direction > 0 && right + StepX > W - 16) || (g.Direction < 0 && left - StepX < 16))
                    {
                        g.Fy += StepDown;
                        g.Direction *= -1;
                    }
                    else
                    {
                        g.Fx += StepX * g.Direction;
                    }
                }
            }

            var alive = new List<Alien>(g.AliveAliens);
            // Gli alieni cancellano gli scudi che attraversano; se arrivano al giocatore è finita.
            foreach (var a in alive)
            {
                var r = a.Rect(g.Fx, g.Fy);
                foreach (var b in g.Bunkers) b.Hit(r, 0);
                if (r.Bottom >= PlayerY) return true;
            }

            // Fuoco alieno: spara il più basso di una colonna a caso
            g.ShootTimer -= dt;
            int maxEnemyBullets = 2 + g.Level / 2;
            if (g.ShootTimer <= 0f && alive.Count > 0)
            {
                g.ShootTimer = Uniform(0.5f, 1.3f) / (1 + 0.15f * (g.Level - 1));
                if (g.EnemyBullets.Count < maxEnemyBullets)
                {
                    var bottoms = new Dictionary<int, Alien>();
                    foreach (var a in alive)
                        if (!bottoms.TryGetValue(a.Col, out var cur) || a.Row > cur.Row) bottoms[a.Col] = a;
                    var shooters = new List<Alien>(bottoms.Values);
                    var shooter = shooters[RandInt(shooters.Count)];
                    var r = shooter.Rect(g.Fx, g.Fy);
                    g.EnemyBullets.Add(new Bullet { X = r.CenterX, Y = r.Bottom, Vy = EnemyBulletSpeed, Zigzag = Rand() < 0.5f });
                    Sfx.Play(SfxId.EnemyShoot, 0.5f);
                }
            }

            // UFO
            if (g.UfoX == null)
            {
                g.UfoTimer -= dt;
                if (g.UfoTimer <= 0f)
                {
                    g.UfoDir = RandInt(2) == 0 ? -1 : 1;
                    g.UfoX = g.UfoDir > 0 ? -40f : W + 40f;
                }
            }
            else
            {
                g.UfoX += 140f * g.UfoDir * dt;
                g.UfoSound -= dt;
                if (g.UfoSound <= 0f)
                {
                    g.UfoSound = 0.42f;
                    Sfx.Play(SfxId.Ufo, 0.5f);
                }
                if (g.UfoX < -60f || g.UfoX > W + 60f)
                {
                    g.UfoX = null;
                    g.UfoTimer = Uniform(18f, 30f);
                }
            }
            PRect? ufoRect = g.UfoX.HasValue ? UfoRect(g.UfoX.Value) : (PRect?)null;

            // Proiettili del giocatore
            foreach (var b in g.Bullets.ToArray())
            {
                b.Y -= PlayerBulletSpeed * dt;
                var br = b.Rect;
                bool removed = false;
                bool bunkerHit = false;
                foreach (var bk in g.Bunkers) if (bk.Hit(br)) { bunkerHit = true; break; }
                if (b.Y < HudH || bunkerHit)
                {
                    removed = true;
                }
                else if (ufoRect.HasValue && br.Collides(ufoRect.Value))
                {
                    var ur = ufoRect.Value;
                    int pts = new[] { 50, 100, 150, 300 }[RandInt(4)];
                    g.Score += pts;
                    _popups.Add(pts.ToString(), ur.CenterX, ur.CenterY, Palette.NeonRed, 1.2f, 3f);
                    _particles.Burst(ur.CenterX, ur.CenterY, new[] { Palette.NeonRed, Palette.White }, 30, 60f, 260f, 0.3f, 0.9f);
                    Sfx.Play(SfxId.Explosion);
                    g.UfoX = null;
                    g.UfoTimer = Uniform(18f, 30f);
                    removed = true;
                }
                else
                {
                    foreach (var eb in g.EnemyBullets)
                    {
                        if (!br.Collides(eb.Rect.Inflate(4, 0))) continue;
                        g.EnemyBullets.Remove(eb);
                        _particles.Burst(b.X, b.Y, Palette.White, 6, 40f, 120f, 0.1f, 0.3f);
                        removed = true;
                        break;
                    }
                    if (!removed)
                    {
                        foreach (var a in alive)
                        {
                            if (!a.Alive) continue;
                            var r = a.Rect(g.Fx, g.Fy);
                            if (!br.Collides(r)) continue;
                            a.Alive = false;
                            int pts = InvadersArt.Points[a.Kind];
                            g.Score += pts;
                            g.Explosions.Add(new Vector3(r.CenterX, r.CenterY, 0.18f));
                            uint color = TypeColors[a.Kind];
                            _particles.Burst(r.CenterX, r.CenterY, new[] { color, Palette.White }, 12, 50f, 200f, 0.2f, 0.5f);
                            _popups.Add("+" + pts, r.CenterX, r.Top, color, 0.5f);
                            Sfx.Play(SfxId.SmallExplosion);
                            removed = true;
                            break;
                        }
                    }
                }
                if (removed) g.Bullets.Remove(b);
            }

            // Proiettili nemici
            var pr = PlayerRect().Inflate(-6, -4);
            foreach (var eb in g.EnemyBullets.ToArray())
            {
                eb.Y += eb.Vy * dt;
                var er = eb.Rect;
                bool bunkerHit = false;
                if (eb.Y <= GroundY)
                    foreach (var bk in g.Bunkers) if (bk.Hit(er)) { bunkerHit = true; break; }
                if (eb.Y > GroundY)
                {
                    g.EnemyBullets.Remove(eb);
                    _particles.Burst(eb.X, GroundY, Palette.NeonGreen, 4, 20f, 80f, 0.1f, 0.3f);
                }
                else if (bunkerHit)
                {
                    g.EnemyBullets.Remove(eb);
                }
                else if (er.Collides(pr) && g.Invulnerable <= 0f)
                {
                    KillPlayer(); // cancella anche tutti i proiettili nemici
                    if (g.Lives <= 0) return true;
                    break;
                }
            }

            for (int i = g.Explosions.Count - 1; i >= 0; i--)
            {
                var e = g.Explosions[i];
                e.z -= dt;
                if (e.z > 0) g.Explosions[i] = e; else g.Explosions.RemoveAt(i);
            }

            if (g.AliveCount == 0)
            {
                g.Score += 100 * g.Level;
                Sfx.Play(SfxId.Levelup);
                _popups.Add("ONDATA SUPERATA! +" + (100 * g.Level), W / 2, H / 2, Palette.NeonGreen, 1.4f, 3f);
                NextWave(g);
            }
            return false;
        }

        private void ShowResults()
        {
            _lostT = -1f;
            bool newRecord = ScoreStore.RecordBest("space_invaders", _g.Score);
            _best = Mathf.Max(_best, _g.Score);
            OpenEndScreen("GAME OVER", new[]
            {
                $"Punteggio: {_g.Score}   Ondata: {_g.Level}",
                $"Record: {_best}",
            }, newRecord);
        }

        // ───────────────────────────── Disegno ─────────────────────────────

        public override void Draw(RetroCanvas g)
        {
            var w = _g;
            g.Offset = _shake.Offset();
            g.FillRect(-20, -20, W + 40, H + 40, BgColor);
            uint starColor = Palette.Rgb(90, 90, 140);
            foreach (var s in _stars) g.FillRect(s.x, s.y, s.s, s.s, starColor);
            g.FillRect(0, GroundY, W, 2, Palette.NeonGreen);
            foreach (var b in w.Bunkers)
                foreach (var c in b.Cells)
                {
                    var r = b.CellRect(c);
                    g.FillRect(r.X, r.Y, r.W, r.H, Palette.NeonGreen);
                }
            foreach (var a in w.AliveAliens)
            {
                var r = a.Rect(w.Fx, w.Fy);
                Thumbs.DrawPixelArt(g, InvadersArt.Sprites[a.Kind][w.Frame], r.X, r.Y, Scale, TypeColors[a.Kind]);
            }
            foreach (var e in w.Explosions)
            {
                var r = PRect.FromCenter((int)e.x, (int)e.y, ArtW(InvadersArt.Explosion), ArtH(InvadersArt.Explosion));
                Thumbs.DrawPixelArt(g, InvadersArt.Explosion, r.X, r.Y, Scale, Palette.White);
            }
            if (w.UfoX.HasValue)
            {
                var r = UfoRect(w.UfoX.Value);
                Thumbs.DrawPixelArt(g, InvadersArt.Ufo, r.X, r.Y, Scale, Palette.NeonRed);
            }
            foreach (var b in w.Bullets)
            {
                var r = b.Rect;
                g.FillRect(r.X, r.Y, r.W, r.H, Palette.NeonYellow);
            }
            foreach (var eb in w.EnemyBullets)
            {
                if (eb.Zigzag)
                {
                    float off = (int)(eb.Y / 8) % 2 != 0 ? 3 : -3;
                    float x = eb.X, y = eb.Y;
                    g.Line(x, y - 6, x + off, y - 2, 2, Palette.NeonRed);
                    g.Line(x + off, y - 2, x - off, y + 2, 2, Palette.NeonRed);
                    g.Line(x - off, y + 2, x, y + 6, 2, Palette.NeonRed);
                }
                else
                {
                    var r = eb.Rect;
                    g.FillRect(r.X, r.Y, r.W - 1, r.H, Palette.NeonOrange);
                }
            }
            if (w.Lives > 0 && (w.Invulnerable <= 0f || (int)(w.Invulnerable * 10) % 2 == 0))
            {
                var pr = PlayerRect();
                Thumbs.DrawPixelArt(g, InvadersArt.Player, pr.X, pr.Y, Scale, Palette.NeonCyan);
            }
            _particles.Draw(g);
            _popups.Draw(g);

            // HUD
            g.PixelText("PUNTI " + w.Score, 14, HudH / 2, 2, Palette.NeonCyan, 0f, 0.5f);
            g.PixelText("RECORD " + Mathf.Max(_best, w.Score), W / 2 - 40, HudH / 2, 2, Palette.NeonYellow, 0.5f, 0.5f);
            g.PixelText("ONDATA " + w.Level, W - 130, HudH / 2, 2, Palette.NeonGreen, 1f, 0.5f);
            Ui.Hearts(g, w.Lives, W - 14, HudH / 2 - 7);
            if (w.Banner > 0f)
                g.PixelText("ONDATA " + w.Level, W / 2, H / 2 + 30, 5, Palette.NeonGreen, 0.5f, 0.5f, glow: true);
            g.Offset = Vector2.zero;
        }
    }
}
