Shader "Custom/AtmosphericDustMote"
{
    Properties
    {
        _TintColor("Tint Color", Color) = (0.92, 0.90, 0.88, 0.20)
        _Softness("Edge Softness", Range(0.1, 3.0)) = 1.6
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
            Name "MistForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha // Alpha blend for natural morning mist & atmospheric dust haze
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
                float _Softness;
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
                // Soft, billowing morning mist puff with organic cloud contours
                float2 center = input.uv - 0.5;
                float dist = length(center) * 2.0;

                // Ultra-smooth edge falloff (no hard borders or glowing core)
                float radialFalloff = smoothstep(1.0, 0.0, dist);

                // Subtle organic cloud turbulence for realistic wispy morning mist puffs
                float2 p = center * 4.0;
                float cloudNoise = sin(p.x * 2.3 + p.y * 1.7) * cos(p.y * 2.8 - p.x * 1.5) * 0.18;
                float mistMask = saturate(radialFalloff + cloudNoise * radialFalloff);
                mistMask = pow(mistMask, _Softness);

                float alpha = mistMask * input.color.a * _TintColor.a;
                half3 rgb = input.color.rgb * _TintColor.rgb;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
