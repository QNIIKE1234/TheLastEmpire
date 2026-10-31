Shader "Custom/AtmosphericDustMote"
{
    Properties
    {
        _TintColor("Tint Color", Color) = (0.55, 0.50, 0.44, 0.60)
        _Softness("Edge Softness", Range(0.1, 3.0)) = 1.2
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
            Name "PM25DustForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha // Alpha blend for physical PM 2.5 dust & ash flakes
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
                // Organic ash flake & PM 2.5 soot particulate shape (slightly irregular flake)
                float2 center = input.uv - 0.5;
                float dist = length(center) * 2.0;

                float angle = atan2(center.y, center.x);
                float wobble = sin(angle * 3.0 + 1.2) * cos(angle * 2.0 - 0.5) * 0.16;
                float radius = 1.0 + wobble;

                // Soft edge falloff with micro-particulate core
                float falloff = smoothstep(radius, radius * 0.25, dist);
                falloff = pow(falloff, _Softness);

                float alpha = falloff * input.color.a * _TintColor.a;
                half3 rgb = input.color.rgb * _TintColor.rgb;

                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
