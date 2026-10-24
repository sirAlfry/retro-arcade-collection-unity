using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetroArcade
{
    /// <summary>
    /// Disegni "da interfaccia" condivisi (pannelli neon, testo normale), come gli helper di <c>game_shared.py</c>.
    /// </summary>
    public static class Ui
    {
        /// <summary>
        /// Testo "normale" (descrizioni, comandi, HUD). In Python era un font di sistema monospazio (Consolas);
        /// qui il font bitmap 6×10 del progetto. <paramref name="size"/> è la dimensione Python in pixel:
        /// 13–17 diventano scala 1.5, 18 e oltre scala 2 (sempre multipli di mezzo pixel: restano nitidi).
        /// </summary>
        public static Rect Body(RetroCanvas g, string text, float x, float y, int size, uint rgb,
                                float originX = 0f, float originY = 0f, float alpha = 1f)
        {
            float scale = BodyScale(size);
            var s = g.Text(text, x, y, scale, rgb, originX, originY, FontFace.Body, alpha);
            return new Rect(x - originX * s.x, y - originY * s.y, s.x, s.y);
        }

        public static float BodyScale(int size) => size >= 18 ? 2f : size >= 11 ? 1.5f : 1f;

        public static Vector2 MeasureBody(RetroCanvas g, string text, int size) => g.Measure(text, BodyScale(size));

        /// <summary>Pannello scuro traslucido con bordo neon (<c>draw_panel</c>).</summary>
        public static void Panel(RetroCanvas g, float x, float y, float w, float h, uint border = Palette.NeonCyan,
                                 int fillAlpha = 225, float radius = 12f)
        {
            // In pygame il bordo largo 6 SOSTITUISCE il riempimento (alpha 90 invece di 225): qui lo stesso.
            g.FillRoundedRect(x + 6, y + 6, w - 12, h - 12, Mathf.Max(0f, radius - 6f), Palette.PanelBg, fillAlpha / 255f);
            g.StrokeRoundedRect(x + 3, y + 3, w - 6, h - 6, Mathf.Max(0f, radius - 3f), 6f, border, 90f / 255f);
            g.StrokeRoundedRect(x + 3, y + 3, w - 6, h - 6, Mathf.Max(0f, radius - 3f), 2f, border);
        }

        /// <summary>Cuoricini delle vite (<c>draw_hearts</c>), ancorati in alto a destra per default.</summary>
        public static void Hearts(RetroCanvas g, int lives, float x, float y, uint color = Palette.NeonRed,
                                  float scale = 2f, float originX = 1f, float originY = 0f)
        {
            if (lives <= 0) return;
            g.PixelText(new string('♥', lives), x, y, scale, color, originX, originY);
        }

        /// <summary>Secondi → "m:ss" (<c>format_time</c>).</summary>
        public static string FormatTime(double seconds)
        {
            int s = Math.Max(0, (int)seconds);
            return (s / 60) + ":" + (s % 60).ToString("00");
        }
    }

    /// <summary>
    /// Il menu generico tastiera + mouse dentro un pannello neon: porting di <c>_run_menu</c>.
    /// Lo usano il menu di pausa, la schermata di fine partita, la scelta della modalità e i messaggi.
    /// <para>Mentre è aperto la scena è "congelata": <see cref="ArcadeScene"/> non chiama più il suo Update
    /// ma continua a disegnarla sotto il menu (come lo snapshot del frame in Python).</para>
    /// </summary>
    public sealed class MenuOverlay
    {
        public const string ToggleSound = "toggle_sound";
        public const string ToggleCrt = "toggle_crt";

        private readonly struct Option
        {
            public readonly Func<string> Label;
            public readonly string Value;
            public Option(Func<string> label, string value) { Label = label; Value = value; }
        }

        private readonly List<Option> _options = new List<Option>();
        private readonly Dictionary<ArcadeKey, string> _hotkeys = new Dictionary<ArcadeKey, string>();
        private readonly List<Rect> _optionRects = new List<Rect>();
        private readonly Action<string> _onChoice;

        public string Title;
        public readonly List<string> InfoLines = new List<string>();
        public string CancelValue;
        public uint TitleColor = Palette.NeonCyan;
        /// <summary>Velo scuro sopra la scena (false per la scelta della modalità, che ha uno sfondo pieno).</summary>
        public bool Dim = true;
        /// <summary>Se true, disegna lo sfondo BG pieno al posto della scena (come <c>select_menu</c>).</summary>
        public bool SolidBackdrop;
        public string Subtitle;
        public uint SubtitleColor = Palette.NeonYellow;
        /// <summary>Per un attimo ignora le conferme: chi sta martellando SPAZIO non salta la schermata finale.</summary>
        public float InputDelayMs;
        public int Selected;

        private float _timeMs;

        public MenuOverlay(string title, Action<string> onChoice)
        {
            Title = title;
            _onChoice = onChoice;
        }

        public MenuOverlay AddOption(string label, string value) => AddOption(() => label, value);

        public MenuOverlay AddOption(Func<string> label, string value)
        {
            _options.Add(new Option(label, value));
            return this;
        }

        public MenuOverlay Hotkey(ArcadeKey key, string value)
        {
            _hotkeys[key] = value;
            return this;
        }

        public int OptionCount => _options.Count;

        /// <summary>Un frame di logica. Restituisce true quando il menu si è chiuso (scelta fatta).</summary>
        public bool Update(IArcadeHost app, float deltaMs)
        {
            _timeMs += deltaMs;
            var input = app.Controls;
            bool accepting = _timeMs >= InputDelayMs;
            string choice = null;

            if (accepting)
            {
                if (input.AnyPressed(ArcadeKey.Up, ArcadeKey.W))
                {
                    Selected = (Selected - 1 + _options.Count) % _options.Count;
                    app.Sfx.Play(SfxId.Move);
                }
                else if (input.AnyPressed(ArcadeKey.Down, ArcadeKey.S))
                {
                    Selected = (Selected + 1) % _options.Count;
                    app.Sfx.Play(SfxId.Move);
                }
                else if (input.AnyPressed(ArcadeKey.Enter, ArcadeKey.Space))
                {
                    choice = _options[Selected].Value;
                }
                else if (input.Pressed(ArcadeKey.Escape) && CancelValue != null)
                {
                    app.Sfx.Play(SfxId.Back);
                    Close(CancelValue);
                    return true;
                }
                else
                {
                    foreach (var kv in _hotkeys)
                    {
                        if (!input.Pressed(kv.Key)) continue;
                        choice = kv.Value;
                        break;
                    }
                }

                if (choice == null && input.PointerMoved)
                {
                    for (int i = 0; i < _optionRects.Count; i++)
                    {
                        if (!_optionRects[i].Contains(input.Pointer) || i == Selected) continue;
                        Selected = i;
                        app.Sfx.Play(SfxId.Move, 0.5f);
                    }
                }
                if (choice == null && input.PointerPressed)
                {
                    for (int i = 0; i < _optionRects.Count; i++)
                    {
                        if (!_optionRects[i].Contains(input.Pointer)) continue;
                        Selected = i;
                        choice = _options[i].Value;
                    }
                }
            }

            if (choice == ToggleSound)
            {
                app.ToggleSound();
            }
            else if (choice == ToggleCrt)
            {
                app.ToggleCrt();
                app.Sfx.Play(SfxId.Select);
            }
            else if (choice != null)
            {
                app.Sfx.Play(SfxId.Select);
                Close(choice);
                return true;
            }
            return false;
        }

        private void Close(string value) => _onChoice?.Invoke(value);

        public void Draw(RetroCanvas g)
        {
            const float W = RetroCanvas.W, H = RetroCanvas.H;
            if (SolidBackdrop) g.FillRect(0, 0, W, H, Palette.Bg);
            if (Dim) g.FillRect(0, 0, W, H, 0x04020e, 170f / 255f);

            float titleScale = Title.Length <= 16 ? 4f : 3f;
            Vector2 titleSize = RetroCanvas.MeasurePixel(Title, titleScale, glow: true);
            const float optH = 34f;
            float infoH = InfoLines.Count * 22f + (InfoLines.Count > 0 ? 12f : 0f);
            float subH = Subtitle != null ? 30f : 0f;
            float panelW = Mathf.Max(460f, titleSize.x + 60f);
            foreach (var line in InfoLines) panelW = Mathf.Max(panelW, Ui.MeasureBody(g, line, 16).x + 60f);
            panelW = Mathf.Min(panelW, W - 40f);
            float panelH = 40f + titleSize.y + subH + infoH + _options.Count * optH + 44f;
            float px = Mathf.Floor(W / 2 - panelW / 2), py = Mathf.Floor(H / 2 - panelH / 2);
            Ui.Panel(g, px, py, panelW, panelH, TitleColor);

            float y = py + 22f;
            g.PixelText(Title, W / 2, y, titleScale, TitleColor, 0.5f, 0f, glow: true);
            y += titleSize.y + 6f;
            if (Subtitle != null)
            {
                float pulse = 0.6f + 0.4f * Mathf.Sin(_timeMs / 1000f * 6f);
                g.PixelText(Subtitle, W / 2, y + 6f, 2f, Palette.Lerp(Palette.Bg, SubtitleColor, pulse), 0.5f, 0f);
                y += subH;
            }
            foreach (var line in InfoLines)
            {
                Ui.Body(g, line, W / 2, y, 16, Palette.TextDim, 0.5f, 0f);
                y += 22f;
            }
            if (InfoLines.Count > 0) y += 12f;

            _optionRects.Clear();
            for (int i = 0; i < _options.Count; i++)
            {
                string text = _options[i].Label();
                var row = new Rect(Mathf.Floor(W / 2 - (panelW - 80f) / 2), y, panelW - 80f, optH - 6f);
                _optionRects.Add(row);
                float cx = row.x + row.width / 2, cy = row.y + row.height / 2;
                if (i == Selected)
                {
                    g.FillRoundedRect(row.x, row.y, row.width, row.height, 6f, TitleColor, 40f / 255f);
                    g.StrokeRoundedRect(row.x + 1, row.y + 1, row.width - 2, row.height - 2, 5f, 2f, TitleColor);
                    g.PixelText("> " + text + " <", cx, cy, 2f, Palette.White, 0.5f, 0.5f);
                }
                else
                {
                    g.PixelText(text, cx, cy, 2f, Palette.TextDim, 0.5f, 0.5f);
                }
                y += optH;
            }

            string hint = "↑↓ SCEGLI   INVIO CONFERMA" + (CancelValue != null ? "   ESC INDIETRO" : "");
            g.PixelText(hint, W / 2, py + panelH - 22f, 1f, Palette.TextDim, 0.5f, 0.5f);
        }
    }
}
