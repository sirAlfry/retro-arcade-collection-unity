using RetroArcade.Logic;
using UnityEngine;

namespace RetroArcade.Games
{
    /// <summary>
    /// Puzzle 15, il classico gioco del 15 di Sam Loyd: porting di <c>puzzle15.py</c>
    /// (regole in <see cref="Puzzle15Logic"/>, coperte da test).
    /// <list type="bullet">
    /// <item>Clic su qualsiasi tessera nella riga/colonna del buco: scorre l'intera fila in un colpo.</item>
    /// <item>Frecce/WASD: spingono nel buco la tessera vicina (la tessera si muove nel verso della freccia).</item>
    /// <item>Ogni tessera scivola morbida verso la sua casella; quelle al posto giusto si illuminano.</item>
    /// <item>Colori per riga di destinazione; record di mosse e di tempo.</item>
    /// </list>
    /// </summary>
    public sealed class Puzzle15Scene : ArcadeScene
    {
        private const int HudH = 44;
        private const int Size = Puzzle15Logic.Size;
        private const int Tile = 110, Gap = 8;
        private const int BoardPx = Size * Tile + (Size - 1) * Gap;
        private const int OffX = ((int)W - BoardPx) / 2;
        private const int OffY = HudH + ((int)H - HudH - BoardPx) / 2;
        private const int WinFrames = 45;

        private static readonly uint[] RowColors = { Palette.NeonCyan, Palette.NeonMagenta, Palette.NeonYellow, Palette.NeonGreen };
        private static readonly string[] Commands =
        {
            "Clic su una tessera nella riga/colonna del buco",
            "Frecce: spingi una tessera nel buco   R: rimescola",
        };

        private Particles _particles;
        private float _winT = -1f;

        // Una partita (Round in Python)
        private int[] _tiles;
        private int _moves;
        private Stopwatch _timer;
        private readonly Vector2[] _visual = new Vector2[Size * Size]; // posizione disegnata di ogni VALORE
        private double? _bestMoves, _bestTime;

        public override void Create()
        {
            _particles = new Particles(App.Rng);
            NewRound();
        }

        private void NewRound()
        {
            _tiles = Puzzle15Logic.Shuffled(new System.Random(App.Rng.Next()));
            _moves = 0;
            _timer = new Stopwatch { Running = false };
            for (int i = 0; i < _tiles.Length; i++)
                if (_tiles[i] != 0) _visual[_tiles[i]] = TilePos(i);
            _bestMoves = ScoreStore.Get("puzzle15_moves");
            _bestTime = ScoreStore.Get("puzzle15_time");
            _winT = -1f;
        }

        protected override void Restart() => NewRound();

        private static Vector2 TilePos(int index) =>
            new Vector2(OffX + index % Size * (Tile + Gap), OffY + index / Size * (Tile + Gap));

        private void Apply(int moved)
        {
            if (moved == 0) return;
            _moves += moved;
            _timer.Running = true;
            Sfx.Play(SfxId.Slide);
        }

        // ───────────────────────────── Logica ─────────────────────────────

        public override void Update(float timeMs, float deltaMs)
        {
            float dt = deltaMs / 1000f;

            if (_winT >= 0f)
            {
                // I 45 fotogrammi di festa dopo la soluzione: tessere ferme, coriandoli, input ignorato.
                _winT += dt;
                for (int i = 0; i < _tiles.Length; i++)
                    if (_tiles[i] != 0) _visual[_tiles[i]] = TilePos(i);
                _particles.Update(dt);
                if (_winT * 60f >= WinFrames) ShowResults();
                return;
            }

            if (PausePressed()) { OpenPauseMenu("Puzzle 15", Commands); return; }
            if (In.Pressed(ArcadeKey.R))
            {
                NewRound();
                Sfx.Play(SfxId.Select);
            }
            else if (In.AnyPressed(ArcadeKey.Up, ArcadeKey.W)) Apply(Puzzle15Logic.Push(_tiles, 0, -1));
            else if (In.AnyPressed(ArcadeKey.Down, ArcadeKey.S)) Apply(Puzzle15Logic.Push(_tiles, 0, 1));
            else if (In.AnyPressed(ArcadeKey.Left, ArcadeKey.A)) Apply(Puzzle15Logic.Push(_tiles, -1, 0));
            else if (In.AnyPressed(ArcadeKey.Right, ArcadeKey.D)) Apply(Puzzle15Logic.Push(_tiles, 1, 0));

            if (In.PointerPressed)
            {
                for (int i = 0; i < _tiles.Length; i++)
                {
                    var p = TilePos(i);
                    if (!new PRect((int)p.x, (int)p.y, Tile, Tile).Contains(In.Pointer.x, In.Pointer.y)) continue;
                    int moved = Puzzle15Logic.Slide(_tiles, i);
                    if (moved > 0) Apply(moved);
                    else Sfx.Play(SfxId.Error, 0.4f);
                    break;
                }
            }

            // Ogni tessera insegue la sua casella (movimento morbido)
            float follow = Mathf.Min(1f, dt * 18f);
            for (int i = 0; i < _tiles.Length; i++)
            {
                int v = _tiles[i];
                if (v == 0) continue;
                _visual[v] += (TilePos(i) - _visual[v]) * follow;
            }
            _timer.Update(dt);
            _particles.Update(dt);

            if (Puzzle15Logic.IsSolved(_tiles) && _moves > 0)
            {
                _timer.Stop();
                for (int i = 0; i < Size * Size - 1; i++)
                {
                    var p = TilePos(i);
                    _particles.Burst(p.x + Tile / 2, p.y + Tile / 2, RowColors[i / Size], 8, 60f, 240f, 0.4f, 1.0f);
                }
                _winT = 0f;
            }
        }

