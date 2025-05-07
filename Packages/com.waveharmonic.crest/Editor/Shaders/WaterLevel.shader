// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

Shader "Hidden/Crest/Editor/Water Level (Depth)"
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            #include "Packages/com.waveharmonic.crest/Editor/Shaders/WaterLevel.hlsl"
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
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #include "Packages/com.waveharmonic.crest/Editor/Shaders/WaterLevel.hlsl"
            ENDHLSL
        }
    }

    SubShader
    {
        Pass
        {
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "UnityCG.cginc"

            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Legacy/Core.hlsl"
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Legacy/InputsDriven.hlsl"

            #include "Packages/com.waveharmonic.crest/Editor/Shaders/WaterLevel.hlsl"
            ENDHLSL
        }
    }
}
