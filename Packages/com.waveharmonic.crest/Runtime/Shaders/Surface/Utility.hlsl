// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef CREST_UTILITY_H
#define CREST_UTILITY_H

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"

m_CrestNameSpace

half InverseLerp(half a, half b, half t)
{
    return (t - a) / (b - a);
}

// Taken from:
// https://github.com/Unity-Technologies/Graphics/blob/f56d2b265eb9e01b0376623e909f98c88bc60662/Packages/com.unity.render-pipelines.high-definition/Runtime/Surface/Shaders/WaterUtilities.hlsl#L781-L797
float EdgeBlendingFactor(float2 screenPosition, float distanceToWaterSurface)
{
    // Convert the screen position to NDC
    float2 screenPosNDC = screenPosition * 2 - 1;

    // We want the value to be 0 at the center and go to 1 at the edges
    float distanceToEdge = 1.0 - min((1.0 - abs(screenPosNDC.x)), (1.0 - abs(screenPosNDC.y)));

    // What we want here is:
    // - +inf -> 0.5 value is 0
    // - 0.5-> 0.25 value is going from  0 to 1
    // - 0.25 -> 0 value is 1
    float distAttenuation = 1.0 - saturate((distanceToWaterSurface - 0.75) / 0.25);

    // Based on if the water surface is close, we want to make the blending region even bigger
    return lerp(saturate((distanceToEdge - 0.8) / (0.2)), saturate(distanceToEdge + 0.25), distAttenuation);
}

m_CrestNameSpaceEnd

#endif
