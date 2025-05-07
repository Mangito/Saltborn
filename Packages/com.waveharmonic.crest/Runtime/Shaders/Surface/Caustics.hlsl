// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef CREST_WATER_CAUSTICS_H
#define CREST_WATER_CAUSTICS_H

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings.Crest.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Texture.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Flow.hlsl"

#if (CREST_SHIFTING_ORIGIN != 0)
#include "Packages/com.waveharmonic.crest.shifting-origin/Runtime/Shaders/ShiftingOrigin.hlsl"
#endif

m_CrestNameSpace

half3 Caustics
(
    const float3 i_ScenePositionWS,
    const float i_SurfacePositionY,
    const half3 i_LightIntensity,
    const half3 i_LightDirection,
    const half i_LightShadow,
    const float i_SceneDepth,
    const TiledTexture i_CausticsTexture,
    const half i_TextureAverage,
    const half i_Strength,
    const half i_FocalDepth,
    const half i_DepthOfField,
    const TiledTexture i_DistortionTexture,
    const half i_DistortionStrength,
    const half i_Blur,
    const bool i_Underwater
)
{
    half sceneDepth = i_SurfacePositionY - i_ScenePositionWS.y;

    // Compute mip index manually, with bias based on sea floor depth. We compute it manually because if it is computed automatically it produces ugly patches
    // where samples are stretched/dilated. The bias is to give a focusing effect to caustics - they are sharpest at a particular depth. This doesn't work amazingly
    // well and could be replaced.
    float mipLod = log2(i_SceneDepth) + abs(sceneDepth - i_FocalDepth) / i_DepthOfField + i_Blur;

    // Project along light dir, but multiply by a fudge factor reduce the angle bit - compensates for fact that in real life
    // caustics come from many directions and don't exhibit such a strong directonality
    // Removing the fudge factor (4.0) will cause the caustics to move around more with the waves. But this will also
    // result in stretched/dilated caustics in certain areas. This is especially noticeable on angled surfaces.
    float2 lightProjection = i_LightDirection.xz * sceneDepth / (4.0 * i_LightDirection.y);

    float3 cuv1 = 0.0; float3 cuv2 = 0.0;
    {
        float2 surfacePosXZ = i_ScenePositionWS.xz;
        float surfacePosScale = 1.37;

#if (CREST_SHIFTING_ORIGIN != 0)
        // Apply tiled floating origin offset. Always needed.
        surfacePosXZ -= ShiftingOriginOffset(i_CausticsTexture);
        // Scale was causing popping.
        surfacePosScale = 1.0;
#endif

        surfacePosXZ += lightProjection;

        float scale = i_CausticsTexture._scale / 10.0;
        const float speed = g_Crest_Time * i_CausticsTexture._speed;

        cuv1 = float3
        (
            surfacePosXZ / scale + float2(0.044 * speed + 17.16, -0.169 * speed),
            mipLod
        );
        cuv2 = float3
        (
            surfacePosScale * surfacePosXZ / scale + float2(0.248 * speed, 0.117 * speed),
            mipLod
        );
    }

    if (i_Underwater)
    {
        float2 surfacePosXZ = i_ScenePositionWS.xz;

#if (CREST_SHIFTING_ORIGIN != 0)
        // Apply tiled floating origin offset. Always needed.
        surfacePosXZ -= ShiftingOriginOffset(i_DistortionTexture);
#endif

        surfacePosXZ += lightProjection;

        float scale = i_DistortionTexture._scale / 10.0;
        half2 causticN = i_DistortionStrength * UnpackNormal(i_DistortionTexture.Sample(surfacePosXZ / scale)).xy;
        cuv1.xy += 1.30 * causticN;
        cuv2.xy += 1.77 * causticN;
    }

    half causticsStrength = i_Strength;

    // Occlusion.
    {
        causticsStrength *= i_LightShadow;
    }

    return 1.0 + causticsStrength *
    (
        0.5 * i_CausticsTexture.SampleLevel(cuv1.xy, cuv1.z).xyz +
        0.5 * i_CausticsTexture.SampleLevel(cuv2.xy, cuv2.z).xyz
        - i_TextureAverage
    );
}

half3 Caustics
(
    const Flow i_Flow,
    const float3 i_ScenePositionWS,
    const float i_SurfacePositionY,
    const half3 i_LightIntensity,
    const half3 i_LightDirection,
    const half i_LightShadow,
    const float i_SceneDepth,
    const TiledTexture i_CausticsTexture,
    const half i_TextureAverage,
    const half i_Strength,
    const half i_FocalDepth,
    const half i_DepthOfField,
    const TiledTexture i_DistortionTexture,
    const half i_DistortionStrength,
    const half i_Blur,
    const bool i_Underwater
)
{
    half blur = 0.0;
    half3 flow = half3(i_Flow._Flow.x, 0, i_Flow._Flow.y);

    if (i_Blur > 0.0)
    {
        // Calculate blur in flowing water as will likely be more disturbed, resulting in
        // caustics being less defined.
        blur = length(i_Flow._Flow) * i_Blur;
    }

    return Caustics
    (
        i_ScenePositionWS - flow * i_Flow._Offset0,
        i_SurfacePositionY,
        i_LightIntensity,
        i_LightDirection,
        i_LightShadow,
        i_SceneDepth,
        i_CausticsTexture,
        i_TextureAverage,
        i_Strength,
        i_FocalDepth,
        i_DepthOfField,
        i_DistortionTexture,
        i_DistortionStrength,
        blur,
        i_Underwater
    ) * i_Flow._Weight0 + Caustics
    (
        i_ScenePositionWS - flow * i_Flow._Offset1,
        i_SurfacePositionY,
        i_LightIntensity,
        i_LightDirection,
        i_LightShadow,
        i_SceneDepth,
        i_CausticsTexture,
        i_TextureAverage,
        i_Strength,
        i_FocalDepth,
        i_DepthOfField,
        i_DistortionTexture,
        i_DistortionStrength,
        blur,
        i_Underwater
    ) * i_Flow._Weight1;
}

m_CrestNameSpaceEnd

#endif