        private void ShowResults()
        {
            _winT = -1f;
            double seconds = System.Math.Round(_timer.Elapsed, 1);
            bool recMoves = ScoreStore.RecordBest("puzzle15_moves", _moves, lowerIsBetter: true);
            bool recTime = ScoreStore.RecordBest("puzzle15_time", seconds, lowerIsBetter: true);
            double bestMoves = ScoreStore.Get("puzzle15_moves") ?? _moves;
            double bestTime = ScoreStore.Get("puzzle15_time") ?? seconds;
            OpenEndScreen("COMPLETATO!", new[]
            {
                $"Mosse: {_moves}   Tempo: {Ui.FormatTime(seconds)}",
                $"Record: {System.Math.Round(bestMoves)} mosse · {Ui.FormatTime(bestTime)}",
            }, recMoves || recTime, win: true);
        }

        // ───────────────────────────── Disegno ─────────────────────────────

        public override void Draw(RetroCanvas g)
        {
            g.FillRect(0, 0, W, H, Palette.Bg);
            var frame = new PRect(OffX - 14, OffY - 14, BoardPx + 28, BoardPx + 28);
            g.FillRoundedRect(frame.X, frame.Y, frame.W, frame.H, 14, Palette.Rgb(10, 8, 26));
            g.StrokeRoundedRect(frame.X + 1.5f, frame.Y + 1.5f, frame.W - 3, frame.H - 3, 12.5f, 3, Palette.NeonYellow);
            for (int i = 0; i < Size * Size; i++)
            {
                var p = TilePos(i);
                g.FillRoundedRect(p.x, p.y, Tile, Tile, 10, Palette.Rgb(18, 16, 40));
            }

            int inPlace = 0;
            for (int i = 0; i < _tiles.Length; i++)
            {
                int v = _tiles[i];
                if (v == 0) continue;
                var rect = new PRect((int)_visual[v].x, (int)_visual[v].y, Tile, Tile);
                uint color = RowColors[(v - 1) / Size];
                bool correct = i == v - 1;
                if (correct) inPlace++;
                uint baseColor = Palette.Lerp(Palette.Rgb(26, 22, 56), color, correct ? 0.28f : 0.12f);
                g.FillRoundedRect(rect.X + 4, rect.Y + 5, Tile, Tile, 10, Palette.Rgb(4, 2, 12));
                g.FillRoundedRect(rect.X, rect.Y, Tile, Tile, 10, baseColor);
                g.StrokeRoundedRect(rect.X + 1.5f, rect.Y + 1.5f, Tile - 3, Tile - 3, 8.5f, 3,
                                    correct ? color : Palette.Lerp(color, Palette.Bg, 0.55f));
                g.Line(rect.X + 10, rect.Y + 6, rect.Right - 10, rect.Y + 6, 2, Palette.Lerp(baseColor, Palette.White, 0.25f));
                g.PixelText(v.ToString(), rect.CenterX, rect.CenterY, 6, correct ? Palette.White : color, 0.5f, 0.5f,
                            shadow: Palette.Black);
            }
            _particles.Draw(g);

            g.PixelText("MOSSE " + _moves, 14, HudH / 2, 2, Palette.NeonCyan, 0f, 0.5f);
            g.PixelText("TEMPO " + _timer, 190, HudH / 2, 2, Palette.NeonGreen, 0f, 0.5f);
            g.PixelText($"AL POSTO {inPlace}/15", 370, HudH / 2, 2, Palette.NeonMagenta, 0f, 0.5f);
            string best = _bestMoves.HasValue && _bestMoves.Value != 0
                ? $"{System.Math.Round(_bestMoves.Value)} · {Ui.FormatTime(_bestTime ?? 0)}"
                : "--";
            g.PixelText("RECORD " + best, W - 14, HudH / 2, 2, Palette.NeonYellow, 1f, 0.5f);
        }
    }
}
