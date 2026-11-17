Shader "Custom/WorldSpaceGroundShader"
{
    Properties
    {
        _BaseMap("Base Map (Albedo)", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Tiling("Tiling Factor", Float) = 0.05
        _Saturation("Saturation", Range(0, 1)) = 1
        [HideInInspector] _BiomeFadeWidth("Biome Fade Width", Float) = 3
        [HideInInspector] _BiomeEdgeBlend("Biome Edge Blend", Vector) = (0, 0, 0, 0)
        [HideInInspector] _GroundBounds("Ground Bounds", Vector) = (0, 0, 0, 0)
        [HideInInspector] _NorthBiomeMap("North Biome Map", 2D) = "white" {}
        [HideInInspector] _EastBiomeMap("East Biome Map", 2D) = "white" {}
        [HideInInspector] _SouthBiomeMap("South Biome Map", 2D) = "white" {}
        [HideInInspector] _WestBiomeMap("West Biome Map", 2D) = "white" {}
        [HideInInspector] _NorthBiomeColor("North Biome Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _EastBiomeColor("East Biome Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _SouthBiomeColor("South Biome Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _WestBiomeColor("West Biome Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _BiomeNeighborTiling("Biome Neighbor Tiling", Vector) = (0.1, 0.1, 0.1, 0.1)
        [HideInInspector] _BiomeNeighborSaturation("Biome Neighbor Saturation", Vector) = (1, 1, 1, 1)
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
            Texture2D _NorthBiomeMap;
            Texture2D _EastBiomeMap;
            Texture2D _SouthBiomeMap;
            Texture2D _WestBiomeMap;

            // Global ambient tint driven by DayNightManager
            half4 _GlobalAmbientTint;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _Tiling;
                half _Saturation;
                float _BiomeFadeWidth;
                float4 _BiomeEdgeBlend;
                float4 _GroundBounds;
                half4 _NorthBiomeColor;
                half4 _EastBiomeColor;
                half4 _SouthBiomeColor;
                half4 _WestBiomeColor;
                float4 _BiomeNeighborTiling;
                half4 _BiomeNeighborSaturation;
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

                half luminance = dot(albedo.rgb, half3(0.299, 0.587, 0.114));
                albedo.rgb = lerp(half3(luminance, luminance, luminance), albedo.rgb, saturate(_Saturation));

                // Blend each edge toward its actual neighboring biome. Both tiles use the same
                // world-space UVs and meet at an equal mix at their shared boundary.
                float fadeWidth = max(_BiomeFadeWidth, 0.001);
                float northFade = (1.0 - smoothstep(0.0, fadeWidth, max(_GroundBounds.w - input.positionWS.z, 0.0))) * _BiomeEdgeBlend.x;
                float eastFade  = (1.0 - smoothstep(0.0, fadeWidth, max(_GroundBounds.y - input.positionWS.x, 0.0))) * _BiomeEdgeBlend.y;
                float southFade = (1.0 - smoothstep(0.0, fadeWidth, max(input.positionWS.z - _GroundBounds.z, 0.0))) * _BiomeEdgeBlend.z;
                float westFade  = (1.0 - smoothstep(0.0, fadeWidth, max(input.positionWS.x - _GroundBounds.x, 0.0))) * _BiomeEdgeBlend.w;
                float edgeFade = saturate(max(max(northFade, eastFade), max(southFade, westFade)));

                half3 neighboringAlbedo = 0;
                float neighborWeight = 0.0;
                if (northFade > 0.001)
                {
                    half3 neighbor = _NorthBiomeMap.Sample(sampler_BaseMap, input.positionWS.xz * _BiomeNeighborTiling.x).rgb * _NorthBiomeColor.rgb;
                    half luminance = dot(neighbor, half3(0.299, 0.587, 0.114));
                    neighbor = lerp(half3(luminance, luminance, luminance), neighbor, _BiomeNeighborSaturation.x);
                    neighboringAlbedo += neighbor * northFade;
                    neighborWeight += northFade;
                }
                if (eastFade > 0.001)
                {
                    half3 neighbor = _EastBiomeMap.Sample(sampler_BaseMap, input.positionWS.xz * _BiomeNeighborTiling.y).rgb * _EastBiomeColor.rgb;
                    half luminance = dot(neighbor, half3(0.299, 0.587, 0.114));
                    neighbor = lerp(half3(luminance, luminance, luminance), neighbor, _BiomeNeighborSaturation.y);
                    neighboringAlbedo += neighbor * eastFade;
                    neighborWeight += eastFade;
                }
                if (southFade > 0.001)
                {
                    half3 neighbor = _SouthBiomeMap.Sample(sampler_BaseMap, input.positionWS.xz * _BiomeNeighborTiling.z).rgb * _SouthBiomeColor.rgb;
                    half luminance = dot(neighbor, half3(0.299, 0.587, 0.114));
                    neighbor = lerp(half3(luminance, luminance, luminance), neighbor, _BiomeNeighborSaturation.z);
                    neighboringAlbedo += neighbor * southFade;
                    neighborWeight += southFade;
                }
                if (westFade > 0.001)
                {
                    half3 neighbor = _WestBiomeMap.Sample(sampler_BaseMap, input.positionWS.xz * _BiomeNeighborTiling.w).rgb * _WestBiomeColor.rgb;
                    half luminance = dot(neighbor, half3(0.299, 0.587, 0.114));
                    neighbor = lerp(half3(luminance, luminance, luminance), neighbor, _BiomeNeighborSaturation.w);
                    neighboringAlbedo += neighbor * westFade;
                    neighborWeight += westFade;
                }

                if (neighborWeight > 0.001)
                {
                    neighboringAlbedo /= neighborWeight;
                    albedo.rgb = lerp(albedo.rgb, neighboringAlbedo, edgeFade * 0.5);
                }

                // Ultra-fast tint (1 instruction multiply, zero performance loss)
                half4 tint = (_GlobalAmbientTint.a > 0.01) ? _GlobalAmbientTint : half4(1, 1, 1, 1);
                return albedo * tint;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex vertDepth
            #pragma fragment fragDepth

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct AttributesDepth
            {
                float4 positionOS : POSITION;
            };

            struct VaryingsDepth
            {
                float4 positionCS : SV_POSITION;
            };

            VaryingsDepth vertDepth(AttributesDepth input)
            {
                VaryingsDepth output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 fragDepth(VaryingsDepth input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
