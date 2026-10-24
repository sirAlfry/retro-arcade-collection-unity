using System.Collections.Generic;
using RetroArcade.Logic;
using UnityEngine;

namespace RetroArcade.Games
{
    /// <summary>
    /// Memory: porting di <c>memory_game.py</c>.
    /// <list type="bullet">
    /// <item>Tre griglie: 4×3 (6 coppie), 6×3 (9 coppie), 6×4 (12 coppie).</item>
    /// <item>12 icone pixel-art disegnate a mano, ognuna col suo colore.</item>
    /// <item>Un vero "giro" della carta sull'asse verticale; combo per le coppie consecutive.</item>
    /// <item>Si gioca col mouse O con la tastiera (frecce + INVIO); record di mosse per ogni griglia.</item>
    /// </list>
    /// </summary>
    public sealed class MemoryGameScene : ArcadeScene
    {
        private const int HudH = MemoryLogic.HudH;
        private const float FlipSpeed = 5f, MismatchDelay = 0.75f;
        private const int WinFrames = 30;

        private static readonly string[] Commands = { "Clic su una carta per girarla", "Oppure frecce + INVIO / SPAZIO" };

        private MemoryLogic.BoardSize _size;
        private bool _started;
        private Particles _particles;
        private FloatingTexts _popups;
        private float _t;
        private float _winT = -1f;

        // Una "mano" (Round in Python)
        private List<MemoryCard> _cards;
        private readonly List<MemoryCard> _selected = new List<MemoryCard>();
        private int _moves, _streak, _cursor;
        private float _mismatchTimer;
        private Stopwatch _timer;
        private double? _best;

        private string RecordKey => "memory_moves_" + _size.Key;

        public override void Create()
        {
            _particles = new Particles(App.Rng);
            _popups = new FloatingTexts();
            _size = MemoryLogic.Sizes[1];
            NewRound();
            var labels = new string[MemoryLogic.Sizes.Length];
            for (int i = 0; i < labels.Length; i++) labels[i] = MemoryLogic.Sizes[i].Label;
            OpenSelectMenu("MEMORY", labels, new[] { "Scegli la dimensione della griglia" }, 1, Palette.NeonCyan, choice =>
            {
                _size = MemoryLogic.Sizes[choice];
                NewRound();
                _started = true;
            });
        }

        private void NewRound()
        {
            _cards = MemoryLogic.Deal(_size, new System.Random(App.Rng.Next()));
            _selected.Clear();
            _moves = _streak = _cursor = 0;
            _mismatchTimer = 0f;
            _timer = new Stopwatch { Running = false };
            _best = ScoreStore.Get(RecordKey);
            _winT = -1f;
        }

        protected override void Restart()
        {
            NewRound();
            _particles.Clear();
            _popups.Clear();
        }

        private void FlipCard(int index)
        {
            if (_mismatchTimer > 0f || _selected.Count >= 2) return;
            var card = _cards[index];
            if (card.FaceUp || card.Matched) return;
            card.FaceUp = true;
            _selected.Add(card);
            _timer.Running = true;
            Sfx.Play(SfxId.Flip);
            if (_selected.Count < 2) return;
            _moves++;
            var a = _selected[0];
            var b = _selected[1];
            if (a.Icon == b.Icon)
            {
                a.Matched = b.Matched = true;
                a.Glow = b.Glow = 0.6f;
                _selected.Clear();
                _streak++;
                Sfx.Play(SfxId.Match);
                foreach (var c in new[] { a, b })
                    _particles.Burst(c.Rect.CenterX, c.Rect.CenterY, new[] { MemoryLogic.Icon(c.Icon).color, Palette.White }, 16,
                                     60f, 220f, 0.3f, 0.7f);
                if (_streak >= 2) _popups.Add("COMBO X" + _streak + "!", b.Rect.CenterX, b.Rect.Top, Palette.NeonYellow, 0.9f);
            }
            else
            {
                _mismatchTimer = MismatchDelay;
                a.Wrong = b.Wrong = true;
                _streak = 0;
            }
        }

        // ───────────────────────────── Logica ─────────────────────────────

