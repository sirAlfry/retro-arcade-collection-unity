using System;
using UnityEngine;

namespace RetroArcade
{
    /// <summary>
    /// Base di ogni schermata (launcher e giochi). Ogni gioco è una classe C# "normale" (non un MonoBehaviour):
    /// la crea <see cref="ArcadeApp"/> quando la si avvia e la butta quando si torna al menu.
    /// La scena parla con l'app solo tramite <see cref="IArcadeHost"/>: così gira anche fuori da Unity
    /// (lo usa tools/PreviewRenderer per gli screenshot e il "fuzz").
    ///
    /// Ciclo di vita:
    /// <list type="bullet">
    /// <item><see cref="Create"/>: preparazione (equivale all'inizio di <c>main()</c> in Python).</item>
    /// <item><see cref="Update"/>: logica, una volta per frame (tempi in millisecondi).</item>
    /// <item><see cref="Draw"/>: si ridisegna tutto ogni frame (immediate mode).</item>
    /// </list>
    /// I menu (pausa, fine partita, scelta modalità) sono sovrapposti alla scena: mentre uno è aperto
    /// <see cref="Update"/> non viene chiamato, ma la scena continua a essere disegnata sotto.
    /// </summary>
    public abstract class ArcadeScene
    {
        public const float W = RetroCanvas.W;
        public const float H = RetroCanvas.H;

        protected IArcadeHost App { get; private set; }
        protected ArcadeInput In => App.Controls;
        protected Sfx Sfx => App.Sfx;

        /// <summary>Orologio della scena (timer ed eventi ritardati).</summary>
        protected GameClock Clock { get; private set; }

        /// <summary>La chiave con cui la scena è registrata (serve per ricominciare da capo).</summary>
        public string Key { get; internal set; }

        /// <summary>Il menu aperto in questo momento (null = si gioca).</summary>
        protected MenuOverlay Menu { get; private set; }

        public bool MenuOpen => Menu != null;

        internal void Attach(IArcadeHost app)
        {
            App = app;
            Clock = new GameClock();
        }

        internal void Tick(float deltaMs)
        {
            if (Menu != null)
            {
                Menu.Update(App, deltaMs);
                return;
            }
            Clock.Tick(deltaMs);
            Update((float)Clock.Now, deltaMs);
        }

        /// <summary>Disegna la scena e, sopra, l'eventuale menu.</summary>
        internal void Render(RetroCanvas g)
        {
            Draw(g);
            Menu?.Draw(g);
        }

        public virtual void Create() { }
        public virtual void Update(float timeMs, float deltaMs) { }
        public abstract void Draw(RetroCanvas g);
        public virtual void Shutdown() { }

        // ───────────────────────────── Navigazione ─────────────────────────────

        /// <summary>Torna al launcher (dissolvenza gestita da <see cref="ArcadeApp"/>).</summary>
        protected void ReturnToMenu()
        {
            Menu = null;
            App.StartScene(SceneKeys.Launcher);
        }

        /// <summary>"Ricomincia": per default ricrea la scena da zero. I giochi possono ridefinirlo.</summary>
        protected virtual void Restart() => App.StartScene(Key);

        /// <summary>Cosa fare con le scelte standard dei menu: "restart", "menu", "quit" (e "resume").</summary>
        protected virtual void HandleMenuResult(string result)
        {
            switch (result)
            {
                case "restart": Restart(); break;
                case "menu": ReturnToMenu(); break;
                case "quit": App.Quit(); break;
            }
        }

        // ───────────────────────────── Menu standard (game_shared.py) ─────────────────────────────

        /// <summary>Apre un menu qualsiasi; <paramref name="onChoice"/> riceve il valore scelto.</summary>
        protected MenuOverlay OpenMenu(string title, Action<string> onChoice)
        {
            Menu = new MenuOverlay(title, value =>
            {
                Menu = null;
                onChoice?.Invoke(value);
            });
            return Menu;
        }

        protected void CloseMenu() => Menu = null;

