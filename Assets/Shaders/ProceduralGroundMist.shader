Shader "Custom/ProceduralGroundMist"
{
    Properties
    {
        _MistColor("Mist Color", Color) = (0.98, 0.92, 0.74, 0.22)
        _NoiseScale("Noise Scale", Float) = 0.12
        _ScrollSpeed("Scroll Speed (Layer 1 & 2)", Vector) = (0.020, 0.010, -0.015, 0.018)
        _Density("Mist Density", Range(0.1, 1.5)) = 0.85
        _Softness("Edge Softness", Range(0.1, 3.0)) = 1.0
    }
    SubShader
    {
        Tags 
        { 
            "Queue" = "Transparent+120" 
            "RenderType" = "Transparent" 
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "GroundMistPass"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float2 uv           : TEXCOORD1;
                float4 screenPos    : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _MistColor;
                float4 _ScrollSpeed;
                float _NoiseScale;
                float _Density;
                float _Softness;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            // Procedural smooth noise function
            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(Hash(i + float2(0.0, 0.0)), Hash(i + float2(1.0, 0.0)), u.x),
                            lerp(Hash(i + float2(0.0, 1.0)), Hash(i + float2(1.0, 1.0)), u.x), u.y);
            }

            float Fbm(float2 p)
            {
                float val = 0.0;
                val += 0.500 * ValueNoise(p); p *= 2.02;
                val += 0.250 * ValueNoise(p); p *= 2.03;
                val += 0.125 * ValueNoise(p);
                return val;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 worldUV = input.positionWS.xz * _NoiseScale;
                float time = _Time.y;

                // Dual scrolling mist noise layers (organic rolling morning fog)
                float2 uv1 = worldUV + _ScrollSpeed.xy * time;
                float2 uv2 = worldUV * 1.35 + _ScrollSpeed.zw * time;

                float n1 = Fbm(uv1);
                float n2 = Fbm(uv2);
                // Soft organic rolling mist wisps
                float mistNoise = saturate((n1 * 0.55 + n2 * 0.45 - 0.12) / 0.88);

                // Seamless radial mask (fills entire screen, only softly fades at the far outer perimeter)
                float2 centerUV = input.uv - 0.5;
                float dist = length(centerUV) * 2.0;
                float radialFade = smoothstep(1.0, 0.80, dist);

                // Depth intersection fade with scene objects (soft contact with floor and props)
                float depthFade = 1.0;
                float2 screenUV = input.screenPos.xy / max(input.screenPos.w, 0.0001);
                float rawDepth = SampleSceneDepth(screenUV);
                if (rawDepth > 0.00001 && rawDepth < 0.99999)
                {
                    float sceneEyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                    float surfaceEyeDepth = input.screenPos.w;
                    float diff = sceneEyeDepth - surfaceEyeDepth;
                    // Soft contact with floor; also fades smoothly if occluded
                    depthFade = saturate(diff * 5.0);
                }

                // Sample real directional light shadow (buildings and obstacles naturally block sunlight!)
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                float shadow = mainLight.shadowAttenuation;

                // Where sun hits: warm golden morning sun. Where buildings block: natural cool ambient shadow.
                half3 sunLitColor = _MistColor.rgb * mainLight.color * 1.25;
                half3 shadowColor = _MistColor.rgb * half3(0.25, 0.28, 0.38); // Cool ambient shadow (zero golden sunlight!)
                half3 finalColor  = lerp(shadowColor, sunLitColor, shadow);

                // In shadow: mist does not catch direct sunlight (faint ambient opacity); in open sun: bright golden mist
                float sunScatter = lerp(0.20, 1.0, shadow);
                float alpha = saturate(mistNoise * _Density * sunScatter * radialFade * depthFade * _MistColor.a);

                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}
