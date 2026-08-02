Shader "EarthSmasher/CloudsSoft"
{
    Properties
    {
        _MainTex ("Cloud Map", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Opacity ("Opacity", Range(0, 2)) = 0.78
        _Softness ("Softness", Range(0.2, 4)) = 0.95
        _Threshold ("Coverage Threshold", Range(0, 0.6)) = 0.22
        _Contrast ("Contrast", Range(0.5, 3)) = 1.55
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.38
        _Volume ("Volume Shade", Range(0, 1)) = 0.45
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+50"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "CLOUDS"
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _Opacity;
            float _Softness;
            float _Threshold;
            float _Contrast;
            float _LightWrap;
            float _Volume;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 tex = tex2D(_MainTex, i.uv);
                float raw = max(tex.r, max(tex.g, tex.b));

                // 얇은 실타래는 남기고, 중간 회색 씻김만 줄여 소용돌이 형태 유지
                float density = saturate((raw - _Threshold) / max(1e-3, 1.0 - _Threshold));
                density = saturate(pow(density, _Softness));
                density = saturate(pow(density, 1.0 / max(0.2, _Contrast)));

                // 덩어리(코어)는 드물게, 얇은 띠 위주
                float core = saturate(smoothstep(0.55, 0.92, density));
                float veil = saturate(pow(density, 1.25));

                float3 n = normalize(i.worldNormal);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float ndotl = saturate(dot(n, lightDir) * (1.0 - _LightWrap) + _LightWrap);

                float shade = lerp(1.0 - _Volume * 0.4, 1.0, ndotl);
                float3 bright = _Color.rgb;
                float3 soft = _Color.rgb * float3(0.82, 0.86, 0.92);
                float3 dayCol = lerp(soft, bright, core) * shade;
                float3 nightCol = _Color.rgb * float3(0.35, 0.4, 0.5);
                float3 col = lerp(nightCol, dayCol, ndotl);

                float alpha = saturate((veil * 0.4 + core * 0.75) * _Opacity * _Color.a);
                return float4(col, alpha);
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}
