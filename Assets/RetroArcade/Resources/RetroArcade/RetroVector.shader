// Shader minimale per RetroCanvas: colore per-vertice × alpha dell'atlante dei font.
// Le forme piene campionano un texel bianco, i glifi campionano il loro rettangolo.
// Scritto in CG "classico" senza LightMode: funziona sia nella Built-in Render Pipeline
// sia in URP (anche con il Renderer 2D), dove viene trattato come pass "SRPDefaultUnlit".
// Sta in una cartella Resources così è sempre incluso nelle build.
Shader "RetroArcade/Vector"
{
    Properties
    {
        _MainTex ("Font atlas", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float4 color : COLOR;
                float2 uv    : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float4 c = i.color;
                c.a *= tex2D(_MainTex, i.uv).a;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
