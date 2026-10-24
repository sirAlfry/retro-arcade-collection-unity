using System;

namespace RetroArcade.Logic
{
    /// <summary>Coppia di interi (x, y): una casella di una griglia o una direzione. Come le tuple di Python.</summary>
    public readonly struct Int2 : IEquatable<Int2>
    {
        public readonly int X, Y;
        public Int2(int x, int y) { X = x; Y = y; }

        public static readonly Int2 Zero = new Int2(0, 0);
        public static readonly Int2 Up = new Int2(0, -1);
        public static readonly Int2 Down = new Int2(0, 1);
        public static readonly Int2 Left = new Int2(-1, 0);
        public static readonly Int2 Right = new Int2(1, 0);

        public static Int2 operator +(Int2 a, Int2 b) => new Int2(a.X + b.X, a.Y + b.Y);
        public static Int2 operator -(Int2 a, Int2 b) => new Int2(a.X - b.X, a.Y - b.Y);
        public static Int2 operator -(Int2 a) => new Int2(-a.X, -a.Y);
        public static bool operator ==(Int2 a, Int2 b) => a.X == b.X && a.Y == b.Y;
        public static bool operator !=(Int2 a, Int2 b) => !(a == b);

        public bool Equals(Int2 o) => this == o;
        public override bool Equals(object o) => o is Int2 v && Equals(v);
        public override int GetHashCode() => X * 73856093 ^ Y * 19349663;
        public override string ToString() => $"({X}, {Y})";

        public void Deconstruct(out int x, out int y) { x = X; y = Y; }
    }
}
