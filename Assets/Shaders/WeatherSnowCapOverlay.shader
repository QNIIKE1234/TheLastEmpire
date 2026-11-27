Shader "Custom/WeatherSnowCapOverlay"
{
    Properties
    {
        _SnowColor("Snow Base Color", Color) = (0.93, 0.96, 1.0, 1.0)
        _SnowShadowColor("Snow Cool Shade", Color) = (0.74, 0.83, 0.94, 1.0)
        _NormalThreshold("Upward Normal Threshold", Range(0.1, 0.9)) = 0.45
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+15" }
        LOD 100

        Pass
        {
            Name "SnowCapOverlay"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back
            Offset -1, -2

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            half4 _GlobalAmbientTint;
            half _GlobalSnowAmount;
            int _GlobalCampfireCount;
            float4 _GlobalCampfireData[4]; // xyz = world pos, w = melt radius

            CBUFFER_START(UnityPerMaterial)
                half4 _SnowColor;
                half4 _SnowShadowColor;
                half _NormalThreshold;
            CBUFFER_END

            float SnowHash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float SnowValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = SnowHash(i);
                float b = SnowHash(i + float2(1.0, 0.0));
                float c = SnowHash(i + float2(0.0, 1.0));
                float d = SnowHash(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                // Tiny upward push along world normal to prevent z-fighting on roofs/cars/props
                posWS += normalWS * 0.012;
                output.positionWS = posWS;
                output.normalWS = normalWS;
                output.positionCS = TransformWorldToHClip(posWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half snowAmount = saturate(_GlobalSnowAmount);
                if (snowAmount <= 0.01) discard;

                float3 n = normalize(input.normalWS);
                // Only upward-facing surfaces (roofs, car hoods, tree tops, crate lids, walls tops)
                half upMask = smoothstep(_NormalThreshold, _NormalThreshold + 0.28, n.y);
                if (upMask <= 0.01) discard;

                // World-space organic patchy accumulation noise
                float n1 = SnowValueNoise(input.positionWS.xz * 0.55);
                float n2 = SnowValueNoise(input.positionWS.xz * 1.85 + 7.3);
                float patchNoise = n1 * 0.65 + n2 * 0.35;

                half coverage = smoothstep(1.0 - snowAmount * 1.25, 1.0 - snowAmount * 0.35 + 0.18, patchNoise);
                half alpha = saturate(upMask * coverage * smoothstep(0.0, 0.25, snowAmount) * 0.92);

                // Melt snow around active Campfires
                for (int c = 0; c < 4; c++)
                {
                    if (c >= _GlobalCampfireCount) break;
                    float4 cf = _GlobalCampfireData[c];
                    float dist = length(input.positionWS.xz - cf.xz);
                    half melt = smoothstep(cf.w * 0.45, cf.w, dist);
                    alpha *= melt;
                }

                if (alpha <= 0.02) discard;

                half shade = saturate(n.y * 0.55 + patchNoise * 0.45);
                half3 snowCol = lerp(_SnowShadowColor.rgb, _SnowColor.rgb, shade);

                half4 tint = (_GlobalAmbientTint.a > 0.01) ? _GlobalAmbientTint : half4(1, 1, 1, 1);
                return half4(snowCol * tint.rgb, alpha);
            }
            ENDHLSL
        }
    }
}
