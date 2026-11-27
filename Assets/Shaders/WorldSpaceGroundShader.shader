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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NorthBiomeMap);
            SAMPLER(sampler_NorthBiomeMap);
            TEXTURE2D(_EastBiomeMap);
            SAMPLER(sampler_EastBiomeMap);
            TEXTURE2D(_SouthBiomeMap);
            SAMPLER(sampler_SouthBiomeMap);
            TEXTURE2D(_WestBiomeMap);
            SAMPLER(sampler_WestBiomeMap);

            // Global ambient tint driven by DayNightManager & weather globals driven by WeatherManager
            half4 _GlobalAmbientTint;
            half _GlobalWetness;
            half _GlobalSnowAmount;
            int _GlobalCampfireCount;
            float4 _GlobalCampfireData[4]; // xyz = campfire world position, w = melt radius

            float GroundSnowHash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float GroundSnowNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = GroundSnowHash(i);
                float b = GroundSnowHash(i + float2(1.0, 0.0));
                float c = GroundSnowHash(i + float2(0.0, 1.0));
                float d = GroundSnowHash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

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
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, worldUV) * _BaseColor;

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

                half3 neighboringAlbedo = half3(0.0, 0.0, 0.0);
                float neighborWeight = 0.0;
                if (northFade > 0.001)
                {
                    half3 neighbor = SAMPLE_TEXTURE2D(_NorthBiomeMap, sampler_NorthBiomeMap, input.positionWS.xz * _BiomeNeighborTiling.x).rgb * _NorthBiomeColor.rgb;
                    half nLum = dot(neighbor, half3(0.299, 0.587, 0.114));
                    neighbor = lerp(half3(nLum, nLum, nLum), neighbor, _BiomeNeighborSaturation.x);
                    neighboringAlbedo += neighbor * northFade;
                    neighborWeight += northFade;
                }
                if (eastFade > 0.001)
                {
                    half3 neighbor = SAMPLE_TEXTURE2D(_EastBiomeMap, sampler_EastBiomeMap, input.positionWS.xz * _BiomeNeighborTiling.y).rgb * _EastBiomeColor.rgb;
                    half eLum = dot(neighbor, half3(0.299, 0.587, 0.114));
                    neighbor = lerp(half3(eLum, eLum, eLum), neighbor, _BiomeNeighborSaturation.y);
                    neighboringAlbedo += neighbor * eastFade;
                    neighborWeight += eastFade;
                }
                if (southFade > 0.001)
                {
                    half3 neighbor = SAMPLE_TEXTURE2D(_SouthBiomeMap, sampler_SouthBiomeMap, input.positionWS.xz * _BiomeNeighborTiling.z).rgb * _SouthBiomeColor.rgb;
                    half sLum = dot(neighbor, half3(0.299, 0.587, 0.114));
                    neighbor = lerp(half3(sLum, sLum, sLum), neighbor, _BiomeNeighborSaturation.z);
                    neighboringAlbedo += neighbor * southFade;
                    neighborWeight += southFade;
                }
                if (westFade > 0.001)
                {
                    half3 neighbor = SAMPLE_TEXTURE2D(_WestBiomeMap, sampler_WestBiomeMap, input.positionWS.xz * _BiomeNeighborTiling.w).rgb * _WestBiomeColor.rgb;
                    half wLum = dot(neighbor, half3(0.299, 0.587, 0.114));
                    neighbor = lerp(half3(wLum, wLum, wLum), neighbor, _BiomeNeighborSaturation.w);
                    neighboringAlbedo += neighbor * westFade;
                    neighborWeight += westFade;
                }

                if (neighborWeight > 0.001)
                {
                    neighboringAlbedo /= neighborWeight;
                    albedo.rgb = lerp(albedo.rgb, neighboringAlbedo, edgeFade * 0.5);
                }

                // Subtle wet-ground darkening & cool damp tint during Rain/Thunderstorm (driven by WeatherManager)
                half wetness = saturate(_GlobalWetness);
                if (wetness > 0.001)
                {
                    albedo.rgb *= lerp(half3(1.0, 1.0, 1.0), half3(0.80, 0.85, 0.93), wetness * 0.72);
                }

                // World-Space Snow blanket accumulation during Snow/Blizzard/Winter (melts around active Campfires!)
                half snowAmt = saturate(_GlobalSnowAmount);
                if (snowAmt > 0.001)
                {
                    float sn1 = GroundSnowNoise(input.positionWS.xz * 0.28);
                    float sn2 = GroundSnowNoise(input.positionWS.xz * 1.15 + 13.7);
                    float snowPatch = sn1 * 0.65 + sn2 * 0.35;
                    half snowBlend = smoothstep(1.0 - snowAmt * 1.32, 1.0 - snowAmt * 0.25 + 0.16, snowPatch) * smoothstep(0.0, 0.20, snowAmt);

                    for (int c = 0; c < 4; c++)
                    {
                        if (c >= _GlobalCampfireCount) break;
                        float4 cf = _GlobalCampfireData[c];
                        float dist = length(input.positionWS.xz - cf.xz);
                        snowBlend *= smoothstep(cf.w * 0.40, cf.w, dist);
                    }

                    half3 snowTint = lerp(half3(0.82, 0.88, 0.96), half3(0.95, 0.97, 1.0), snowPatch);
                    albedo.rgb = lerp(albedo.rgb, snowTint, snowBlend * 0.92);
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
