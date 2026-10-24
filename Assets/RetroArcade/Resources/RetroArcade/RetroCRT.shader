// Shader di presentazione: prende la RenderTexture 800x600 e la mostra come un vecchio monitor.
// Tutti gli effetti sono regolati da CrtPresenter.cs (F1 cicla Off / Soft / Full).
//  - _Curvature : distorsione "a botte" (0 = schermo piatto). DEVE combaciare con CrtPresenter.CurveUV
//  - _Scanline  : intensità delle linee orizzontali (una per riga logica, _Lines = 600)
//  - _Vignette  : scurimento dei bordi
//  - _Aberration: separazione R/B in pixel della sorgente
//  - _Glow      : bagliore dei colori brillanti (bloom "povero" a 4 campioni)
Shader "RetroArcade/CRT"
{
    Properties
    {
        _MainTex ("Screen", 2D) = "black" {}
        _Curvature ("Curvature", Range(0, 0.2)) = 0.045
        _Scanline ("Scanline", Range(0, 1)) = 0.3
        _Vignette ("Vignette", Range(0, 2)) = 0.85
        _Aberration ("Aberration (px)", Range(0, 3)) = 0.7
        _Glow ("Glow", Range(0, 1)) = 0.35
        _Lines ("Logical lines", Float) = 600
        _SourceSize ("Source size (w, h, 1/w, 1/h)", Vector) = (1600, 1200, 0.000625, 0.000833)
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Curvature, _Scanline, _Vignette, _Aberration, _Glow, _Lines;
            float4 _SourceSize;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float2 CurveUV (float2 uv)
            {
                // Barile classico: i punti a metà dei bordi restano fissi, gli angoli escono dallo schermo
                // (diventano neri) e il contenuto si "gonfia" verso il centro. Nessun pixel di gioco va perso.
                float2 d = uv * 2.0 - 1.0;
                d *= 1.0 + _Curvature * d.yx * d.yx;
                return d * 0.5 + 0.5;
            }

            float4 frag (v2f i) : SV_Target
            {
                float2 uv = CurveUV(i.uv);
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                    return float4(0, 0, 0, 1);

                // Aberrazione cromatica: rosso e blu leggermente sfasati in orizzontale
                float2 ab = float2(_SourceSize.z * _Aberration, 0);
                float3 col;
                col.r = tex2D(_MainTex, uv + ab).r;
                col.g = tex2D(_MainTex, uv).g;
                col.b = tex2D(_MainTex, uv - ab).b;

                // Glow: media di 4 campioni vicini, aggiunta solo dove l'immagine è luminosa
                if (_Glow > 0.001)
                {
                    float2 o = _SourceSize.zw * 2.5;
                    float3 blur = tex2D(_MainTex, uv + float2(o.x, 0)).rgb
                                + tex2D(_MainTex, uv - float2(o.x, 0)).rgb
                                + tex2D(_MainTex, uv + float2(0, o.y)).rgb
                                + tex2D(_MainTex, uv - float2(0, o.y)).rgb;
                    blur *= 0.25;
                    col += max(blur - 0.12, float3(0, 0, 0)) * _Glow;
                }

                // Scanline: luminose al centro di ogni riga logica, scure sul confine
                // (attenzione: "line" è una parola riservata in HLSL, per questo la variabile si chiama "scan")
                float scan = 0.5 - 0.5 * cos(uv.y * _Lines * 6.2831853);
                col *= lerp(1.0, 0.5 + 0.5 * scan, _Scanline);
                col *= 1.0 + _Scanline * 0.3;

                // Vignetta
                if (_Vignette > 0.001)
                {
                    float2 vv = uv * (1.0 - uv.yx);
                    col *= pow(saturate(vv.x * vv.y * 16.0), 0.25 * _Vignette);
                }

                // Bordo morbido dello schermo curvo (solo se c'è curvatura)
                if (_Curvature > 0.0001)
                {
                    float2 edge = smoothstep(float2(0, 0), float2(0.004, 0.004), uv)
                                * smoothstep(float2(0, 0), float2(0.004, 0.004), 1.0 - uv);
                    col *= edge.x * edge.y;
                }

                return float4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
