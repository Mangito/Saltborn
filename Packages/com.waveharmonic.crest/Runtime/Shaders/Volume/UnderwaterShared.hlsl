// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef CREST_UNDERWATER_EFFECT_SHARED_INCLUDED
#define CREST_UNDERWATER_EFFECT_SHARED_INCLUDED

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings.Crest.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Constants.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/InputsDriven.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Helpers.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Depth.hlsl"

#if defined(CREST_WATER_VOLUME) || defined(CREST_WATER_VOLUME_FULLSCREEN)
#include "Packages/com.waveharmonic.crest.portals/Runtime/Shaders/Library/Portals.hlsl"
#endif

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Cascade.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Texture.hlsl"

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Lighting.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Surface/VolumeLighting.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Surface/Caustics.hlsl"

// These are set via call to CopyPropertiesFromMaterial() and must have the same
// names as the surface material parameters.
CBUFFER_START(CrestPerMaterial)

//
// Surface Shared
//

half4 _Crest_Absorption;
half4 _Crest_Scattering;
half _Crest_Anisotropy;

bool _Crest_CausticsEnabled;
float _Crest_CausticsTextureScale;
float _Crest_CausticsScrollSpeed;
float _Crest_CausticsTextureAverage;
float _Crest_CausticsStrength;
float _Crest_CausticsFocalDepth;
float _Crest_CausticsDepthOfField;
float _Crest_CausticsDistortionStrength;
float _Crest_CausticsDistortionScale;
half _Crest_CausticsMotionBlur;
float4 _Crest_CausticsTexture_TexelSize;
float4 _Crest_CausticsDistortionTexture_TexelSize;

half _Crest_DirectTerm;
half _Crest_AmbientTerm;
half _Crest_ShadowsAffectsAmbientFactor;

//
// Volume Only
//

float2 _Crest_HorizonNormal;

// Out-scattering. Driven by the Water Renderer and Underwater Environmental Lighting.
float _Crest_VolumeExtinctionLength;
float _Crest_UnderwaterEnvironmentalLightingWeight;

// Also applied to transparent objects.
half _Crest_ExtinctionMultiplier;
half _Crest_SunBoost;
float _Crest_OutScatteringFactor;
float _Crest_OutScatteringExtinctionFactor;
half3 _Crest_AmbientLighting;
int _Crest_DataSliceOffset;

half _Crest_DitheringIntensity;
CBUFFER_END

TEXTURE2D_X(_Crest_WaterMaskTexture);
TEXTURE2D_X(_Crest_WaterMaskDepthTexture);
TEXTURE2D_X(_Crest_CameraColorTexture);

TEXTURE2D(_Crest_CausticsTexture);
SAMPLER(sampler_Crest_CausticsTexture);
TEXTURE2D(_Crest_CausticsDistortionTexture);
SAMPLER(sampler_Crest_CausticsDistortionTexture);

// NOTE: Cannot put this in namespace due to compiler bug. Fixed when using DXC.
static const m_Crest::TiledTexture _Crest_CausticsTiledTexture =
    m_Crest::TiledTexture::Make(_Crest_CausticsTexture, sampler_Crest_CausticsTexture, _Crest_CausticsTexture_TexelSize, _Crest_CausticsTextureScale, _Crest_CausticsScrollSpeed);
static const m_Crest::TiledTexture _Crest_CausticsDistortionTiledTexture =
    m_Crest::TiledTexture::Make(_Crest_CausticsDistortionTexture, sampler_Crest_CausticsDistortionTexture, _Crest_CausticsDistortionTexture_TexelSize, _Crest_CausticsDistortionScale, 1.0);

m_CrestNameSpace

float LinearToDeviceDepth(float linearDepth, float4 zBufferParam)
{
    //linear = 1.0 / (zBufferParam.z * device + zBufferParam.w);
    float device = (1.0 / linearDepth - zBufferParam.w) / zBufferParam.z;
    return device;
}

#if d_Dithering
// Adapted from:
// https://alex.vlachos.com/graphics/Alex_Vlachos_Advanced_VR_Rendering_GDC2015.pdf
float3 ScreenSpaceDither(const float2 i_ScreenPosition)
{
    // Iestyn's RGB dither (7 asm instructions) from Portal 2 X360, slightly modified for VR.
    float3 dither = dot(float2(171.0, 231.0), i_ScreenPosition.xy);
    dither.rgb = frac(dither.rgb / float3(103.0, 71.0, 97.0)) - float3(0.5, 0.5, 0.5);
    return (dither.rgb / 255.0);
}
#endif

