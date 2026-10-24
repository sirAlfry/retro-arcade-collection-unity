using System.Collections.Generic;
using UnityEngine;

namespace RetroArcade.Games
{
    /// <summary>
    /// Il launcher: porting di <c>retro_arcade_interface.py</c> (classe <c>Launcher</c>).
    /// Sfondo synthwave (cielo sfumato, stelle che brillano, griglia prospettica che scorre), tessere 3×3
    /// con miniature animate, pannello informativo con descrizione, comandi e record, crediti a scorrimento.
    /// Tasti: frecce/WASD, INVIO/SPAZIO/clic per giocare, 1–9 avvio diretto, C crediti, M audio, V filtro CRT,
    /// ESC/Q esci.
    /// </summary>
    public sealed class LauncherScene : ArcadeScene
    {
        // ── Layout (stesse costanti di Python) ──
        private const int Cols = 3;
        private const float TileW = 210, TileH = 112, GapX = 22, GapY = 14;
        private const float GridX = (int)((W - (Cols * TileW + (Cols - 1) * GapX)) / 2); // 63
        private const float GridY = 98;
        private static readonly Rect InfoRect = new Rect(GridX, GridY + 3 * TileH + 2 * GapY + 10, Cols * TileW + (Cols - 1) * GapX, 98);
        private const float HorizonY = 300;
        private static readonly Rect CreditsArea = new Rect(60, 90, W - 120, H - 150);

        private const uint ColTileBody = 0x0c081e;   // (12, 8, 30)
        private const uint ColThumb = 0x04020c;      // (4, 2, 12)
        private const uint ColTileBorder = 0x462d78; // (70, 45, 120)
        private const uint ColNumber = 0x6e5aa0;     // (110, 90, 160)
        private const uint ColFps = 0x6ec878;        // (110, 200, 120)
        private const uint ColGrid = 0x9628aa;       // (150, 40, 170)

        /// <summary>Le stesse 90 stelle della versione Python (random.Random(7)): x, y, fase, dimensione.</summary>
        private static readonly (int x, int y, float phase, int size)[] Stars =
        {
            (331, 77, 2.48075f, 1), (74, 274, 0.59144f, 1), (519, 109, 0.23559f, 2), (428, 35, 1.51213f, 2), (60, 289, 0.77787f, 1), (645, 31, 3.62604f, 2),
            (50, 113, 0.29269f, 1), (296, 214, 0.90638f, 1), (584, 157, 3.52020f, 1), (105, 96, 2.33984f, 1), (577, 30, 3.88935f, 2), (696, 272, 2.68664f, 1),
            (476, 232, 2.27189f, 1), (184, 124, 0.51431f, 1), (537, 253, 5.49865f, 2), (294, 37, 0.74183f, 2), (168, 175, 0.95495f, 2), (431, 20, 6.04454f, 1),
            (782, 285, 3.60043f, 1), (348, 179, 3.73454f, 2), (70, 47, 5.93561f, 2), (713, 33, 0.38120f, 1), (662, 228, 1.78817f, 2), (684, 177, 0.14177f, 2),
            (363, 86, 3.83852f, 2), (60, 111, 4.82695f, 1), (756, 126, 2.50006f, 2), (82, 85, 2.82233f, 1), (140, 220, 5.42857f, 1), (723, 212, 6.19816f, 2),
            (236, 77, 0.52141f, 1), (237, 119, 0.07579f, 1), (269, 144, 0.02572f, 2), (547, 189, 3.83156f, 1), (128, 263, 5.97043f, 1), (467, 286, 2.46539f, 2),
            (403, 53, 3.02550f, 2), (63, 97, 0.42316f, 1), (451, 83, 0.69070f, 1), (104, 0, 3.56121f, 1), (372, 13, 0.44181f, 1), (628, 192, 0.93337f, 1),
            (355, 186, 2.97918f, 1), (499, 238, 3.01841f, 1), (87, 73, 0.64206f, 1), (758, 135, 3.00727f, 1), (528, 11, 1.28940f, 1), (150, 278, 5.74375f, 1),
            (658, 46, 4.37433f, 1), (530, 187, 5.70676f, 1), (790, 114, 3.34638f, 1), (651, 114, 3.85303f, 1), (245, 205, 4.64876f, 1), (204, 265, 3.09624f, 1),
            (28, 143, 2.96717f, 1), (709, 176, 2.81001f, 1), (373, 41, 1.38521f, 1), (481, 100, 2.12207f, 2), (639, 0, 3.01262f, 1), (658, 43, 5.24425f, 1),
            (397, 102, 3.00357f, 1), (444, 170, 0.54507f, 2), (474, 205, 4.67062f, 1), (742, 81, 1.06816f, 1), (28, 77, 3.71218f, 2), (671, 74, 3.84263f, 2),
            (673, 179, 0.97963f, 1), (21, 7, 5.02251f, 1), (539, 71, 2.72571f, 1), (216, 14, 1.58232f, 1), (513, 123, 4.79834f, 1), (265, 278, 2.63273f, 1),
            (62, 181, 5.64044f, 2), (513, 66, 3.34155f, 1), (450, 93, 3.82366f, 1), (176, 72, 2.97504f, 1), (569, 31, 2.04821f, 2), (795, 54, 5.54948f, 1),
            (254, 97, 1.73992f, 1), (519, 231, 3.52945f, 1), (453, 166, 3.84863f, 1), (709, 141, 2.84217f, 2), (519, 126, 4.39332f, 1), (572, 103, 5.27787f, 1),
            (426, 62, 2.46530f, 1), (74, 123, 2.69133f, 1), (685, 155, 4.92562f, 1), (733, 187, 0.89836f, 1), (478, 112, 4.69154f, 1), (407, 249, 1.02287f, 1),
        };

