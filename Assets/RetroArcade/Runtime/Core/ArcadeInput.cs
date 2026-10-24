using System;
using UnityEngine;
#if RETRO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace RetroArcade
{
    /// <summary>I tasti usati dalla raccolta (gli stessi della versione Python, più F1/F2/F3 come extra).</summary>
    public enum ArcadeKey
    {
        Left, Right, Up, Down,
        W, A, S, D,
        Enter, Space, Escape, Tab, Shift, Ctrl, Alt,
        B, C, H, M, P, Q, R, T, V, X, Z,
        One, Two, Three, Four, Five, Six, Seven, Eight, Nine,
        F1, F2, F3, F11, F12,
    }

    /// <summary>
    /// Input unificato tastiera + mouse/touch.
    /// <list type="bullet">
    /// <item>Funziona con il NUOVO Input System (package <c>com.unity.inputsystem</c>, default nei template Unity 6)
    /// e, in alternativa, con il vecchio Input Manager: sceglie da solo in base a Player Settings → Active Input Handling.</item>
    /// <item>Converte la posizione del mouse nelle coordinate logiche 800×600, tenendo conto delle bande nere
    /// e perfino della curvatura dello schermo CRT.</item>
    /// <item>Implementa l'auto-repeat dei tasti tenuti premuti (es. tenere ← in Tetris).</item>
    /// </list>
    /// </summary>
    public sealed class ArcadeInput
    {
        private static readonly int KeyCount = Enum.GetValues(typeof(ArcadeKey)).Length;

        private readonly bool[] _down = new bool[KeyCount];
        private readonly bool[] _pressed = new bool[KeyCount];
        private readonly float[] _heldMs = new float[KeyCount];
        private float _lastDeltaMs = 16.67f;
        private Vector2 _lastScreen;
        private bool _hasLastScreen;

        /// <summary>Quando false (es. durante una dissolvenza) tutte le query rispondono "niente premuto".</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Posizione del puntatore in coordinate logiche (0..800, 0..600).</summary>
        public Vector2 Pointer { get; private set; }

        /// <summary>True se il puntatore è sopra l'immagine di gioco.</summary>
        public bool PointerInside { get; private set; }

        /// <summary>Il puntatore si è mosso in questo frame (equivale a <c>MOUSEMOTION</c>).</summary>
        public bool PointerMoved => Enabled && _pointerMoved;

        /// <summary>Click / tap iniziato in questo frame (equivale a <c>MOUSEBUTTONDOWN</c>).</summary>
        public bool PointerPressed => Enabled && _pointerPressed && PointerInside;

        /// <summary>Tasto sinistro (o dito) tenuto premuto.</summary>
        public bool PointerHeld => Enabled && _pointerHeld && PointerInside;

        private bool _pointerMoved, _pointerPressed, _pointerHeld;

        public bool Held(ArcadeKey k) => Enabled && _down[(int)k];

        /// <summary>
        /// Premuto in questo frame. Invio con Alt tenuto NON conta: Alt+Invio è lo schermo intero
        /// (come <c>handle_global_event</c> in Python, che "consuma" l'evento).
        /// </summary>
        public bool Pressed(ArcadeKey k)
        {
            if (!Enabled || !_pressed[(int)k]) return false;
            if (k == ArcadeKey.Enter && _down[(int)ArcadeKey.Alt]) return false;
            return true;
        }

        /// <summary>Uno qualsiasi dei tasti indicati è stato premuto in questo frame.</summary>
        public bool AnyPressed(params ArcadeKey[] keys)
        {
            foreach (var k in keys) if (Pressed(k)) return true;
            return false;
        }

        /// <summary>Uno qualsiasi dei tasti indicati è tenuto premuto.</summary>
        public bool AnyHeld(params ArcadeKey[] keys)
        {
            foreach (var k in keys) if (Held(k)) return true;
            return false;
        }

        /// <summary>Millisecondi da cui il tasto è tenuto premuto (0 se non è premuto).</summary>
        public float HeldMs(ArcadeKey k) => Enabled && _down[(int)k] ? _heldMs[(int)k] : 0f;

        /// <summary>
        /// Vero al momento della pressione e poi, se il tasto resta giù, ogni <paramref name="intervalMs"/>
        /// dopo un'attesa iniziale di <paramref name="delayMs"/> (come l'auto-repeat della tastiera).
        /// </summary>
        public bool Repeat(ArcadeKey k, float delayMs = 220f, float intervalMs = 50f)
        {
            if (!Enabled) return false;
            int i = (int)k;
            if (_pressed[i]) return true;
            if (!_down[i]) return false;
            float now = _heldMs[i], prev = now - _lastDeltaMs;
            if (now < delayMs) return false;
            if (prev < delayMs) return true;
            return Mathf.Floor((now - delayMs) / intervalMs) > Mathf.Floor((prev - delayMs) / intervalMs);
        }

        // ───────────── Iniezione di input: per test automatici e per tools/PreviewRenderer ─────────────

        /// <summary>Forza lo stato di un tasto (resta così fino al prossimo Poll o Inject).</summary>
        internal void Inject(ArcadeKey key, bool down, bool pressed)
        {
            int i = (int)key;
            _down[i] = down;
            _pressed[i] = pressed;
            if (pressed) _heldMs[i] = 0f;
        }

        /// <summary>Simula il puntatore in coordinate logiche (ed eventualmente un click).</summary>
        internal void InjectPointer(Vector2 virtualPos, bool pressed, bool moved = true)
        {
            Pointer = virtualPos;
            PointerInside = true;
            _pointerPressed = pressed;
            _pointerHeld = pressed;
            _pointerMoved = moved;
        }

        /// <summary>Azzera i "premuto in questo frame" (da chiamare dopo ogni tick simulato).</summary>
        internal void EndInjectedFrame(float deltaMs)
        {
            _lastDeltaMs = deltaMs;
            for (int i = 0; i < KeyCount; i++)
            {
                if (_down[i] && !_pressed[i]) _heldMs[i] += deltaMs;
                _pressed[i] = false;
            }
            _pointerPressed = false;
            _pointerMoved = false;
        }

        internal void Poll(float deltaMs, CrtPresenter presenter)
        {
            _lastDeltaMs = deltaMs;
            for (int i = 0; i < KeyCount; i++)
            {
                bool wasDown = _down[i];
                ReadKey((ArcadeKey)i, out bool isDown, out bool pressedThisFrame);
                _down[i] = isDown;
                _pressed[i] = pressedThisFrame || (isDown && !wasDown);
                _heldMs[i] = _pressed[i] ? 0f : (isDown ? _heldMs[i] + deltaMs : 0f);
            }

            ReadPointer(out Vector2 screen, out bool hasPointer, out bool click, out bool held);
            _pointerMoved = false;
            _pointerPressed = false;
            _pointerHeld = false;
            if (hasPointer)
            {
                if (_hasLastScreen && (screen - _lastScreen).sqrMagnitude > 0.25f) _pointerMoved = true;
                _lastScreen = screen;
                _hasLastScreen = true;
                if (presenter != null && presenter.ScreenToVirtual(screen, out Vector2 v))
                {
                    Pointer = v;
                    PointerInside = true;
                }
                else
                {
                    PointerInside = false;
                }
                _pointerPressed = click;
                _pointerHeld = held;
            }
            else
            {
                PointerInside = false;
            }
        }

        // ───────────────────────────── Backend: nuovo Input System ─────────────────────────────
#if RETRO_INPUT_SYSTEM && ENABLE_INPUT_SYSTEM
        private static readonly Key[][] Map = BuildMap();

        /// <summary>Tabella costruita per NOME del tasto: l'ordine dell'enum non conta più (niente tasti "sfasati").</summary>
        private static Key[][] BuildMap()
        {
            var map = new Key[KeyCount][];
            void Set(ArcadeKey k, params Key[] keys) => map[(int)k] = keys;
            Set(ArcadeKey.Left, Key.LeftArrow); Set(ArcadeKey.Right, Key.RightArrow);
            Set(ArcadeKey.Up, Key.UpArrow); Set(ArcadeKey.Down, Key.DownArrow);
            Set(ArcadeKey.W, Key.W); Set(ArcadeKey.A, Key.A); Set(ArcadeKey.S, Key.S); Set(ArcadeKey.D, Key.D);
            Set(ArcadeKey.Enter, Key.Enter, Key.NumpadEnter); Set(ArcadeKey.Space, Key.Space);
            Set(ArcadeKey.Escape, Key.Escape); Set(ArcadeKey.Tab, Key.Tab);
            Set(ArcadeKey.Shift, Key.LeftShift, Key.RightShift); Set(ArcadeKey.Ctrl, Key.LeftCtrl, Key.RightCtrl);
            Set(ArcadeKey.Alt, Key.LeftAlt, Key.RightAlt);
            Set(ArcadeKey.B, Key.B); Set(ArcadeKey.C, Key.C); Set(ArcadeKey.H, Key.H); Set(ArcadeKey.M, Key.M);
            Set(ArcadeKey.P, Key.P); Set(ArcadeKey.Q, Key.Q); Set(ArcadeKey.R, Key.R); Set(ArcadeKey.T, Key.T);
            Set(ArcadeKey.V, Key.V); Set(ArcadeKey.X, Key.X); Set(ArcadeKey.Z, Key.Z);
            Set(ArcadeKey.One, Key.Digit1, Key.Numpad1); Set(ArcadeKey.Two, Key.Digit2, Key.Numpad2);
            Set(ArcadeKey.Three, Key.Digit3, Key.Numpad3); Set(ArcadeKey.Four, Key.Digit4, Key.Numpad4);
            Set(ArcadeKey.Five, Key.Digit5, Key.Numpad5); Set(ArcadeKey.Six, Key.Digit6, Key.Numpad6);
            Set(ArcadeKey.Seven, Key.Digit7, Key.Numpad7); Set(ArcadeKey.Eight, Key.Digit8, Key.Numpad8);
            Set(ArcadeKey.Nine, Key.Digit9, Key.Numpad9);
            Set(ArcadeKey.F1, Key.F1); Set(ArcadeKey.F2, Key.F2); Set(ArcadeKey.F3, Key.F3);
            Set(ArcadeKey.F11, Key.F11); Set(ArcadeKey.F12, Key.F12);
            return map;
        }

        private static void ReadKey(ArcadeKey k, out bool down, out bool pressed)
        {
            down = pressed = false;
            var kb = Keyboard.current;
            var keys = Map[(int)k];
            if (kb == null || keys == null) return;
            foreach (var key in keys)
            {
                var ctrl = kb[key];
                down |= ctrl.isPressed;
                pressed |= ctrl.wasPressedThisFrame;
            }
        }

        private static void ReadPointer(out Vector2 screen, out bool has, out bool click, out bool held)
        {
            screen = default;
            has = click = held = false;
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.press.isPressed)
            {
                screen = touch.primaryTouch.position.ReadValue();
                has = true;
                click = touch.primaryTouch.press.wasPressedThisFrame;
                held = true;
                return;
            }
            var mouse = Mouse.current;
            if (mouse != null)
            {
                screen = mouse.position.ReadValue();
                has = true;
                click = mouse.leftButton.wasPressedThisFrame;
                held = mouse.leftButton.isPressed;
            }
        }

        // ───────────────────────────── Backend: vecchio Input Manager ─────────────────────────────
#elif ENABLE_LEGACY_INPUT_MANAGER
        private static readonly KeyCode[][] Map = BuildMap();

        private static KeyCode[][] BuildMap()
        {
            var map = new KeyCode[KeyCount][];
            void Set(ArcadeKey k, params KeyCode[] keys) => map[(int)k] = keys;
            Set(ArcadeKey.Left, KeyCode.LeftArrow); Set(ArcadeKey.Right, KeyCode.RightArrow);
            Set(ArcadeKey.Up, KeyCode.UpArrow); Set(ArcadeKey.Down, KeyCode.DownArrow);
            Set(ArcadeKey.W, KeyCode.W); Set(ArcadeKey.A, KeyCode.A); Set(ArcadeKey.S, KeyCode.S); Set(ArcadeKey.D, KeyCode.D);
            Set(ArcadeKey.Enter, KeyCode.Return, KeyCode.KeypadEnter); Set(ArcadeKey.Space, KeyCode.Space);
            Set(ArcadeKey.Escape, KeyCode.Escape); Set(ArcadeKey.Tab, KeyCode.Tab);
            Set(ArcadeKey.Shift, KeyCode.LeftShift, KeyCode.RightShift); Set(ArcadeKey.Ctrl, KeyCode.LeftControl, KeyCode.RightControl);
            Set(ArcadeKey.Alt, KeyCode.LeftAlt, KeyCode.RightAlt);
            Set(ArcadeKey.B, KeyCode.B); Set(ArcadeKey.C, KeyCode.C); Set(ArcadeKey.H, KeyCode.H); Set(ArcadeKey.M, KeyCode.M);
            Set(ArcadeKey.P, KeyCode.P); Set(ArcadeKey.Q, KeyCode.Q); Set(ArcadeKey.R, KeyCode.R); Set(ArcadeKey.T, KeyCode.T);
            Set(ArcadeKey.V, KeyCode.V); Set(ArcadeKey.X, KeyCode.X); Set(ArcadeKey.Z, KeyCode.Z);
            Set(ArcadeKey.One, KeyCode.Alpha1, KeyCode.Keypad1); Set(ArcadeKey.Two, KeyCode.Alpha2, KeyCode.Keypad2);
            Set(ArcadeKey.Three, KeyCode.Alpha3, KeyCode.Keypad3); Set(ArcadeKey.Four, KeyCode.Alpha4, KeyCode.Keypad4);
            Set(ArcadeKey.Five, KeyCode.Alpha5, KeyCode.Keypad5); Set(ArcadeKey.Six, KeyCode.Alpha6, KeyCode.Keypad6);
            Set(ArcadeKey.Seven, KeyCode.Alpha7, KeyCode.Keypad7); Set(ArcadeKey.Eight, KeyCode.Alpha8, KeyCode.Keypad8);
            Set(ArcadeKey.Nine, KeyCode.Alpha9, KeyCode.Keypad9);
            Set(ArcadeKey.F1, KeyCode.F1); Set(ArcadeKey.F2, KeyCode.F2); Set(ArcadeKey.F3, KeyCode.F3);
            Set(ArcadeKey.F11, KeyCode.F11); Set(ArcadeKey.F12, KeyCode.F12);
            return map;
        }

        private static void ReadKey(ArcadeKey k, out bool down, out bool pressed)
        {
            down = pressed = false;
            var keys = Map[(int)k];
            if (keys == null) return;
            foreach (var key in keys)
            {
                down |= Input.GetKey(key);
                pressed |= Input.GetKeyDown(key);
            }
        }

        private static void ReadPointer(out Vector2 screen, out bool has, out bool click, out bool held)
        {
            screen = Input.mousePosition; // i tocchi sono simulati come mouse di default
            has = Input.mousePresent || Input.touchCount > 0;
            click = Input.GetMouseButtonDown(0);
            held = Input.GetMouseButton(0);
        }

        // ───────────────────────────── Nessun backend disponibile ─────────────────────────────
#else
        private static bool _warned;

        private static void ReadKey(ArcadeKey k, out bool down, out bool pressed)
        {
            down = pressed = false;
            if (_warned) return;
            _warned = true;
            Debug.LogError("[RetroArcade] Nessun sistema di input attivo: installa il package Input System " +
                           "oppure abilita 'Input Manager (Old)' in Project Settings → Player → Active Input Handling.");
        }

        private static void ReadPointer(out Vector2 screen, out bool has, out bool click, out bool held)
        {
            screen = default;
            has = click = held = false;
        }
#endif
    }
}
