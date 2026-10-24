namespace RetroArcade
{
    /// <summary>
    /// Ciò che una scena può chiedere all'applicazione. In gioco lo implementa <see cref="ArcadeApp"/>;
    /// fuori da Unity (anteprime, test) lo implementa un host finto.
    /// </summary>
    public interface IArcadeHost
    {
        ArcadeInput Controls { get; }
        Sfx Sfx { get; }
        System.Random Rng { get; }

        /// <summary>Su Web e mobile "uscire" non ha senso: lì l'opzione "Esci" non compare.</summary>
        bool CanQuit { get; }

        /// <summary>Frame al secondo misurati (per il contatore del launcher).</summary>
        float Fps { get; }
        bool ShowFps { get; }

        bool SoundOn { get; }
        bool CrtOn { get; }
        /// <summary>Audio on/off: salva la scelta, mostra la notifica e (se acceso) suona "select".</summary>
        void ToggleSound();
        /// <summary>Filtro CRT on/off, salvato nelle impostazioni.</summary>
        void ToggleCrt();

        /// <summary>Messaggio a comparsa in basso a destra (come <c>notify</c> in Python).</summary>
        void Toast(string message, float seconds = 1.6f);

        void StartScene(string key);
        void Quit();
    }
}
