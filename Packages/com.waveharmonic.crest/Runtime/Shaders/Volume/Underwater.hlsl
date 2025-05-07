// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Volume/UnderwaterShared.hlsl"

#ifndef SUPPORTS_FOVEATED_RENDERING_NON_UNIFORM_RASTER
#define FoveatedRemapLinearToNonUniform(uv) uv
#endif

m_CrestNameSpace

struct Attributes
{
#if CREST_WATER_VOLUME
    float3 positionOS : POSITION;
#else
    uint id : SV_VertexID;
#endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings Vertex(Attributes input)
{
    Varyings output;
    ZERO_INITIALIZE(Varyings, output);
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

#if CREST_WATER_VOLUME
    // Use actual geometry instead of full screen triangle.
    output.positionCS = TransformObjectToHClip(input.positionOS);
#else
    output.positionCS = GetFullScreenTriangleVertexPosition(input.id, UNITY_RAW_FAR_CLIP_VALUE);
#endif

    return output;
}

half4 Fragment(Varyings input)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    uint2 positionSS = input.positionCS.xy;
    float mask = LOAD_TEXTURE2D_X(_Crest_WaterMaskTexture, positionSS).x;

    const float2 uv = FoveatedRemapLinearToNonUniform(positionSS / _ScreenSize.xy);

#if !_DEBUG_VISUALIZE_MASK
#if !d_Meniscus
    // Preserve alpha channel.
    if (mask > CREST_MASK_BELOW_SURFACE)
    {
        discard;
    }
#endif
#endif

    float rawDepth = LoadCameraDepth(positionSS);
    half3 sceneColour = LOAD_TEXTURE2D_X(_Crest_CameraColorTexture, positionSS).rgb;
    const float rawMaskDepth = LOAD_TEXTURE2D_X(_Crest_WaterMaskDepthTexture, positionSS).x;

#if _DEBUG_VISUALIZE_STENCIL
    return DebugRenderStencil(sceneColour);
#endif

    bool isWaterSurface; bool isUnderwater; bool hasCaustics; bool hasMeniscus; float sceneZ; float meniscusRawDepth;
    GetWaterSurfaceAndUnderwaterData(input.positionCS, positionSS, rawMaskDepth, mask, rawDepth, meniscusRawDepth, isWaterSurface, isUnderwater, hasCaustics, hasMeniscus, sceneZ);

    float wt = 1.0;

    if (hasMeniscus)
    {
        wt = ComputeMeniscusWeight(positionSS, mask, _Crest_HorizonNormal, sceneZ);
    }

#if !_DEBUG_VISUALIZE_MASK
#if d_Meniscus
    // Preserve alpha channel.
    if (!isUnderwater && wt >= 1.0)
    {
        discard;
    }
#endif
#endif

    float fogDistance = sceneZ;
    float meniscusDepth = 0.0;
#if defined(CREST_WATER_VOLUME) || defined(CREST_WATER_VOLUME_FULLSCREEN)
    ApplyWaterVolumeToUnderwaterFogAndMeniscus(input.positionCS, meniscusRawDepth, fogDistance, meniscusDepth);
#endif

#if _DEBUG_VISUALIZE_MASK
    return DebugRenderWaterMask(isWaterSurface, isUnderwater, mask, sceneColour);
#endif

    if (isUnderwater)
    {
        float3 positionWS = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
        const half3 view = GetWorldSpaceNormalizeViewDir(positionWS);
#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
        positionWS += _WorldSpaceCameraPos;
#endif
        sceneColour = ApplyUnderwaterEffect(sceneColour, rawDepth, sceneZ, fogDistance, view, positionSS, positionWS, hasCaustics);
    }

    return half4(wt * sceneColour, 1.0);
}

half4 FragmentPlanarReflections(Varyings input)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    const uint2 positionSS = input.positionCS.xy;
    float depth = LoadCameraDepth(positionSS);

    // TODO: Do something nicer. Could zero alpha if scene depth is above threshold.
    if (depth == 0.0)
    {
        return half4(_Crest_Scattering.xyz, 1.0);
    }

    half3 color = LOAD_TEXTURE2D_X(_Crest_CameraColorTexture, positionSS).rgb;

    // Calculate position and account for possible NaNs discovered during testing.
    float3 positionWS;
    {
        float4 positionCS  = ComputeClipSpacePosition(positionSS / _ScreenSize.xy, depth);
        float4 hpositionWS = mul(UNITY_MATRIX_I_VP, positionCS);

        // w is sometimes zero when using oblique projection.
        // Zero is better than NaN.
        positionWS = hpositionWS.w > 0.0 ? hpositionWS.xyz / hpositionWS.w : 0.0;
    }

#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
    positionWS += _WorldSpaceCameraPos;
#endif

    const half3 view = GetWorldSpaceNormalizeViewDir(positionWS);
    const bool hasCaustics = depth > 0.0;

    color = ApplyUnderwaterEffect(color, depth, 0.0, 0.0, view, positionSS, positionWS, hasCaustics);

    return half4(color, 1.0);
}

m_CrestNameSpaceEnd

m_CrestVertex
m_CrestFragment(half4)

half4 FragmentPlanarReflections(m_Crest::Varyings input) : SV_Target
{
    return m_Crest::FragmentPlanarReflections(input);
}
