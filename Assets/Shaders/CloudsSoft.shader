Shader "EarthSmasher/CloudsSoft"
{
    Properties
    {
        _MainTex ("Cloud Map", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Opacity ("Opacity", Range(0, 1.5)) = 0.85
        _Softness ("Softness", Range(0.2, 4)) = 1.35
        _LightWrap ("Light Wrap", Range(0, 1)) = 0.35
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
                density = pow(saturate(density), _Softness);

                float3 n = normalize(i.worldNormal);
                float3 lightDir = normalize(_WorldSpaceLightPos0.xyz);
                float ndotl = saturate(dot(n, lightDir) * (1.0 - _LightWrap) + _LightWrap);

                float3 col = _Color.rgb * lerp(0.35, 1.0, ndotl);
                float alpha = density * _Opacity * _Color.a;
                return float4(col, alpha);
            }
            ENDCG
        }
    }
    FallBack "Transparent/Diffuse"
}
