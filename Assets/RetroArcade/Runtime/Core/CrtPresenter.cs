using System;
using UnityEngine;

namespace RetroArcade
{
    /// <summary>Effetto monitor: Off = pixel puri (come l'originale), Soft = scanline + vignetta, Full = tubo catodico completo.</summary>
    public enum CrtMode { Off, Soft, Full }

    /// <summary>
    /// Gestisce le due telecamere del progetto:
    /// <list type="number">
    /// <item><b>GameCamera</b>: ortografica, inquadra l'area logica 800×600 e la disegna in una RenderTexture
    /// (800×600 × renderScale). È l'equivalente del canvas WebGL di Phaser.</item>
    /// <item><b>PresentCamera</b>: disegna a schermo un quad con quella texture, mantenendo il 4:3 con bande nere
    /// (come <c>Phaser.Scale.FIT</c>) e applicando lo shader CRT.</item>
    /// </list>
    /// Le due "scene" sono separate nello spazio (il quad sta a X = 10000) quindi non servono layer dedicati.
    /// </summary>
    public sealed class CrtPresenter : IDisposable
    {
        private const float Far = 10000f;
        private const float Aspect = RetroCanvas.W / RetroCanvas.H;

        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int CurvatureId = Shader.PropertyToID("_Curvature");
        private static readonly int ScanlineId = Shader.PropertyToID("_Scanline");
        private static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        private static readonly int AberrationId = Shader.PropertyToID("_Aberration");
        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int SourceSizeId = Shader.PropertyToID("_SourceSize");
        private static readonly int LinesId = Shader.PropertyToID("_Lines");

        private readonly Material _material;
        private readonly Transform _quad;
        private readonly Mesh _quadMesh;
        private CrtMode _mode;

        public RenderTexture Target { get; }
        public Camera GameCamera { get; }
        public Camera PresentCamera { get; }

        /// <summary>Rettangolo (in pixel schermo, origine in basso a sinistra) occupato dall'immagine 4:3.</summary>
        public Rect ScreenRect { get; private set; }

        public CrtMode Mode
        {
            get => _mode;
            set
            {
                _mode = value;
                // Senza curvatura i pixel restano "duri" come con image-rendering: pixelated nel browser.
                Target.filterMode = value == CrtMode.Full ? FilterMode.Bilinear : FilterMode.Point;
            }
        }

        private float Curvature => _mode == CrtMode.Full ? 0.045f : 0f;

        internal CrtPresenter(Transform root, int renderScale, CrtMode mode, Shader crtShader)
        {
            renderScale = Mathf.Clamp(renderScale, 1, 4);
            Target = new RenderTexture((int)RetroCanvas.W * renderScale, (int)RetroCanvas.H * renderScale, 24,
                                       RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default)
            {
                name = "RetroArcade_Screen",
                wrapMode = TextureWrapMode.Clamp,
                antiAliasing = 1,
                useMipMap = false,
                hideFlags = HideFlags.DontSave,
            };
            Target.Create();

            // ── Camera di gioco: vede x ∈ [0,800], y ∈ [-600,0] (la Y è ribaltata dal canvas) ──
            var gameGo = new GameObject("GameCamera (800x600)");
            gameGo.transform.SetParent(root, false);
            gameGo.transform.position = new Vector3(RetroCanvas.W * 0.5f, -RetroCanvas.H * 0.5f, -10f);
            GameCamera = gameGo.AddComponent<Camera>();
            GameCamera.orthographic = true;
            GameCamera.orthographicSize = RetroCanvas.H * 0.5f;
            GameCamera.nearClipPlane = 0.1f;
            GameCamera.farClipPlane = 100f;
            GameCamera.clearFlags = CameraClearFlags.SolidColor;
            GameCamera.backgroundColor = new Color32(0x14, 0x14, 0x2a, 0xff);
            GameCamera.targetTexture = Target;
            GameCamera.depth = -50;
            GameCamera.allowHDR = false;
            GameCamera.allowMSAA = false;
            GameCamera.useOcclusionCulling = false;

            // ── Camera di presentazione: disegna il quad a schermo ──
            var presentGo = new GameObject("PresentCamera (CRT)");
            presentGo.transform.SetParent(root, false);
            presentGo.transform.position = new Vector3(Far, 0f, -10f);
            PresentCamera = presentGo.AddComponent<Camera>();
            PresentCamera.orthographic = true;
            PresentCamera.orthographicSize = 1f;
            PresentCamera.nearClipPlane = 0.1f;
            PresentCamera.farClipPlane = 100f;
            PresentCamera.clearFlags = CameraClearFlags.SolidColor;
            PresentCamera.backgroundColor = Color.black;
            PresentCamera.depth = 100;
            PresentCamera.allowHDR = false;
            PresentCamera.allowMSAA = false;
            PresentCamera.useOcclusionCulling = false;

            _quadMesh = BuildQuad();
            var quadGo = new GameObject("ScreenQuad");
            quadGo.transform.SetParent(root, false);
            quadGo.transform.position = new Vector3(Far, 0f, 0f);
            quadGo.AddComponent<MeshFilter>().sharedMesh = _quadMesh;
            var mr = quadGo.AddComponent<MeshRenderer>();
            _material = new Material(crtShader) { name = "RetroArcade_CRT", hideFlags = HideFlags.DontSave };
            _material.SetTexture(MainTexId, Target);
            mr.sharedMaterial = _material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _quad = quadGo.transform;

            Mode = mode;
            UpdateLayout();
        }