        private static readonly (char kind, string text)[] Credits = BuildCredits();

        private static (char, string)[] BuildCredits()
        {
            var list = new List<(char, string)>
            {
                ('h', "RETRO ARCADE COLLECTION"),
                ('t', "Versione " + ArcadeInfo.Version),
                ('s', ""),
                ('h', "IDEATO E SVILUPPATO DA"),
                ('t', "Sir Alfry  (Alfredo De Donno)"),
                ('s', ""),
                ('h', "TECNOLOGIE"),
                ('t', "Unity 6  ·  C#  (porting della versione Python + Pygame-CE)"),
                ('t', "Effetti sonori sintetizzati in tempo reale (zero file audio)"),
                ('t', "Font bitmap 5x7 disegnato a mano"),
                ('t', "AI Minimax / Alpha-Beta con iterative deepening"),
                ('s', ""),
                ('h', "I GIOCHI"),
            };
            foreach (var g in GameCatalog.Games) list.Add(('t', g.Name));
            list.AddRange(new (char, string)[]
            {
                ('s', ""),
                ('h', "SEGUIMI"),
                ('t', "Instagram: instagram.com/sir_alfry"),
                ('t', "ArtStation: artstation.com/siralfry"),
                ('t', "GitHub: github.com/sirAlfry"),
                ('s', ""),
                ('h', "GRAZIE PER AVER GIOCATO!"),
                ('s', ""), ('s', ""),
            });
            return list.ToArray();
        }

        private static readonly Rect[] TileRects = BuildTileRects();

        private static Rect[] BuildTileRects()
        {
            var r = new Rect[GameCatalog.Games.Length];
            for (int i = 0; i < r.Length; i++)
                r[i] = new Rect(GridX + (i % Cols) * (TileW + GapX), GridY + (i / Cols) * (TileH + GapY), TileW, TileH);
            return r;
        }

        private int _selected;
        private bool _credits;
        private float _time;           // secondi
        private float _scroll;         // griglia synthwave, 0..1
        private float _creditsScroll;
        private string[] _records;

        /// <summary>Errore di un gioco da mostrare al ritorno (come il message_box "OPS!" di Python).</summary>
        internal static string[] PendingError;

