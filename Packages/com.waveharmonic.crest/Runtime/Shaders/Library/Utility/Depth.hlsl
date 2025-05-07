// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Helpers that will only be used for shaders (eg depth, lighting etc).

#ifndef d_WaveHarmonic_Utility_Depth
#define d_WaveHarmonic_Utility_Depth

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Macros.hlsl"

// Silence Unity errors in SG editor.
#ifdef SHADERGRAPH_PREVIEW
#define LOAD_DEPTH_TEXTURE_X(a, b) 0
#define TEXTURE2D_X(t) Texture2D t
#else
#define LOAD_DEPTH_TEXTURE_X(textureName, coord2) LOAD_TEXTURE2D_X(textureName, coord2).r
#endif

m_UtilityNameSpace

// Taken from:
// https://www.cyanilux.com/tutorials/depth/#depth-output
float LinearDepthToNonLinear(float depth, float4 zBufferParameters)
{
    return (1.0 - depth * zBufferParameters.y) / (depth * zBufferParameters.x);
}

// Taken from:
// https://www.cyanilux.com/tutorials/depth/#depth-output
float EyeDepthToNonLinear(float depth, float4 zBufferParameters)
{
    return (1.0 - depth * zBufferParameters.w) / (depth * zBufferParameters.z);
}

// Same as LinearEyeDepth except supports orthographic projection. Use projection keywords to restrict support to either
// of these modes as an optimisation.
float CrestLinearEyeDepth(const float i_rawDepth)
{
#if !defined(_PROJECTION_ORTHOGRAPHIC)
    // Handles UNITY_REVERSED_Z for us.
#if defined(UNITY_CG_INCLUDED)
    float perspective = LinearEyeDepth(i_rawDepth);
#elif defined(UNITY_COMMON_INCLUDED)
    float perspective = LinearEyeDepth(i_rawDepth, _ZBufferParams);
#endif
#endif // _PROJECTION

#if !defined(_PROJECTION_PERSPECTIVE)
    // Orthographic Depth taken and modified from:
    // https://github.com/keijiro/DepthInverseProjection/blob/master/Assets/InverseProjection/Resources/InverseProjection.shader
    float near = _ProjectionParams.y;
    float far  = _ProjectionParams.z;
    float isOrthographic = unity_OrthoParams.w;

#if defined(UNITY_REVERSED_Z)
    float orthographic = lerp(far, near, i_rawDepth);
#else
    float orthographic = lerp(near, far, i_rawDepth);
#endif // UNITY_REVERSED_Z
#endif // _PROJECTION

#if defined(_PROJECTION_ORTHOGRAPHIC)
    return orthographic;
#elif defined(_PROJECTION_PERSPECTIVE)
    return perspective;
#else
    // If a shader does not have the projection enumeration, then assume they want to support both projection modes.
    return lerp(perspective, orthographic, isOrthographic);
#endif // _PROJECTION
}

m_UtilityNameSpaceEnd

#endif // d_WaveHarmonic_Utility_Depth
