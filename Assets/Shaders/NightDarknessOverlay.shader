Shader "Custom/NightDarknessOverlay"
{
    Properties
    {
        [HideInInspector] _MainTex("Texture", 2D) = "white" {}
        [HideInInspector] _Color("Color", Color) = (1, 1, 1, 1)
        _NightColor("Night Ambiance Color", Color) = (0.01, 0.02, 0.04, 1.0)
        _DuskColor("Dusk Sunset Tint", Color) = (0.9, 0.45, 0.15, 0.35)
        _InnerRadius("Inner Bright Radius", Range(0.05, 0.5)) = 0.10
        _OuterRadius("Outer Fade Radius", Range(0.15, 1.2)) = 0.45
        _MaxDarkness("Max Darkness (90% Dark)", Range(0.3, 0.98)) = 0.90
        _FlickerAmount("Torch Flicker Amount", Range(0, 0.04)) = 0.008
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+200" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "NightDarknessPass"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
            };

            Texture2D _MainTex;
            SamplerState sampler_MainTex;

            // Globals driven by DayNightManager & NightDarknessController
            float _GlobalDarknessAlpha;    // 0.0 (Day) -> 1.0 (Full Night)
            float _GlobalDuskAlpha;        // 0.0 -> 1.0 during Dusk, 0 at Night
            float2 _GlobalPlayerScreenPos; // Viewport position of player (0..1, 0..1)
            float _GlobalHasTorchLight;    // 1 if torch/flashlight on, 0 if off
            float _GlobalAspect;           // Screen aspect ratio for circular light

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _NightColor;
                half4 _DuskColor;
                float _InnerRadius;
                float _OuterRadius;
                float _MaxDarkness;
                float _FlickerAmount;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float darkAlpha = saturate(_GlobalDarknessAlpha);
                float duskAlpha = saturate(_GlobalDuskAlpha);

                // Day Mode: Completely transparent
                if (darkAlpha <= 0.005 && duskAlpha <= 0.005)
                {
                    return half4(0, 0, 0, 0);
                }

                // Dusk Phase: Warm amber screen tint
                if (duskAlpha > 0.005 && darkAlpha <= 0.05)
                {
                    return half4(_DuskColor.rgb, _DuskColor.a * duskAlpha);
                }

                // Night Phase: Smooth distance fade from bright center to 90% dark ambiance
                float2 diff = input.uv - _GlobalPlayerScreenPos;
                diff.x *= _GlobalAspect; // Correct aspect ratio for circular light
                float dist = length(diff);

                // Subtle organic torch breathing
                float flicker = sin(_Time.y * 6.0) * cos(_Time.y * 11.0) * _FlickerAmount;
                float inner = max(0.01, _InnerRadius + flicker);
                float outer = max(inner + 0.08, _OuterRadius + flicker);

                // Smooth gradual distance decay
                float falloff = smoothstep(inner, outer, dist);
                falloff = pow(falloff, 1.2); // Cinematic natural light falloff curve

                float hasTorch = saturate(_GlobalHasTorchLight);

                // Outside the light, reach 90% darkness as requested (0.90)
                float maxDark = lerp(0.96, _MaxDarkness, hasTorch);
                float darkness = lerp(maxDark, falloff * maxDark, hasTorch);

                float finalAlpha = darkness * darkAlpha;
                return half4(_NightColor.rgb, finalAlpha);
            }
            ENDHLSL
        }
    }
}
