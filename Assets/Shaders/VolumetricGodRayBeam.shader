Shader "Custom/VolumetricGodRayBeam"
{
    Properties
    {
        _BaseColor("Tint Color", Color) = (1.0, 0.88, 0.65, 0.35)
        _Intensity("Ray Intensity", Float) = 1.8
        _SoftDepthFade("Soft Depth Intersection Distance", Float) = 1.5
        _EdgeSoftness("Lateral Edge Softness", Range(0.01, 0.5)) = 0.35
        _NoiseScale("Dust Noise Scale", Float) = 8.0
        _NoiseSpeed("Noise Scroll Speed", Float) = 0.15
        _FalloffPower("Length Falloff Exponent", Range(0.5, 4.0)) = 1.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Transparent+100" 
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "VolumetricBeamForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One // Additive for glowing light beams
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

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
                float4 screenPos    : TEXCOORD1;
                float4 color        : COLOR;
                float3 positionWS   : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Intensity;
                float _SoftDepthFade;
                float _EdgeSoftness;
                float _NoiseScale;
                float _NoiseSpeed;
                float _FalloffPower;
            CBUFFER_END

            // Simple 2D procedural noise for dust particles inside the light beam
            float Hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float SmoothNoise(float2 uv)
            {
                float2 id = floor(uv);
                float2 lv = frac(uv);
                lv = lv * lv * (3.0 - 2.0 * lv); // Hermite curve

                float bl = Hash21(id);
                float br = Hash21(id + float2(1, 0));
                float tl = Hash21(id + float2(0, 1));
                float tr = Hash21(id + float2(1, 1));

                float b = lerp(bl, br, lv.x);
                float t = lerp(tl, tr, lv.x);
                return lerp(b, t, lv.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.uv = input.uv;
                output.color = input.color;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 1. Soft Edge Falloff across width (X axis)
                float edgeLeft  = smoothstep(0.0, _EdgeSoftness, input.uv.x);
                float edgeRight = smoothstep(1.0, 1.0 - _EdgeSoftness, input.uv.x);
                float lateralMask = edgeLeft * edgeRight;

                // 2. Lengthwise Falloff (Y axis) - Bright at top origin, decaying towards ground
                float lengthFalloff = pow(saturate(input.uv.y), _FalloffPower);

                // 3. Subtle Animated Dust Motes / Fog Noise
                float2 noiseUV = input.uv * _NoiseScale + float2(_Time.y * 0.05, _Time.y * _NoiseSpeed);
                float dustNoise = SmoothNoise(noiseUV);
                float dustLayer = lerp(0.85, 1.25, dustNoise);

                // 4. Soft Depth Intersection (Fade cleanly against terrain and props without harsh seam lines)
                float depthFade = 1.0;
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 0.0001);
                float rawDepth = SampleSceneDepth(screenUV);
                if (rawDepth > 0.00001 && rawDepth < 0.99999)
                {
                    float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                    float beamDepth = LinearEyeDepth(input.positionCS.z, _ZBufferParams);
                    float diff = sceneDepth - beamDepth;
                    depthFade = saturate(diff / max(0.01, _SoftDepthFade));
                }

                // Combine all visibility masks
                float combinedAlpha = lateralMask * lengthFalloff * dustLayer * depthFade;

                // Final color composition
                half3 finalRGB = _BaseColor.rgb * _Intensity * combinedAlpha * input.color.rgb;
                half finalA = _BaseColor.a * combinedAlpha * input.color.a;

                return half4(finalRGB, finalA);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
