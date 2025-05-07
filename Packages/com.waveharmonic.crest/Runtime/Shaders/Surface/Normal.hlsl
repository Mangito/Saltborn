// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef CREST_WATER_NORMAL_H
#define CREST_WATER_NORMAL_H

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings.Crest.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Texture.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Flow.hlsl"

#if (CREST_SHIFTING_ORIGIN != 0)
#include "Packages/com.waveharmonic.crest.shifting-origin/Runtime/Shaders/ShiftingOrigin.hlsl"
#endif

m_CrestNameSpace

half2 SampleNormalMaps
(
    const TiledTexture i_NormalMap,
    const half i_Strength,
    const float2 i_UndisplacedXZ,
    const float i_LodAlpha,
    const Cascade i_CascadeData
)
{
    float2 worldXZUndisplaced = i_UndisplacedXZ;

#if (CREST_SHIFTING_ORIGIN != 0)
    // Apply tiled floating origin offset. Always needed.
    worldXZUndisplaced -= ShiftingOriginOffset(i_NormalMap, i_CascadeData);
#endif

    const float2 v0 = float2(0.94, 0.34), v1 = float2(-0.85, -0.53);
    float scale = i_NormalMap._scale * i_CascadeData._Scale / 10.0;
    const float spdmulL = _Crest_ChunkNormalScrollSpeed.x * i_NormalMap._speed;
    half2 norm =
        UnpackNormal(i_NormalMap.Sample((worldXZUndisplaced + v0 * g_Crest_Time * spdmulL) / scale)).xy +
        UnpackNormal(i_NormalMap.Sample((worldXZUndisplaced + v1 * g_Crest_Time * spdmulL) / scale)).xy;

    // blend in next higher scale of normals to obtain continuity
    const half nblend = i_LodAlpha * _Crest_ChunkFarNormalsWeight;
    if (nblend > 0.001)
    {
        // next lod level
        scale *= 2.0;
        const float spdmulH = _Crest_ChunkNormalScrollSpeed.y * i_NormalMap._speed;
        norm = lerp(norm,
            UnpackNormal(i_NormalMap.Sample((worldXZUndisplaced + v0 * g_Crest_Time * spdmulH) / scale)).xy +
            UnpackNormal(i_NormalMap.Sample((worldXZUndisplaced + v1 * g_Crest_Time * spdmulH) / scale)).xy,
            nblend);
    }

    // approximate combine of normals. would be better if normals applied in local frame.
    return i_Strength * norm;
}

half2 SampleNormalMaps
(
    const Flow i_Flow,
    const TiledTexture i_NormalMap,
    const half i_Strength,
    const float2 i_UndisplacedXZ,
    const float i_LodAlpha,
    const Cascade i_CascadeData
)
{
    return SampleNormalMaps
    (
        i_NormalMap,
        i_Strength,
        i_UndisplacedXZ - i_Flow._Flow * (i_Flow._Offset0 - i_Flow._Period * 0.5),
        i_LodAlpha,
        i_CascadeData
    ) * i_Flow._Weight0 + SampleNormalMaps
    (
        i_NormalMap,
        i_Strength,
        i_UndisplacedXZ - i_Flow._Flow * (i_Flow._Offset1 - i_Flow._Period * 0.5),
        i_LodAlpha,
        i_CascadeData
    ) * i_Flow._Weight1;
}

void WaterNormal
(
    const float2 i_WaterLevelDerivatives,
    const half3 i_ViewDirectionWS,
    const half i_MinimumReflectionDirectionY,
    const bool i_Underwater,
    inout half3 io_NormalWS
)
{
    // Account for water level changes which change angle of water surface, impacting normal.
    io_NormalWS.xz += -i_WaterLevelDerivatives;

    // Finalise normal
    io_NormalWS = normalize(io_NormalWS);

    if (i_Underwater)
    {
        return;
    }

    // Limit how close to horizontal reflection ray can get, useful to avoid unsightly below-horizon reflections.
    {
        float3 refl = reflect(-i_ViewDirectionWS, io_NormalWS);
        if (refl.y < i_MinimumReflectionDirectionY)
        {
            // Find the normal that keeps the reflection direction above the horizon. Compute
            // the reflection dir that does work, normalize it, and then normal is half vector
            // between this good reflection direction and view direction.
            float3 FL = refl;
            FL.y = i_MinimumReflectionDirectionY;
            FL = normalize(FL);
            io_NormalWS = normalize(FL + i_ViewDirectionWS);
        }
    }
}

m_CrestNameSpaceEnd

#endif
