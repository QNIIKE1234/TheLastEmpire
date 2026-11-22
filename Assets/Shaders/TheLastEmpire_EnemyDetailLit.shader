Shader "TheLastEmpire/URP/EnemyDetailedLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map (Optional UV Texture)", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Color("Legacy Color", Color) = (1, 1, 1, 1)
        _UseBaseMap("Use UV Base Map", Float) = 0

        [Header(Triplanar PBR Surface Detail)]
        _DetailMap("Triplanar Detail Albedo", 2D) = "gray" {}
        _DetailBumpMap("Triplanar Detail Normal", 2D) = "bump" {}
        _DetailScale("Detail Tiling Scale", Float) = 3.2
        _DetailStrength("Detail Albedo Strength", Range(0, 1.5)) = 0.72
        _BumpScale("Detail Normal Strength", Range(0, 2.5)) = 1.05
        _CavityStrength("Micro Cavity Darkening", Range(0, 1)) = 0.25

        [Header(PBR Surface)]
        _Smoothness("Smoothness", Range(0, 1)) = 0.28
        _Metallic("Metallic", Range(0, 1)) = 0.0
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)

        [Header(Transparency Compatibility)]
        [HideInInspector] _Surface("Surface Type", Float) = 0
        [HideInInspector] _SrcBlend("Src Blend", Float) = 1
        [HideInInspector] _DstBlend("Dst Blend", Float) = 0
        [HideInInspector] _ZWrite("ZWrite", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionOS  : TEXCOORD2;
                float3 normalOS    : TEXCOORD3;
                float2 uv          : TEXCOORD4;
                float  fogFactor   : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);       SAMPLER(sampler_BaseMap);
            TEXTURE2D(_DetailMap);     SAMPLER(sampler_DetailMap);
            TEXTURE2D(_DetailBumpMap); SAMPLER(sampler_DetailBumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _Color;
                float  _UseBaseMap;
                float  _DetailScale;
                half   _DetailStrength;
                half   _BumpScale;
                half   _CavityStrength;
                half   _Smoothness;
                half   _Metallic;
                half4  _EmissionColor;
                float  _Surface;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normInputs.normalWS);
                output.positionOS = input.positionOS.xyz;
                output.normalOS = normalize(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half3 SampleTriplanarAlbedo(float3 posOS, half3 weights, float scale)
            {
                half3 cx = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, posOS.zy * scale).rgb;
                half3 cy = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, posOS.xz * scale).rgb;
                half3 cz = SAMPLE_TEXTURE2D(_DetailMap, sampler_DetailMap, posOS.xy * scale).rgb;
                return cx * weights.x + cy * weights.y + cz * weights.z;
            }

            float3 SampleTriplanarNormalWS(float3 posOS, float3 normalOS, float3 normalWS, half3 weights, float scale, half bumpScale)
            {
                if (bumpScale <= 0.001h) return normalWS;

                // Whiteout-style triplanar normal perturbation in object space -> world space
                float3 tnx = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailBumpMap, sampler_DetailBumpMap, posOS.zy * scale), bumpScale);
                float3 tny = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailBumpMap, sampler_DetailBumpMap, posOS.xz * scale), bumpScale);
                float3 tnz = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailBumpMap, sampler_DetailBumpMap, posOS.xy * scale), bumpScale);

                float3 nx = float3(0.0, tnx.y, tnx.x);
                float3 ny = float3(tny.x, 0.0, tny.y);
                float3 nz = float3(tnz.x, tnz.y, 0.0);

                float3 deltaOS = nx * weights.x + ny * weights.y + nz * weights.z;
                float3 perturbedOS = normalize(normalOS + deltaOS);
                return normalize(TransformObjectToWorldNormal(perturbedOS));
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float3 nOS = normalize(input.normalOS);
                float3 nWS = normalize(input.normalWS);
                half3 triWeights = pow(abs(half3(nOS)), 4.0h);
                triWeights /= max(triWeights.x + triWeights.y + triWeights.z, 0.0001h);

                // Base color (optionally multiplied with UV-mapped painted face/tattoo texture)
                half4 baseCol = _BaseColor;
                if (_UseBaseMap > 0.5)
                {
                    baseCol *= SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                }

                // Triplanar organic/metal/fur/cloth micro-detail breaks up flat Play-Doh shading!
                half3 detailSample = SampleTriplanarAlbedo(input.positionOS, triWeights, _DetailScale);
                half detailVal = dot(detailSample, half3(0.333h, 0.333h, 0.333h));
                // Centered contrast modulation around 0.5 so base hue stays faithful while gaining crisp grit & texture
                half3 detailMod = lerp(half3(1.0h, 1.0h, 1.0h), detailSample * 2.0h, _DetailStrength);
                half3 albedo = saturate(baseCol.rgb * detailMod);

                // Subtle crevice/cavity darkening in micro-recesses for realistic depth
                half cavity = lerp(1.0h - _CavityStrength, 1.0h, saturate(detailVal * 1.45h));
                albedo *= cavity;

                // Perturbed micro-normal + micro-roughness variation
                float3 finalNormalWS = SampleTriplanarNormalWS(input.positionOS, nOS, nWS, triWeights, _DetailScale, _BumpScale);
                half finalSmoothness = saturate(_Smoothness * lerp(0.68h, 1.22h, detailVal));

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = finalNormalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(finalNormalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = _Metallic;
                surfaceData.specular = half3(0.0h, 0.0h, 0.0h);
                surfaceData.smoothness = finalSmoothness;
                surfaceData.normalTS = half3(0, 0, 1);
                surfaceData.occlusion = cavity;
                surfaceData.emission = _EmissionColor.rgb;
                surfaceData.alpha = baseCol.a;

                half4 finalColor = UniversalFragmentPBR(inputData, surfaceData);
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);
                return finalColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirWS = _LightDirection;
                #endif
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirWS));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                return 0;
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
            #pragma target 3.5
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthOnlyVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 DepthOnlyFragment(Varyings input) : SV_TARGET
            {
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Lit"
}
