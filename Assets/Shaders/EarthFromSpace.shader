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
        _PierceEdge ("Pierce Molten Edge", Float) = 0.35
        _MoltenColor ("Molten Color", Color) = (1, 0.28, 0.05, 1)
        _PierceOrigin0 ("Pierce Origin 0", Vector) = (0,0,0,0)
        _PierceAxis0 ("Pierce Axis 0", Vector) = (0,1,0,0)
        _PierceRadius0 ("Pierce Radius 0", Float) = 0
        _PierceOrigin1 ("Pierce Origin 1", Vector) = (0,0,0,0)
        _PierceAxis1 ("Pierce Axis 1", Vector) = (0,1,0,0)
        _PierceRadius1 ("Pierce Radius 1", Float) = 0
        _PierceOrigin2 ("Pierce Origin 2", Vector) = (0,0,0,0)
        _PierceAxis2 ("Pierce Axis 2", Vector) = (0,1,0,0)
        _PierceRadius2 ("Pierce Radius 2", Float) = 0
        _PierceOrigin3 ("Pierce Origin 3", Vector) = (0,0,0,0)
        _PierceAxis3 ("Pierce Axis 3", Vector) = (0,1,0,0)
        _PierceRadius3 ("Pierce Radius 3", Float) = 0
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
            int _PierceCount;
            float _PierceEdge;
            float4 _MoltenColor;
            float4 _PierceOrigin0;
            float4 _PierceAxis0;
            float _PierceRadius0;
            float4 _PierceOrigin1;
            float4 _PierceAxis1;
            float _PierceRadius1;
            float4 _PierceOrigin2;
            float4 _PierceAxis2;
            float _PierceRadius2;
            float4 _PierceOrigin3;
            float4 _PierceAxis3;
            float _PierceRadius3;

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

            float DistToAxis(float3 worldPos, float3 origin, float3 axis)
            {
                float3 pa = worldPos - origin;
                float3 ax = normalize(axis);
                float3 closest = origin + ax * dot(pa, ax);
                return length(worldPos - closest);
            }

            // x = inHole (1 clip), y = molten amount 0..1
            float2 PierceSample(float3 worldPos, float3 origin, float3 axis, float radius)
            {
                float d = DistToAxis(worldPos, origin, axis);
                if (d < radius)
                    return float2(1, 0);
                float edge = max(_PierceEdge, 1e-4);
                float m = 1.0 - saturate((d - radius) / edge);
                m = m * m * (3.0 - 2.0 * m);
                return float2(0, m);
            }

            float2 EvaluatePierce(float3 worldPos)
            {
                float2 r = float2(0, 0);
                if (_PierceCount < 1) return r;
                if (_PierceCount > 0)
                {
                    float2 s = PierceSample(worldPos, _PierceOrigin0.xyz, _PierceAxis0.xyz, _PierceRadius0);
                    r.x = max(r.x, s.x);
                    r.y = max(r.y, s.y);
                }
                if (_PierceCount > 1)
                {
                    float2 s = PierceSample(worldPos, _PierceOrigin1.xyz, _PierceAxis1.xyz, _PierceRadius1);
                    r.x = max(r.x, s.x);
                    r.y = max(r.y, s.y);
                }
                if (_PierceCount > 2)
                {
                    float2 s = PierceSample(worldPos, _PierceOrigin2.xyz, _PierceAxis2.xyz, _PierceRadius2);
                    r.x = max(r.x, s.x);
                    r.y = max(r.y, s.y);
                }
                if (_PierceCount > 3)
                {
                    float2 s = PierceSample(worldPos, _PierceOrigin3.xyz, _PierceAxis3.xyz, _PierceRadius3);
                    r.x = max(r.x, s.x);
                    r.y = max(r.y, s.y);
                }
                return r;
            }

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
                float2 pierce = EvaluatePierce(i.worldPos);
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

                // 구멍 가장자리 용암(주황/빨강) — 메시 추가 없이 셰이더만
                float molten = pierce.y;
                float3 hot = _MoltenColor.rgb * (1.5 + molten * 2.0);
                col = lerp(col, hot, saturate(molten * 0.95));

                return float4(col, 1);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
