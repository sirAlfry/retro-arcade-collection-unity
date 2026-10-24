using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace RetroArcade
{
    /// <summary>
    /// L'UNICO MonoBehaviour del progetto: crea telecamere, render texture, canvas, input e audio, poi fa
    /// girare le scene (una alla volta) con le dissolvenze del launcher Python.
    ///
    /// Tasti globali (come <c>handle_global_event</c>): F11 o Alt+Invio schermo intero · F12 screenshot.
    /// Extra della versione Unity: F1 effetto CRT (Off/Soft/Full) · F2 audio · F3 contatore FPS.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-100)]
    [AddComponentMenu("Retro Arcade/Arcade App")]
    public sealed class ArcadeApp : MonoBehaviour, IArcadeHost
    {
        [Header("Rendering")]
        [Tooltip("Risoluzione interna = 800x600 × questo valore. Con 2 (consigliato) cerchi più morbidi e testo a scala 1.5 perfettamente nitido.")]
        [Range(1, 4)]
        [SerializeField] private int renderScale = 2;

        [Tooltip("Effetto monitor quando il filtro CRT è acceso (V in gioco, F1 per provare le varianti).")]
        [SerializeField] private CrtMode crtOnMode = CrtMode.Full;

        [Header("Avvio")]
        [Tooltip("Scena di partenza: utile per provare direttamente un gioco (es. TetrisScene).")]
        [SerializeField] private string startScene = SceneKeys.Launcher;

        [Tooltip("Contatore FPS in alto a destra nel launcher (F3 lo attiva/disattiva).")]
        [SerializeField] private bool showFps = true;

        [Tooltip("0 = partite casuali. Un numero fisso rende tutto riproducibile (comodo per il debug).")]
        [SerializeField] private int randomSeed = 0;

        public static ArcadeApp Instance { get; private set; }

        public RetroCanvas Canvas { get; private set; }
        public ArcadeInput Controls { get; private set; }
        public Sfx Sfx { get; private set; }
        public CrtPresenter Presenter { get; private set; }
        public System.Random Rng { get; private set; }
        public string CurrentSceneKey { get; private set; }

        public bool ShowFps => showFps;
        public float Fps { get; private set; }

        public bool CanQuit => !Application.isMobilePlatform && Application.platform != RuntimePlatform.WebGLPlayer;

        public bool SoundOn => !Sfx.Muted;
        public bool CrtOn => Presenter.Mode != CrtMode.Off;

        private enum Phase { Running, FadingOut }

        private ArcadeScene _scene;
        private string _pendingKey;
        private Phase _phase = Phase.Running;
        private float _fade = 1f;              // 0 = trasparente, 1 = coperto
        private uint _fadeColor = Palette.BgDark;
        private float _fadeOutMs = 200f, _fadeInMs = 250f;
        private Material _vectorMaterial;
        private Texture2D _atlasTexture;
        private string _toast;
        private float _toastMs;
        private float _fpsAccumMs;
        private int _fpsFrames;
        private bool _capturing;

        // ───────────────────────────── Ciclo di vita Unity ─────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Il root deve stare nell'origine: le due telecamere usano coordinate mondo fisse.
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;

#if !UNITY_WEBGL
            if (QualitySettings.vSyncCount == 0) Application.targetFrameRate = 60;
#endif
            Rng = randomSeed == 0 ? new System.Random() : new System.Random(randomSeed);

            var vectorShader = LoadShader("RetroArcade/RetroVector", "RetroArcade/Vector");
            var crtShader = LoadShader("RetroArcade/RetroCRT", "RetroArcade/CRT");

            // Impostazioni salvate in scores.json (audio, filtro CRT), come load_settings() in Python.
            bool sound = ScoreStore.GetSetting("sound", true);
            var crt = (CrtMode)Mathf.Clamp((int)ScoreStore.GetSetting("crt_mode", (int)crtOnMode), 0, 2);
            if (!ScoreStore.GetSetting("crt", true)) crt = CrtMode.Off;
            if (crt != CrtMode.Off) crtOnMode = crt;

            Presenter = new CrtPresenter(transform, renderScale, crt, crtShader);
            Canvas = new RetroCanvas(renderScale, QualitySettings.activeColorSpace == ColorSpace.Linear);
            Controls = new ArcadeInput();

            var canvasGo = new GameObject("RetroCanvas (mesh)");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.AddComponent<MeshFilter>().sharedMesh = Canvas.Mesh;
            var mr = canvasGo.AddComponent<MeshRenderer>();
            _atlasTexture = FontAtlas.CreateTexture();
            _vectorMaterial = new Material(vectorShader)
            {
                name = "RetroArcade_Vector",
                mainTexture = _atlasTexture,
                hideFlags = HideFlags.DontSave,
            };
            mr.sharedMaterial = _vectorMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            if (FindAnyObjectByType<AudioListener>() == null)
                Presenter.PresentCamera.gameObject.AddComponent<AudioListener>();
            Sfx = new Sfx(gameObject) { Muted = !sound };

            FirstRunWindowSize();
        }

        private void Start()
        {
            string key = SceneRegistry.Factories.ContainsKey(startScene) ? startScene : SceneKeys.Launcher;
            _pendingKey = key;
            SwapScene();
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime * 1000f, 50f); // ms, con tetto (MAX_DT = 0.05 s)

            Presenter.UpdateLayout();
            Controls.Enabled = true;
            Controls.Poll(dt, Presenter);
            HandleGlobalKeys();

            if (_phase == Phase.FadingOut)
            {
                Controls.Enabled = false; // niente doppi avvii durante la dissolvenza
                _fade += dt / _fadeOutMs;
                if (_fade >= 1f)
                {
                    _fade = 1f;
                    SwapScene();
                }
            }
            else if (_fade > 0f)
            {
                _fade = _fadeInMs <= 0f ? 0f : Mathf.Max(0f, _fade - dt / _fadeInMs);
            }

            try
            {
                _scene?.Tick(dt);
            }
            catch (Exception e)
            {
                OnSceneCrash(e);
            }

            if (_toastMs > 0f) _toastMs -= Time.unscaledDeltaTime * 1000f;
            _fpsAccumMs += Time.unscaledDeltaTime * 1000f;
            _fpsFrames++;
            if (_fpsAccumMs >= 500f)
            {
                Fps = _fpsFrames * 1000f / _fpsAccumMs;
                _fpsAccumMs = 0f;
                _fpsFrames = 0;
            }
        }

        private void LateUpdate()
        {
            Canvas.Begin();
            try
            {
                _scene?.Render(Canvas);
            }
            catch (Exception e)
            {
                Canvas.Begin(); // il frame era a metà: si riparte da uno pulito
                OnSceneCrash(e);
            }
            Canvas.Offset = Vector2.zero;
            DrawOverlays(Canvas);
            Canvas.End();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            _scene?.Shutdown();
            Presenter?.Dispose();
            if (_vectorMaterial != null) Destroy(_vectorMaterial);
            if (_atlasTexture != null) Destroy(_atlasTexture);
            if (Canvas != null) Destroy(Canvas.Mesh);
            Instance = null;
        }

        // ───────────────────────────── Gestione scene ─────────────────────────────

        /// <summary>Dissolvenza in nero (200 ms, come il fade a 12 frame del launcher) e cambio scena.</summary>
        public void StartScene(string key)
        {
            if (_phase == Phase.FadingOut) return;
            if (!SceneRegistry.Factories.ContainsKey(key))
            {
                Debug.LogError("[RetroArcade] Scena sconosciuta: " + key);
                return;
            }
            _pendingKey = key;
            _phase = Phase.FadingOut;
            _fadeColor = Palette.Black;
            // Ricominciare lo stesso gioco è immediato, come "restart" in Python.
            _fadeOutMs = key == CurrentSceneKey ? 1f : 200f;
        }

        private void SwapScene()
        {
            _scene?.Shutdown();
            _scene = null;

            var scene = SceneRegistry.Factories[_pendingKey]();
            scene.Key = _pendingKey;
            scene.Attach(this);
            bool same = _pendingKey == CurrentSceneKey;
            CurrentSceneKey = _pendingKey;
            _pendingKey = null;
            _scene = scene;
            scene.Create();

            _phase = Phase.Running;
            // Il launcher riappare dal blu notte (fade di 0,25 s); i giochi partono subito, come in Python.
            bool launcher = CurrentSceneKey == SceneKeys.Launcher;
            _fadeColor = Palette.BgDark;
            _fadeInMs = launcher ? 250f : 0f;
            if (same) _fade = 0f;
        }

        /// <summary>
        /// Un gioco che va in errore non deve trascinarsi dietro tutta la raccolta (come in Python):
        /// si registra l'errore e si torna al launcher, che mostra il messaggio "OPS!".
        /// </summary>
        private void OnSceneCrash(Exception e)
        {
            Debug.LogException(e);
            if (CurrentSceneKey == SceneKeys.Launcher) return;
            var info = Games.GameCatalog.ByKey(CurrentSceneKey);
            string msg = e.GetType().Name + ": " + e.Message;
            if (msg.Length > 58) msg = msg.Substring(0, 58);
            Games.LauncherScene.PendingError = new[] { (info != null ? info.Name : CurrentSceneKey) + " si è interrotto per un errore:", msg };
            _pendingKey = SceneKeys.Launcher;
            SwapScene();
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            if (CanQuit) Application.Quit();
#endif
        }

        // ───────────────────────────── Impostazioni ─────────────────────────────

        public void ToggleSound()
        {
            Sfx.Muted = !Sfx.Muted;
            ScoreStore.SetSetting("sound", !Sfx.Muted);
            if (!Sfx.Muted) Sfx.Play(SfxId.Select);
            Toast(Sfx.Muted ? "AUDIO OFF" : "AUDIO ON");
        }

        public void ToggleCrt()
        {
            Presenter.Mode = Presenter.Mode == CrtMode.Off ? crtOnMode : CrtMode.Off;
            ScoreStore.SetSetting("crt", Presenter.Mode != CrtMode.Off);
            Toast(Presenter.Mode != CrtMode.Off ? "FILTRO CRT ON" : "FILTRO CRT OFF");
        }

        /// <summary>F1 (extra Unity): Off → Soft → Full. La variante scelta diventa quella del "CRT ON".</summary>
        private void CycleCrt()
        {
            Presenter.Mode = (CrtMode)(((int)Presenter.Mode + 1) % 3);
            if (Presenter.Mode != CrtMode.Off)
            {
                crtOnMode = Presenter.Mode;
                ScoreStore.SetSetting("crt_mode", (int)crtOnMode);
            }
            ScoreStore.SetSetting("crt", Presenter.Mode != CrtMode.Off);
            Toast("CRT: " + Presenter.Mode.ToString().ToUpperInvariant());
        }

        public void Toast(string message, float seconds = 1.6f)
        {
            _toast = message;
            _toastMs = seconds * 1000f;
        }

        // ───────────────────────────── Tasti globali ─────────────────────────────

        private void HandleGlobalKeys()
        {
            var c = Controls;
            // Alt+Invio lo gestisce già Unity (Player Settings ▸ Allow Fullscreen Switch), F11 lo aggiungiamo noi.
            if (c.Pressed(ArcadeKey.F11)) ToggleFullscreen();
            if (c.Pressed(ArcadeKey.F12)) TakeScreenshot();
            if (c.Pressed(ArcadeKey.F1)) CycleCrt();
            if (c.Pressed(ArcadeKey.F2)) ToggleSound();
            if (c.Pressed(ArcadeKey.F3)) showFps = !showFps;
        }

        private void ToggleFullscreen()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                Screen.fullScreen = !Screen.fullScreen;
                return;
            }
            if (Screen.fullScreen)
            {
                Screen.fullScreenMode = FullScreenMode.Windowed;
            }
            else
            {
                var r = Screen.currentResolution;
                Screen.SetResolution(r.width, r.height, FullScreenMode.FullScreenWindow);
            }
        }

        /// <summary>
        /// La prima volta che il gioco parte (in finestra) la porta a una misura comoda: 4:3, circa l'80% dell'altezza
        /// dello schermo (es. 1120×840 su un monitor Full HD). Poi Unity ricorda la misura scelta dal giocatore.
        /// </summary>
        private void FirstRunWindowSize()
        {
            if (Application.isEditor || Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer) return;
            if (Screen.fullScreen || ScoreStore.GetSetting("window_init", false)) return;
            int deskH = Screen.currentResolution.height;
            if (deskH <= 0) return;
            int h = Mathf.Max(600, (int)(deskH * 0.8f) / 60 * 60);
            int w = h * 4 / 3;
            Screen.SetResolution(w, h, FullScreenMode.Windowed);
            ScoreStore.SetSetting("window_init", true);
        }

        // ───────────────────────────── Screenshot (F12) ─────────────────────────────

        /// <summary>Cartella degli screenshot: accanto a scores.json, come in Python.</summary>
        public static string ScreenshotsDir()
        {
            string scores = ScoreStore.Storage is JsonFileStorage ? ScoreStore.Storage.Location : ScoreStore.DefaultPath();
            return Path.Combine(Path.GetDirectoryName(Path.GetFullPath(scores)) ?? Application.persistentDataPath, "screenshots");
        }

        private void TakeScreenshot()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                Toast("SCREENSHOT NON DISPONIBILE");
                return;
            }
            if (!_capturing) StartCoroutine(CaptureRoutine());
        }

        /// <summary>Salva l'immagine di gioco (800×600 × renderScale, senza bande nere) a fine frame.</summary>
        private IEnumerator CaptureRoutine()
        {
            _capturing = true;
            yield return new WaitForEndOfFrame();
            try
            {
                var rt = Presenter.Target;
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                byte[] png = tex.EncodeToPNG();
                Destroy(tex);

                string dir = ScreenshotsDir();
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "retro_arcade_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".png");
                File.WriteAllBytes(path, png);
                Sfx.Play(SfxId.Blip);
                Toast("SCREENSHOT SALVATO");
                Debug.Log("[RetroArcade] Screenshot: " + path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RetroArcade] Screenshot non riuscito: " + e.Message);
                Toast("SCREENSHOT NON RIUSCITO");
            }
            _capturing = false;
        }

        // ───────────────────────────── Overlay globali ─────────────────────────────

        private void DrawOverlays(RetroCanvas g)
        {
            if (_fade > 0f) g.FillRect(0, 0, RetroCanvas.W, RetroCanvas.H, _fadeColor, _fade);
            DrawToast(g, _toast, _toastMs / 1000f);
        }

        /// <summary>Notifica in basso a destra (<c>_draw_toast</c>): testo giallo in un pannellino col bordo.</summary>
        public static void DrawToast(RetroCanvas g, string text, float remaining)
        {
            if (remaining <= 0f || string.IsNullOrEmpty(text)) return;
            float a = 220f / 255f * Mathf.Min(1f, remaining / 0.3f);
            var size = RetroCanvas.MeasurePixel(text, 2f);
            float bw = size.x + 18f, bh = size.y + 14f;
            float bx = RetroCanvas.W - 14f - size.x - 9f, by = RetroCanvas.H - 14f - size.y - 7f;
            g.FillRoundedRect(bx, by, bw, bh, 6f, Palette.BgDark, a);
            g.StrokeRoundedRect(bx + 1, by + 1, bw - 2, bh - 2, 5f, 2f, Palette.NeonYellow, a);
            g.PixelText(text, bx + bw / 2, by + bh / 2, 2f, Palette.NeonYellow, 0.5f, 0.5f, Mathf.Min(1f, a + 35f / 255f));
        }

        private static Shader LoadShader(string resourcePath, string shaderName)
        {
            var shader = Resources.Load<Shader>(resourcePath);
            if (shader == null) shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError($"[RetroArcade] Shader '{shaderName}' non trovato in Resources/{resourcePath}. Uso Sprites/Default come ripiego.");
                shader = Shader.Find("Sprites/Default");
            }
            return shader;
        }
    }

    /// <summary>
    /// Avvio automatico: se premi Play in QUALSIASI scena e non c'è un <see cref="ArcadeApp"/>, ne crea uno.
    /// Così il progetto parte anche se la scena generata non è ancora stata aperta.
    /// Per disattivarlo aggiungi il simbolo RETRO_ARCADE_NO_AUTOBOOT in Player Settings → Scripting Define Symbols.
    /// </summary>
    internal static class ArcadeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoStart()
        {
#if !RETRO_ARCADE_NO_AUTOBOOT
            if (UnityEngine.Object.FindAnyObjectByType<ArcadeApp>() != null) return;
            new GameObject("RetroArcade (auto)").AddComponent<ArcadeApp>();
#endif
        }
    }
}
