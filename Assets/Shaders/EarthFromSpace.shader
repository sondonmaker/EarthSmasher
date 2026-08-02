Shader "EarthSmasher/EarthFromSpace"
{
    Properties
    {
        _MainTex ("Day Map", 2D) = "white" {}
        _NightTex ("Night Lights", 2D) = "black" {}
        _Color ("Tint", Color) = (0.85, 0.88, 0.9, 1)
        _Exposure ("Exposure", Range(0.2, 2)) = 0.78
        _Contrast ("Contrast", Range(0.5, 2)) = 1.25
        _Terminator ("Terminator Softness", Range(0.05, 0.8)) = 0.28
        _NightIntensity ("Night Lights", Range(0, 3)) = 1.1
        _AmbientFloor ("Ambient Floor", Range(0, 0.2)) = 0.02
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        Pass
        {
            Name "EARTH"
            Tags { "LightMode" = "ForwardBase" }
            Cull Back
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _NightTex;
            float4 _Color;
            float _Exposure;
            float _Contrast;
            float _Terminator;
            float _NightIntensity;
            float _AmbientFloor;

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
                float3 day = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                // 시네마틱 대비 — 바다는 더 어둡고, 대륙 질감은 살림
                day = saturate(pow(max(day, 1e-4), _Contrast) * _Exposure);

                float3 night = tex2D(_NightTex, i.uv).rgb * _NightIntensity;

                float3 n = normalize(i.worldNormal);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float ndotl = dot(n, l);

                // 아폴로/달 시점: 부드러운 터미네이터 + 거의 검은 밤
                float dayAmt = smoothstep(-_Terminator, _Terminator * 0.85, ndotl);
                float lit = saturate(ndotl);
                float3 sun = _LightColor0.rgb * (0.15 + 0.85 * lit);

                float3 col = day * (sun * dayAmt + _AmbientFloor) + night * (1.0 - dayAmt);
                return float4(col, 1);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