        public override void Create()
        {
            _selected = Mathf.Abs((int)ScoreStore.GetSetting("last_game", 0)) % GameCatalog.Games.Length;
            RefreshRecords();
            if (PendingError != null)
            {
                var lines = new System.Collections.Generic.List<string>(PendingError)
                {
                    "I dettagli sono nella Console di Unity (o nel Player.log)",
                };
                PendingError = null;
                Sfx.Play(SfxId.Error);
                OpenMessage("OPS!", lines.ToArray(), null);
            }
        }

        private void RefreshRecords()
        {
            _records = new string[GameCatalog.Games.Length];
            for (int i = 0; i < _records.Length; i++) _records[i] = GameCatalog.Games[i].Record();
        }

        // ───────────────────────────── Logica ─────────────────────────────

        public override void Update(float timeMs, float deltaMs)
        {
            float dt = deltaMs / 1000f;
            _time += dt;
            _scroll = (_scroll + dt * 0.35f) % 1f;

            if (_credits)
            {
                _creditsScroll += dt * 45f;
                if (In.AnyPressed(ArcadeKey.Escape, ArcadeKey.B, ArcadeKey.C, ArcadeKey.Enter, ArcadeKey.Space))
                {
                    Sfx.Play(SfxId.Back);
                    _credits = false;
                }
                else if (In.PointerPressed)
                {
                    _credits = false;
                }
                return;
            }

            if (In.AnyPressed(ArcadeKey.Enter, ArcadeKey.Space)) { Launch(_selected); return; }
            if (In.AnyPressed(ArcadeKey.Right, ArcadeKey.D)) MoveSelection(1, 0);
            else if (In.AnyPressed(ArcadeKey.Left, ArcadeKey.A)) MoveSelection(-1, 0);
            else if (In.AnyPressed(ArcadeKey.Down, ArcadeKey.S)) MoveSelection(0, 1);
            else if (In.AnyPressed(ArcadeKey.Up, ArcadeKey.W)) MoveSelection(0, -1);
            else if (In.Pressed(ArcadeKey.C))
            {
                Sfx.Play(SfxId.Select);
                _credits = true;
                _creditsScroll = CreditsArea.height - 24f; // le prime righe si vedono subito
            }
            else if (In.Pressed(ArcadeKey.M)) App.ToggleSound();
            else if (In.Pressed(ArcadeKey.V)) App.ToggleCrt();
            else if (In.AnyPressed(ArcadeKey.Escape, ArcadeKey.Q))
            {
                if (App.CanQuit) App.Quit();
            }
            else
            {
                var digits = new[] { ArcadeKey.One, ArcadeKey.Two, ArcadeKey.Three, ArcadeKey.Four, ArcadeKey.Five,
                                     ArcadeKey.Six, ArcadeKey.Seven, ArcadeKey.Eight, ArcadeKey.Nine };
                for (int i = 0; i < digits.Length && i < GameCatalog.Games.Length; i++)
                {
                    if (!In.Pressed(digits[i])) continue;
                    Launch(i);
                    return;
                }
            }

            if (In.PointerMoved && In.PointerInside)
            {
                for (int i = 0; i < TileRects.Length; i++)
                {
                    if (!TileRects[i].Contains(In.Pointer) || i == _selected) continue;
                    _selected = i;
                    Sfx.Play(SfxId.Move, 0.4f);
                }
            }
            if (In.PointerPressed)
            {
                for (int i = 0; i < TileRects.Length; i++)
                {
                    if (!TileRects[i].Contains(In.Pointer)) continue;
                    Launch(i);
                    return;
                }
            }
        }

        private void MoveSelection(int dc, int dr)
        {
            int rows = GameCatalog.Games.Length / Cols;
            int col = ((_selected % Cols + dc) % Cols + Cols) % Cols;
            int row = ((_selected / Cols + dr) % rows + rows) % rows;
            _selected = row * Cols + col;
            Sfx.Play(SfxId.Move);
        }

        private void Launch(int index)
        {
            _selected = index;
            ScoreStore.SetSetting("last_game", index);
            Sfx.Play(SfxId.Launch);
            App.StartScene(GameCatalog.Games[index].SceneKey);
        }