        public override void Update(float timeMs, float deltaMs)
        {
            if (!_started) return;
            float dt = deltaMs / 1000f;
            _t += dt;

            if (_winT >= 0f)
            {
                _winT += dt;
                _particles.Update(dt);
                _popups.Update(dt);
                if (_winT * 60f >= WinFrames) ShowResults();
                return;
            }

            if (PausePressed()) { OpenPauseMenu("Memory Game", Commands); return; }
            int n = _cards.Count, cols = _size.Cols;
            if (In.AnyPressed(ArcadeKey.Left, ArcadeKey.A)) _cursor = (_cursor - 1 + n) % n;
            else if (In.AnyPressed(ArcadeKey.Right, ArcadeKey.D)) _cursor = (_cursor + 1) % n;
            else if (In.AnyPressed(ArcadeKey.Up, ArcadeKey.W)) _cursor = ((_cursor - cols) % n + n) % n;
            else if (In.AnyPressed(ArcadeKey.Down, ArcadeKey.S)) _cursor = (_cursor + cols) % n;
            else if (In.AnyPressed(ArcadeKey.Enter, ArcadeKey.Space)) FlipCard(_cursor);

            if (In.PointerMoved && In.PointerInside)
                for (int i = 0; i < n; i++)
                    if (_cards[i].Rect.Contains(In.Pointer.x, In.Pointer.y)) _cursor = i;
            if (In.PointerPressed)
                for (int i = 0; i < n; i++)
                {
                    if (!_cards[i].Rect.Contains(In.Pointer.x, In.Pointer.y)) continue;
                    _cursor = i;
                    FlipCard(i);
                }

            // Animazioni e pausa dopo un errore
            foreach (var card in _cards)
            {
                float target = card.FaceUp || card.Matched ? 1f : 0f;
                if (card.Flip < target) card.Flip = Mathf.Min(target, card.Flip + FlipSpeed * dt);
                else if (card.Flip > target) card.Flip = Mathf.Max(target, card.Flip - FlipSpeed * dt);
                card.Glow = Mathf.Max(0f, card.Glow - dt);
            }
            if (_mismatchTimer > 0f)
            {
                _mismatchTimer -= dt;
                if (_mismatchTimer <= 0f)
                {
                    foreach (var c in _selected)
                    {
                        c.FaceUp = false;
                        c.Wrong = false;
                    }
                    _selected.Clear();
                    Sfx.Play(SfxId.Error, 0.5f);
                }
            }
            _timer.Update(dt);
            _particles.Update(dt);
            _popups.Update(dt);

            bool allDone = true;
            foreach (var c in _cards) allDone &= c.Matched && c.Flip >= 1f;
            if (allDone)
            {
                _timer.Stop();
                _winT = 0f;
            }
        }

        private void ShowResults()
        {
            _winT = -1f;
            bool newRecord = ScoreStore.RecordBest(RecordKey, _moves, lowerIsBetter: true);
            double best = ScoreStore.Get(RecordKey) ?? _moves;
            int perfect = _cards.Count / 2;
            string size = _size.Label.Split(' ')[0].ToLowerInvariant();
            OpenEndScreen("HAI VINTO!", new[]
            {
                $"Mosse: {_moves} (minimo possibile: {perfect})   Tempo: {_timer}",
                $"Record ({size}): {best} mosse",
            }, newRecord, win: true);
        }

        // ───────────────────────────── Disegno ─────────────────────────────

        public override void Draw(RetroCanvas g)
        {
            g.FillRect(0, 0, W, H, Palette.Bg);
            if (!_started) return;
            for (int i = 0; i < _cards.Count; i++) DrawCard(g, _cards[i], i == _cursor);
            _particles.Draw(g);
            _popups.Draw(g);
            int matchedPairs = 0;
            foreach (var c in _cards) if (c.Matched) matchedPairs++;
            matchedPairs /= 2;
            g.PixelText("MOSSE " + _moves, 14, HudH / 2, 2, Palette.NeonCyan, 0f, 0.5f);
            g.PixelText("TEMPO " + _timer, 200, HudH / 2, 2, Palette.NeonGreen, 0f, 0.5f);
            g.PixelText($"COPPIE {matchedPairs}/{_cards.Count / 2}", 390, HudH / 2, 2, Palette.NeonMagenta, 0f, 0.5f);
            string best = _best.HasValue && _best.Value != 0 ? System.Math.Round(_best.Value).ToString() : "--";
            g.PixelText("RECORD " + best, W - 14, HudH / 2, 2, Palette.NeonYellow, 1f, 0.5f);
        }

