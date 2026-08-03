Shader "EarthSmasher/MantlePierce"
{
    Properties
    {
        _Color ("Rock Albedo", Color) = (0.13, 0.08, 0.06, 1)
        _MoltenColor ("Molten Color", Color) = (1, 0.3, 0.05, 1)
        _Emission ("Molten Strength", Range(0, 3)) = 0.5
        _CrackScale ("Crack Scale", Float) = 6.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        Pass
        {
            Name "MANTLE"
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

            float4 _Color;
            float4 _MoltenColor;
            float _Emission;
            float _CrackScale;

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

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash(i);
                float b = Hash(i + float2(1, 0));
                float c = Hash(i + float2(0, 1));
                float d = Hash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float sum = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 4; i++)
                {
                    sum += ValueNoise(p) * amp;
                    p *= 2.03;
                    amp *= 0.5;
                }
                return sum;
            }

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 p = i.uv * _CrackScale;
                float rock = Fbm(p);
                float detail = Fbm(p * 3.7);

                // 갈라진 틈: fbm 능선을 뒤집어 좁은 골짜기를 만든다
                float ridge = abs(rock - 0.5) * 2.0;
                float crack = saturate(1.0 - ridge * 2.6);

                float3 albedo = _Color.rgb * (0.55 + 0.9 * rock) * (0.75 + 0.5 * detail);

                float3 n = normalize(i.worldNormal);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float lit = saturate(dot(n, l)) * 0.45 + 0.4;

                float3 col = albedo * lit;

                // 틈 사이로 새어나오는 용암
                float glow = crack * crack * _Emission;
                col += _MoltenColor.rgb * glow;

                return float4(col, 1);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
