// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#pragma multi_compile_fragment _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile_fragment _ _SHADOWS_SOFT

#define CREST_URP 1
#define CREST_SHADERGRAPH_CONSTANTS_H

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

TEXTURE2D_X(_CameraDepthTexture); SAMPLER(sampler_CameraDepthTexture);

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"

m_CrestNameSpace

// Shim as HDRP uses this.
float LoadCameraDepth(uint2 pixelCoords)
{
    return LOAD_TEXTURE2D_X(_CameraDepthTexture, pixelCoords).r;
}

m_CrestNameSpaceEnd
