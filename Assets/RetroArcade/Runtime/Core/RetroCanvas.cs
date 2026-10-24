using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RetroArcade
{
    public enum TextAlign { Auto, Left, Center, Right }

    /// <summary>
    /// Il cuore del porting: un renderer "immediate mode" che imita l'API
    /// <c>Phaser.GameObjects.Graphics</c> (fillRect, fillCircle, strokeRoundedRect, ...) e
    /// <c>this.add.text</c>, ma in C#.
    ///
    /// Coordinate IDENTICHE a Phaser: area logica 800×600, origine in alto a sinistra, Y verso il basso.
    /// Così il codice delle scene si porta quasi 1:1.
    ///
    /// Come funziona: ogni frame le scene chiamano i metodi di disegno; i comandi diventano triangoli
    /// colorati in un'unica <see cref="Mesh"/> dinamica (una sola draw call per frame). I triangoli
    /// vengono disegnati nell'ordine in cui li chiedi: "chi disegna dopo sta sopra", come con i depth
    /// di Phaser ma senza doverli gestire.
    /// </summary>
    public sealed class RetroCanvas
    {
        public const float W = 800f;
        public const float H = 600f;

        private readonly List<Vector3> _verts = new List<Vector3>(16384);
        private readonly List<Color> _colors = new List<Color>(16384);
        private readonly List<Vector2> _uvs = new List<Vector2>(16384);
        private readonly List<int> _tris = new List<int>(32768);
        private readonly List<Vector2> _outer = new List<Vector2>(128);
        private readonly List<Vector2> _inner = new List<Vector2>(128);

        private readonly List<Rect> _clipStack = new List<Rect>(4);
        private bool _clipping;
        private Rect _clip;

        private readonly float _snap; // pixel della render texture per unità logica
        private readonly bool _linear;
        private Vector2 _white;

        private Mesh _mesh;

        /// <summary>La mesh dinamica che contiene tutto il frame (creata alla prima richiesta).</summary>
        public Mesh Mesh
        {
            get
            {
                if (_mesh == null)
                {
                    _mesh = new Mesh { name = "RetroCanvas", indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
                    _mesh.MarkDynamic();
                }
                return _mesh;
            }
        }

        /// <param name="renderScale">Pixel della render texture per unità logica (per allineare il testo ai pixel).</param>
        /// <param name="linearColorSpace">
        /// True se il progetto è in Linear color space (default dei template Unity 6): i colori dei vertici
        /// vanno convertiti, altrimenti l'esadecimale "sRGB" di Phaser uscirebbe slavato.
        /// </param>
        internal RetroCanvas(int renderScale, bool linearColorSpace)
        {
            FontAtlas.EnsureBuilt();
            _white = FontAtlas.WhiteUV;
            _snap = Mathf.Max(1, renderScale);
            _linear = linearColorSpace;
        }

        public int VertexCount => _verts.Count;

        // Accesso in sola lettura alla geometria del frame (usato dal renderer software di anteprima).
        internal List<Vector3> Vertices => _verts;
        internal List<Color> Colors => _colors;
        internal List<Vector2> UVs => _uvs;
        internal List<int> Triangles => _tris;

        internal void Begin()
        {
            _clipStack.Clear();
            _clipping = false;
            Offset = Vector2.zero;
            _verts.Clear();
            _colors.Clear();
            _uvs.Clear();
            _tris.Clear();
        }

        internal void End()
        {
            Mesh.Clear(true);
            Mesh.SetVertices(_verts);
            Mesh.SetColors(_colors);
            Mesh.SetUVs(0, _uvs);
            Mesh.SetTriangles(_tris, 0, false);
            Mesh.bounds = new Bounds(new Vector3(W * 0.5f, -H * 0.5f, 0f), new Vector3(W * 4f, H * 4f, 10f));
        }

        // ───────────────────────────── Colori ─────────────────────────────

        /// <summary>Come <c>Phaser.Display.Color.GetColor(r, g, b)</c>.</summary>
        public static uint Rgb(int r, int g, int b) =>
            ((uint)Mathf.Clamp(r, 0, 255) << 16) | ((uint)Mathf.Clamp(g, 0, 255) << 8) | (uint)Mathf.Clamp(b, 0, 255);

        /// <summary>Moltiplica i canali RGB per <paramref name="f"/> (es. 0.25 = colore del "ghost" in Tetris).</summary>
        public static uint Scale(uint rgb, float f) =>
            Rgb((int)(((rgb >> 16) & 0xFF) * f), (int)(((rgb >> 8) & 0xFF) * f), (int)((rgb & 0xFF) * f));

        /// <summary>Colore esadecimale in stile CSS ("#rrggbb"), utile per il debug.</summary>
        public static string Css(uint rgb) => "#" + rgb.ToString("x6");

        private Color Col(uint rgb, float alpha)
        {
            var c = new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, Mathf.Clamp01(alpha));
            return _linear ? c.linear : c;
        }

        // ───────────────────────────── Primitive di basso livello ─────────────────────────────

        /// <summary>
        /// Spostamento applicato a tutto ciò che si disegna (tremolio dello schermo). Si azzera a ogni frame.
        /// </summary>
        public Vector2 Offset;

        private int V(float x, float y, Color c, Vector2 uv)
        {
            _verts.Add(new Vector3(x + Offset.x, -(y + Offset.y), 0f)); // Y verso il basso come in pygame
            _colors.Add(c);
            _uvs.Add(uv);
            return _verts.Count - 1;
        }

        private void Tri(int a, int b, int c)
        {
            _tris.Add(a);
            _tris.Add(b);
            _tris.Add(c);
        }

        private void QuadRaw(float x0, float y0, float x1, float y1, Color c, Vector2 uvTL, Vector2 uvBR)
        {
            if (_clipping && !ClipQuad(ref x0, ref y0, ref x1, ref y1, ref uvTL, ref uvBR)) return;
            int a = V(x0, y0, c, uvTL);
            int b = V(x1, y0, c, new Vector2(uvBR.x, uvTL.y));
            int d = V(x1, y1, c, uvBR);
            int e = V(x0, y1, c, new Vector2(uvTL.x, uvBR.y));
            Tri(a, b, d);
            Tri(a, d, e);
        }

        /// <summary>Quad con un colore per angolo (sfumature). Rispetta il clip.</summary>
        private void QuadGradient(float x0, float y0, float x1, float y1, Color tl, Color tr, Color br, Color bl)
        {
            if (_clipping)
            {
                float nx0 = Mathf.Max(x0, _clip.x), ny0 = Mathf.Max(y0, _clip.y);
                float nx1 = Mathf.Min(x1, _clip.x + _clip.width), ny1 = Mathf.Min(y1, _clip.y + _clip.height);
                if (nx1 <= nx0 || ny1 <= ny0) return;
                Color At(float px, float py)
                {
                    float u = (px - x0) / (x1 - x0), v = (py - y0) / (y1 - y0);
                    var top = Color.LerpUnclamped(tl, tr, u);
                    var bottom = Color.LerpUnclamped(bl, br, u);
                    return Color.LerpUnclamped(top, bottom, v);
                }
                tl = At(nx0, ny0); tr = At(nx1, ny0); br = At(nx1, ny1); bl = At(nx0, ny1);
                x0 = nx0; y0 = ny0; x1 = nx1; y1 = ny1;
            }
            int a = V(x0, y0, tl, _white);
            int b = V(x1, y0, tr, _white);
            int d = V(x1, y1, br, _white);
            int e = V(x0, y1, bl, _white);
            Tri(a, b, d);
            Tri(a, d, e);
        }

        /// <summary>Taglia un quad allineato agli assi contro il rettangolo di clip, aggiustando le UV.</summary>
        private bool ClipQuad(ref float x0, ref float y0, ref float x1, ref float y1, ref Vector2 uvTL, ref Vector2 uvBR)
        {
            float cx0 = _clip.x, cy0 = _clip.y, cx1 = _clip.x + _clip.width, cy1 = _clip.y + _clip.height;
            if (x1 <= cx0 || x0 >= cx1 || y1 <= cy0 || y0 >= cy1) return false;
            float w = x1 - x0, h = y1 - y0;
            if (w <= 0f || h <= 0f) return false;
            float du = uvBR.x - uvTL.x, dv = uvBR.y - uvTL.y;
            float l = Mathf.Max(0f, cx0 - x0) / w, r = Mathf.Max(0f, x1 - cx1) / w;
            float t = Mathf.Max(0f, cy0 - y0) / h, b = Mathf.Max(0f, y1 - cy1) / h;
            var newTL = new Vector2(uvTL.x + du * l, uvTL.y + dv * t);
            var newBR = new Vector2(uvBR.x - du * r, uvBR.y - dv * b);
            x0 += w * l; x1 -= w * r; y0 += h * t; y1 -= h * b;
            uvTL = newTL; uvBR = newBR;
            return true;
        }

        /// <summary>
        /// Limita il disegno a un rettangolo (come <c>surface.set_clip</c> in pygame). I quad (rettangoli, testo)
        /// vengono tagliati con precisione; cerchi, linee e poligoni vengono solo scartati se del tutto fuori.
        /// Va sempre chiuso con <see cref="PopClip"/>.
        /// </summary>
        public void PushClip(float x, float y, float w, float h)
        {
            var r = new Rect(x, y, Mathf.Max(0f, w), Mathf.Max(0f, h));
            if (_clipping)
            {
                float nx0 = Mathf.Max(r.x, _clip.x), ny0 = Mathf.Max(r.y, _clip.y);
                float nx1 = Mathf.Min(r.x + r.width, _clip.x + _clip.width), ny1 = Mathf.Min(r.y + r.height, _clip.y + _clip.height);
                r = new Rect(nx0, ny0, Mathf.Max(0f, nx1 - nx0), Mathf.Max(0f, ny1 - ny0));
            }
            _clipStack.Add(r);
            _clip = r;
            _clipping = true;
        }

        public void PopClip()
        {
            if (_clipStack.Count == 0) return;
            _clipStack.RemoveAt(_clipStack.Count - 1);
            _clipping = _clipStack.Count > 0;
            if (_clipping) _clip = _clipStack[_clipStack.Count - 1];
        }

        /// <summary>Per le forme non tagliabili: false se il riquadro è completamente fuori dal clip.</summary>
        private bool Visible(float minX, float minY, float maxX, float maxY) =>
            !_clipping || !(maxX <= _clip.x || minX >= _clip.x + _clip.width || maxY <= _clip.y || minY >= _clip.y + _clip.height);

        // ───────────────────────────── Forme (API in stile Phaser Graphics) ─────────────────────────────

        public void FillRect(float x, float y, float w, float h, uint rgb, float alpha = 1f)
        {
            if (w <= 0f || h <= 0f || alpha <= 0f) return;
            QuadRaw(x, y, x + w, y + h, Col(rgb, alpha), _white, _white);
        }

        /// <summary>Bordo rettangolare centrato sul perimetro, come <c>lineStyle + strokeRect</c>.</summary>
        public void StrokeRect(float x, float y, float w, float h, float lineWidth, uint rgb, float alpha = 1f)
        {
            if (alpha <= 0f || lineWidth <= 0f) return;
            float t = lineWidth * 0.5f;
            var c = Col(rgb, alpha);
            // 4 strisce che non si sovrappongono (niente angoli "più scuri" con l'alpha)
            QuadRaw(x - t, y - t, x + w + t, y + t, c, _white, _white);          // alto
            QuadRaw(x - t, y + h - t, x + w + t, y + h + t, c, _white, _white);  // basso
            QuadRaw(x - t, y + t, x + t, y + h - t, c, _white, _white);          // sinistra
            QuadRaw(x + w - t, y + t, x + w + t, y + h - t, c, _white, _white);  // destra
        }

        /// <summary>Rettangolo con sfumatura verticale (dall'alto verso il basso).</summary>
        public void FillRectGradientV(float x, float y, float w, float h, uint topRgb, uint bottomRgb, float alpha = 1f)
        {
            if (w <= 0f || h <= 0f || alpha <= 0f) return;
            Color t = Col(topRgb, alpha), b = Col(bottomRgb, alpha);
            QuadGradient(x, y, x + w, y + h, t, t, b, b);
        }

        /// <summary>Rettangolo con sfumatura orizzontale (da sinistra a destra).</summary>
        public void FillRectGradientH(float x, float y, float w, float h, uint leftRgb, uint rightRgb, float alpha = 1f)
        {
            if (w <= 0f || h <= 0f || alpha <= 0f) return;
            Color l = Col(leftRgb, alpha), r = Col(rightRgb, alpha);
            QuadGradient(x, y, x + w, y + h, l, r, r, l);
        }

        /// <summary>
        /// Velo a tutto schermo con trasparenza variabile: la griglia di quad (lato <paramref name="cell"/>) prende
        /// l'alpha di ogni vertice da <paramref name="alphaAt"/> e la GPU sfuma in mezzo. Serve per l'oscurità
        /// "con torcia" di Maze Runner senza texture.
        /// </summary>
        public void FillAlphaField(uint rgb, System.Func<float, float, float> alphaAt, float cell = 16f)
        {
            int nx = Mathf.CeilToInt(W / cell), ny = Mathf.CeilToInt(H / cell);
            for (int j = 0; j < ny; j++)
            {
                float y0 = j * cell, y1 = Mathf.Min(H, y0 + cell);
                for (int i = 0; i < nx; i++)
                {
                    float x0 = i * cell, x1 = Mathf.Min(W, x0 + cell);
                    float a = alphaAt(x0, y0), b = alphaAt(x1, y0), c = alphaAt(x1, y1), d = alphaAt(x0, y1);
                    if (a <= 0f && b <= 0f && c <= 0f && d <= 0f) continue;
                    QuadGradient(x0, y0, x1, y1, Col(rgb, a), Col(rgb, b), Col(rgb, c), Col(rgb, d));
                }
            }
        }

        /// <summary>Poligono convesso pieno (come <c>pygame.draw.polygon</c> per forme semplici).</summary>
        public void FillPolygon(IReadOnlyList<Vector2> pts, uint rgb, float alpha = 1f)
        {
            if (pts == null || pts.Count < 3 || alpha <= 0f) return;
            var c = Col(rgb, alpha);
            int first = V(pts[0].x, pts[0].y, c, _white);
            int prev = V(pts[1].x, pts[1].y, c, _white);
            for (int i = 2; i < pts.Count; i++)
            {
                int v = V(pts[i].x, pts[i].y, c, _white);
                Tri(first, prev, v);
                prev = v;
            }
        }

        public void FillTriangle(float x1, float y1, float x2, float y2, float x3, float y3, uint rgb, float alpha = 1f)
        {
            if (alpha <= 0f) return;
            if (!Visible(Mathf.Min(x1, Mathf.Min(x2, x3)), Mathf.Min(y1, Mathf.Min(y2, y3)),
                         Mathf.Max(x1, Mathf.Max(x2, x3)), Mathf.Max(y1, Mathf.Max(y2, y3)))) return;
            var c = Col(rgb, alpha);
            if (_clipping)
            {
                _poly.Clear();
                _poly.Add(new Vector2(x1, y1));
                _poly.Add(new Vector2(x2, y2));
                _poly.Add(new Vector2(x3, y3));
                EmitClippedPolygon(c);
                return;
            }
            Tri(V(x1, y1, c, _white), V(x2, y2, c, _white), V(x3, y3, c, _white));
        }

        /// <summary>
        /// Segmento spesso come <c>pygame.draw.line</c>: lo spessore si misura in verticale per le linee
        /// "orizzontali" (|dx| > |dy|) e in orizzontale per quelle "verticali", con le estremità tagliate dritte.
        /// Le diagonali risultano quindi più sottili di un tratto perpendicolare: proprio come in pygame.
        /// </summary>
        public void Line(float x1, float y1, float x2, float y2, float lineWidth, uint rgb, float alpha = 1f)
        {
            if (alpha <= 0f || lineWidth <= 0f) return;
            if (!Visible(Mathf.Min(x1, x2) - lineWidth, Mathf.Min(y1, y2) - lineWidth,
                         Mathf.Max(x1, x2) + lineWidth, Mathf.Max(y1, y2) + lineWidth)) return;
            float dx = x2 - x1, dy = y2 - y1;
            if (dx * dx + dy * dy < 1e-8f) return;
            // Banda come pygame: da c - (w-1)//2 per w pixel (per w = 1 copre esattamente la riga/colonna c).
            float lo = -Mathf.Floor((lineWidth - 1f) * 0.5f), hi = lo + lineWidth;
            float lx = 0f, ly = 0f, hx = 0f, hy = 0f;
            if (Mathf.Abs(dx) > Mathf.Abs(dy)) { ly = lo; hy = hi; } else { lx = lo; hx = hi; }
            var c = Col(rgb, alpha);
            if (_clipping)
            {
                _poly.Clear();
                _poly.Add(new Vector2(x1 + lx, y1 + ly));
                _poly.Add(new Vector2(x2 + lx, y2 + ly));
                _poly.Add(new Vector2(x2 + hx, y2 + hy));
                _poly.Add(new Vector2(x1 + hx, y1 + hy));
                EmitClippedPolygon(c);
                return;
            }
            int a = V(x1 + lx, y1 + ly, c, _white);
            int b = V(x2 + lx, y2 + ly, c, _white);
            int d = V(x2 + hx, y2 + hy, c, _white);
            int e = V(x1 + hx, y1 + hy, c, _white);
            Tri(a, b, d);
            Tri(a, d, e);
        }

        private readonly List<Vector2> _poly = new List<Vector2>(16);
        private readonly List<Vector2> _polyTmp = new List<Vector2>(16);

        /// <summary>Ritaglia il poligono convesso in <see cref="_poly"/> sul clip (Sutherland–Hodgman) e lo disegna.</summary>
        private void EmitClippedPolygon(Color c)
        {
            float x0 = _clip.x, y0 = _clip.y, x1 = _clip.x + _clip.width, y1 = _clip.y + _clip.height;
            ClipEdge(p => p.x >= x0, (a, b) => Lerp(a, b, (x0 - a.x) / (b.x - a.x)));
            ClipEdge(p => p.x <= x1, (a, b) => Lerp(a, b, (x1 - a.x) / (b.x - a.x)));
            ClipEdge(p => p.y >= y0, (a, b) => Lerp(a, b, (y0 - a.y) / (b.y - a.y)));
            ClipEdge(p => p.y <= y1, (a, b) => Lerp(a, b, (y1 - a.y) / (b.y - a.y)));
            if (_poly.Count < 3) return;
            int first = V(_poly[0].x, _poly[0].y, c, _white);
            int prev = V(_poly[1].x, _poly[1].y, c, _white);
            for (int i = 2; i < _poly.Count; i++)
            {
                int v = V(_poly[i].x, _poly[i].y, c, _white);
                Tri(first, prev, v);
                prev = v;
            }
        }

        private static Vector2 Lerp(Vector2 a, Vector2 b, float t) => new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);

        private void ClipEdge(System.Func<Vector2, bool> inside, System.Func<Vector2, Vector2, Vector2> cross)
        {
            if (_poly.Count == 0) return;
            _polyTmp.Clear();
            for (int i = 0; i < _poly.Count; i++)
            {
                var cur = _poly[i];
                var prev = _poly[(i + _poly.Count - 1) % _poly.Count];
                bool cin = inside(cur), pin = inside(prev);
                if (cin)
                {
                    if (!pin) _polyTmp.Add(cross(prev, cur));
                    _polyTmp.Add(cur);
                }
                else if (pin)
                {
                    _polyTmp.Add(cross(prev, cur));
                }
            }
            _poly.Clear();
            _poly.AddRange(_polyTmp);
        }

        public void FillCircle(float cx, float cy, float r, uint rgb, float alpha = 1f)
        {
            if (r <= 0f || alpha <= 0f) return;
            if (!Visible(cx - r, cy - r, cx + r, cy + r)) return;
            var c = Col(rgb, alpha);
            int segs = CircleSegments(r);
            int center = V(cx, cy, c, _white);
            int first = -1, prev = -1;
            for (int i = 0; i < segs; i++)
            {
                float a = i * Mathf.PI * 2f / segs;
                int v = V(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, c, _white);
                if (prev >= 0) Tri(center, prev, v); else first = v;
                prev = v;
            }
            Tri(center, prev, first);
        }

        public void StrokeCircle(float cx, float cy, float r, float lineWidth, uint rgb, float alpha = 1f)
        {
            if (r <= 0f || alpha <= 0f || lineWidth <= 0f) return;
            if (!Visible(cx - r - lineWidth, cy - r - lineWidth, cx + r + lineWidth, cy + r + lineWidth)) return;
            _outer.Clear();
            _inner.Clear();
            int segs = CircleSegments(r + lineWidth);
            float ro = r + lineWidth * 0.5f, ri = Mathf.Max(0f, r - lineWidth * 0.5f);
            for (int i = 0; i < segs; i++)
            {
                float a = i * Mathf.PI * 2f / segs;
                float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                _outer.Add(new Vector2(cx + cs * ro, cy + sn * ro));
                _inner.Add(new Vector2(cx + cs * ri, cy + sn * ri));
            }
            Ring(Col(rgb, alpha));
        }

        /// <summary>
        /// Arco spesso come <c>pygame.draw.arc</c>: angoli in radianti, 0 = destra, crescono in senso ANTIORARIO
        /// sullo schermo; lo spessore cresce verso l'interno a partire da <paramref name="outerRadius"/>.
        /// </summary>
        public void StrokeArc(float cx, float cy, float outerRadius, float lineWidth, float startRad, float endRad,
                              uint rgb, float alpha = 1f)
        {
            if (outerRadius <= 0f || lineWidth <= 0f || alpha <= 0f || endRad <= startRad) return;
            if (!Visible(cx - outerRadius, cy - outerRadius, cx + outerRadius, cy + outerRadius)) return;
            float ri = Mathf.Max(0f, outerRadius - lineWidth);
            int segs = Mathf.Max(2, Mathf.CeilToInt(CircleSegments(outerRadius) * (endRad - startRad) / (Mathf.PI * 2f)));
            var c = Col(rgb, alpha);
            int prevO = -1, prevI = -1;
            for (int i = 0; i <= segs; i++)
            {
                float a = Mathf.Lerp(startRad, endRad, (float)i / segs);
                float cs = Mathf.Cos(a), sn = -Mathf.Sin(a); // y verso il basso: l'antiorario visivo ha il seno negato
                int o = V(cx + cs * outerRadius, cy + sn * outerRadius, c, _white);
                int n = V(cx + cs * ri, cy + sn * ri, c, _white);
                if (prevO >= 0)
                {
                    Tri(prevO, o, n);
                    Tri(prevO, n, prevI);
                }
                prevO = o;
                prevI = n;
            }
        }

        public void FillRoundedRect(float x, float y, float w, float h, float radius, uint rgb, float alpha = 1f)
        {
            if (w <= 0f || h <= 0f || alpha <= 0f) return;
            if (radius <= 0.01f) { FillRect(x, y, w, h, rgb, alpha); return; } // il rettangolo semplice si può tagliare
            if (!Visible(x, y, x + w, y + h)) return;
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(w, h) * 0.5f);
            _outer.Clear();
            int segs = CornerSegments(radius);
            RoundedContour(_outer, x, y, w, h, radius, segs);
            var c = Col(rgb, alpha);
            int center = V(x + w * 0.5f, y + h * 0.5f, c, _white);
            int first = -1, prev = -1;
            foreach (var p in _outer)
            {
                int v = V(p.x, p.y, c, _white);
                if (prev >= 0) Tri(center, prev, v); else first = v;
                prev = v;
            }
            Tri(center, prev, first);
        }

        public void StrokeRoundedRect(float x, float y, float w, float h, float radius, float lineWidth, uint rgb, float alpha = 1f)
        {
            if (alpha <= 0f || lineWidth <= 0f) return;
            if (radius <= 0.01f) { StrokeRect(x, y, w, h, lineWidth, rgb, alpha); return; }
            if (!Visible(x - lineWidth, y - lineWidth, x + w + lineWidth, y + h + lineWidth)) return;
            radius = Mathf.Clamp(radius, 0f, Mathf.Min(w, h) * 0.5f);
            float t = lineWidth * 0.5f;
            int segs = CornerSegments(radius + t);
            _outer.Clear();
            _inner.Clear();
            RoundedContour(_outer, x - t, y - t, w + 2f * t, h + 2f * t, radius + t, segs);
            RoundedContour(_inner, x + t, y + t, Mathf.Max(0f, w - 2f * t), Mathf.Max(0f, h - 2f * t), Mathf.Max(0f, radius - t), segs);
            Ring(Col(rgb, alpha));
        }

        private void Ring(Color c)
        {
            int n = _outer.Count;
            int baseIndex = _verts.Count;
            for (int i = 0; i < n; i++)
            {
                V(_outer[i].x, _outer[i].y, c, _white);
                V(_inner[i].x, _inner[i].y, c, _white);
            }
            for (int i = 0; i < n; i++)
            {
                int o0 = baseIndex + i * 2, i0 = o0 + 1;
                int o1 = baseIndex + ((i + 1) % n) * 2, i1 = o1 + 1;
                Tri(o0, o1, i1);
                Tri(o0, i1, i0);
            }
        }

        /// <summary>Contorno di un rettangolo arrotondato in senso orario (Y verso il basso).</summary>
        private static void RoundedContour(List<Vector2> pts, float x, float y, float w, float h, float r, int segs)
        {
            AddArc(pts, x + r, y + r, r, 180f, 270f, segs);         // alto-sinistra
            AddArc(pts, x + w - r, y + r, r, 270f, 360f, segs);     // alto-destra
            AddArc(pts, x + w - r, y + h - r, r, 0f, 90f, segs);    // basso-destra
            AddArc(pts, x + r, y + h - r, r, 90f, 180f, segs);      // basso-sinistra
        }

        private static void AddArc(List<Vector2> pts, float cx, float cy, float r, float fromDeg, float toDeg, int segs)
        {
            for (int i = 0; i <= segs; i++)
            {
                float a = Mathf.Lerp(fromDeg, toDeg, (float)i / segs) * Mathf.Deg2Rad;
                pts.Add(new Vector2(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r));
            }
        }

        private static int CircleSegments(float r) => Mathf.Clamp(Mathf.CeilToInt(r * 1.2f) + 10, 14, 72);
        private static int CornerSegments(float r) => Mathf.Clamp(Mathf.CeilToInt(r * 0.6f), 2, 16);

        // ───────────────────────────── Testo pixel-art 5×7 (pixel_font.py) ─────────────────────────────

        /// <summary>
        /// Dimensioni del blocco che <see cref="PixelText"/> occuperebbe, COMPRESO il margine degli effetti
        /// (alone: 3 pixel del font per lato; ombra: 1), esattamente come la Surface di <c>render_pixel_text</c>.
        /// Così le ancore ("center", "midtop", "topright"...) cadono negli stessi punti della versione Python.
        /// </summary>
        public static Vector2 MeasurePixel(string text, float scale, bool glow = false, bool shadow = false)
        {
            var m = PixelFont5x7.Measure(text);
            float pad = glow ? scale * 3f : (shadow ? scale : 0f);
            return new Vector2(m.x * scale + pad * 2f, m.y * scale + pad * 2f);
        }

        /// <summary>
        /// Scritta con il font 5×7 della versione Python. <paramref name="originX"/>/<paramref name="originY"/>
        /// funzionano come le ancore di pygame: (0,0) = topleft, (0.5,0.5) = center, (1,0) = topright...
        /// <para><paramref name="glow"/> aggiunge l'alone neon dei titoli; <paramref name="shadow"/> un'ombra
        /// spostata di un pixel del font (in basso a destra).</para>
        /// </summary>
        /// <returns>Il rettangolo occupato (come il Rect restituito da <c>blit_pixel_text</c>).</returns>
        public Rect PixelText(string text, float x, float y, float scale, uint rgb,
                              float originX = 0f, float originY = 0f, float alpha = 1f,
                              bool glow = false, uint? shadow = null)
        {
            if (string.IsNullOrEmpty(text) || alpha <= 0f) return new Rect(x, y, 0f, 0f);
            Vector2 size = MeasurePixel(text, scale, glow, shadow.HasValue);
            float left = Snap(x - originX * size.x), top = Snap(y - originY * size.y);
            float pad = glow ? scale * 3f : (shadow.HasValue ? scale : 0f);
            float ox = left + pad, oy = top + pad;

            if (glow)
            {
                // Alone: la versione Python sfoca le lettere (rimpicciolisce e ringrandisce la Surface).
                // Qui: tre "gusci" sempre più larghi e trasparenti attorno a ogni striscia di pixel.
                // Sovrapponendosi, si sommano vicino alle lettere e sfumano allontanandosi: lo stesso effetto.
                // Come in pygame la sfocatura ha un raggio fisso di pochi pixel, indipendente dalla scala.
                float a = alpha;
                GlyphPass(text, ox, oy, scale, Col(rgb, 0.10f * a), 1f);
                GlyphPass(text, ox, oy, scale, Col(rgb, 0.07f * a), 2f);
                GlyphPass(text, ox, oy, scale, Col(rgb, 0.04f * a), 3.2f);
            }
            if (shadow.HasValue) GlyphPass(text, ox + scale, oy + scale, scale, Col(shadow.Value, alpha), 0f);
            GlyphPass(text, ox, oy, scale, Col(rgb, alpha), 0f);
            return new Rect(left, top, size.x, size.y);
        }

        private void GlyphPass(string text, float x, float y, float scale, Color c, float grow)
        {
            float pen = x;
            foreach (char ch in text)
            {
                var glyph = PixelFont5x7.Get(ch);
                foreach (var r in glyph.Runs)
                {
                    float x0 = pen + r.x * scale - grow, y0 = y + r.y * scale - grow;
                    QuadRaw(x0, y0, x0 + r.width * scale + 2f * grow, y0 + r.height * scale + 2f * grow, c, _white, _white);
                }
                pen += (glyph.Width + PixelFont5x7.LetterSpacing) * scale;
            }
        }

        // ───────────────────────────── Testo bitmap ─────────────────────────────

        /// <summary>
        /// Disegna testo con i font bitmap del progetto (equivalente di <c>this.add.text</c>).
        /// <para><paramref name="scale"/>: 1 = font a grandezza naturale (Body 6×10, Display 8×8). Con renderScale 2
        /// anche i mezzi valori (1.5, 2.5) restano perfettamente nitidi.</para>
        /// <para><paramref name="originX"/>/<paramref name="originY"/>: come <c>setOrigin</c> di Phaser (0.5 = centrato).</para>
        /// <para>Supporta "\n", lettere accentate italiane, frecce ←↑→↓, — · … ✓ e 🎉.</para>
        /// </summary>
        /// <returns>Dimensioni del blocco di testo in coordinate logiche.</returns>
        public Vector2 Text(string text, float x, float y, float scale, uint rgb,
                            float originX = 0f, float originY = 0f, FontFace face = FontFace.Body,
                            float alpha = 1f, float stroke = 0f, uint strokeColor = 0x000000,
                            TextAlign align = TextAlign.Auto)
        {
            if (string.IsNullOrEmpty(text) || alpha <= 0f) return Vector2.zero;
            var font = face == FontFace.Display ? FontAtlas.Display : FontAtlas.Body;

            Vector2 size = Measure(text, scale, face, out int lines);
            float left = Snap(x - originX * size.x);
            float top = Snap(y - originY * size.y);
            if (align == TextAlign.Auto)
                align = originX >= 0.75f ? TextAlign.Right : originX >= 0.25f ? TextAlign.Center : TextAlign.Left;

            if (stroke > 0f)
            {
                var sc = Col(strokeColor, alpha);
                for (int oy = -1; oy <= 1; oy++)
                    for (int ox = -1; ox <= 1; ox++)
                        if (ox != 0 || oy != 0)
                            DrawGlyphs(text, font, left + ox * stroke, top + oy * stroke, scale, size.x, align, sc);
            }

            DrawGlyphs(text, font, left, top, scale, size.x, align, Col(rgb, alpha));
            return size;
        }

        /// <summary>Misura un testo senza disegnarlo.</summary>
        public Vector2 Measure(string text, float scale, FontFace face = FontFace.Body) => Measure(text, scale, face, out _);

        private Vector2 Measure(string text, float scale, FontFace face, out int lines)
        {
            var font = face == FontFace.Display ? FontAtlas.Display : FontAtlas.Body;
            lines = 1;
            int maxLen = 0, len = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch == '\n') { lines++; if (len > maxLen) maxLen = len; len = 0; continue; }
                if (char.IsHighSurrogate(ch) && i + 1 < text.Length) i++;
                len++;
            }
            if (len > maxLen) maxLen = len;
            float lineH = (font.CellH + font.LineGap) * scale;
            return new Vector2(maxLen * font.CellW * scale, lines * lineH - font.LineGap * scale);
        }

        private void DrawGlyphs(string text, PixelFont font, float left, float top, float scale, float blockW, TextAlign align, Color color)
        {
            float cw = font.CellW * scale, ch = font.CellH * scale, lineH = (font.CellH + font.LineGap) * scale;
            int lineStart = 0;
            float lineY = top;
            for (int i = 0; i <= text.Length; i++)
            {
                if (i < text.Length && text[i] != '\n') continue;

                // Riga [lineStart, i)
                int count = 0;
                for (int k = lineStart; k < i; k++) { if (char.IsHighSurrogate(text[k]) && k + 1 < i) k++; count++; }
                float lineW = count * cw;
                float penX = align == TextAlign.Center ? left + Snap((blockW - lineW) * 0.5f)
                           : align == TextAlign.Right ? left + (blockW - lineW)
                           : left;

                for (int k = lineStart; k < i; k++)
                {
                    int cp = text[k];
                    if (char.IsHighSurrogate(text[k]) && k + 1 < i)
                    {
                        cp = char.ConvertToUtf32(text[k], text[k + 1]);
                        k++;
                    }
                    if (cp != ' ' && font.TryGet(cp, out Rect uv))
                        QuadRaw(penX, lineY, penX + cw, lineY + ch, color,
                                new Vector2(uv.x, uv.y), new Vector2(uv.x + uv.width, uv.y + uv.height));
                    penX += cw;
                }

                lineStart = i + 1;
                lineY += lineH;
            }
        }

        private float Snap(float v) => Mathf.Round(v * _snap) / _snap;
    }
}
