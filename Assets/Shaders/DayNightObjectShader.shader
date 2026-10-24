Shader "Custom/DayNightObjectShader"
{
    Properties
    {
        _BaseMap("Texture (Albedo)", 2D) = "white" {}
        _BaseColor("Color", Color) = (1, 1, 1, 1)

        [Header(Day Night Atmosphere)]
        _DayColor("Day Ambient Tint", Color) = (1.0, 0.98, 0.92, 1.0)
        _DuskColor("Sunset Dusk Ambient Tint", Color) = (0.95, 0.55, 0.35, 1.0)
        _NightColor("Night Ambient Tint", Color) = (0.06, 0.09, 0.18, 1.0)
        _NightDarkness("Night Darkness Level", Range(0, 1)) = 0.85

        [Header(Player Flashlight and Perimeter Halo)]
        _FlashlightColor("Flashlight Beam Color", Color) = (1.0, 0.95, 0.82, 1.0)
        _FlashlightRange("Flashlight Range", Float) = 11.0
        _FlashlightAngle("Flashlight Cone Angle (Deg)", Range(10, 80)) = 35.0
        _FlashlightIntensity("Flashlight Intensity", Float) = 2.2
        _PlayerLightRadius("Player Ambient Perimeter Radius", Float) = 2.4
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
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
            };

            Texture2D _BaseMap;
            SamplerState sampler_BaseMap;

            // Global parameters driven by DayNightShaderController
            float _GlobalDayNightBlend;
            float3 _GlobalPlayerPos;
            float3 _GlobalPlayerForward;
            float _GlobalFlashlightOn;

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _DayColor;
                half4 _DuskColor;
                half4 _NightColor;
                float _NightDarkness;
                half4 _FlashlightColor;
                float _FlashlightRange;
                float _FlashlightAngle;
                float _FlashlightIntensity;
                float _PlayerLightRadius;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 albedo = _BaseMap.Sample(sampler_BaseMap, input.uv) * _BaseColor;

                float blend = saturate(_GlobalDayNightBlend);
                float flashlightOn = saturate(_GlobalFlashlightOn);

                // ULTRA-FAST PATH: If daytime and flashlight is off, return immediately
                if (blend <= 0.005 && flashlightOn <= 0.005)
                {
                    return half4(albedo.rgb * _DayColor.rgb, albedo.a);
                }

                // Ambient Lighting
                half3 dayDusk = lerp(_DayColor.rgb, _DuskColor.rgb, saturate(blend * 2.5));
                half3 deepNight = _NightColor.rgb * (1.0 - _NightDarkness * 0.7);
                half3 duskNight = lerp(_DuskColor.rgb, deepNight, saturate((blend - 0.4) * 1.6667));
                half3 ambientColor = (blend < 0.4) ? dayDusk : duskNight;

                // Dynamic Player Lighting with Square-Distance Early-Out
                float2 toFrag2D = input.positionWS.xz - _GlobalPlayerPos.xz;
                float distSq = dot(toFrag2D, toFrag2D);
                float maxRadius = max(_FlashlightRange, _PlayerLightRadius);

                half3 lightColor = half3(0, 0, 0);

                if (distSq < (maxRadius * maxRadius))
                {
                    float dist2D = sqrt(distSq);

                    float perimeter = saturate(1.0 - (dist2D / max(0.01, _PlayerLightRadius)));
                    perimeter = perimeter * perimeter * 0.65;

                    float flashlight = 0.0;
                    if (flashlightOn > 0.005 && dist2D < _FlashlightRange)
                    {
                        float2 fragDir = toFrag2D / max(0.001, dist2D);
                        float dotDir = dot(fragDir, _GlobalPlayerForward.xz);

                        float angleCos = cos(radians(_FlashlightAngle * 0.5));
                        float cone = saturate((dotDir - (angleCos - 0.08)) * 10.0);

                        float rangeAtten = 1.0 - (dist2D / _FlashlightRange);
                        flashlight = cone * (rangeAtten * rangeAtten) * _FlashlightIntensity * flashlightOn;
                    }

                    float totalLight = saturate(perimeter + flashlight);
                    lightColor = _FlashlightColor.rgb * totalLight;
                }

                half3 finalLighting = ambientColor + (lightColor * blend);
                return half4(albedo.rgb * finalLighting, albedo.a);
            }
            ENDHLSL
        }
    }
}
