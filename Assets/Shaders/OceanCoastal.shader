Shader "EarthSmasher/OceanCoastal"
{
    Properties
    {
        _MainTex ("Water Mask (white=ocean)", 2D) = "white" {}
        _DeepColor ("Deep Ocean", Color) = (0.01, 0.08, 0.28, 0.55)
        _ShallowColor ("Shallow / Coast", Color) = (0.05, 0.55, 0.65, 0.4)
        _Gloss ("Gloss", Range(0, 1)) = 0.92
        _FresnelPower ("Fresnel", Range(0.5, 8)) = 3.5
        _SpecIntensity ("Spec", Range(0, 3)) = 1.4
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+40"
            "RenderType" = "Transparent"
        }

        Pass
        {
            Cull Back
            ZWrite On
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _DeepColor;
            float4 _ShallowColor;
            float _Gloss;
            float _FresnelPower;
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
                float mask = tex2D(_MainTex, i.uv).r;
                // 가장자리(얕은 바다) 근사: 마스크 중간값
                float shallow = smoothstep(0.15, 0.55, mask) * (1.0 - smoothstep(0.55, 0.95, mask));
                float ocean = smoothstep(0.2, 0.7, mask);

                float4 water = lerp(_DeepColor, _ShallowColor, saturate(shallow * 1.8));

                float3 n = normalize(i.worldNormal);
                float3 v = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 l = normalize(_WorldSpaceLightPos0.xyz);
                float3 h = normalize(l + v);

                float fresnel = pow(1.0 - saturate(dot(n, v)), _FresnelPower);
                float spec = pow(saturate(dot(n, h)), lerp(16.0, 128.0, _Gloss)) * _SpecIntensity;
                float ndotl = saturate(dot(n, l));

                float3 col = water.rgb * (0.35 + 0.65 * ndotl) + _LightColor0.rgb * spec * fresnel * 0.65;
                // 심해는 거의 불투명 — see-through 방지
                float alpha = ocean * lerp(max(water.a, 0.92), 0.98, fresnel * 0.5);
                return float4(col, alpha);
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}
