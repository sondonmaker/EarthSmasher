Shader "EarthSmasher/AtmosphereFresnel"
{
    Properties
    {
        _Color ("Atmosphere Color", Color) = (0.55, 0.78, 1.0, 1)
        _RimPower ("Rim Power", Range(0.4, 8)) = 2.2
        _Intensity ("Intensity", Range(0, 6)) = 2.2
        _HorizonBoost ("Horizon Boost", Range(0, 2)) = 0.65
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Name "ATMOSPHERE"
            Cull Front
            ZWrite Off
            Blend One One
            Lighting Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            float _RimPower;
            float _Intensity;
            float _HorizonBoost;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                // Cull Front → 안쪽 면에서 바깥 normal 반대 방향 사용
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float ndotv = saturate(dot(viewDir, -n));
                float rim = pow(1.0 - ndotv, _RimPower);

                // 지평선(가장자리) 살짝 더 밝게
                float horizon = pow(rim, 0.65) * _HorizonBoost;
                float glow = (rim + horizon) * _Intensity;

                float3 col = _Color.rgb * glow;
                return float4(col, glow);
            }
            ENDCG
        }
    }
    FallBack Off
}