        /// <summary>Una carta che gira sull'asse verticale (<c>draw_card</c>).</summary>
        private void DrawCard(RetroCanvas g, MemoryCard card, bool highlighted)
        {
            var r = card.Rect;
            int width = Mathf.Max(2, (int)(r.W * Mathf.Abs(Mathf.Cos(card.Flip * Mathf.PI))));
            bool face = card.Flip > 0.5f;
            var body = PRect.FromCenter(r.CenterX, r.CenterY, width, r.H);
            g.FillRoundedRect(body.X + 4, body.Y + 5, body.W, body.H, 10, Palette.Rgb(4, 2, 12));
            if (face)
            {
                uint border = card.Matched ? Palette.NeonGreen : card.Wrong ? Palette.NeonRed : Palette.Rgb(90, 90, 130);
                g.FillRoundedRect(body.X, body.Y, body.W, body.H, 10, Palette.Rgb(232, 232, 244));
                g.StrokeRoundedRect(body.X + 1.5f, body.Y + 1.5f, body.W - 3, body.H - 3, 8.5f, 3, border);
                int scale = Mathf.Max(2, r.W / 11);
                var (art, color) = MemoryLogic.Icon(card.Icon);
                float iconW = 7 * scale;
                float sx = width < iconW ? Mathf.Max(1, width - 4) / iconW : 1f; // icona "schiacciata" a metà giro
                DrawIcon(g, art, color, r.CenterX, r.CenterY, scale, sx);
                if (card.Matched) g.FillRoundedRect(body.X, body.Y, body.W, body.H, 10, Palette.Rgb(10, 40, 20), 70f / 255f);
            }
            else
            {
                uint back = Palette.Rgb(60, 30, 110);
                g.FillRoundedRect(body.X, body.Y, body.W, body.H, 10, back);
                var inner = body.Inflate(-12, -12);
                if (inner.W > 4)
                {
                    g.PushClip(inner.X, inner.Y, inner.W, inner.H); // le righe restano dentro la carta
                    uint stripe = Palette.Rgb(85, 45, 150);
                    for (int k = -r.H; k < r.W; k += 12)
                        g.Line(inner.X + k, inner.Y, inner.X + k + inner.H, inner.Bottom, 3, stripe);
                    g.PopClip();
                    var mid = inner.Inflate(-(int)(r.W / 2.2f), -(int)(r.H / 2.2f));
                    g.FillRoundedRect(mid.X, mid.Y, mid.W, mid.H, 6, back);
                    if (inner.W > r.W / 2)
                        g.PixelText("?", r.CenterX, r.CenterY, Mathf.Max(2, r.W / 28), Palette.NeonMagenta, 0.5f, 0.5f);
                }
                g.StrokeRoundedRect(body.X + 1, body.Y + 1, body.W - 2, body.H - 2, 9, 2, Palette.NeonMagenta);
            }
            if (card.Glow > 0f)
            {
                var gr = r.Inflate(24, 24);
                g.StrokeRoundedRect(gr.X + 2, gr.Y + 2, gr.W - 4, gr.H - 4, 12, 4, Palette.NeonGreen, 160f * card.Glow / 255f);
            }
            if (highlighted)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 6f);
                var hr = r.Inflate(10, 10);
                g.StrokeRoundedRect(hr.X + 1.5f, hr.Y + 1.5f, hr.W - 3, hr.H - 3, 11.5f, 3,
                                    Palette.Lerp(Palette.NeonYellow, Palette.White, pulse));
            }
        }

        /// <summary>Icona 7×7 con ombra (<c>icon_surface</c>), eventualmente schiacciata in orizzontale.</summary>
        private static void DrawIcon(RetroCanvas g, string[] art, uint color, float cx, float cy, int scale, float sx)
        {
            float size = 7 * scale;
            float w = size * sx;
            float ox = (int)(cx - w / 2), oy = (int)(cy - size / 2);
            uint shade = Palette.Lerp(color, Palette.Black, 0.55f);
            int depth = Mathf.Max(1, scale / 4);
            for (int pass = 0; pass < 2; pass++)
            {
                uint c = pass == 0 ? shade : color;
                float d = pass == 0 ? depth : 0;
                for (int y = 0; y < 7; y++)
                    for (int x = 0; x < 7; x++)
                    {
                        if (art[y][x] != '#') continue;
                        int start = x;
                        while (x + 1 < 7 && art[y][x + 1] == '#') x++;
                        float x0 = ox + (start * scale + d) * sx, x1 = ox + ((x + 1) * scale + d) * sx;
                        g.FillRect(x0, oy + y * scale + d, x1 - x0, scale, c);
                    }
            }
        }
    }
}
