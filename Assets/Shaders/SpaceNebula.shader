Shader "EarthSmasher/SpaceNebula"
{
    Properties
    {
        _MainTex ("Soft Mask", 2D) = "white" {}
        _Color ("Color", Color) = (0.4, 0.25, 0.8, 0.35)
        _Intensity ("Intensity", Range(0, 4)) = 1.2
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One One
            Lighting Off
            Fog { Mode Off }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _Color;
            float _Intensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float m = tex2D(_MainTex, i.uv).r;
                float3 rgb = _Color.rgb * m * _Intensity * _Color.a;
                return fixed4(rgb, 0);
            }
            ENDCG
        }
    }
    Fallback Off
}
