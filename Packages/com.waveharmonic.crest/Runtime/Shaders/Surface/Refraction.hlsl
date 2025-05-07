// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef CREST_WATER_REFRACTION_H
#define CREST_WATER_REFRACTION_H

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings.Crest.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Depth.hlsl"

#if (CREST_PORTALS != 0)
#include "Packages/com.waveharmonic.crest.portals/Runtime/Shaders/Library/Portals.hlsl"
#endif

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Surface/Utility.hlsl"

#ifndef SUPPORTS_FOVEATED_RENDERING_NON_UNIFORM_RASTER
#define FoveatedRemapLinearToNonUniform(uv) uv
#endif

m_CrestNameSpace

// We take the unrefracted scene colour as input because having a Scene Colour node in the graph
// appears to be necessary to ensure the scene colours are bound?
void RefractedScene
(
    const half i_RefractionStrength,
    const half3 i_NormalWS,
    const float2 i_PositionNDC,
    const float i_PixelZ,
    const half3 i_SceneColorUnrefracted,
    const float i_SceneZ,
    const float i_SceneZRaw,
    const bool i_Underwater,
    out half3 o_SceneColor,
    out float o_SceneDistance,
    out float3 o_ScenePositionWS,
    out bool o_Caustics
)
{
    float2 positionNDC = i_PositionNDC;
    float sceneDepthRaw = i_SceneZRaw;

    o_Caustics = true;

    // View ray intersects geometry surface either above or below water surface.
    float2 refractOffset = i_RefractionStrength * i_NormalWS.xz;
    if (!i_Underwater)
    {
        // We're above the water, so behind interface is depth fog.
        refractOffset *= min(1.0, 0.5 * (i_SceneZ - i_PixelZ)) / i_SceneZ;
    }
    else
    {
        // When looking up through water, full strength ends up being quite intense so reduce it a bunch.
        refractOffset *= 0.3;
    }

    // Blend at the edge of the screen to avoid artifacts.
    refractOffset *= 1.0 - EdgeBlendingFactor(positionNDC, i_PixelZ);

    const float2 positionNDCRefracted = FoveatedRemapLinearToNonUniform(positionNDC + refractOffset);
    float sceneDepthRawRefracted = SHADERGRAPH_SAMPLE_SCENE_DEPTH(positionNDCRefracted);

#if (CREST_PORTALS != 0)
#if _ALPHATEST_ON
    // Portals
    ApplyPortalRefractions(positionNDCRefracted, i_SceneZRaw, i_Underwater, sceneDepthRawRefracted, o_Caustics);
#endif
#endif


    const float sceneZRefract = Utility::CrestLinearEyeDepth(sceneDepthRawRefracted);

    // Depth fog & caustics - only if view ray starts from above water.
    // Compute depth fog alpha based on refracted position if it landed on an
    // underwater surface, or on unrefracted depth otherwise.
    if (sceneZRefract > i_PixelZ)
    {
        // Refracted.
        o_SceneDistance = sceneZRefract - i_PixelZ;
        o_SceneColor = SHADERGRAPH_SAMPLE_SCENE_COLOR(positionNDCRefracted);

        positionNDC = positionNDCRefracted;
        sceneDepthRaw = sceneDepthRawRefracted;
    }
    else
    {
        // Unrefracted.
        // It seems that when MSAA is enabled this can sometimes be negative.
        o_SceneDistance = max(i_SceneZ - i_PixelZ, 0.0);
        o_SceneColor = i_SceneColorUnrefracted;

        // NOTE: Causes refraction artifact with caustics. Cannot remember exactly why this was added.
        // o_Caustics = false;
        positionNDC = FoveatedRemapLinearToNonUniform(positionNDC);
    }

    if (i_Underwater)
    {
        // Depth fog is handled by underwater shader.
        o_SceneDistance = i_PixelZ;
    }

    o_ScenePositionWS = ComputeWorldSpacePosition(positionNDC, sceneDepthRaw, UNITY_MATRIX_I_VP);
#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
    o_ScenePositionWS += _WorldSpaceCameraPos;
#endif
}

m_CrestNameSpaceEnd

#endif
