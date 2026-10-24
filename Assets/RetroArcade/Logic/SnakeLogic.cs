using System;
using System.Collections.Generic;

namespace RetroArcade.Logic
{
    /// <summary>
    /// Stato e regole di Snake, senza grafica (porting di <c>SnakeLogic</c> di <c>snake.py</c>).
    /// <list type="bullet">
    /// <item>Mela rossa: +2 punti, sempre in campo.</item>
    /// <item>Frutto blu: +5 punti, compare per qualche secondo e poi torna dopo una pausa.</item>
    /// <item>Velocità: 8 passi/s + 1 ogni 5 punti (massimo 20).</item>
    /// <item>Buffer dei comandi (fino a 3): le curve rapide "su poi sinistra" non vanno perse.</item>
    /// <item>Il gioco aspetta la prima freccia; muri e corpo sono letali (ma si può inseguire la coda).</item>
    /// </list>
    /// </summary>
    public sealed class SnakeLogic
    {
        public const int BaseSpeed = 8, MaxSpeed = 20;
        public const float BonusLifetime = 7f, BonusCooldownMin = 4f, BonusCooldownMax = 8f;
        public const int RedPoints = 2, BluePoints = 5;
        public const int QueueSize = 3;

        public enum EventKind { Eat, Bonus, Die }

        private readonly Random _rng;
        public readonly int Cols, Rows;
        public List<Int2> Body;
        public Int2 Direction = Int2.Right;
        public readonly List<Int2> Queue = new List<Int2>(QueueSize);
        public bool Alive = true;
        public bool Started;
        public int Score;
        public Int2 Red;
        public Int2? Blue;
        public float BlueTimer;
        public float BlueCooldown;
        public float StepTimer;
        private readonly List<(EventKind, Int2)> _events = new List<(EventKind, Int2)>();

        public SnakeLogic(Random rng, int cols, int rows)
        {
            _rng = rng ?? new Random();
            Cols = cols;
            Rows = rows;
            int cx = cols / 2, cy = rows / 2;
            Body = new List<Int2> { new Int2(cx, cy), new Int2(cx - 1, cy), new Int2(cx - 2, cy) };
            Red = FreeCell(includeRed: false);
            BlueCooldown = Uniform(BonusCooldownMin, BonusCooldownMax);
        }

        private float Uniform(float a, float b) => a + (b - a) * (float)_rng.NextDouble();

        /// <summary>Una casella libera a caso (né serpente né cibo).</summary>
        public Int2 FreeCell() => FreeCell(includeRed: true);

        private Int2 FreeCell(bool includeRed)
        {
            var occupied = new HashSet<Int2>(Body);
            if (includeRed) occupied.Add(Red);
            if (Blue.HasValue) occupied.Add(Blue.Value);
            var free = new List<Int2>();
            for (int x = 0; x < Cols; x++)
                for (int y = 0; y < Rows; y++)
                {
                    var c = new Int2(x, y);
                    if (!occupied.Contains(c)) free.Add(c);
                }
            return free.Count > 0 ? free[_rng.Next(free.Count)] : Body[Body.Count - 1];
        }

        /// <summary>Passi al secondo.</summary>
        public int Speed => Math.Min(BaseSpeed + Score / 5, MaxSpeed);

        /// <summary>Mette in coda un cambio di direzione (le inversioni a 180° vengono ignorate).</summary>
        public void Turn(Int2 d)
        {
            var last = Queue.Count > 0 ? Queue[Queue.Count - 1] : Direction;
            if (d != last && d != -last)
            {
                if (Queue.Count == QueueSize) Queue.RemoveAt(0); // deque(maxlen=3)
                Queue.Add(d);
            }
            Started = true;
        }

        /// <summary>Avanza di una casella.</summary>
        public void Step()
        {
            while (Queue.Count > 0)
            {
                var d = Queue[0];
                Queue.RemoveAt(0);
                if (d != -Direction)
                {
                    Direction = d;
                    break;
                }
            }
            var head = Body[0] + Direction;
            bool willGrow = head == Red || (Blue.HasValue && head == Blue.Value);
            int checkCount = willGrow ? Body.Count : Body.Count - 1; // la coda si sposta: ci si può entrare
            bool hitsBody = false;
            for (int i = 0; i < checkCount; i++) if (Body[i] == head) { hitsBody = true; break; }
            if (head.X < 0 || head.X >= Cols || head.Y < 0 || head.Y >= Rows || hitsBody)
            {
                Alive = false;
                _events.Add((EventKind.Die, Body[0]));
                return;
            }
            Body.Insert(0, head);
            if (head == Red)
            {
                Score += RedPoints;
                _events.Add((EventKind.Eat, head));
                Red = FreeCell();
            }
            else if (Blue.HasValue && head == Blue.Value)
            {
                Score += BluePoints;
                _events.Add((EventKind.Bonus, head));
                Blue = null;
                BlueCooldown = Uniform(BonusCooldownMin, BonusCooldownMax);
            }
            else
            {
                Body.RemoveAt(Body.Count - 1);
            }
        }

        /// <param name="dt">Secondi.</param>
        public void Update(float dt)
        {
            if (!Alive || !Started) return;
            if (Blue.HasValue)
            {
                BlueTimer -= dt;
                if (BlueTimer <= 0f)
                {
                    Blue = null;
                    BlueCooldown = Uniform(BonusCooldownMin, BonusCooldownMax);
                }
            }
            else
            {
                BlueCooldown -= dt;
                if (BlueCooldown <= 0f)
                {
                    Blue = FreeCell();
                    BlueTimer = BonusLifetime;
                }
            }
            StepTimer += dt;
            float interval = 1f / Speed;
            while (StepTimer >= interval && Alive)
            {
                StepTimer -= interval;
                Step();
            }
        }

        public List<(EventKind kind, Int2 cell)> PopEvents()
        {
            var events = new List<(EventKind, Int2)>(_events);
            _events.Clear();
            return events;
        }
    }
}
