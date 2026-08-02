Shader "EarthSmasher/CloudsSoft"
{
    Properties
    {
        _MainTex ("Cloud Map", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Opacity ("Opacity", Range(0, 2)) = 1.25
        _Softness ("Softness", Range(0.2, 4)) = 0.85
        _Threshold ("Coverage Threshold", Range(0, 0.6)) = 0.12
        _Contrast ("Contrast", Range(0.5, 3)) = 1.45
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.45
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
                float density = max(tex.r, max(tex.g, tex.b));

                // 얇은 구름도 살리고 두꺼운 부분은 더 하얗게
                density = saturate((density - _Threshold) / max(1e-3, 1.0 - _Threshold));
                density = saturate(pow(density, _Softness));
                density = saturate(pow(density, 1.0 / max(0.2, _Contrast)));

                float3 n = normalize(i.worldNormal);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float ndotl = saturate(dot(n, lightDir) * (1.0 - _LightWrap) + _LightWrap);

                // 낮 쪽은 밝은 흰 구름, 밤 쪽은 살짝 푸른 회색
                float3 dayCol = _Color.rgb;
                float3 nightCol = _Color.rgb * float3(0.55, 0.62, 0.75);
                float3 col = lerp(nightCol, dayCol, ndotl);
                float alpha = saturate(density * _Opacity * _Color.a);
                return float4(col, alpha);
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}
