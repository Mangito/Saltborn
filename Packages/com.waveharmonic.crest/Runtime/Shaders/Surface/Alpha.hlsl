// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef CREST_WATER_ALPHA_H
#define CREST_WATER_ALPHA_H

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/InputsDriven.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Helpers.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Cascade.hlsl"

m_CrestNameSpace

float ClipSurface(const float2 i_PositionWSXZ)
{
    // Do not include transition slice to avoid blending as we do a black border instead.
    uint slice0; uint slice1; float alpha;
    PosToSliceIndices(i_PositionWSXZ, 0.0, g_Crest_LodCount - 1.0, g_Crest_WaterScale, slice0, slice1, alpha);

    const Cascade cascade0 = Cascade::Make(slice0);
    const Cascade cascade1 = Cascade::Make(slice1);
    const float weight0 = (1.0 - alpha) * cascade0._Weight;
    const float weight1 = (1.0 - weight0) * cascade1._Weight;

    float value = 0.0;

    if (weight0 > m_CrestSampleLodThreshold)
    {
        Cascade::MakeClip(slice0).SampleClip(i_PositionWSXZ, weight0, value);
    }
    if (weight1 > m_CrestSampleLodThreshold)
    {
        Cascade::MakeClip(slice1).SampleClip(i_PositionWSXZ, weight1, value);
    }

    return lerp(g_Crest_ClipByDefault, value, weight0 + weight1);
}

m_CrestNameSpaceEnd

#endif