float4 DebugRenderWaterMask(const bool isWaterSurface, const bool isUnderwater, const float mask, const float3 sceneColour)
{
    // Red:     surface front face when above water
    // Green:   surface back face when below water
    // Cyan:    background when above water
    // Magenta: background when below water
    if (isWaterSurface)
    {
        return float4(sceneColour * float3(mask >= CREST_MASK_ABOVE_SURFACE, mask <= CREST_MASK_BELOW_SURFACE, 0.0), 1.0);
    }
    else
    {
        return float4(sceneColour * float3(isUnderwater * 0.5, (1.0 - isUnderwater) * 0.5, 1.0), 1.0);
    }
}

float4 DebugRenderStencil(float3 sceneColour)
{
    float3 stencil = 1.0;
#if CREST_WATER_VOLUME_FRONT_FACE
    stencil = float3(1.0, 0.0, 0.0);
#elif CREST_WATER_VOLUME_BACK_FACE
    stencil = float3(0.0, 1.0, 0.0);
#elif CREST_WATER_VOLUME_FULLSCREEN
    stencil = float3(0.0, 0.0, 1.0);
#endif
    return float4(sceneColour * stencil, 1.0);
}

float MeniscusSampleWaterMask(const float mask, const int2 positionSS, const float2 offset, const float magnitude, const float scale)
{
    float2 uv = positionSS + offset * magnitude
#if defined(CREST_WATER_VOLUME) || defined(CREST_WATER_VOLUME_FULLSCREEN)
    * scale
#endif
    ;

    float newMask = LOAD_TEXTURE2D_X(_Crest_WaterMaskTexture, uv).r;

#if CREST_UNDERWATER_BEFORE_TRANSPARENT
    // Normalize mask.
    newMask = clamp(newMask, -1.0, 1.0);
#endif

#if CREST_WATER_VOLUME
    // No mask means no underwater effect so ignore the value.
    return (newMask == CREST_MASK_NONE ? mask : newMask);
#endif
    return newMask;
}

half ComputeMeniscusWeight(const int2 positionSS, float mask, const float2 horizonNormal, const float meniscusDepth)
{
    float weight = 1.0;
#if d_Meniscus
#if !_FULL_SCREEN_EFFECT

#if CREST_UNDERWATER_BEFORE_TRANSPARENT
    // Normalize mask.
    mask = clamp(mask, -1.0, 1.0);
#endif

    // Render meniscus by checking the mask along the horizon normal which is flipped using the surface normal from
    // mask. Adding the mask value will flip the UV when mask is below surface.
    float2 offset = (float2)-mask * horizonNormal;
    float multiplier = 0.9;

#if defined(CREST_WATER_VOLUME) || defined(CREST_WATER_VOLUME_FULLSCREEN)
    // The meniscus at the boundary can be at a distance. We need to scale the offset as 1 pixel at a distance is much
    // larger than 1 pixel up close.
    const float scale = 1.0 - saturate(meniscusDepth / MENISCUS_MAXIMUM_DISTANCE);

    // Exit early.
    if (scale == 0.0)
    {
        return 1.0;
    }
#else
    // Dummy value.
    const float scale = 0.0;
#endif

    // Sample three pixels along the normal. If the sample is different than the current mask, apply meniscus.
    // Offset must be added to positionSS as floats.
    weight *= (MeniscusSampleWaterMask(mask, positionSS, offset, 1.0, scale) != mask) ? multiplier : 1.0;
    weight *= (MeniscusSampleWaterMask(mask, positionSS, offset, 2.0, scale) != mask) ? multiplier : 1.0;
    weight *= (MeniscusSampleWaterMask(mask, positionSS, offset, 3.0, scale) != mask) ? multiplier : 1.0;
#endif // _FULL_SCREEN_EFFECT
#endif // d_Meniscus
    return weight;
}

