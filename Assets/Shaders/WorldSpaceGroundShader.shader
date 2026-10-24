Shader "Custom/WorldSpaceGroundShader"
{
    Properties
    {
        _BaseMap("Base Map (Albedo)", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Tiling("Tiling Factor", Float) = 0.05
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
            };

            Texture2D _BaseMap;
            SamplerState sampler_BaseMap;

            // Global ambient tint driven by DayNightManager
            half4 _GlobalAmbientTint;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _Tiling;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Seamless World-Space Tiling across X and Z plane
                float2 worldUV = input.positionWS.xz * _Tiling;
                half4 albedo = _BaseMap.Sample(sampler_BaseMap, worldUV) * _BaseColor;

                // Ultra-fast tint (1 instruction multiply, zero performance loss)
                half4 tint = (_GlobalAmbientTint.a > 0.01) ? _GlobalAmbientTint : half4(1, 1, 1, 1);
                return albedo * tint;
            }
            ENDHLSL
        }
    }
}
