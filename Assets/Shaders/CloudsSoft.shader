Shader "EarthSmasher/CloudsSoft"
{
    Properties
    {
        _MainTex ("Cloud Map (RGB + Alpha)", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Opacity ("Opacity", Range(0, 2)) = 0.85
        _AlphaBoost ("Alpha Boost", Range(0.2, 3)) = 1.15
        _AlphaGamma ("Alpha Gamma", Range(0.4, 2.5)) = 1.35
        _CoverageCut ("Coverage Cut", Range(0, 0.5)) = 0.08
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.35
        _Volume ("Volume Shade", Range(0, 1)) = 0.35
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
            float _AlphaBoost;
            float _AlphaGamma;
            float _CoverageCut;
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
                // 알파 맵 우선, 없으면 휘도 폴백
                float aSrc = tex.a;
                float lum = max(tex.r, max(tex.g, tex.b));
                // 알파 맵이면 알파, JPG 폴백이면 휘도
                float density = (aSrc < 0.995 && aSrc > 0.001) ? aSrc : lum;

                density = saturate((density - _CoverageCut) / max(1e-3, 1.0 - _CoverageCut));
                density = saturate(pow(density * _AlphaBoost, _AlphaGamma));

                // 넓은 뭉침(뱅크) 깨기: 중간 밀도는 거의 투명, 가장 진한 핵심만 남김
                float peak = saturate(pow(density, 2.4));
                float broken = lerp(density * 0.28, peak, peak);

                float thick = saturate(smoothstep(0.45, 0.95, broken));

                float3 n = normalize(i.worldNormal);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float ndotl = saturate(dot(n, lightDir) * (1.0 - _LightWrap) + _LightWrap);

                float shade = lerp(1.0 - _Volume * 0.45, 1.0, ndotl);
                float3 dayCol = _Color.rgb * lerp(float3(0.86, 0.89, 0.94), float3(1,1,1), thick) * shade;
                float3 nightCol = _Color.rgb * float3(0.32, 0.38, 0.48);
                float3 col = lerp(nightCol, dayCol, ndotl);

                float alpha = saturate(broken * _Opacity * _Color.a);
                return float4(col, alpha);
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}
