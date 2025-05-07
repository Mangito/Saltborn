// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

Shader "Crest/Underwater"
{
    Properties
    {
        _Crest_ExtinctionMultiplier("Density Factor", Range(0, 1)) = 1
        _Crest_SunBoost("Sun Boost", Range(0, 100)) = 2

        _Crest_OutScatteringFactor("Out-Scattering Factor", Range(0, 1)) = 0.2
        _Crest_OutScatteringExtinctionFactor("Out-Scattering Extinction Factor", Range(0, 1)) = 0.2

        [Space(10)]

        [Toggle(d_Dithering)]
        _Crest_DitheringEnabled("Dithering", Integer) = 1
        _Crest_DitheringIntensity("Dithering Intensity", Range(0, 10)) = 1

        [Space(10)]

        [Toggle(d_Meniscus)]
        _Crest_MeniscusEnabled("Meniscus", Integer) = 1

        [Header(Advanced)]
        [Space(6)]

        // This adds an offset to the cascade index when sampling water data, in effect smoothing/blurring it. Default
        // to shifting the maximum amount (shift from lod 0 to penultimate lod - dont use last lod as it cross-fades
        // data in/out), as more filtering was better in testing.
        [CrestIntegerRange]
        _Crest_DataSliceOffset("Filter Water Data", Integer) = 13

        [HideInInspector]
        _Crest_Version("Version", Integer) = 0

        [Header(Copied From Water Surface)]
        [Space(6)]

        [PerRendererData] _Crest_AbsorptionColor("Absorption Color", Color) = (0.3416268, 0.6954546, 0.85, 0.1019608)
        [PerRendererData] _Crest_Scattering("Scattering", Color) = (0, 0.09803922, 0.2, 1)
        [PerRendererData] _Crest_Anisotropy("Anisotropy", Range(0, 1)) = 0.5
        [PerRendererData] _Crest_DirectTerm("Direct Term", Float) = 1
        [PerRendererData] _Crest_AmbientTerm("Ambient Term", Float) = 1
        [PerRendererData] _Crest_ShadowsAffectsAmbientFactor("Shadows Affects Ambient Factor", Float) = 0.5

        // Caustics
        [PerRendererData] [ToggleUI] _Crest_CausticsEnabled("Caustics Enabled", Float) = 1
        [PerRendererData] [NoScaleOffset] _Crest_CausticsTexture("Caustics Texture", 2D) = "black" {}
        [PerRendererData] _Crest_CausticsStrength("Caustics Strength", Range(0, 10)) = 3.2
        [PerRendererData] _Crest_CausticsTextureScale("Caustics Scale", Range(0.01, 100)) = 50
        [PerRendererData] _Crest_CausticsScrollSpeed("Caustics Scroll Speed", Range(0, 10)) = 1
        [PerRendererData] _Crest_CausticsTextureAverage("Caustics Grey Point", Range(0, 1)) = 0.07
        [PerRendererData] _Crest_CausticsFocalDepth("Caustics Focal Depth", Range(0, 25)) = 2
        [PerRendererData] _Crest_CausticsDepthOfField("Caustics Depth of Field", Range(0.01, 10)) = 6
        [PerRendererData] [NoScaleOffset] _Crest_CausticsDistortionTexture("Caustics Distortion Texture", 2D) = "grey" {}
        [PerRendererData] _Crest_CausticsDistortionStrength("Caustics Distortion Strength", Range(0, 0.25)) = 0.16
        [PerRendererData] _Crest_CausticsDistortionScale("Caustics Distortion Scale", Range(0.01, 1000)) = 250
        [PerRendererData] _Crest_CausticsMotionBlur("Caustics Motion Blur", Range(0, 10)) = 1
        [PerRendererData] [Toggle] CREST_FLOW("Flow Enabled", Float) = 0
    }

    HLSLINCLUDE
    #pragma vertex Vertex

    // #pragma enable_d3d11_debug_symbols

    // Also on the water shader.
    #pragma multi_compile_local_fragment __ CREST_FLOW_ON

    #pragma shader_feature_local_fragment __ d_Meniscus
    #pragma shader_feature_local_fragment __ d_Dithering

    #pragma multi_compile_local_fragment __ _DEBUG_VISUALIZE_MASK
    #pragma multi_compile_local_fragment __ _DEBUG_VISUALIZE_STENCIL
    ENDHLSL

    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.high-definition"
        }

        Tags { "RenderPipeline"="HDRenderPipeline" }

        ZWrite Off
        Blend Off

        Pass
        {
            Name "Full Screen"
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterHDRP.hlsl"

            // Both "__" and "_FULL_SCREEN_EFFECT" are fullscreen triangles. The latter only denotes an optimisation of
            // whether to skip the horizon calculation.
            #pragma multi_compile_local_fragment __ _FULL_SCREEN_EFFECT
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }

        Pass
        {
            Name "Reflection"
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #define CREST_REFLECTION 1
            #include_with_pragmas "UnderwaterHDRP.hlsl"
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment FragmentPlanarReflections
            ENDHLSL
        }

        Pass
        {
            PackageRequirements
            {
                "com.waveharmonic.crest.portals"
            }

            // Only adds fog to the front face and in effect anything behind it.
            Name "Volume: Front Face (2D)"
            Cull Back
            ZTest LEqual

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterHDRP.hlsl"

            #define CREST_WATER_VOLUME 1
            #define CREST_WATER_VOLUME_FRONT_FACE 1
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }

        Pass
        {
            PackageRequirements
            {
                "com.waveharmonic.crest.portals"
            }

            // Only adds fog to the front face and in effect anything behind it.
            Name "Volume: Front Face (3D)"
            Cull Back
            ZTest LEqual

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterHDRP.hlsl"

            #define CREST_WATER_VOLUME 1
            #define CREST_WATER_VOLUME_HAS_BACKFACE 1
            #define CREST_WATER_VOLUME_FRONT_FACE 1
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }

        Pass
        {
            PackageRequirements
            {
                "com.waveharmonic.crest.portals"
            }

            // Only adds fog to the front face and in effect anything behind it.
            Name "Volume: Front Face (Fly-Through)"
            Cull Back
            ZTest LEqual

            Stencil
            {
                // Must match k_StencilValueVolume in:
                // Portals.cs
                Ref 5
                Comp Always
                Pass Replace
                ZFail IncrSat
            }

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterHDRP.hlsl"

            #define CREST_WATER_VOLUME 1
            #define CREST_WATER_VOLUME_HAS_BACKFACE 1
            #define CREST_WATER_VOLUME_FRONT_FACE 1
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }

        Pass
        {
            // Back face will only render if view is within the volume and there is no scene in front. It will only add
            PackageRequirements
            {
                "com.waveharmonic.crest.portals"
            }

            // fog to the back face (and in effect anything behind it). No caustics.
            Name "Volume: Back Face"
            Cull Front
            ZTest LEqual

            Stencil
            {
                // Must match k_StencilValueVolume in:
                // Portals.cs
                Ref 5
                Comp NotEqual
                Pass Replace
                ZFail IncrSat
            }

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterHDRP.hlsl"

            #define CREST_WATER_VOLUME 1
            #define CREST_WATER_VOLUME_BACK_FACE 1
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }

        Pass
        {
            PackageRequirements
            {
                "com.waveharmonic.crest.portals"
            }

            // When inside a volume, this pass will render to the scene within the volume.
            Name "Volume: Scene (Full Screen)"
            Cull Back
            ZTest Always
            Stencil
            {
                // We want to render over the scene that's inside the volume, but not over already fogged areas. It will
                // handle all of the scene within the geometry once the camera is within the volume.
                // 0 = Outside of geometry as neither face passes have touched it.
                // 1 = Only back face z failed which means scene is in front of back face but not front face.
                // 2 = Both front and back face z failed which means outside geometry.
                Ref 1
                Comp Equal
                Pass Replace
            }

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterHDRP.hlsl"

            #define CREST_WATER_VOLUME_FULLSCREEN 1
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }

        Pass
        {
            PackageRequirements
            {
                "com.waveharmonic.crest.portals"
            }

            Name "Volume: Negative (Full Screen)"
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterHDRP.hlsl"

            #define CREST_WATER_VOLUME_FULLSCREEN 1
            #define CREST_WATER_VOLUME_NEGATIVE 1
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }
    }

    Subshader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal"
        }

        Tags { "RenderPipeline"="UniversalPipeline" }

        ZWrite Off

        Pass
        {
            Name "Full Screen"
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterURP.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

            // Both "__" and "_FULL_SCREEN_EFFECT" are fullscreen triangles. The latter only denotes an optimisation of
            // whether to skip the horizon calculation.
            #pragma multi_compile_local_fragment __ _FULL_SCREEN_EFFECT

            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }

        Pass
        {
            Name "Reflection"
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #define CREST_REFLECTION 1
            #include_with_pragmas "UnderwaterURP.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment FragmentPlanarReflections
            ENDHLSL
        }

        Pass
        {
            PackageRequirements
            {
                "com.waveharmonic.crest.portals"
            }

            // Only adds fog to the front face and in effect anything behind it.
            Name "Volume: Front Face (2D)"
            Cull Back
            ZTest LEqual

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterURP.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

            #define CREST_WATER_VOLUME 1
            #define CREST_WATER_VOLUME_FRONT_FACE 1
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }

        Pass
        {
            PackageRequirements
            {
                "com.waveharmonic.crest.portals"
            }

            // Only adds fog to the front face and in effect anything behind it.
            Name "Volume: Front Face (3D)"
            Cull Back
            ZTest LEqual

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterURP.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

            #define CREST_WATER_VOLUME 1
            #define CREST_WATER_VOLUME_HAS_BACKFACE 1
            #define CREST_WATER_VOLUME_FRONT_FACE 1
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }

        Pass
        {
            PackageRequirements
            {
                "com.waveharmonic.crest.portals"
            }

            // Only adds fog to the front face and in effect anything behind it.
            Name "Volume: Front Face (Fly-Through)"
            Cull Back
            ZTest LEqual

            Stencil
            {
                // Must match k_StencilValueVolume in:
                // Portals.cs
                Ref 5
                Comp Always
                Pass Replace
                ZFail IncrSat
            }

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterURP.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

            #define CREST_WATER_VOLUME 1
            #define CREST_WATER_VOLUME_HAS_BACKFACE 1
            #define CREST_WATER_VOLUME_FRONT_FACE 1
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }

        Pass
        {
            // Back face will only render if view is within the volume and there is no scene in front. It will only add
            PackageRequirements
            {
                "com.waveharmonic.crest.portals"
            }

            // fog to the back face (and in effect anything behind it). No caustics.
            Name "Volume: Back Face"
            Cull Front
            ZTest LEqual

            Stencil
            {
                // Must match k_StencilValueVolume in:
                // Portals.cs
                Ref 5
                Comp NotEqual
                Pass Replace
                ZFail IncrSat
            }

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterURP.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

            #define CREST_WATER_VOLUME 1
            #define CREST_WATER_VOLUME_BACK_FACE 1
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }

        Pass
        {
            PackageRequirements
            {
                "com.waveharmonic.crest.portals"
            }

            // When inside a volume, this pass will render to the scene within the volume.
            Name "Volume: Scene (Full Screen)"
            Cull Back
            ZTest Always
            Stencil
            {
                // We want to render over the scene that's inside the volume, but not over already fogged areas. It will
                // handle all of the scene within the geometry once the camera is within the volume.
                // 0 = Outside of geometry as neither face passes have touched it.
                // 1 = Only back face z failed which means scene is in front of back face but not front face.
                // 2 = Both front and back face z failed which means outside geometry.
                Ref 1
                Comp Equal
                Pass Replace
            }

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterURP.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

            #define CREST_WATER_VOLUME_FULLSCREEN 1
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }

        Pass
        {
            PackageRequirements
            {
                "com.waveharmonic.crest.portals"
            }

            Name "Volume: Negative (Full Screen)"
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterURP.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRendering.hlsl"

            #define CREST_WATER_VOLUME_FULLSCREEN 1
            #define CREST_WATER_VOLUME_NEGATIVE 1
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }
    }

    SubShader
    {
        ZWrite Off

        Pass
        {
            Name "Full Screen"
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterBIRP.hlsl"

            // Both "__" and "_FULL_SCREEN_EFFECT" are fullscreen triangles. The latter only denotes an optimisation of
            // whether to skip the horizon calculation.
            #pragma multi_compile_local_fragment __ _FULL_SCREEN_EFFECT

            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }

        Pass
        {
            Name "Reflection"
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #define CREST_REFLECTION 1
            #include_with_pragmas "UnderwaterBIRP.hlsl"
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment FragmentPlanarReflections
            ENDHLSL
        }

        Pass
        {
            PackageRequirements
            {
                "com.waveharmonic.crest.portals"
            }

            // Only adds fog to the front face and in effect anything behind it.
            Name "Volume: Front Face (2D)"
            Cull Back
            ZTest LEqual

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterBIRP.hlsl"

            #define CREST_WATER_VOLUME 1
            #define CREST_WATER_VOLUME_FRONT_FACE 1
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }

        Pass
        {
            PackageRequirements
            {
                "com.waveharmonic.crest.portals"
            }

            // Only adds fog to the front face and in effect anything behind it.
            Name "Volume: Front Face (3D)"
            Cull Back
            ZTest LEqual

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterBIRP.hlsl"

            #define CREST_WATER_VOLUME 1
            #define CREST_WATER_VOLUME_HAS_BACKFACE 1
            #define CREST_WATER_VOLUME_FRONT_FACE 1
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }

        Pass
        {
            PackageRequirements
            {
                "com.waveharmonic.crest.portals"
            }

            // Only adds fog to the front face and in effect anything behind it.
            Name "Volume: Front Face (Fly-Through)"
            Cull Back
            ZTest LEqual

            Stencil
            {
                // Must match k_StencilValueVolume in:
                // Portals.cs
                Ref 5
                Comp Always
                Pass Replace
                ZFail IncrSat
            }

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterBIRP.hlsl"

            #define CREST_WATER_VOLUME 1
            #define CREST_WATER_VOLUME_HAS_BACKFACE 1
            #define CREST_WATER_VOLUME_FRONT_FACE 1
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }

        Pass
        {
            // Back face will only render if view is within the volume and there is no scene in front. It will only add
            PackageRequirements
            {
                "com.waveharmonic.crest.portals"
            }

            // fog to the back face (and in effect anything behind it). No caustics.
            Name "Volume: Back Face"
            Cull Front
            ZTest LEqual

            Stencil
            {
                // Must match k_StencilValueVolume in:
                // Portals.cs
                Ref 5
                Comp NotEqual
                Pass Replace
                ZFail IncrSat
            }

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterBIRP.hlsl"

            #define CREST_WATER_VOLUME 1
            #define CREST_WATER_VOLUME_BACK_FACE 1
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }

        Pass
        {
            PackageRequirements
            {
                "com.waveharmonic.crest.portals"
            }

            // When inside a volume, this pass will render to the scene within the volume.
            Name "Volume: Scene (Full Screen)"
            Cull Back
            ZTest Always
            Stencil
            {
                // We want to render over the scene that's inside the volume, but not over already fogged areas. It will
                // handle all of the scene within the geometry once the camera is within the volume.
                // 0 = Outside of geometry as neither face passes have touched it.
                // 1 = Only back face z failed which means scene is in front of back face but not front face.
                // 2 = Both front and back face z failed which means outside geometry.
                Ref 1
                Comp Equal
                Pass Replace
            }

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterBIRP.hlsl"

            #define CREST_WATER_VOLUME_FULLSCREEN 1
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }

        Pass
        {
            PackageRequirements
            {
                "com.waveharmonic.crest.portals"
            }

            Name "Volume: Negative (Full Screen)"
            Cull Off
            ZTest Always

            HLSLPROGRAM
            #include_with_pragmas "UnderwaterBIRP.hlsl"

            #define CREST_WATER_VOLUME_FULLSCREEN 1
            #define CREST_WATER_VOLUME_NEGATIVE 1
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/Underwater.hlsl"

            #pragma fragment Fragment
            ENDHLSL
        }
    }
    CustomEditor "WaveHarmonic.Crest.Editor.CustomShaderGUI"
}
