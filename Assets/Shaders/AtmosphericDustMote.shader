Shader "Custom/AtmosphericDustMote"
{
    Properties
    {
        _TintColor("Tint Color", Color) = (1.0, 0.92, 0.72, 0.8)
        _GlowIntensity("Glow Intensity", Float) = 2.0
    }
    SubShader
    {
        Tags 
        { 
            "Queue" = "Transparent+150" 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "DustMoteForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One // Additive for glowing illuminated dust motes
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 color        : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TintColor;
                float _GlowIntensity;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Soft radial circle (smooth particle falloff from center)
                float2 center = input.uv - 0.5;
                float dist = length(center) * 2.0;
                float circle = saturate(1.0 - dist);
                circle = circle * circle; // Smooth exponential curve

                float alpha = circle * input.color.a * _TintColor.a;
                half3 rgb = input.color.rgb * _TintColor.rgb * (_GlowIntensity * circle);
                return half4(rgb * alpha, 1.0);
            }
            ENDHLSL
        }
    }
}
