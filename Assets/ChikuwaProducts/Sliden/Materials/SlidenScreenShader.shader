﻿Shader "ChikuwaProducts/SlidenScreenShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [Toggle(APPLY_GAMMA)] _ApplyGamma("Apply Gamma", Float) = 0
    }
    SubShader
    {
        Tags {
            "RenderType"="Opaque"
            "ForceNoShadowCasting" = "True"
        }
        
        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile APPLY_GAMMA_OFF APPLY_GAMMA

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);
            #if APPLY_GAMMA
                col.rgb = pow(col.rgb, 2.2);
            #endif
                return col;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