        // ───────────────────────────── Disegno ─────────────────────────────

        public override void Draw(RetroCanvas g)
        {
            if (_credits) DrawCredits(g);
            else DrawMain(g);
        }

        private void DrawMain(RetroCanvas g)
        {
            DrawBackground(g);
            g.PixelText("RETRO ARCADE", W / 2, 34, 4, Palette.NeonCyan, 0.5f, 0.5f, glow: true);
            g.PixelText("COLLECTION", W / 2, 72, 3, Palette.NeonMagenta, 0.5f, 0.5f, glow: true);
            for (int i = 0; i < TileRects.Length; i++)
                if (i != _selected) DrawTile(g, i, false);
            DrawTile(g, _selected, true);
            DrawInfoPanel(g);

            string footer = "↑↓←→ SCEGLI · INVIO GIOCA · C CREDITI · M AUDIO " + (App.SoundOn ? "ON" : "OFF") +
                            " · V CRT · F11 SCHERMO INTERO · ESC ESCI";
            g.PixelText(footer, W / 2, H - 14, 1, Palette.TextDim, 0.5f, 0.5f);
            if (App.ShowFps)
                g.PixelText(Mathf.RoundToInt(App.Fps).ToString().PadLeft(2) + " FPS", W - 10, 8, 1, ColFps, 1f, 0f);
            g.PixelText("V" + ArcadeInfo.Version, 10, 8, 1, ColNumber);
        }

        /// <summary>Cielo sfumato, stelle e griglia prospettica (<c>SynthwaveBackground</c>).</summary>
        private void DrawBackground(RetroCanvas g)
        {
            const uint top = 0x060414, mid = 0x2e0a42, bottom = 0x0c041a; // (6,4,20) (46,10,66) (12,4,26)
            // Il cielo Python ha sfumature NON lineari (potenze 1.6 e 0.6): lo approssimiamo con tante fasce lineari.
            const int bands = 24;
            for (int i = 0; i < bands; i++)
            {
                float y0 = HorizonY * i / bands, y1 = HorizonY * (i + 1) / bands;
                g.FillRectGradientV(0, y0, W, y1 - y0,
                    Palette.Lerp(top, mid, Mathf.Pow(y0 / HorizonY, 1.6f)), Palette.Lerp(top, mid, Mathf.Pow(y1 / HorizonY, 1.6f)));
                float z0 = (float)i / bands, z1 = (float)(i + 1) / bands;
                g.FillRectGradientV(0, HorizonY + (H - HorizonY) * z0, W, (H - HorizonY) / bands,
                    Palette.Lerp(mid, bottom, Mathf.Pow(z0, 0.6f)), Palette.Lerp(mid, bottom, Mathf.Pow(z1, 0.6f)));
            }
            g.FillRect(0, HorizonY - 1, W, 2, Palette.Rgb(255, 80, 200));

            foreach (var s in Stars)
            {
                int b = (int)(120 + 120 * (0.5f + 0.5f * Mathf.Sin(_time * 2.2f + s.phase)));
                g.FillRect(s.x, s.y, s.size, s.size, Palette.Rgb(b, b, Mathf.Min(255, b + 30)));
            }

            // Linee orizzontali: spaziatura prospettica, scorrono verso chi guarda.
            float depth = H - HorizonY;
            for (int i = 0; i < 14; i++)
            {
                float z = (i + _scroll) / 14f;
                float y = HorizonY + (int)(depth * z * z);
                g.FillRect(0, y, W, 1, ColGrid);
            }
            // Linee verticali che convergono nel punto di fuga.
            float vx = W / 2;
            for (int i = -12; i <= 12; i++)
                g.Line(vx + i * 18, HorizonY, vx + i * 130, H, 1f, ColGrid);
        }

