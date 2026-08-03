Shader "EarthSmasher/AuroraGlow"
{
    Properties
    {
        _MainTex ("Aurora Oval (RGB=color, A=mask)", 2D) = "black" {}
        _Color ("Tint (A = strength)", Color) = (1, 1, 1, 0.85)
        _EmissionColor ("Glow Color", Color) = (0.5, 1, 0.75, 1)
        _Intensity ("Intensity", Range(0, 4)) = 0.75
        _EdgeGlow ("Limb Glow", Range(0, 4)) = 1.1
        _NightBias ("Day-side Dimming", Range(0, 1)) = 0.92
        _PolarStart ("Polar Fade Start", Range(0, 1)) = 0.28
        _PolarFull ("Polar Fade Full", Range(0, 1)) = 0.48
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
            Name "AURORA"
            Tags { "LightMode" = "ForwardBase" }
            Cull Back
            ZWrite Off
            // 가산 블렌딩 — 지표면을 절대 덮거나 어둡게 할 수 없다.
            Blend One One
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _EmissionColor;
            float _Intensity;
            float _EdgeGlow;
            float _NightBias;
            float _PolarStart;
            float _PolarFull;

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
                float3 objNormal : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.objNormal = normalize(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 tex = tex2D(_MainTex, i.uv);
                float mask = tex.a * _Color.a;

                // 위도 가드 — 타원 밖(중저위도)에는 어떤 경우에도 안 뜬다.
                float absLat = abs(normalize(i.objNormal).y);
                mask *= smoothstep(_PolarStart, _PolarFull, absLat);

                float3 n = normalize(i.worldNormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);

                // 커튼을 비스듬히 볼 때 더 밝음 (림에서 강조)
                float rim = pow(1.0 - saturate(dot(n, viewDir)), 2.2);
                float brightness = 1.0 + rim * _EdgeGlow;

                // 오로라는 밤면에서만 보인다
                float3 lRaw = _WorldSpaceLightPos0.xyz;
                float lLen = length(lRaw);
                float night = lLen > 1e-4
                    ? smoothstep(0.30, -0.12, dot(n, lRaw / lLen))
                    : 1.0;
                brightness *= lerp(1.0 - _NightBias, 1.0, night);

                float3 col = tex.rgb * _Color.rgb * _EmissionColor.rgb * (_Intensity * brightness);
                return float4(col * mask, mask);
            }
            ENDCG
        }
    }
    FallBack Off
}
