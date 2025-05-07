// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef CREST_WATER_REFLECTION_H
#define CREST_WATER_REFLECTION_H

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Surface/Utility.hlsl"

float4 _Crest_ReflectionPositionNormal[2];
Texture2DArray _Crest_ReflectionTexture;
SamplerState sampler_Crest_ReflectionTexture;

m_CrestNameSpace

half4 PlanarReflection
(
    const Texture2DArray i_ReflectionsTexture,
    const SamplerState i_ReflectionsSampler,
    const half i_Intensity,
    const half i_Smoothness,
    const half i_Roughness,
    const half3 i_NormalWS,
    const half i_NormalStrength,
    const half3 i_ViewDirectionWS,
    const float2 i_PositionNDC,
    const bool i_Underwater
)
{
    half3 planeNormal = half3(0.0, i_Underwater ? -1.0 : 1.0, 0.0);
    half3 reflected = reflect(-i_ViewDirectionWS, lerp(planeNormal, i_NormalWS, i_NormalStrength));
    reflected.y = -reflected.y;

    float4 positionCS = mul(UNITY_MATRIX_VP, half4(reflected, 0.0));
#if UNITY_UV_STARTS_AT_TOP
    positionCS.y = -positionCS.y;
#endif

    float2 positionNDC = positionCS.xy * rcp(positionCS.w) * 0.5 + 0.5;

    // Cancel out distortion if out of bounds. We could make this nicer by doing an edge fade but the improvement is
    // barely noticeable. Edge fade requires recalculating the above a second time.
    {
        float4 positionAndNormal = _Crest_ReflectionPositionNormal[i_Underwater];
        if (dot(positionNDC - positionAndNormal.xy, positionAndNormal.zw) < 0.0)
        {
            positionNDC = lerp(i_PositionNDC, positionNDC, 0.25);
        }
    }

    const half roughness = PerceptualSmoothnessToPerceptualRoughness(i_Smoothness);
    const half level = PerceptualRoughnessToMipmapLevel(roughness, i_Roughness);
    half4 reflection = i_ReflectionsTexture.SampleLevel(sampler_Crest_ReflectionTexture, float3(positionNDC, i_Underwater), level);

    // If more than four layers are used on the terrain, they will appear black if HDR
    // is enabled on the planar reflection camera. Alpha is probably a negative value.
    reflection.a = saturate(reflection.a);

    reflection.a *= i_Intensity;

    return reflection;
}

m_CrestNameSpaceEnd

#endif
