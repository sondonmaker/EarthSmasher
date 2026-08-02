Shader "EarthSmasher/EarthFromSpace"
{
    Properties
    {
        _MainTex ("Day Map", 2D) = "white" {}
        _NightTex ("Night Lights", 2D) = "black" {}
        _WaterTex ("Water Mask", 2D) = "black" {}
        _Color ("Tint", Color) = (0.9, 0.92, 0.94, 1)
        _OceanColor ("Ocean Color", Color) = (0.01, 0.05, 0.14, 1)
        _OceanBlend ("Ocean Blend", Range(0, 1)) = 0.92
        _Exposure ("Exposure", Range(0.2, 2)) = 0.9
        _Contrast ("Contrast", Range(0.5, 2)) = 1.15
        _Terminator ("Terminator Softness", Range(0.05, 0.8)) = 0.26
        _NightIntensity ("Night Lights", Range(0, 3)) = 1.1
        _AmbientFloor ("Ambient Floor", Range(0, 0.2)) = 0.04
        _SpecIntensity ("Ocean Spec", Range(0, 2)) = 0.55
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
            sampler2D _WaterTex;
            float4 _Color;
            float4 _OceanColor;
            float _OceanBlend;
            float _Exposure;
            float _Contrast;
            float _Terminator;
            float _NightIntensity;
            float _AmbientFloor;
            float _SpecIntensity;

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
                float3 worldPos : TEXCOORD2;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 day = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                day = saturate(pow(max(day, 1e-4), _Contrast) * _Exposure);

                // 바다 = 불투명하게 덮음 (별도 반투명 스피어 없음 → see-through 제거)
                float water = tex2D(_WaterTex, i.uv).r;
                water = smoothstep(0.25, 0.75, water);
                float3 ocean = _OceanColor.rgb;
                day = lerp(day, ocean, water * _OceanBlend);

                float3 night = tex2D(_NightTex, i.uv).rgb * _NightIntensity;

                float3 n = normalize(i.worldNormal);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 v = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 h = normalize(l + v);
                float ndotl = dot(n, l);

                float dayAmt = smoothstep(-_Terminator, _Terminator * 0.85, ndotl);
                float lit = saturate(ndotl);
                float3 sun = _LightColor0.rgb * (0.2 + 0.8 * lit);

                float fresnel = pow(1.0 - saturate(dot(n, v)), 4.0);
                float spec = pow(saturate(dot(n, h)), 96.0) * _SpecIntensity * water * dayAmt;

                float3 col = day * (sun * dayAmt + _AmbientFloor)
                           + night * (1.0 - dayAmt)
                           + _LightColor0.rgb * spec * fresnel;
                return float4(col, 1);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
