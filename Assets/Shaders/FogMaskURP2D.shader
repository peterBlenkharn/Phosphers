Shader "Phosphers/FogMaskURP2D"
{
    Properties
    {
        _MaskTex ("Mask (A=seen 0..1)", 2D) = "white" {}
        _FogColor ("Fog Color (RGB)", Color) = (0.02, 0.02, 0.03, 1)
        _Opacity  ("Opacity Multiplier", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float4 col : COLOR;
            };

            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);
            float4 _FogColor;
            float  _Opacity;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.uv  = v.uv;
                o.col = v.color;
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                // Alpha8 texture => sample .a; our CPU buffer stores seen amount 0..1
                half seen = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv).a;
                half fogA = saturate((1.0h - seen) * _Opacity); // unseen => opaque, seen => transparent
                return half4(_FogColor.rgb, fogA);
            }
            ENDHLSL
        }
    }
}
