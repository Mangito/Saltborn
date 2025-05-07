// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Soft shadow term is red, hard shadow term is green. In HDRP, hard shadows are not computed and y channel will be 0.

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Constants.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/InputsDriven.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Cascade.hlsl"
// Noise functions used for jitter.
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Noise/Noise.hlsl"

CBUFFER_START(CrestPerMaterial)
// Settings._jitterDiameterSoft, Settings._jitterDiameterHard, Settings._currentFrameWeightSoft, Settings._currentFrameWeightHard
float4 _Crest_JitterDiameters_CurrentFrameWeights;
float _Crest_SimDeltaTime;

bool _Crest_ClearShadows;

float3 _Crest_CenterPos;
float3 _Crest_Scale;
float4x4 _Crest_MainCameraProjectionMatrix;

bool _Crest_SampleColorMap;
float3 _Crest_Absorption;
float3 _Crest_Scattering;
CBUFFER_END

m_CrestNameSpace

struct Attributes
{
    uint id : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float3 positionWS : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings Vertex(Attributes input)
{
    // This will work for all pipelines.
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.positionCS = GetFullScreenTriangleVertexPosition(input.id);
    float2 uv = GetFullScreenTriangleTexCoord(input.id);

    // World position from UV.
    output.positionWS.xyz = float3(uv.x - 0.5, 0.0, uv.y - 0.5) * _Crest_Scale * 4.0 + _Crest_CenterPos;
    output.positionWS.y = g_Crest_WaterCenter.y;

    return output;
}

half SampleShadows(const float4 i_positionWS);
half ComputeShadowFade(const float4 i_positionWS);

// Compiler shows warning when using intermediate returns, disable this.
#pragma warning(push)
#pragma warning(disable: 4000)
half ComputeShadow(const float4 i_positionWS, const float i_jitterDiameter, const half i_terrainHeight)
{
    float4 positionWS = i_positionWS;

    if (i_jitterDiameter > 0.0)
    {
        // Add jitter.
        positionWS.xz += i_jitterDiameter * (hash33(uint3(abs(positionWS.xz * 10.0), _Time.y * 120.0)) - 0.5).xy;

        // Shadow Bleeding.
        // If we are not within a terrain, then check for shadow bleeding.
        if (i_positionWS.y > i_terrainHeight)
        {
            // WorldToSafeUV
            half terrainHeight = Cascade::MakeDepth(_Crest_LodIndex).SampleSceneHeight(positionWS.xz);

            // If our current position is below the jittered terrain height, then we have landed within a terrain and
            // we do not want to sample those shadows.
            if (i_positionWS.y < terrainHeight)
            {
                // Return no shadows.
                return 1.0;
            }
        }
    }

    return SampleShadows(positionWS);
}
#pragma warning(pop)

half2 Fragment(Varyings input)
{
    float4 positionWS = float4(input.positionWS.xyz, 1.0);

    // Shadow from last frame. Manually implement black border.
    half2 shadow = Cascade::MakeShadowSource(_Crest_LodIndex + g_Crest_LodChange).SampleShadowOverflow(positionWS.xz, 1.0);

    // Add displacement so shorelines do not receive shadows incorrectly.
    positionWS.xyz += Cascade::MakeAnimatedWaves(_Crest_LodIndex).SampleDisplacement(positionWS.xz);

    // This was calculated in vertex but we have to sample sea level offset in fragment.
    float4 mainCameraCoordinates = mul(_Crest_MainCameraProjectionMatrix, positionWS);

    // Check if the current sample is visible in the main camera (and therefore the shadow map can be sampled). This is
    // required as the shadow buffer is world aligned and surrounds viewer.
    float3 projected = mainCameraCoordinates.xyz / mainCameraCoordinates.w;
    if (projected.z < 1.0 && projected.z > 0.0 && abs(projected.x) < 1.0 && abs(projected.y) < 1.0)
    {
        half2 shadowThisFrame = 1.0;

#if (SHADEROPTIONS_CAMERA_RELATIVE_RENDERING != 0)
        positionWS.xyz -= _WorldSpaceCameraPos.xyz;
#endif

        half terrainHeight = Cascade::MakeDepth(_Crest_LodIndex).SampleSceneHeight(positionWS.xz);

        half softJitter = _Crest_JitterDiameters_CurrentFrameWeights[CREST_SHADOW_INDEX_SOFT];

        if (_Crest_SampleColorMap)
        {
            half3 absorption = _Crest_Absorption;
            half3 scattering = _Crest_Scattering;

            if (g_Crest_SampleAbsorptionSimulation)
            {
                absorption = Cascade::MakeAbsorption(_Crest_LodIndex).SampleAbsorption(positionWS.xz);
            }

            if (g_Crest_SampleScatteringSimulation)
            {
                scattering = Cascade::MakeScattering(_Crest_LodIndex).SampleScattering(positionWS.xz);
            }

            half3 extinction = absorption + scattering;
            half factor = saturate(min(min(extinction.x, extinction.y), extinction.z) * g_Crest_DynamicSoftShadowsFactor);
            softJitter = (1.0 - factor) * k_Crest_MaximumShadowJitter;
        }

        // Add soft shadowing data.
        shadowThisFrame[CREST_SHADOW_INDEX_SOFT] = ComputeShadow
        (
            positionWS,
            softJitter,
            terrainHeight
        );

#ifdef CREST_SAMPLE_SHADOW_HARD
        // Add hard shadowing data.
        shadowThisFrame[CREST_SHADOW_INDEX_HARD] = ComputeShadow
        (
            positionWS,
            _Crest_JitterDiameters_CurrentFrameWeights[CREST_SHADOW_INDEX_HARD],
            terrainHeight
        );
#endif

        shadowThisFrame = (half2)1.0 - saturate(shadowThisFrame + ComputeShadowFade(positionWS));

        shadow = lerp(shadow, shadowThisFrame, _Crest_JitterDiameters_CurrentFrameWeights.zw * _Crest_SimDeltaTime * 60.0);
    }

    return shadow;
}

m_CrestNameSpaceEnd

m_CrestVertex
m_CrestFragment(half2)