void GetWaterSurfaceAndUnderwaterData
(
    const float4 positionCS,
    const int2 positionSS,
    const float rawMaskDepth,
    const float mask,
    inout float rawDepth,
    inout float rawMeniscusDepth,
    inout bool isWaterSurface,
    inout bool isUnderwater,
    inout bool hasCaustics,
    inout bool hasMeniscus,
    inout float sceneZ
)
{
    const float rawSceneDepth = rawDepth;
    hasCaustics = rawDepth != 0.0;
    isWaterSurface = false;
    isUnderwater = mask <= CREST_MASK_BELOW_SURFACE;
    hasMeniscus = true;
    rawMeniscusDepth = positionCS.z;

#if defined(CREST_WATER_VOLUME_HAS_BACKFACE) || defined(CREST_WATER_VOLUME_BACK_FACE) || defined(CREST_WATER_VOLUME_NEGATIVE)
    const float rawGeometryDepth =
#if CREST_WATER_VOLUME_HAS_BACKFACE
    // 3D has a back face texture for the depth.
    LOAD_DEPTH_TEXTURE_X(_Crest_WaterVolumeBackFaceTexture, positionSS);
#elif CREST_WATER_VOLUME_NEGATIVE
    LOAD_DEPTH_TEXTURE_X(_Crest_WaterVolumeFrontFaceTexture, positionSS);
#else
    // Volume is rendered using the back face so that is the depth.
    positionCS.z;
#endif // CREST_WATER_VOLUME_HAS_BACKFACE
    ;

    if ((rawMaskDepth > 0.0 && rawMaskDepth <= rawSceneDepth) || (rawGeometryDepth > 0.0 && rawGeometryDepth <= rawSceneDepth))
    {
        hasMeniscus = false;
    }

    // Use backface depth if closest.
    if (rawDepth < rawGeometryDepth)
    {
        // Cancels out caustics.
        hasCaustics = false;
        rawDepth = rawGeometryDepth;
    }

#if CREST_WATER_VOLUME_NEGATIVE
    rawMeniscusDepth = rawGeometryDepth;
    if (rawGeometryDepth == 0.0)
    {
        const float rawBackFaceDepth = LOAD_DEPTH_TEXTURE_X(_Crest_WaterVolumeBackFaceTexture, positionSS);

        // We are in the negative volume. Already handled by front face.
        if (rawBackFaceDepth > 0.0)
        {
            if (rawBackFaceDepth <= rawSceneDepth)
            {
                hasMeniscus = false;
            }

            isUnderwater = false;
        }
    }
#endif // CREST_WATER_VOLUME_NEGATIVE
#endif // CREST_WATER_VOLUME

#if CREST_WATER_VOLUME_FRONT_FACE
    // If negative volume, we keep the mask intact, and just mark it.
    if (mask >= CREST_MASK_ABOVE_SURFACE_KEPT)
    {
        isUnderwater = true;
    }
    else if (mask <= CREST_MASK_BELOW_SURFACE_KEPT)
    {
        isUnderwater = false;
    }
#endif

    // Merge water depth with scene depth.
    if (rawDepth < rawMaskDepth)
    {
#if CREST_UNDERWATER_BEFORE_TRANSPARENT
        // Apply fog to culled tiles otherwise there will be no fog as water shader can only fog enabled tiles. And
        // only apply fog to culled tiles otherwise it will be fogged twice (second by water shader).
        isUnderwater = mask <= CREST_MASK_BELOW_SURFACE_CULLED;
#endif
        isWaterSurface = true;
        hasCaustics = false;
        rawDepth = rawMaskDepth;
    }

    sceneZ = Utility::CrestLinearEyeDepth(rawDepth);
}

void ApplyWaterVolumeToUnderwaterFogAndMeniscus(float4 positionCS, const float meniscusRawDepth, inout float fogDistance, inout float meniscusDepth)
{
#if CREST_WATER_VOLUME_FRONT_FACE
    float depth = Utility::CrestLinearEyeDepth(positionCS.z);
    // Meniscus is rendered at the boundary so use the geometry z.
    meniscusDepth = depth;
    fogDistance -= depth;
#else
    float depth = Utility::CrestLinearEyeDepth(meniscusRawDepth);
    // Meniscus is rendered at the boundary so use the geometry z.
    meniscusDepth = depth;
#endif
}