        /// <summary>Da chiamare ogni frame: adatta il quad alla finestra e aggiorna i parametri dello shader.</summary>
        internal void UpdateLayout()
        {
            float sw = Mathf.Max(1, Screen.width), sh = Mathf.Max(1, Screen.height);
            float w, h;
            if (sw / sh >= Aspect) { h = sh; w = h * Aspect; }
            else { w = sw; h = w / Aspect; }
            ScreenRect = new Rect((sw - w) * 0.5f, (sh - h) * 0.5f, w, h);

            // La PresentCamera è alta 2 unità = sh pixel.
            _quad.localScale = new Vector3(w / sh * 2f, h / sh * 2f, 1f);

            // Le scanline hanno senso solo se ogni riga logica occupa almeno ~2 pixel reali,
            // altrimenti creano moiré: le attenuiamo dolcemente sulle finestre piccole.
            float pixelsPerRow = h / RetroCanvas.H;
            float scanFade = Mathf.Clamp01((pixelsPerRow - 1.4f) / 1.0f);

            float scan = 0f, vignette = 0f, aberration = 0f, glow = 0f;
            switch (_mode)
            {
                case CrtMode.Soft: scan = 0.22f; vignette = 0.45f; break;
                case CrtMode.Full: scan = 0.30f; vignette = 0.85f; aberration = 0.7f; glow = 0.35f; break;
            }

            _material.SetFloat(CurvatureId, Curvature);
            _material.SetFloat(ScanlineId, scan * scanFade);
            _material.SetFloat(VignetteId, vignette);
            _material.SetFloat(AberrationId, aberration);
            _material.SetFloat(GlowId, glow);
            _material.SetFloat(LinesId, RetroCanvas.H);
            _material.SetVector(SourceSizeId, new Vector4(Target.width, Target.height, 1f / Target.width, 1f / Target.height));
        }

        /// <summary>Pixel schermo → coordinate logiche 800×600 (Y verso il basso). False se fuori dall'immagine.</summary>
        public bool ScreenToVirtual(Vector2 screen, out Vector2 virtualPos)
        {
            var r = ScreenRect;
            var uv = new Vector2((screen.x - r.x) / r.width, (screen.y - r.y) / r.height);
            uv = CurveUV(uv, Curvature); // stessa formula dello shader: il click cade dove lo vedi
            virtualPos = new Vector2(uv.x * RetroCanvas.W, (1f - uv.y) * RetroCanvas.H);
            return uv.x >= 0f && uv.x <= 1f && uv.y >= 0f && uv.y <= 1f;
        }

        /// <summary>Distorsione "a botte" del CRT. DEVE restare identica a CurveUV in RetroCRT.shader.</summary>
        public static Vector2 CurveUV(Vector2 uv, float k)
        {
            if (k <= 0f) return uv;
            float dx = uv.x * 2f - 1f, dy = uv.y * 2f - 1f;
            float sx = dx * (1f + k * dy * dy);
            float sy = dy * (1f + k * dx * dx);
            return new Vector2(sx * 0.5f + 0.5f, sy * 0.5f + 0.5f);
        }

        private static Mesh BuildQuad()
        {
            var m = new Mesh { name = "RetroArcade_ScreenQuad", hideFlags = HideFlags.DontSave };
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
            };
            m.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }

        public void Dispose()
        {
            if (GameCamera != null) GameCamera.targetTexture = null;
            if (Target != null) { Target.Release(); UnityEngine.Object.Destroy(Target); }
            if (_material != null) UnityEngine.Object.Destroy(_material);
            if (_quadMesh != null) UnityEngine.Object.Destroy(_quadMesh);
        }
    }
}
