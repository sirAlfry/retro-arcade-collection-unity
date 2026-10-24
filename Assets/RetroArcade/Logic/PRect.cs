using System;

namespace RetroArcade.Logic
{
    /// <summary>
    /// Rettangolo a coordinate intere con la stessa semantica di <c>pygame.Rect</c> (x, y, w, h; il bordo
    /// destro/inferiore è escluso). Comodo per portare 1:1 il codice delle collisioni dalla versione Python.
    /// </summary>
    public struct PRect : IEquatable<PRect>
    {
        public int X, Y, W, H;

        public PRect(int x, int y, int w, int h) { X = x; Y = y; W = w; H = h; }

        public int Left { get => X; set => X = value; }
        public int Top { get => Y; set => Y = value; }
        public int Right { get => X + W; set => X = value - W; }
        public int Bottom { get => Y + H; set => Y = value - H; }
        public int CenterX { get => X + W / 2; set => X = value - W / 2; }
        public int CenterY { get => Y + H / 2; set => Y = value - H / 2; }

        /// <summary>Come <c>rect.center = (cx, cy)</c>.</summary>
        public void SetCenter(int cx, int cy) { CenterX = cx; CenterY = cy; }

        public static PRect FromCenter(int cx, int cy, int w, int h)
        {
            var r = new PRect(0, 0, w, h);
            r.SetCenter(cx, cy);
            return r;
        }

        /// <summary><c>colliderect</c>: sovrapposizione stretta (i rettangoli vuoti non collidono mai).</summary>
        public bool Collides(PRect o) =>
            W > 0 && H > 0 && o.W > 0 && o.H > 0 &&
            X < o.X + o.W && X + W > o.X && Y < o.Y + o.H && Y + H > o.Y;

        /// <summary><c>collidepoint</c></summary>
        public bool Contains(float px, float py) => px >= X && px < X + W && py >= Y && py < Y + H;

        /// <summary><c>contains(other)</c>: l'altro rettangolo sta tutto dentro.</summary>
        public bool Contains(PRect o) => o.X >= X && o.Y >= Y && o.X + o.W <= X + W && o.Y + o.H <= Y + H;

        /// <summary><c>inflate(dx, dy)</c>: cresce di dx/dy in totale restando centrato (come in pygame).</summary>
        public PRect Inflate(int dx, int dy) => new PRect(X - dx / 2, Y - dy / 2, W + dx, H + dy);

        public PRect Move(int dx, int dy) => new PRect(X + dx, Y + dy, W, H);

        public bool Equals(PRect o) => X == o.X && Y == o.Y && W == o.W && H == o.H;
        public override bool Equals(object o) => o is PRect r && Equals(r);
        public override int GetHashCode() => (X * 397 ^ Y) * 397 ^ (W * 31 + H);
        public override string ToString() => $"<rect({X}, {Y}, {W}, {H})>";
    }

    /// <summary>Piccoli aiuti di fisica dei giochi d'azione (Pong, Breakout).</summary>
    public static class ArcadePhysics
    {
        /// <summary>
        /// Dove attraverserà la palla la linea x = <paramref name="targetX"/>? I rimbalzi sui muri vengono
        /// "srotolati": la traiettoria è una retta in uno spazio specchiato, poi ripiegata in [top, bottom]
        /// con un modulo. Se la palla si allontana restituisce il centro (porting di <c>pong.predict_y</c>).
        /// </summary>
        public static float PredictY(float x, float y, float vx, float vy, float targetX, float top, float bottom)
        {
            if (vx == 0f || (targetX - x) / vx < 0f) return (top + bottom) / 2f;
            float t = (targetX - x) / vx;
            float span = bottom - top;
            float py = Mod(y + vy * t - top, 2f * span);
            if (py > span) py = 2f * span - py;
            return top + py;
        }

        /// <summary>Modulo "alla Python": il risultato ha il segno del divisore.</summary>
        public static float Mod(float a, float b)
        {
            float r = a % b;
            return r < 0 ? r + b : r;
        }

        /// <summary>Il cerchio tocca il rettangolo? (punto del rettangolo più vicino al centro: <c>circle_hits_rect</c>).</summary>
        public static bool CircleHitsRect(float cx, float cy, float r, PRect rect)
        {
            float nx = Math.Max(rect.Left, Math.Min(cx, rect.Right));
            float ny = Math.Max(rect.Top, Math.Min(cy, rect.Bottom));
            float dx = cx - nx, dy = cy - ny;
            return dx * dx + dy * dy < r * r;
        }
    }
}
