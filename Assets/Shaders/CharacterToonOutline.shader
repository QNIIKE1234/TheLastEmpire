Shader "TheLastEmpire/CharacterToonOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (0.04, 0.03, 0.05, 1.0)
        _OutlineWidth ("Outline Width (World)", Range(0.001, 0.06)) = 0.010
        _ScreenSpaceScale ("Screen Space Compensation", Range(0.0, 0.012)) = 0.0016
        _MinWorldWidth ("Min World Width", Range(0.001, 0.04)) = 0.006
        _MaxWorldWidth ("Max World Width", Range(0.005, 0.08)) = 0.026
        _ZBias ("Depth Bias", Range(0.0, 0.01)) = 0.0006
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry+1"
        }

        Pass
        {
            Name "ToonOutline"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
                float _ScreenSpaceScale;
                float _MinWorldWidth;
                float _MaxWorldWidth;
                float _ZBias;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float  fogFactor  : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                // Outline shell mesh has position-welded smooth normals baked directly into NORMAL,
                // so Unity's SkinnedMeshRenderer skins them accurately with every bone matrix!
                float3 nOS = normalize(IN.normalOS);
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(nOS);

                // Distance-compensated outline thickness: stays crisp both up close and in top-down view
                float camDist = length(_WorldSpaceCameraPos - posWS);
                float widthWS = clamp(_OutlineWidth + camDist * _ScreenSpaceScale, _MinWorldWidth, _MaxWorldWidth);

                posWS += normWS * widthWS;

                float4 posCS = TransformWorldToHClip(posWS);

                // Subtle depth push-back so outline never z-fights over face details/eyes/hair bangs
                #if UNITY_REVERSED_Z
                    posCS.z -= _ZBias * posCS.w;
                #else
                    posCS.z += _ZBias * posCS.w;
                #endif

                OUT.positionCS = posCS;
                OUT.fogFactor = ComputeFogFactor(posCS.z);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half3 col = MixFog(_OutlineColor.rgb, IN.fogFactor);
                return half4(col, _OutlineColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
