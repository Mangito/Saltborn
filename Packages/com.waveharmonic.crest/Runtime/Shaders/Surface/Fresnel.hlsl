// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef CREST_WATER_FRESNEL_H
#define CREST_WATER_FRESNEL_H

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"

m_CrestNameSpace

float CalculateFresnelReflectionCoefficient(const float i_CosineTheta, const float i_RefractiveIndexOfAir, const float i_RefractiveIndexOfWater)
{
    // Fresnel calculated using Schlick's approximation.
    // See: http://www.cs.virginia.edu/~jdl/bib/appearance/analytic%20models/schlick94b.pdf
    // Reflectance at facing angle.
    float R_0 = (i_RefractiveIndexOfAir - i_RefractiveIndexOfWater) / (i_RefractiveIndexOfAir + i_RefractiveIndexOfWater);
    R_0 *= R_0;
    const float R_theta = R_0 + (1.0 - R_0) * pow(max(0., 1.0 - i_CosineTheta), 5.0);
    return R_theta;
}

void ApplyReflectionUnderwater(
    const half3 i_ViewDirectionWS,
    const half3 i_NormalWS,
    const float i_RefractiveIndexOfAir,
    const float i_RefractiveIndexOfWater,
    out float o_LightTransmitted,
    out float o_LightReflected
) {
    // The the angle of outgoing light from water's surface (whether refracted form outside or internally reflected).
    const float cosOutgoingAngle = max(dot(i_NormalWS, i_ViewDirectionWS), 0.);

    // Calculate the amount of light transmitted from the sky (o_LightTransmitted).
    {
        // Have to calculate the incident angle of incoming light to water.
        // Surface based on how it would be refracted so as to hit the camera.
        const float cosIncomingAngle = cos(asin(clamp((i_RefractiveIndexOfWater * sin(acos(cosOutgoingAngle))) / i_RefractiveIndexOfAir, -1.0, 1.0)));
        const float reflectionCoefficient = CalculateFresnelReflectionCoefficient(cosIncomingAngle, i_RefractiveIndexOfAir, i_RefractiveIndexOfWater);
        o_LightTransmitted = (1.0 - reflectionCoefficient);
        o_LightTransmitted = max(o_LightTransmitted, 0.0);
    }

    // Calculate the amount of light reflected from below the water.
    {
        // Angle of incident is angle of reflection.
        const float cosIncomingAngle = cosOutgoingAngle;
        const float reflectionCoefficient = CalculateFresnelReflectionCoefficient(cosIncomingAngle, i_RefractiveIndexOfAir, i_RefractiveIndexOfWater);
        o_LightReflected = reflectionCoefficient;
    }
}

void ApplyFresnel
(
    const half3 i_ViewDirectionWS,
    const half3 i_NormalWS,
    const bool i_IsUnderwater,
    const float i_RefractiveIndexOfAir,
    const float i_RefractiveIndexOfWater,
    const float i_TirIntensity,
    out float o_LightTransmitted,
    out float o_LightReflected
)
{
    o_LightTransmitted = 1.0;

    if (i_IsUnderwater)
    {
        ApplyReflectionUnderwater(i_ViewDirectionWS, i_NormalWS, i_RefractiveIndexOfAir, i_RefractiveIndexOfWater, o_LightTransmitted, o_LightReflected);
        // Limit how strong TIR is. Not sure if this is the best way but it seems to work gracefully.
        o_LightTransmitted = max(o_LightTransmitted, 1.0 - i_TirIntensity);
        o_LightReflected = min(o_LightReflected, i_TirIntensity);
    }
    else
    {
        const float cosAngle = max(dot(i_NormalWS, i_ViewDirectionWS), 0.0);
        // Hardcode water IOR for above surface.
        o_LightReflected = CalculateFresnelReflectionCoefficient(cosAngle, i_RefractiveIndexOfAir, 1.33);
    }
}

m_CrestNameSpaceEnd

#endif
