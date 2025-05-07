// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Soft shadow term is red, hard shadow term is green.

Shader "Hidden/Crest/Simulation/Update Shadow"
{
    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.high-definition"
        }

        Tags { "RenderPipeline"="HDRenderPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            // #pragma enable_d3d11_debug_symbols

            // SHADOW_ULTRA_LOW uses Gather which is 4.5. HDRP minimum is 5.0 so this is fine.
            #pragma target 4.5

            // TODO: We might be able to expose this to give developers the option.
            // #pragma multi_compile SHADOW_ULTRA_LOW SHADOW_LOW SHADOW_MEDIUM SHADOW_HIGH

            // Ultra low uses Gather to filter which should be same cost as not filtering. See algorithms per keyword:
            // Runtime/Lighting/Shadow/HDShadowAlgorithms.hlsl
            #define SHADOW_ULTRA_LOW
            #define AREA_SHADOW_LOW
            #define PUNCTUAL_SHADOW_ULTRA_LOW
            #define DIRECTIONAL_SHADOW_ULTRA_LOW

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/Lighting/LightLoop/HDShadow.hlsl"

            float4x4 _Crest_ViewProjectionMatrix;

            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Data/UpdateShadow.hlsl"

            m_CrestNameSpace

            half SampleShadows(const float4 i_positionWS)
            {
                // Get directional light data. By definition we only have one directional light casting shadow.
                DirectionalLightData light = _DirectionalLightDatas[_DirectionalShadowIndex];
                HDShadowContext context = InitShadowContext();

                // Zeros are for screen space position and world space normal which are for filtering and normal bias
                // respectively. They did not appear to have an impact.
                half shadows = GetDirectionalShadowAttenuation(context, 0, i_positionWS.xyz, 0, _DirectionalShadowIndex, -light.forward);
                // Apply shadow strength from main light.
                shadows = LerpWhiteTo(shadows, light.shadowDimmer);

                return shadows;
            }

            half ComputeShadowFade(const float4 i_positionWS)
            {
                // TODO: Work out shadow fade.
                return 0.0;
            }

            m_CrestNameSpaceEnd
            ENDHLSL
        }
    }

    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal"
        }

        Tags { "RenderPipeline"="UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex Vertex
            #pragma fragment Fragment
            // #pragma enable_d3d11_debug_symbols

            // Maybe this is the equivalent of the SHADOW_COLLECTOR_PASS define? Inspired from com.unity.render-pipelines.universal/Shaders/Utils/ScreenSpaceShadows.shader
            #define _MAIN_LIGHT_SHADOWS_CASCADE
            #define MAIN_LIGHT_CALCULATE_SHADOWS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            #define CREST_SAMPLE_SHADOW_HARD

            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Data/UpdateShadow.hlsl"

            m_CrestNameSpace

            half SampleShadows(const float4 i_positionWS)
            {
                // Includes soft shadows if _SHADOWS_SOFT is defined (requires multi-compile pragma).
                return MainLightRealtimeShadow(TransformWorldToShadowCoord(i_positionWS.xyz));
            }

            half ComputeShadowFade(const float4 i_positionWS)
            {
                return GetShadowFade(i_positionWS.xyz);
            }

            m_CrestNameSpaceEnd
            ENDHLSL
        }
    }

    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #pragma multi_compile_shadowcollector

            // #pragma enable_d3d11_debug_symbols

            #define CREST_SAMPLE_SHADOW_HARD
            #define d_Crest_ReceiveShadowsTransparent 1

            #include "UnityCG.cginc"

            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Legacy/Core.hlsl"
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Legacy/Shadows.hlsl"
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Data/UpdateShadow.hlsl"

            m_CrestNameSpace

            half SampleShadows(const float4 i_positionWS)
            {
                // NOTE: "Shadow Projection > Close Fit" can still produce artefacts when away from caster, but this
                // appears to be an improvement over the compute shader.
                float4 shadowCoord = GET_SHADOW_COORDINATES(i_positionWS);
                half shadows = UNITY_SAMPLE_SHADOW(_ShadowMapTexture, shadowCoord);
                if (_Crest_ClearShadows) shadows = 1.0;
                shadows = lerp(_LightShadowData.r, 1.0, shadows);

                return shadows;
            }

            half ComputeShadowFade(const float4 i_positionWS)
            {
                float z = dot(i_positionWS.xyz - _WorldSpaceCameraPos.xyz, unity_CameraToWorld._m02_m12_m22);
                float fadeDistance = UnityComputeShadowFadeDistance(i_positionWS.xyz, z);
                float fade = UnityComputeShadowFade(fadeDistance);
                return fade;
            }

            m_CrestNameSpaceEnd
            ENDHLSL
        }
    }
}
