// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Renders the water horizon line into the mask.

#ifndef CREST_UNDERWATER_MASK_HORIZON_SHARED_INCLUDED
#define CREST_UNDERWATER_MASK_HORIZON_SHARED_INCLUDED

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Constants.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Depth.hlsl"

// Driven by scripting. It is a non-linear converted from a linear 0-1 value.
float _Crest_FarPlaneOffset;

struct Attributes
{
    uint id : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings Vertex(Attributes input)
{
    // This will work for all pipelines.
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.positionCS = GetFullScreenTriangleVertexPosition(input.id, _Crest_FarPlaneOffset);
    output.uv = GetFullScreenTriangleTexCoord(input.id);

    return output;
}

half4 Fragment(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float3 positionWS = ComputeWorldSpacePosition(input.uv, _Crest_FarPlaneOffset, UNITY_MATRIX_I_VP);

#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
    positionWS.y += _WorldSpaceCameraPos.y;
#endif

    return (half4) positionWS.y > g_Crest_WaterCenter.y
        ? CREST_MASK_ABOVE_SURFACE
        : CREST_MASK_BELOW_SURFACE;
}

#endif // CREST_UNDERWATER_MASK_HORIZON_SHARED_INCLUDED
