// Mini "UnityEngine" in puro C#: SOLO i tipi e i metodi che le scene e il RetroCanvas usano,
// con la stessa semantica di Unity (Mathf, Vector2, Rect, Color...). Serve a far girare le vere scene
// fuori dall'editor, per generare screenshot. Le classi legate al motore (Mesh, Texture2D, AudioClip...)
// sono gusci vuoti: il renderer di anteprima non le usa mai.
using System;

namespace UnityEngine
{
    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Deg2Rad = PI / 180f;
        public static float Sin(float f) => (float)Math.Sin(f);
        public static float Cos(float f) => (float)Math.Cos(f);
        public static float Sqrt(float f) => (float)Math.Sqrt(f);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Tan(float f) => (float)Math.Tan(f);
        public static float Sign(float f) => f >= 0f ? 1f : -1f;
        public static float Repeat(float t, float length) => Clamp(t - Floor(t / length) * length, 0f, length);
        public static float PingPong(float t, float length) { t = Repeat(t, length * 2f); return length - Math.Abs(t - length); }
        public static float Exp(float f) => (float)Math.Exp(f);
        public static float Log(float f) => (float)Math.Log(f);
        public static float Abs(float f) => Math.Abs(f);
        public static int Abs(int v) => Math.Abs(v);
        public static float Pow(float f, float p) => (float)Math.Pow(f, p);
        public static float Floor(float f) => (float)Math.Floor(f);
        public static float Round(float f) => (float)Math.Round(f);
        public static int FloorToInt(float f) => (int)Math.Floor(f);
        public static int CeilToInt(float f) => (int)Math.Ceiling(f);
        public static int RoundToInt(float f) => (int)Math.Round(f);
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Min(int a, int b) => a < b ? a : b;
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;
        public static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;
        public static float Clamp01(float v) => Clamp(v, 0f, 1f);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float MoveTowards(float current, float target, float maxDelta) =>
            Math.Abs(target - current) <= maxDelta ? target : current + Math.Sign(target - current) * maxDelta;
        public static float GammaToLinearSpace(float c) =>
            c <= 0.04045f ? c / 12.92f : (float)Math.Pow((c + 0.055) / 1.055, 2.4);
    }

    public struct Vector2 : IEquatable<Vector2>
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public float sqrMagnitude => x * x + y * y;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator *(Vector2 a, float d) => new Vector2(a.x * d, a.y * d);
        public static bool operator ==(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 9.99999944E-11f;
        public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);
        public static Vector2 MoveTowards(Vector2 current, Vector2 target, float maxDistanceDelta)
        {
            var d = target - current;
            float dist = d.magnitude;
            if (dist <= maxDistanceDelta || dist == 0f) return target;
            return current + d * (maxDistanceDelta / dist);
        }
        public bool Equals(Vector2 o) => x == o.x && y == o.y;
        public override bool Equals(object o) => o is Vector2 v && Equals(v);
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);
    }

    public struct Vector2Int : IEquatable<Vector2Int>
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public static Vector2Int zero => new Vector2Int(0, 0);
        public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new Vector2Int(a.x + b.x, a.y + b.y);
        public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new Vector2Int(a.x - b.x, a.y - b.y);
        public static bool operator ==(Vector2Int a, Vector2Int b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(Vector2Int a, Vector2Int b) => !(a == b);
        public bool Equals(Vector2Int o) => this == o;
        public override bool Equals(object o) => o is Vector2Int v && Equals(v);
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public bool Contains(Vector2 p) => p.x >= x && p.x < x + width && p.y >= y && p.y < y + height;
    }

    public struct RectInt
    {
        public int x, y, width, height;
        public RectInt(int x, int y, int w, int h) { this.x = x; this.y = y; width = w; height = h; }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color LerpUnclamped(Color x, Color y, float t) =>
            new Color(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t);
        public Color linear => new Color(Mathf.GammaToLinearSpace(r), Mathf.GammaToLinearSpace(g), Mathf.GammaToLinearSpace(b), a);
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }

    public struct Bounds { public Bounds(Vector3 center, Vector3 size) { } }

    public enum HideFlags { None, DontSave = 52 }
    public enum TextureFormat { RGBA32 = 4 }
    public enum FilterMode { Point, Bilinear }
    public enum TextureWrapMode { Clamp = 1 }

    public class Object { public string name; public HideFlags hideFlags; }
    public class Component : Object { }
    public class Behaviour : Component { }
    public sealed class GameObject : Object { public T AddComponent<T>() where T : Component, new() => new T(); }

    public sealed class Mesh : Object
    {
        public Rendering.IndexFormat indexFormat;
        public Bounds bounds;
        public void MarkDynamic() { }
        public void Clear(bool keepVertexLayout) { }
        public void SetVertices(System.Collections.Generic.List<Vector3> v) { }
        public void SetColors(System.Collections.Generic.List<Color> c) { }
        public void SetUVs(int channel, System.Collections.Generic.List<Vector2> uvs) { }
        public void SetTriangles(System.Collections.Generic.List<int> t, int submesh, bool calculateBounds) { }
    }

    public sealed class Texture2D : Object
    {
        public FilterMode filterMode;
        public TextureWrapMode wrapMode;
        public Texture2D(int w, int h, TextureFormat f, bool mip, bool linear) { }
        public void SetPixels32(Color32[] p) { }
        public void Apply(bool updateMipmaps, bool makeNoLongerReadable) { }
    }

    public sealed class AudioClip : Object
    {
        public static AudioClip Create(string name, int lengthSamples, int channels, int frequency, bool stream) => new AudioClip();
        public bool SetData(float[] data, int offset) => true;
    }

    public sealed class AudioSource : Behaviour
    {
        public bool playOnAwake;
        public float spatialBlend, volume;
        public void PlayOneShot(AudioClip clip, float volumeScale) { }
        public static implicit operator bool(AudioSource s) => !ReferenceEquals(s, null);
    }

    public static class PlayerPrefs
    {
        public static int GetInt(string key, int fallback) => fallback;
        public static void SetInt(string key, int value) { }
        public static bool HasKey(string key) => false;
        public static string GetString(string key, string fallback = "") => fallback;
        public static void SetString(string key, string value) { }
        public static void DeleteKey(string key) { }
        public static void Save() { }
    }

    public enum RuntimePlatform { OSXPlayer = 1, WindowsPlayer = 2, LinuxPlayer = 13, WebGLPlayer = 17 }

    public static class Application
    {
        public static bool isEditor => false;
        public static bool isMobilePlatform => false;
        public static RuntimePlatform platform => RuntimePlatform.LinuxPlayer;
        public static string dataPath => System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "Preview_Data");
        public static string persistentDataPath => System.IO.Path.GetTempPath();
    }

    public static class Debug
    {
        public static void Log(object m) => Console.WriteLine(m);
        public static void LogWarning(object m) => Console.WriteLine("WARN " + m);
        public static void LogError(object m) => Console.WriteLine("ERROR " + m);
    }
}

namespace UnityEngine.Rendering
{
    public enum IndexFormat { UInt16, UInt32 }
}

namespace RetroArcade
{
    /// <summary>Guscio: il vero CrtPresenter vive in Unity; qui serve solo per la firma di ArcadeInput.Poll.</summary>
    public sealed class CrtPresenter
    {
        public bool ScreenToVirtual(UnityEngine.Vector2 screen, out UnityEngine.Vector2 v) { v = screen; return true; }
    }
}
