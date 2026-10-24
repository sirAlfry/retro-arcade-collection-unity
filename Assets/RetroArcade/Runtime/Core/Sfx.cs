using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetroArcade
{
    /// <summary>Gli effetti sonori: gli stessi nomi (e gli stessi suoni) di <c>sfx.RECIPES</c> della versione Python.</summary>
    public enum SfxId
    {
        // UI
        Select, Move, Back, Launch, Pause,
        // eventi generici
        Blip, Hit, Wall, Score, Bonus, Powerup, Hurt, Explosion, SmallExplosion, Shoot, EnemyShoot, Ufo,
        Win, Lose, Record, Error,
        // specifici dei giochi
        Eat, Rotate, Drop, Line, Tetris, Levelup, Flip, Match, Brick, Slide,
        Step, Step2, Step3, Step4, PlaceX, PlaceO,
    }

    /// <summary>
    /// Sintetizzatore chiptune: porting 1:1 di <c>sfx.py</c>. Ogni suono è una "ricetta" (onda, frequenza,
    /// glissando, inviluppo) trasformata in campioni all'avvio, come facevano i chip sonori delle sale giochi
    /// anni '80. Nessun file audio nel progetto.
    /// <para>Se l'audio non è disponibile ogni chiamata diventa silenziosamente una no-op.</para>
    /// </summary>
    public sealed class Sfx
    {
        /// <summary>Stessa frequenza del mixer pygame (22050 Hz): il rumore "sample &amp; hold" suona identico.</summary>
        public const int Rate = 22050;
        public const float MasterVolume = 0.35f;

        private readonly AudioSource _source;
        private readonly AudioClip[] _clips;

        public bool Muted { get; set; }

        /// <summary>Istanza muta, senza AudioSource: per test e anteprime fuori da Unity.</summary>
        public static Sfx CreateSilent() => new Sfx();

        private Sfx()
        {
            _clips = new AudioClip[Enum.GetValues(typeof(SfxId)).Length];
        }

        internal Sfx(GameObject host)
        {
            _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = 1f; // il volume generale è già nei campioni (MasterVolume), come in Python

            _clips = new AudioClip[Enum.GetValues(typeof(SfxId)).Length];
            try
            {
                foreach (var pair in Recipes.All)
                {
                    float[] data = Synthesize(pair.Value);
                    var clip = AudioClip.Create("sfx_" + pair.Key, data.Length, 1, Rate, false);
                    clip.SetData(data, 0);
                    _clips[(int)pair.Key] = clip;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[RetroArcade] Impossibile generare l'audio procedurale: " + e.Message);
            }
        }

        /// <summary>Come <c>sfx.play(name, volume)</c>.</summary>
        public void Play(SfxId id, float volume = 1f)
        {
            // ReferenceEquals evita chiamate native con l'istanza muta; !_source copre un AudioSource distrutto.
            if (Muted || ReferenceEquals(_source, null) || !_source) return;
            var clip = _clips[(int)id];
            if (clip != null) _source.PlayOneShot(clip, Mathf.Clamp01(volume));
        }

        // ───────────────────────────── Ricette (sfx.RECIPES) ─────────────────────────────

        public enum Wave { Square, Triangle, Saw, Noise }

        /// <summary>Una nota sintetizzata (dataclass <c>Tone</c> di Python).</summary>
        public readonly struct Tone
        {
            public readonly float Freq, Dur, Vol, Duty, Attack, Vibrato;
            public readonly float? EndFreq;
            public readonly Wave Wave;

            public Tone(float freq, float dur, Wave wave = Wave.Square, float? endFreq = null, float vol = 1f,
                        float duty = 0.5f, float attack = 0.004f, float vibrato = 0f)
            {
                Freq = freq; Dur = dur; Wave = wave; EndFreq = endFreq; Vol = vol; Duty = duty; Attack = attack; Vibrato = vibrato;
            }
        }

        /// <summary>Nome di nota ("C5", "F#4") → frequenza in Hz.</summary>
        public static float Note(string name)
        {
            string pitch = name.Substring(0, name.Length - 1);
            int octave = name[name.Length - 1] - '0';
            int semis;
            switch (pitch)
            {
                case "C": semis = -9; break; case "C#": semis = -8; break; case "D": semis = -7; break;
                case "D#": semis = -6; break; case "E": semis = -5; break; case "F": semis = -4; break;
                case "F#": semis = -3; break; case "G": semis = -2; break; case "G#": semis = -1; break;
                case "A": semis = 0; break; case "A#": semis = 1; break; case "B": semis = 2; break;
                default: throw new ArgumentException("Nota sconosciuta: " + name);
            }
            semis += (octave - 4) * 12;
            return 440f * Mathf.Pow(2f, semis / 12f);
        }

        private static Tone[] Arp(string notes, float step, Wave wave = Wave.Square, float vol = 0.8f, float duty = 0.5f)
        {
            var parts = notes.Split(' ');
            var tones = new Tone[parts.Length];
            for (int i = 0; i < parts.Length; i++) tones[i] = new Tone(Note(parts[i]), step, wave, vol: vol, duty: duty);
            return tones;
        }

        private static Tone[] Join(Tone[] a, params Tone[] b)
        {
            var r = new Tone[a.Length + b.Length];
            a.CopyTo(r, 0);
            b.CopyTo(r, a.Length);
            return r;
        }

        public static class Recipes
        {
            public static readonly Dictionary<SfxId, Tone[]> All = new Dictionary<SfxId, Tone[]>
            {
                // --- UI ---
                [SfxId.Select] = new[] { new Tone(660, 0.04f, duty: 0.25f, vol: 0.6f), new Tone(990, 0.05f, duty: 0.25f, vol: 0.6f) },
                [SfxId.Move] = new[] { new Tone(520, 0.025f, duty: 0.25f, vol: 0.35f) },
                [SfxId.Back] = new[] { new Tone(500, 0.04f, duty: 0.25f, vol: 0.5f), new Tone(330, 0.06f, duty: 0.25f, vol: 0.5f) },
                [SfxId.Launch] = Arp("C5 E5 G5 C6", 0.055f, duty: 0.25f),
                [SfxId.Pause] = new[] { new Tone(880, 0.05f, Wave.Triangle), new Tone(660, 0.07f, Wave.Triangle) },
                // --- eventi generici ---
                [SfxId.Blip] = new[] { new Tone(880, 0.03f, duty: 0.25f, vol: 0.5f) },
                [SfxId.Hit] = new[] { new Tone(520, 0.045f, duty: 0.5f, vol: 0.7f) },
                [SfxId.Wall] = new[] { new Tone(260, 0.04f, duty: 0.5f, vol: 0.55f) },
                [SfxId.Score] = new[] { new Tone(660, 0.07f, endFreq: 1320, duty: 0.25f, vol: 0.7f) },
                [SfxId.Bonus] = Arp("E5 G5 B5 E6", 0.05f, duty: 0.125f, vol: 0.7f),
                [SfxId.Powerup] = new[] { new Tone(400, 0.25f, endFreq: 1600, duty: 0.25f, vol: 0.6f, vibrato: 25) },
                [SfxId.Hurt] = new[] { new Tone(420, 0.28f, endFreq: 90, duty: 0.5f, vol: 0.8f) },
                [SfxId.Explosion] = new[] { new Tone(1800, 0.35f, Wave.Noise, endFreq: 120, vol: 0.9f) },
                [SfxId.SmallExplosion] = new[] { new Tone(2400, 0.12f, Wave.Noise, endFreq: 500, vol: 0.6f) },
                [SfxId.Shoot] = new[] { new Tone(1400, 0.11f, endFreq: 350, duty: 0.25f, vol: 0.45f) },
                [SfxId.EnemyShoot] = new[] { new Tone(700, 0.09f, endFreq: 250, duty: 0.125f, vol: 0.3f) },
                [SfxId.Ufo] = new[] { new Tone(900, 0.4f, Wave.Triangle, vol: 0.45f, vibrato: 120) },
                [SfxId.Win] = Arp("C5 E5 G5 C6 G5 C6", 0.09f, duty: 0.25f),
                [SfxId.Lose] = Join(Arp("G4 E4 C4", 0.14f, duty: 0.5f), new Tone(Note("C3"), 0.3f, Wave.Triangle)),
                [SfxId.Record] = Arp("G5 C6 E6 G6 E6 G6", 0.07f, duty: 0.125f),
                [SfxId.Error] = new[] { new Tone(150, 0.12f, duty: 0.5f, vol: 0.6f), new Tone(120, 0.12f, duty: 0.5f, vol: 0.6f) },
                // --- specifici dei giochi ---
                [SfxId.Eat] = new[] { new Tone(600, 0.06f, endFreq: 1100, duty: 0.25f, vol: 0.6f) },
                [SfxId.Rotate] = new[] { new Tone(700, 0.03f, Wave.Triangle, vol: 0.5f) },
                [SfxId.Drop] = new[] { new Tone(3000, 0.06f, Wave.Noise, endFreq: 800, vol: 0.5f) },
                [SfxId.Line] = Arp("C5 G5 C6", 0.05f, duty: 0.25f),
                [SfxId.Tetris] = Arp("C5 E5 G5 C6 E6 G6", 0.05f, duty: 0.125f),
                [SfxId.Levelup] = Arp("G4 C5 E5 G5", 0.07f, duty: 0.25f),
                [SfxId.Flip] = new[] { new Tone(480, 0.04f, Wave.Triangle, endFreq: 620, vol: 0.5f) },
                [SfxId.Match] = Arp("E5 B5", 0.07f, Wave.Triangle),
                [SfxId.Brick] = new[] { new Tone(780, 0.04f, duty: 0.25f, vol: 0.5f) },
                [SfxId.Slide] = new[] { new Tone(300, 0.04f, Wave.Triangle, endFreq: 200, vol: 0.5f) },
                [SfxId.Step] = new[] { new Tone(Note("A2"), 0.07f, Wave.Triangle, vol: 0.8f) },
                [SfxId.Step2] = new[] { new Tone(Note("G2"), 0.07f, Wave.Triangle, vol: 0.8f) },
                [SfxId.Step3] = new[] { new Tone(Note("F2"), 0.07f, Wave.Triangle, vol: 0.8f) },
                [SfxId.Step4] = new[] { new Tone(Note("E2"), 0.07f, Wave.Triangle, vol: 0.8f) },
                [SfxId.PlaceX] = new[] { new Tone(500, 0.05f, duty: 0.25f, vol: 0.6f) },
                [SfxId.PlaceO] = new[] { new Tone(380, 0.05f, Wave.Triangle, vol: 0.7f) },
            };
        }

        /// <summary>
        /// Trasforma una sequenza di note in campioni mono (-1..1). Funzione pura, identica a
        /// <c>sfx.synthesize</c>: glissando lineare, vibrato a 7 Hz, rumore "sample &amp; hold" la cui
        /// velocità segue la frequenza, inviluppo con attacco lineare e decadimento lineare a zero.
        /// </summary>
        public static float[] Synthesize(Tone[] tones, int rate = Rate, int seed = 1234)
        {
            var rng = new System.Random(seed);
            var output = new List<float>(rate / 2);
            foreach (var tone in tones)
            {
                int n = Math.Max(1, (int)(tone.Dur * rate));
                int attackN = Math.Max(1, (int)(tone.Attack * rate));
                double phase = 0.0, noiseCounter = 0.0;
                float noiseVal = 0f;
                float end = tone.EndFreq ?? tone.Freq;
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / n;
                    double freq = tone.Freq + (end - tone.Freq) * t;
                    if (tone.Vibrato != 0f) freq += tone.Vibrato * Math.Sin(2 * Math.PI * 7 * i / rate);
                    phase = (phase + freq / rate) % 1.0;
                    if (phase < 0) phase += 1.0;
                    float s;
                    switch (tone.Wave)
                    {
                        case Wave.Square: s = phase < tone.Duty ? 1f : -1f; break;
                        case Wave.Triangle: s = (float)(4.0 * Math.Abs(phase - 0.5) - 1.0); break;
                        case Wave.Saw: s = (float)(2.0 * phase - 1.0); break;
                        default:
                            noiseCounter += freq / rate;
                            if (noiseCounter >= 1.0)
                            {
                                noiseCounter -= 1.0;
                                noiseVal = (float)(rng.NextDouble() * 2.0 - 1.0);
                            }
                            s = noiseVal;
                            break;
                    }
                    float env = i < attackN ? (float)i / attackN : 1f - (float)(i - attackN) / Math.Max(1, n - attackN);
                    output.Add(s * env * tone.Vol * MasterVolume);
                }
            }
            return output.ToArray();
        }
    }
}
