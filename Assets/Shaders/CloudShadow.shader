Shader "EarthSmasher/CloudShadow"
{
    Properties
    {
        _MainTex ("Cloud Map", 2D) = "white" {}
        _Strength ("Strength", Range(0, 1)) = 0.42
        _Threshold ("Threshold", Range(0, 0.6)) = 0.2
        _Softness ("Softness", Range(0.2, 4)) = 1.1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+45"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "CLOUD_SHADOW"
            Cull Back
            ZWrite Off
            // 표면을 살짝 어둡게 — 구름 아래 그림자
            Blend DstColor Zero
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Strength;
            float _Threshold;
            float _Softness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 tex = tex2D(_MainTex, i.uv);
                float raw = (tex.a < 0.995 && tex.a > 0.001) ? tex.a : tex.r;
                float d = saturate((raw - _Threshold) / max(1e-3, 1.0 - _Threshold));
                d = saturate(pow(d, _Softness));
                float shade = 1.0 - d * _Strength;
                return float4(shade, shade, shade, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
