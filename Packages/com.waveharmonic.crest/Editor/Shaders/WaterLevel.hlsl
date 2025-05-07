// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef d_WaterLevelDepth
#define d_WaterLevelDepth

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/InputsDriven.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Cascade.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Surface/Geometry.hlsl"

m_CrestNameSpace

struct Attributes
{
    float3 positionOS : POSITION;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
};

Varyings Vertex(Attributes attributes)
{
    // This will work for all pipelines.
    Varyings varyings = (Varyings)0;

    const Cascade cascade0 = Cascade::Make(_Crest_LodIndex);
    const Cascade cascade1 = Cascade::Make(_Crest_LodIndex + 1);

    float3 positionWS = mul(UNITY_MATRIX_M, float4(attributes.positionOS.xyz, 1.0)).xyz;

#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
    positionWS.xz += _WorldSpaceCameraPos.xz;
#endif

    float alpha;
    SnapAndTransitionVertLayout(_Crest_ChunkMeshScaleAlpha, cascade0, _Crest_ChunkGeometryGridWidth, positionWS, alpha);

    {
        // :WaterGridPrecisionErrors
        float2 center = UNITY_MATRIX_M._m03_m23;
#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
        center += _WorldSpaceCameraPos.xz;
#endif
        const float2 camera = abs(_WorldSpaceCameraPos.xz);
        positionWS.xz = lerp(center, positionWS.xz, lerp(1.0, 1.01, max(camera.x, camera.y) * 0.00001));
    }

    const float weight0 = (1.0 - alpha) * cascade0._Weight;
    const float weight1 = (1.0 - weight0) * cascade1._Weight;

    half offset = 0.0;
    if (weight0 > m_CrestSampleLodThreshold)
    {
        Cascade::MakeLevel(_Crest_LodIndex).SampleLevel(positionWS.xz, weight0, offset);
    }
    if (weight1 > m_CrestSampleLodThreshold)
    {
        Cascade::MakeLevel(_Crest_LodIndex + 1).SampleLevel(positionWS.xz, weight1, offset);
    }

    positionWS.y += offset;

#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
    positionWS.xz -= _WorldSpaceCameraPos.xz;
#endif

    varyings.positionCS = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));

    return varyings;
}

half4 Fragment(Varyings varyings)
{
    return half4(0.0, 0.0, 0.0, 1.0);
}

m_CrestNameSpaceEnd

m_CrestVertex
m_CrestFragment(half4)

#endif // d_WaterLevelDepth