        private void DrawTile(RetroCanvas g, int i, bool selected)
        {
            var info = GameCatalog.Games[i];
            var r = TileRects[i];
            float pulse = 0.5f + 0.5f * Mathf.Sin(_time * 4f);
            if (selected)
            {
                // Alone magenta a 6 livelli (in pygame ogni livello SOSTITUISCE il precedente: anelli concentrici).
                for (int k = 0; k < 3; k++)
                {
                    float a = (14f + 14f * pulse) * (1f - k / 6f) / 255f;
                    float inset = -18f + k * 6f;
                    g.StrokeRoundedRect(r.x + inset + 3, r.y + inset + 3, r.width - 2 * inset - 6, r.height - 2 * inset - 6,
                                        19f, 6f, Palette.NeonMagenta, a);
                }
            }
            g.FillRoundedRect(r.x, r.y, r.width, r.height, 12, ColTileBody, 235f / 255f);
            var thumb = new Rect(r.x + 10, r.y + 10, r.width - 20, r.height - 40);
            g.FillRoundedRect(thumb.x, thumb.y, thumb.width, thumb.height, 6, ColThumb);
            g.PushClip(thumb.x, thumb.y, thumb.width, thumb.height);
            Thumbs.Draw(i, g, thumb, selected ? _time : 0f);
            g.PopClip();

            if (selected) g.StrokeRoundedRect(r.x + 1.5f, r.y + 1.5f, r.width - 3, r.height - 3, 10.5f, 3, Palette.NeonCyan);
            else g.StrokeRoundedRect(r.x + 1, r.y + 1, r.width - 2, r.height - 2, 11, 2, ColTileBorder);

            g.PixelText(info.Name.ToUpperInvariant(), r.x + 12, r.y + r.height - 17, 2, selected ? Palette.White : Palette.TextDim, 0f, 0.5f);
            if (_records[i] != null)
                g.PixelText("★", r.x + r.width - 12, r.y + r.height - 17, 2, Palette.NeonYellow, 1f, 0.5f);
            g.PixelText((i + 1).ToString(), r.x + r.width - 8, r.y + 6, 1, ColNumber, 1f, 0f);
        }

        private void DrawInfoPanel(RetroCanvas g)
        {
            var info = GameCatalog.Games[_selected];
            var r = InfoRect;
            Ui.Panel(g, r.x, r.y, r.width, r.height, info.Accent, 215, 10);
            float x = r.x + 16;
            g.PixelText(info.Name.ToUpperInvariant(), x, r.y + 12, 3, info.Accent);
            string record = _records[_selected] ?? "NESSUN RECORD... ANCORA!";
            g.PixelText(record, r.x + r.width - 16, r.y + 16, 2, Palette.NeonYellow, 1f, 0f);
            Ui.Body(g, info.Description, x, r.y + 38, 15, Palette.Text);
            for (int k = 0; k < info.Controls.Length; k++)
                Ui.Body(g, info.Controls[k], x, r.y + 59 + k * 16, 13, Palette.TextDim);
        }

        private void DrawCredits(RetroCanvas g)
        {
            DrawBackground(g);
            g.PixelText("CREDITI", W / 2, 44, 5, Palette.NeonYellow, 0.5f, 0.5f, glow: true);
            var area = CreditsArea;
            Ui.Panel(g, area.x, area.y, area.width, area.height, Palette.NeonPurple, 200);
            g.PushClip(area.x + 4, area.y + 4, area.width - 8, area.height - 8);
            const float lineH = 30f;
            float total = Credits.Length * lineH;
            float start = area.y + area.height - (_creditsScroll % (total + area.height));
            for (int k = 0; k < Credits.Length; k++)
            {
                float y = (int)(start + k * lineH);
                if (y < area.y - lineH || y > area.y + area.height) continue;
                var (kind, text) = Credits[k];
                if (kind == 'h') g.PixelText(text, W / 2, y, 3, Palette.NeonCyan, 0.5f, 0f);
                else if (kind == 't') Ui.Body(g, text, W / 2, y + 2, 17, Palette.Text, 0.5f, 0f);
            }
            g.PopClip();
            g.PixelText("ESC / B / C: TORNA AL MENU", W / 2, H - 30, 2, Palette.TextDim, 0.5f, 0.5f);
        }
    }
}
