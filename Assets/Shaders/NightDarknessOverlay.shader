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

            // Globals driven by DayNightManager, NightDarknessController & NightCombatItemManager
            float _GlobalDarknessAlpha;       // 0.0 (Day) -> 1.0 (Full Night)
            float _GlobalDuskAlpha;           // 0.0 -> 1.0 during Dusk, 0 at Night
            float2 _GlobalPlayerScreenPos;    // Viewport position of player (0..1, 0..1)
            float4 _GlobalCampfireScreenPos;  // xy = pos, z = radius, w = active
            float _GlobalHasTorchLight;       // 1 if torch/flashlight on, 0 if off
            float _GlobalAspect;              // Screen aspect ratio for circular light

            // Night Combat Active Items
            float4 _GlobalFlashlightDir;      // xy = aspect-corrected normalized aim dir, z = cone length, w = strength (0..1)
            float _GlobalNvgStrength;         // 0..1 Full-screen tactical green Night Vision Goggles
            float _GlobalRedFlareStrength;    // 0..1 Overhead sky Signal Flare crimson-red illumination
            float _GlobalFlashbangStrength;   // 0..1 UV Flashbang white-violet blast flash

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
                float nvg = saturate(_GlobalNvgStrength);
                float redFlare = saturate(_GlobalRedFlareStrength);
                float flashbang = saturate(_GlobalFlashbangStrength);
                float flashlight = saturate(_GlobalFlashlightDir.w);

                // Day & no special Night Combat visual overlay active -> completely transparent
                if (darkAlpha <= 0.005 && nvg <= 0.005 && redFlare <= 0.005 && flashbang <= 0.005)
                {
                    return half4(0, 0, 0, 0);
                }

                // 1. Base night darkness around player
                float2 diff = input.uv - _GlobalPlayerScreenPos;
                diff.x *= _GlobalAspect;
                float dist = length(diff);

                float flicker = sin(_Time.y * 6.0) * cos(_Time.y * 11.0) * _FlickerAmount;
                float inner = max(0.01, _InnerRadius + flicker);
                float outer = max(inner + 0.08, _OuterRadius + flicker);

                float playerFalloff = smoothstep(inner, outer, dist);
                float finalFalloff = playerFalloff;

                // 2. Campfire dynamic light cut-through
                if (_GlobalCampfireScreenPos.w > 0.5)
                {
                    float2 cDiff = input.uv - _GlobalCampfireScreenPos.xy;
                    cDiff.x *= _GlobalAspect;
                    float cDist = length(cDiff);
                    float cRadius = _GlobalCampfireScreenPos.z;
                    float cFlicker = flicker * 0.5;
                    float cInner = max(0.01, cRadius * 0.35 + cFlicker);
                    float cOuter = max(cInner + 0.06, cRadius + cFlicker);
                    float campfireFalloff = smoothstep(cInner, cOuter, cDist);
                    finalFalloff = min(finalFalloff, campfireFalloff);
                }

                // 3. Tactical Flashlight directional beam cone (follows player aim!)
                if (flashlight > 0.01 && dist > 0.001)
                {
                    float2 aimDir = normalize(_GlobalFlashlightDir.xy + float2(1e-5, 0));
                    float coneReach = max(0.35, _GlobalFlashlightDir.z);
                    float cosAng = dot(diff / dist, aimDir);
                    // Inner crisp cone (cos > 0.90 ~25 deg) fading smoothly out to ~38 deg
                    float angMask = smoothstep(0.76, 0.93, cosAng);
                    float distMask = 1.0 - smoothstep(coneReach * 0.45, coneReach, dist);
                    float coneLit = angMask * distMask * flashlight;
                    // Also slightly widen close halo around the flashlight wielder
                    float closeHalo = (1.0 - smoothstep(0.08, 0.24, dist)) * flashlight * 0.85;
                    float beamCut = 1.0 - saturate(max(coneLit, closeHalo));
                    finalFalloff = min(finalFalloff, beamCut);
                }

                finalFalloff = pow(finalFalloff, 1.2);

                float hasTorch = saturate(max(_GlobalHasTorchLight, flashlight));
                float maxDark = lerp(0.96, _MaxDarkness, hasTorch);
                float darkness = lerp(maxDark, finalFalloff * maxDark, hasTorch);

                // 4. Overhead Red Signal Flare opens up night darkness across the whole stage!
                darkness *= (1.0 - redFlare * 0.86);

                // 5. Night Vision Goggles clears all night darkness across the entire screen!
                darkness *= (1.0 - nvg * 0.96);

                // 6. UV Flashbang clears darkness completely while flashing!
                darkness *= (1.0 - flashbang);

                float3 outRgb = _NightColor.rgb;
                float outAlpha = darkness * darkAlpha;

                // Blend dramatic Crimson-Red Sky Flare wash across the whole illuminated arena
                if (redFlare > 0.005)
                {
                    float flarePulse = 0.90 + 0.10 * sin(_Time.y * 9.5 + input.uv.y * 3.0);
                    float3 flareTint = float3(0.92, 0.10, 0.08) * flarePulse;
                    float flareAlpha = redFlare * 0.26;
                    outRgb = lerp(outRgb, flareTint, saturate(flareAlpha / max(0.001, outAlpha + flareAlpha)));
                    outAlpha = saturate(max(outAlpha, flareAlpha));
                }

                // Blend Tactical Phosphor-Green Night Vision Goggles (scanlines + subtle binocular vignette)
                if (nvg > 0.005)
                {
                    float scanline = 0.90 + 0.10 * sin(input.uv.y * 420.0 + _Time.y * 8.0);
                    float2 vUv = (input.uv - 0.5) * float2(_GlobalAspect * 0.65, 1.0);
                    float vig = smoothstep(0.45, 0.92, length(vUv));
                    float3 nvgTint = lerp(float3(0.10, 0.88, 0.30) * scanline, float3(0.01, 0.14, 0.05), vig * 0.78);
                    float nvgAlpha = nvg * lerp(0.22, 0.52, vig);
                    outRgb = lerp(outRgb, nvgTint, nvg);
                    outAlpha = saturate(max(outAlpha, nvgAlpha));
                }

                // Blend dazzling UV White-Violet Flashbang flash on top!
                if (flashbang > 0.005)
                {
                    float3 uvColor = float3(0.92, 0.80, 1.0);
                    float fbAlpha = flashbang * 0.78;
                    outRgb = lerp(outRgb, uvColor, saturate(fbAlpha));
                    outAlpha = saturate(max(outAlpha, fbAlpha));
                }

                return half4(outRgb, outAlpha);
            }
            ENDHLSL
        }
    }
}