        /// <summary>
        /// Menu di pausa (<c>in_game_menu</c>): Riprendi · Ricomincia · Audio · Filtro CRT · Torna al menu · Esci.
        /// Scorciatoie: ESC/P = riprendi, R = ricomincia, M = menu, Q = esci.
        /// </summary>
        protected void OpenPauseMenu(string gameTitle, params string[] commands)
        {
            Sfx.Play(SfxId.Pause);
            var m = OpenMenu("PAUSA", r => { OnResume(); HandleMenuResult(r); });
            m.Subtitle = gameTitle.ToUpperInvariant();
            m.TitleColor = Palette.NeonCyan;
            m.CancelValue = "resume";
            m.InfoLines.AddRange(commands);
            m.AddOption("RIPRENDI", "resume")
             .AddOption("RICOMINCIA", "restart")
             .AddOption(() => "AUDIO: " + (App.SoundOn ? "ON" : "OFF"), MenuOverlay.ToggleSound)
             .AddOption(() => "FILTRO CRT: " + (App.CrtOn ? "ON" : "OFF"), MenuOverlay.ToggleCrt)
             .AddOption("TORNA AL MENU", "menu");
            if (App.CanQuit) m.AddOption("ESCI DAL GIOCO", "quit");
            m.Hotkey(ArcadeKey.R, "restart").Hotkey(ArcadeKey.M, "menu").Hotkey(ArcadeKey.P, "resume");
            if (App.CanQuit) m.Hotkey(ArcadeKey.Q, "quit");
        }

        /// <summary>Chiamato quando il menu di pausa si chiude (qualunque sia la scelta).</summary>
        protected virtual void OnResume() { }

        /// <summary>
        /// Schermata di fine partita (<c>end_screen</c>) sopra l'ultimo frame:
        /// Gioca ancora · Torna al menu · Esci. ESC = torna al menu.
        /// </summary>
        protected void OpenEndScreen(string title, string[] lines, bool newRecord = false, bool win = false)
        {
            Sfx.Play(newRecord ? SfxId.Record : (win ? SfxId.Win : SfxId.Lose));
            var m = OpenMenu(title, HandleMenuResult);
            m.CancelValue = "menu";
            m.TitleColor = win ? Palette.NeonGreen : Palette.NeonMagenta;
            if (newRecord) m.Subtitle = "★ NUOVO RECORD! ★";
            m.InputDelayMs = 600f;
            if (lines != null) m.InfoLines.AddRange(lines);
            m.AddOption("GIOCA ANCORA", "restart").AddOption("TORNA AL MENU", "menu");
            if (App.CanQuit) m.AddOption("ESCI DAL GIOCO", "quit");
            m.Hotkey(ArcadeKey.R, "restart").Hotkey(ArcadeKey.M, "menu");
            if (App.CanQuit) m.Hotkey(ArcadeKey.Q, "quit");
        }

        /// <summary>
        /// Scelta prima di giocare (<c>select_menu</c>: modalità, difficoltà...) su sfondo pieno.
        /// <paramref name="onPick"/> riceve l'indice scelto; ESC torna al launcher.
        /// </summary>
        protected void OpenSelectMenu(string title, string[] options, string[] info, int defaultIndex,
                                      uint titleColor, Action<int> onPick)
        {
            var m = OpenMenu(title, r =>
            {
                if (r == "menu") { ReturnToMenu(); return; }
                if (r == "quit") { App.Quit(); return; }
                onPick(int.Parse(r));
            });
            m.CancelValue = "menu";
            m.TitleColor = titleColor;
            m.SolidBackdrop = true;
            m.Dim = false;
            if (info != null) m.InfoLines.AddRange(info);
            for (int i = 0; i < options.Length; i++) m.AddOption(options[i], i.ToString());
            m.Selected = Mathf.Clamp(defaultIndex, 0, options.Length - 1);
        }

        /// <summary>Messaggio con un solo pulsante (<c>message_box</c>).</summary>
        protected void OpenMessage(string title, string[] lines, Action onOk, string button = "OK",
                                   uint titleColor = Palette.NeonOrange)
        {
            var m = OpenMenu(title, _ => onOk?.Invoke());
            m.CancelValue = "ok";
            m.TitleColor = titleColor;
            if (lines != null) m.InfoLines.AddRange(lines);
            m.AddOption(button, "ok");
        }

        // ───────────────────────────── Helper comuni ─────────────────────────────

        /// <summary><c>random.random()</c></summary>
        protected float Rand() => (float)App.Rng.NextDouble();

        /// <summary><c>random.uniform(a, b)</c></summary>
        protected float Uniform(float a, float b) => a + (b - a) * (float)App.Rng.NextDouble();

        /// <summary><c>random.randrange(n)</c></summary>
        protected int RandInt(int n) => App.Rng.Next(n);

        /// <summary><c>random.randint(a, b)</c> (estremi inclusi)</summary>
        protected int RandInt(int a, int b) => App.Rng.Next(a, b + 1);

        /// <summary>I tasti "apri pausa" di tutti i giochi Python: ESC o P.</summary>
        protected bool PausePressed() => In.AnyPressed(ArcadeKey.Escape, ArcadeKey.P);
    }
}
