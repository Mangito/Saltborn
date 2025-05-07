// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef CREST_UNDERWATER_MASK_SHARED_INCLUDED
#define CREST_UNDERWATER_MASK_SHARED_INCLUDED

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings.Crest.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Constants.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/InputsDriven.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Helpers.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Cascade.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Surface/Geometry.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Depth.hlsl"

#if (CREST_PORTALS != 0)
#include "Packages/com.waveharmonic.crest.portals/Runtime/Shaders/Library/Portals.hlsl"
#endif

// Variable mask for when fog is applied before transparent pass and water tile might be culled.
half _Crest_MaskBelowSurface;

m_CrestNameSpace

struct Attributes
{
    // The old unity macros require this name and type.
    float4 vertex : POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings Vertex(Attributes v)
{
    // This will work for all pipelines.
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    const Cascade cascade0 = Cascade::Make(_Crest_LodIndex);
    const Cascade cascade1 = Cascade::Make(_Crest_LodIndex + 1);

    float3 worldPos = mul(UNITY_MATRIX_M, float4(v.vertex.xyz, 1.0)).xyz;

#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
    worldPos.xz += _WorldSpaceCameraPos.xz;
#endif

    // Vertex snapping and lod transition
    float lodAlpha;
    SnapAndTransitionVertLayout(_Crest_ChunkMeshScaleAlpha, cascade0, _Crest_ChunkGeometryGridWidth, worldPos, lodAlpha);

    {
        // Scale up by small "epsilon" to solve numerical issues. Expand slightly about tile center.
        // :WaterGridPrecisionErrors
        float2 tileCenterXZ = UNITY_MATRIX_M._m03_m23;
#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
        tileCenterXZ += _WorldSpaceCameraPos.xz;
#endif
        const float2 cameraPositionXZ = abs(_WorldSpaceCameraPos.xz);
        // Scale "epsilon" by distance from zero. There is an issue where overlaps can cause SV_IsFrontFace
        // to be flipped (needs to be investigated). Gaps look bad from above surface, and overlaps look bad
        // from below surface. We want to close gaps without introducing overlaps. A fixed "epsilon" will
        // either not solve gaps at large distances or introduce too many overlaps at small distances. Even
        // with scaling, there are still unsolvable overlaps underwater (especially at large distances).
        // 100,000 (0.00001) is the maximum position before Unity warns the user of precision issues.
        worldPos.xz = lerp(tileCenterXZ, worldPos.xz, lerp(1.0, 1.01, max(cameraPositionXZ.x, cameraPositionXZ.y) * 0.00001));
    }

    // Calculate sample weights. params.z allows shape to be faded out (used on last lod to support pop-less scale transitions)
    const float wt_smallerLod = (1.0 - lodAlpha) * cascade0._Weight;
    const float wt_biggerLod = (1.0 - wt_smallerLod) * cascade1._Weight;
    // Sample displacement textures, add results to current world pos / normal / foam
    const float2 positionWS_XZ_before = worldPos.xz;

    // Data that needs to be sampled at the undisplaced position
    if (wt_smallerLod > m_CrestSampleLodThreshold)
    {
        Cascade::MakeAnimatedWaves(_Crest_LodIndex).SampleDisplacement(positionWS_XZ_before, wt_smallerLod, worldPos);
    }
    if (wt_biggerLod > m_CrestSampleLodThreshold)
    {
        Cascade::MakeAnimatedWaves(_Crest_LodIndex + 1).SampleDisplacement(positionWS_XZ_before, wt_biggerLod, worldPos);
    }

#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
    worldPos.xz -= _WorldSpaceCameraPos.xz;
#endif

    output.positionCS = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));

    return output;
}

half4 Fragment(const Varyings input, const bool i_isFrontFace)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    half4 result = 0.0;

#if (CREST_PORTALS != 0)
#if !d_Tunnel
    bool masked = false;

    if (m_CrestPortal)
    {
        masked = ApplyVolumeToWaterMask(input.positionCS);
    }
#endif
#endif

    if (IsUnderwater(i_isFrontFace, g_Crest_ForceUnderwater))
    {
        result = (half4)_Crest_MaskBelowSurface;
    }
    else
    {
        result = (half4)CREST_MASK_ABOVE_SURFACE;
    }

#if (CREST_PORTALS != 0)
#if d_Tunnel
    const float rawFrontFaceZ = LOAD_DEPTH_TEXTURE_X(_Crest_WaterVolumeFrontFaceTexture, input.positionCS.xy);
    const float rawBackFaceZ = LOAD_DEPTH_TEXTURE_X(_Crest_WaterVolumeBackFaceTexture, input.positionCS.xy);
    if (rawFrontFaceZ <= 0.0 && rawBackFaceZ > 0.0)
    {
        result = (half4)CREST_MASK_ABOVE_SURFACE;
    }
#else
    if (m_CrestPortal)
    {
        result *= masked ? 2 : 1;
    }
#endif
#endif

    return result;
}

m_CrestNameSpaceEnd

m_CrestVertex
m_CrestFragmentWithFrontFace(half4)

#endif // CREST_UNDERWATER_MASK_SHARED_INCLUDED
