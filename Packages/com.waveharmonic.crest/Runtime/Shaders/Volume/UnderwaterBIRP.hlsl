// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#define BUILTIN_TARGET_API 1

#define CREST_BIRP 1
#define CREST_SHADERGRAPH_CONSTANTS_H

#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Shim/Shims.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/LegacySurfaceVertex.hlsl"
#include "Packages/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/ShaderLibrary/ShaderGraphFunctions.hlsl"

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Legacy/InputsDriven.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Legacy/Common.hlsl"

TEXTURE2D_X(_CameraDepthTexture); SAMPLER(sampler_CameraDepthTexture);

m_CrestNameSpace

float LoadCameraDepth(uint2 pixelCoords)
{
    return LOAD_TEXTURE2D_X(_CameraDepthTexture, pixelCoords).r;
}

m_CrestNameSpaceEnd
