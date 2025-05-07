// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// LOD data - data, samplers and functions associated with LODs

#ifndef CREST_WATER_HELPERS_H
#define CREST_WATER_HELPERS_H

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/InputsDriven.hlsl"

m_CrestNameSpace

#define m_Blend(type) \
    type Blend(const int i_Blend, const float i_Alpha, const float i_DeltaTime, const type i_Source, const type i_Target) \
    { \
        switch (i_Blend) \
        { \
            case m_CrestBlendMinimum: \
                return min(i_Target, i_Source * i_Alpha); \
            case m_CrestBlendMaximum: \
                return max(i_Target, i_Source * i_Alpha); \
            case m_CrestBlendAdditive: \
                return i_Target + i_Source * i_Alpha * i_DeltaTime; \
            case m_CrestBlendAlpha: \
                return lerp(i_Target, i_Source, i_Alpha); \
            case m_CrestBlendNone: \
            default: \
                return i_Source * i_Alpha; \
        } \
    } \

m_Blend(float)
m_Blend(float2)
m_Blend(float3)
m_Blend(float4)

uint PositionToSliceIndex
(
    const float2 i_PositionXZ,
    const float i_MinimumSlice,
    const float i_WaterScale0
)
{
    const float2 offsetFromCenter = abs(i_PositionXZ - g_Crest_WaterCenter.xz);
    const float taxicab = max(offsetFromCenter.x, offsetFromCenter.y);
    const float radius0 = i_WaterScale0;
    float sliceNumber = log2(max(taxicab / radius0, 1.0));
    // Don't use last slice - this is a "transition" slice used to cross fade waves
    // between LOD resolutions to avoid pops.
    sliceNumber = clamp(sliceNumber, i_MinimumSlice, g_Crest_LodCount - 2.0);
    return floor(sliceNumber);
}

void PosToSliceIndices
(
    const float2 worldXZ,
    const float minSlice,
    const float maxSlice,
    const float waterScale0,
    out uint slice0,
    out uint slice1,
    out float lodAlpha
)
{
    const float2 offsetFromCenter = abs(worldXZ - g_Crest_WaterCenter.xz);
    const float taxicab = max(offsetFromCenter.x, offsetFromCenter.y);
    const float radius0 = waterScale0;
    float sliceNumber = log2( max( taxicab / radius0, 1.0 ) );
    sliceNumber = clamp( sliceNumber, minSlice, maxSlice );

    lodAlpha = frac(sliceNumber);

    // Fixes artefact with DX12 & Vulkan. Likely a compiler bug.
    // Sampling result appears to be all over the place.
    slice0 = floor(sliceNumber) + 0.01;
    slice1 = slice0 + 1;

    // lod alpha is remapped to ensure patches weld together properly. patches can vary significantly in shape (with
    // strips added and removed), and this variance depends on the base density of the mesh, as this defines the strip width.
    // using .15 as black and .85 as white should work for base mesh density as low as 16.
    const float BLACK_POINT = 0.15, WHITE_POINT = 0.85;
    lodAlpha = saturate((lodAlpha - BLACK_POINT) / (WHITE_POINT - BLACK_POINT));

    if (slice0 == 0)
    {
        // blend out lod0 when viewpoint gains altitude. we're using the global g_Crest_MeshScaleLerp so check for LOD0 is necessary
        lodAlpha = min(lodAlpha + g_Crest_MeshScaleLerp, 1.0);
    }
}

bool IsUnderwater(const bool i_FrontFace, const int i_ForceUnderwater)
{
    // We are well below water.
    if (i_ForceUnderwater == 1)
    {
        return true;
    }

    // We are well above water.
    if (i_ForceUnderwater == 2)
    {
        return false;
    }

    return !i_FrontFace;
}

float FeatherWeightFromUV(const float2 i_uv, const half i_featherWidth)
{
    float2 offset = abs(i_uv - 0.5);
    float r_l1 = max(offset.x, offset.y) - (0.5 - i_featherWidth);
    if (i_featherWidth > 0.0) r_l1 /= i_featherWidth;
    float weight = saturate(1.0 - r_l1);
    return weight;
}

bool WithinUV(const float2 i_UV)
{
    const float2 d = abs(i_UV - 0.5);
    return max(d.x, d.y) <= 0.5;
}

m_CrestNameSpaceEnd

#endif // CREST_WATER_HELPERS_H
