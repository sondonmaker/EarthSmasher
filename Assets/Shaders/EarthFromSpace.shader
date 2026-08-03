Shader "EarthSmasher/EarthFromSpace"
{
    Properties
    {
        _MainTex ("Day Map", 2D) = "white" {}
        _NightTex ("Night Lights", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Exposure ("Exposure", Range(0.2, 2)) = 1.0
        _Contrast ("Contrast", Range(0.5, 2)) = 1.05
        _Terminator ("Terminator Softness", Range(0.05, 0.8)) = 0.22
        _NightIntensity ("Night Lights", Range(0, 3)) = 1.0
        _AmbientFloor ("Ambient Floor", Range(0, 0.2)) = 0.05
        _PierceCount ("Pierce Count", Int) = 0
        _PierceEdge ("Pierce Molten Edge", Float) = 0.04
        _MoltenColor ("Molten Color", Color) = (1, 0.28, 0.05, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        Pass
        {
            Name "EARTH"
            Tags { "LightMode" = "ForwardBase" }
            Cull Off
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            #define MAX_PIERCE 16

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _NightTex;
            float4 _Color;
            float _Exposure;
            float _Contrast;
            float _Terminator;
            float _NightIntensity;
            float _AmbientFloor;

            int _PierceCount;
            float _PierceEdge;
            float4 _MoltenColor;
            // xyz = 오브젝트 공간 축, w = 오브젝트 공간 반지름.
            // 오브젝트 공간이라 지구가 자전해도 구멍이 표면에 붙어 있는다.
            float4 _PierceAxes[MAX_PIERCE];

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
                float3 objPos : TEXCOORD2;
            };

            float DistToAxis(float3 p, float3 axis)
            {
                float3 ax = normalize(axis);
                return length(p - ax * dot(p, ax));
            }

            // x = inHole (1 clip), y = molten amount 0..1
            float2 EvaluatePierce(float3 objPos)
            {
                float2 acc = float2(0, 0);
                int count = min(_PierceCount, MAX_PIERCE);
                for (int i = 0; i < count; i++)
                {
                    float radius = _PierceAxes[i].w;
                    if (radius <= 1e-5)
                        continue;

                    float d = DistToAxis(objPos, _PierceAxes[i].xyz);
                    if (d < radius)
                    {
                        acc.x = 1;
                        continue;
                    }

                    float edge = max(_PierceEdge, 1e-4);
                    float m = 1.0 - saturate((d - radius) / edge);
                    m = m * m * (3.0 - 2.0 * m);
                    acc.y = max(acc.y, m);
                }
                return acc;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.objPos = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 pierce = EvaluatePierce(i.objPos);
                if (pierce.x > 0.5)
                    clip(-1);

                float3 day = tex2D(_MainTex, i.uv).rgb * _Color.rgb;
                day = saturate(pow(max(day, 1e-4), _Contrast) * _Exposure);

                float3 night = tex2D(_NightTex, i.uv).rgb * _NightIntensity;

                float3 n = normalize(i.worldNormal);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float ndotl = dot(n, l);

                float dayAmt = smoothstep(-_Terminator, _Terminator * 0.85, ndotl);
                float lit = saturate(ndotl);
                float3 sun = _LightColor0.rgb * (0.25 + 0.75 * lit);

                float3 col = day * (sun * dayAmt + _AmbientFloor) + night * (1.0 - dayAmt);

                // 구멍 가장자리: 식은 암석 → 벌겋게 달아오른 테두리
                float molten = pierce.y;
                float3 charred = col * 0.18;
                col = lerp(col, charred, saturate(molten * 1.4));
                float3 hot = _MoltenColor.rgb * (1.2 + molten * 2.6);
                col = lerp(col, hot, saturate((molten - 0.45) * 1.9));

                return float4(col, 1);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
