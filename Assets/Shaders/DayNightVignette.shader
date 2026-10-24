Shader "Custom/DayNightVignette"
{
    Properties
    {
        _VignetteColor("Night Vignette Color", Color) = (0.01, 0.02, 0.05, 0.95)
        _VignetteRadius("Vignette Radius", Range(0.1, 1.5)) = 0.8
        _VignetteSoftness("Vignette Softness", Range(0.01, 1.0)) = 0.5
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "VignettePass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            float _GlobalDayNightBlend;

            CBUFFER_START(UnityPerMaterial)
                half4 _VignetteColor;
                float _VignetteRadius;
                float _VignetteSoftness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float blend = saturate(_GlobalDayNightBlend);
                if (blend <= 0.02)
                {
                    return half4(0, 0, 0, 0);
                }

                // Center UV around (0, 0)
                float2 centeredUV = (input.uv - 0.5) * 2.0;
                float distSq = dot(centeredUV, centeredUV);

                float radiusSq = _VignetteRadius * _VignetteRadius;
                float vignette = saturate((distSq - radiusSq) / max(0.01, _VignetteSoftness));

                float alpha = vignette * _VignetteColor.a * blend;
                return half4(_VignetteColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
