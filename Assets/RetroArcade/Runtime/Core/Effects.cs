using System;
using System.Collections.Generic;
using UnityEngine;

namespace RetroArcade
{
    /// <summary>
    /// Sistema di particelle minimale (scintille, detriti, coriandoli): porting di <c>Particles</c>.
    /// Velocità in pixel al secondo, vita in secondi, disegnate come quadratini che si rimpiccioliscono.
    /// </summary>
    public sealed class Particles
    {
        private struct P
        {
            public float X, Y, Vx, Vy, Life, MaxLife, Size, Gravity;
            public uint Color;
        }

        private readonly List<P> _items = new List<P>(256);
        private readonly System.Random _rng;
        public int MaxParticles = 600;

        public Particles(System.Random rng) { _rng = rng; }

        public int Count => _items.Count;

        private float U(float a, float b) => a + (b - a) * (float)_rng.NextDouble();

        public void Burst(float x, float y, uint color, int count = 14, float speedMin = 60f, float speedMax = 240f,
                          float lifeMin = 0.3f, float lifeMax = 0.8f, int sizeMin = 2, int sizeMax = 4,
                          float gravity = 0f, float angle = 0f, float spread = Mathf.PI * 2f) =>
            Burst(x, y, new[] { color }, count, speedMin, speedMax, lifeMin, lifeMax, sizeMin, sizeMax, gravity, angle, spread);

        public void Burst(float x, float y, uint[] colors, int count = 14, float speedMin = 60f, float speedMax = 240f,
                          float lifeMin = 0.3f, float lifeMax = 0.8f, int sizeMin = 2, int sizeMax = 4,
                          float gravity = 0f, float angle = 0f, float spread = Mathf.PI * 2f)
        {
            for (int i = 0; i < count; i++)
            {
                if (_items.Count >= MaxParticles) _items.RemoveAt(0);
                float a = angle + U(-spread / 2f, spread / 2f);
                float v = U(speedMin, speedMax);
                float lt = U(lifeMin, lifeMax);
                _items.Add(new P
                {
                    X = x, Y = y, Vx = Mathf.Cos(a) * v, Vy = Mathf.Sin(a) * v, Life = lt, MaxLife = lt,
                    Color = colors[_rng.Next(colors.Length)], Size = _rng.Next(sizeMin, sizeMax + 1), Gravity = gravity,
                });
            }
        }

        /// <param name="dt">Secondi.</param>
        public void Update(float dt)
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                var p = _items[i];
                p.Life -= dt;
                if (p.Life <= 0f) { _items.RemoveAt(i); continue; }
                p.Vy += p.Gravity * dt;
                p.X += p.Vx * dt;
                p.Y += p.Vy * dt;
                _items[i] = p;
            }
        }

        public void Draw(RetroCanvas g, float ox = 0f, float oy = 0f)
        {
            foreach (var p in _items)
            {
                float s = Mathf.Max(1, (int)(p.Size * (p.Life / p.MaxLife) + 0.5f));
                g.FillRect((int)p.X - (int)(s / 2) + ox, (int)p.Y - (int)(s / 2) + oy, s, s, p.Color);
            }
        }

        public void Clear() => _items.Clear();
    }

    /// <summary>Punteggi a comparsa ("+10") che salgono e svaniscono (<c>FloatingTexts</c>).</summary>
    public sealed class FloatingTexts
    {
        private sealed class Item
        {
            public string Text;
            public float X, Y, Life, MaxLife, Scale;
            public uint Color;
        }

        private readonly List<Item> _items = new List<Item>();

        public void Add(string text, float x, float y, uint color = Palette.NeonYellow, float life = 0.8f, float scale = 2f) =>
            _items.Add(new Item { Text = text, X = x, Y = y, Life = life, MaxLife = life, Scale = scale, Color = color });

        public void Update(float dt)
        {
            foreach (var i in _items)
            {
                i.Y -= 40f * dt;
                i.Life -= dt;
            }
            _items.RemoveAll(i => i.Life <= 0f);
        }

        public void Draw(RetroCanvas g)
        {
            foreach (var i in _items)
            {
                float a = Mathf.Min(1f, i.Life / i.MaxLife * 1.5f);
                g.PixelText(i.Text, (int)i.X, (int)i.Y, i.Scale, i.Color, 0.5f, 0.5f, a, shadow: Palette.Black);
            }
        }

        public void Clear() => _items.Clear();
    }

    /// <summary>Tremolio dello schermo "a trauma": <c>Add(0.4f)</c> all'impatto, <see cref="Offset"/> ogni frame.</summary>
    public sealed class ScreenShake
    {
        private readonly System.Random _rng;
        public float Trauma;
        public float MaxOffset;

        public ScreenShake(System.Random rng, float maxOffset = 9f)
        {
            _rng = rng;
            MaxOffset = maxOffset;
        }

        public void Add(float amount) => Trauma = Mathf.Min(1f, Trauma + amount);

        /// <param name="dt">Secondi.</param>
        public void Update(float dt) => Trauma = Mathf.Max(0f, Trauma - dt * 1.8f);

        /// <summary>Spostamento da applicare al frame (0,0 quando il tremolio è finito).</summary>
        public Vector2 Offset()
        {
            if (Trauma <= 0.01f) return Vector2.zero;
            float mag = MaxOffset * Trauma * Trauma;
            return new Vector2((int)(((float)_rng.NextDouble() * 2f - 1f) * mag), (int)(((float)_rng.NextDouble() * 2f - 1f) * mag));
        }
    }

    /// <summary>Cronometro guidato dai frame: le pause sono escluse automaticamente (<c>Stopwatch</c>).</summary>
    public sealed class Stopwatch
    {
        public double Elapsed;
        public bool Running = true;

        /// <param name="dt">Secondi.</param>
        public void Update(float dt)
        {
            if (Running) Elapsed += dt;
        }

        public void Stop() => Running = false;

        public void Reset()
        {
            Elapsed = 0;
            Running = true;
        }

        public override string ToString() => Ui.FormatTime(Elapsed);
    }
}