half3 ApplyUnderwaterEffect(
    half3 sceneColour,
    const float rawDepth,
    const float sceneZ,
    const float fogDistance,
    const half3 view,
    const uint2 i_positionSS,
    const float3 i_positionWS,
    const bool hasCaustics
) {
    const bool isUnderwater = true;

    float3 lightDirection; float3 lightColor;
    PrimaryLight(i_positionWS, lightColor, lightDirection);

    // Uniform effect calculated from camera position.
    half3 volumeLight = 0.0;
    half3 volumeOpacity = 1.0;
    {
        half3 absorption = _Crest_Absorption.xyz;
        half3 scattering = _Crest_Scattering.xyz;

        // We sample shadows at the camera position. Pass a user defined slice offset for smoothing out detail.
        // Offset slice so that we dont get high freq detail. But never use last lod as this has crossfading.
        int sliceIndex = clamp(_Crest_DataSliceOffset, 0, g_Crest_LodCount - 2);

        if (g_Crest_SampleAbsorptionSimulation) absorption = Cascade::MakeAbsorption(sliceIndex).Sample(_WorldSpaceCameraPos.xz).xyz;
        if (g_Crest_SampleScatteringSimulation) scattering = Cascade::MakeScattering(sliceIndex).Sample(_WorldSpaceCameraPos.xz).xyz;

        const float waterLevel = g_Crest_WaterCenter.y + Cascade::MakeAnimatedWaves(sliceIndex).Sample(_WorldSpaceCameraPos.xz).w;

        half shadow = 1.0;
        {
// #if CREST_SHADOWS_ON
            // Camera should be at center of LOD system so no need for blending (alpha, weights, etc). This might not be
            // the case if there is large horizontal displacement, but the _Crest_DataSliceOffset should help by setting a
            // large enough slice as minimum.
            half2 shadowSoftHard = Cascade::MakeShadow(sliceIndex).SampleShadow(_WorldSpaceCameraPos.xz);
            // Soft in red, hard in green. But hard not computed in HDRP.
            shadow = 1.0 - shadowSoftHard.x;
// #endif
        }

        half3 ambientLighting = _Crest_AmbientLighting;
#if CREST_HDRP
        ApplyIndirectLightingMultiplier(ambientLighting);
#endif

        // Out-Scattering Term.
        {
            float3 positionWS = i_positionWS;

#if !CREST_REFLECTION
            // Project point onto sphere at the extinction length.
            float3 toSphere = -view * _Crest_VolumeExtinctionLength * _Crest_OutScatteringExtinctionFactor;
            float3 toScene = i_positionWS - _WorldSpaceCameraPos.xyz;
            positionWS = _WorldSpaceCameraPos.xyz + toSphere;

            // Get closest position.
            positionWS = dot(toScene, toScene) < dot(toSphere, toSphere) ? i_positionWS : positionWS;
#endif

            // Account for average extinction of light as it travels down through volume. Assume flat water as anything
            // else would be expensive.
            half3 extinction = (absorption + scattering) * _Crest_ExtinctionMultiplier;
            float waterDepth = max(0.0, (waterLevel - positionWS.y));
#if CREST_REFLECTION
            waterDepth *= 2.0;
            if (rawDepth == 0.0) waterDepth = _Crest_VolumeExtinctionLength;
#else
            // Full strength seems too extreme. Third strength seems reasonable.
            waterDepth *= _Crest_OutScatteringFactor;
#endif

            float3 outScatteringTerm = exp(-extinction * waterDepth);

            // Transition between the Underwater Environmental Lighting (if present) and this. This will give us the
            // benefit of both approaches.
            outScatteringTerm = lerp(outScatteringTerm, 1.0, _Crest_UnderwaterEnvironmentalLightingWeight);

            // Darken scene and light.
            sceneColour *= outScatteringTerm;
#if !CREST_REFLECTION
            lightColor *= outScatteringTerm;
            ambientLighting *= outScatteringTerm;
#endif
        }

        VolumeLighting
        (
            absorption * _Crest_ExtinctionMultiplier,
            scattering * _Crest_ExtinctionMultiplier,
            _Crest_Anisotropy,
            shadow,
            view,
            ambientLighting,
            lightDirection,
            lightColor,
            half3(0.0, 0.0, 0.0),
            _Crest_AmbientTerm,
            _Crest_DirectTerm,
            fogDistance,
            _Crest_SunBoost,
            _Crest_ShadowsAffectsAmbientFactor,
            volumeLight,
            volumeOpacity
        );
    }

#ifndef k_DisableCaustics
    if (_Crest_CausticsEnabled && hasCaustics)
    {
        float3 position = i_positionWS;
#if CREST_BIRP
        position = float3(i_positionSS, 0);
#endif

        half lightOcclusion = PrimaryLightShadows(position);
        half blur = 0.0;

        const uint slice0 = PositionToSliceIndex(i_positionWS.xz, 0, g_Crest_WaterScale);

#ifdef CREST_FLOW_ON
        half2 flowData = Cascade::MakeFlow(slice0).SampleFlow(i_positionWS.xz);
        const Flow flow = Flow::Make(flowData, g_Crest_Time);
        blur = _Crest_CausticsMotionBlur;
#endif

        const float4 displacement = Cascade::MakeAnimatedWaves(slice0).Sample(i_positionWS.xz);
        const float surfaceHeight = displacement.y + g_Crest_WaterCenter.y + displacement.w;

        sceneColour *= Caustics
        (
#ifdef CREST_FLOW_ON
            flow,
#endif
            i_positionWS,
            surfaceHeight,
            lightColor,
            lightDirection,
            lightOcclusion,
            sceneZ,
            _Crest_CausticsTiledTexture,
            _Crest_CausticsTextureAverage,
            _Crest_CausticsStrength,
            _Crest_CausticsFocalDepth,
            _Crest_CausticsDepthOfField,
            _Crest_CausticsDistortionTiledTexture,
            _Crest_CausticsDistortionStrength,
            blur,
            isUnderwater
        );
    }
#endif

#if CREST_HDRP
    volumeLight *= GetCurrentExposureMultiplier();
#endif

#ifndef k_DisableDithering
#if d_Dithering
    // Increasing intensity can be required for HDRP.
    volumeLight += ScreenSpaceDither(i_positionSS) * _Crest_DitheringIntensity;
#endif
#endif

    return lerp(sceneColour, volumeLight, volumeOpacity);
}

m_CrestNameSpaceEnd

#endif // CREST_UNDERWATER_EFFECT_SHARED_INCLUDED
