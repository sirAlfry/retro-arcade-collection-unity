using System;
using System.IO;
using System.IO.Compression;
using RetroArcade;
using UnityEngine;

namespace RetroArcade.Preview
{
    /// <summary>
    /// Rasterizzatore software minimale: prende la geometria che RetroCanvas avrebbe mandato alla GPU
    /// (triangoli colorati + UV dell'atlante dei font) e la disegna in un buffer RGB, con alpha blending.
    /// Stesso campionamento "a centro pixel" e filtro Point dello shader RetroVector.
    /// </summary>
    internal static class SoftwareRaster
    {
        public static float[] Render(RetroCanvas canvas, int scale, uint clearColor)
        {
            int w = (int)RetroCanvas.W * scale, h = (int)RetroCanvas.H * scale;
            var buf = new float[w * h * 3];
            float cr = ((clearColor >> 16) & 255) / 255f, cg = ((clearColor >> 8) & 255) / 255f, cb = (clearColor & 255) / 255f;
            for (int i = 0; i < w * h; i++) { buf[i * 3] = cr; buf[i * 3 + 1] = cg; buf[i * 3 + 2] = cb; }

            var atlas = FontAtlas.Pixels;
            int asz = FontAtlas.AtlasSize;
            var v = canvas.Vertices; var col = canvas.Colors; var uv = canvas.UVs; var t = canvas.Triangles;

            for (int k = 0; k < t.Count; k += 3)
            {
                int ia = t[k], ib = t[k + 1], ic = t[k + 2];
                float ax = v[ia].x * scale, ay = -v[ia].y * scale;
                float bx = v[ib].x * scale, by = -v[ib].y * scale;
                float cx = v[ic].x * scale, cy = -v[ic].y * scale;
                float area = (bx - ax) * (cy - ay) - (by - ay) * (cx - ax);
                if (Math.Abs(area) < 1e-8f) continue;

                int minX = Math.Max(0, (int)Math.Floor(Math.Min(ax, Math.Min(bx, cx))));
                int maxX = Math.Min(w - 1, (int)Math.Ceiling(Math.Max(ax, Math.Max(bx, cx))));
                int minY = Math.Max(0, (int)Math.Floor(Math.Min(ay, Math.Min(by, cy))));
                int maxY = Math.Min(h - 1, (int)Math.Ceiling(Math.Max(ay, Math.Max(by, cy))));
                Color ca = col[ia], cbb = col[ib], ccc = col[ic];
                bool flat = ca.r == cbb.r && ca.g == cbb.g && ca.b == cbb.b && ca.a == cbb.a
                            && ca.r == ccc.r && ca.g == ccc.g && ca.b == ccc.b && ca.a == ccc.a;
                Color c = ca;
                Vector2 ua = uv[ia], ub = uv[ib], uc = uv[ic];

                for (int py = minY; py <= maxY; py++)
                {
                    // piccolo offset irrazionale: nessun centro pixel cade esattamente su un lato condiviso
                    float sy = py + 0.5f + 1.3e-4f;
                    for (int px = minX; px <= maxX; px++)
                    {
                        float sx = px + 0.5f + 0.7e-4f;
                        float w0 = ((bx - sx) * (cy - sy) - (by - sy) * (cx - sx)) / area;
                        float w1 = ((cx - sx) * (ay - sy) - (cy - sy) * (ax - sx)) / area;
                        float w2 = 1f - w0 - w1;
                        if (w0 < 0 || w1 < 0 || w2 < 0) continue;

                        float u = w0 * ua.x + w1 * ub.x + w2 * uc.x;
                        float vv = w0 * ua.y + w1 * ub.y + w2 * uc.y;
                        int tx = Math.Clamp((int)(u * asz), 0, asz - 1);
                        int ty = Math.Clamp((int)(vv * asz), 0, asz - 1);
                        if (!flat) // sfumature: colore interpolato tra i vertici, come fa la GPU
                            c = new Color(w0 * ca.r + w1 * cbb.r + w2 * ccc.r, w0 * ca.g + w1 * cbb.g + w2 * ccc.g,
                                          w0 * ca.b + w1 * cbb.b + w2 * ccc.b, w0 * ca.a + w1 * cbb.a + w2 * ccc.a);
                        float a = c.a * atlas[ty * asz + tx].a / 255f;
                        if (a <= 0f) continue;
                        int o = (py * w + px) * 3;
                        buf[o] = c.r * a + buf[o] * (1 - a);
                        buf[o + 1] = c.g * a + buf[o + 1] * (1 - a);
                        buf[o + 2] = c.b * a + buf[o + 2] * (1 - a);
                    }
                }
            }
            return buf;
        }

        public static void SavePng(string path, float[] rgb, int w, int h)
        {
            var raw = new byte[h * (w * 3 + 1)];
            for (int y = 0; y < h; y++)
            {
                int row = y * (w * 3 + 1);
                raw[row] = 0; // filtro "None"
                for (int x = 0; x < w * 3; x++)
                    raw[row + 1 + x] = (byte)Math.Clamp((int)(rgb[y * w * 3 + x] * 255f + 0.5f), 0, 255);
            }

            using var fs = File.Create(path);
            fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
            var ihdr = new byte[13];
            WriteBE(ihdr, 0, w); WriteBE(ihdr, 4, h);
            ihdr[8] = 8; ihdr[9] = 2; // 8 bit, RGB
            Chunk(fs, "IHDR", ihdr);
            using (var ms = new MemoryStream())
            {
                using (var z = new ZLibStream(ms, System.IO.Compression.CompressionLevel.Optimal, true)) z.Write(raw, 0, raw.Length);
                Chunk(fs, "IDAT", ms.ToArray());
            }
            Chunk(fs, "IEND", Array.Empty<byte>());
        }

        private static void Chunk(Stream s, string type, byte[] data)
        {
            var len = new byte[4]; WriteBE(len, 0, data.Length); s.Write(len);
            var td = new byte[4 + data.Length];
            for (int i = 0; i < 4; i++) td[i] = (byte)type[i];
            Buffer.BlockCopy(data, 0, td, 4, data.Length);
            s.Write(td);
            var crc = new byte[4]; WriteBE(crc, 0, (int)Crc32(td)); s.Write(crc);
        }

        private static void WriteBE(byte[] b, int o, int v)
        {
            b[o] = (byte)(v >> 24); b[o + 1] = (byte)(v >> 16); b[o + 2] = (byte)(v >> 8); b[o + 3] = (byte)v;
        }

        private static uint[] _crcTable;

        private static uint Crc32(byte[] data)
        {
            if (_crcTable == null)
            {
                _crcTable = new uint[256];
                for (uint n = 0; n < 256; n++)
                {
                    uint c = n;
                    for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                    _crcTable[n] = c;
                }
            }
            uint crc = 0xFFFFFFFFu;
            foreach (byte b in data) crc = _crcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
            return crc ^ 0xFFFFFFFFu;
        }
    }
}
