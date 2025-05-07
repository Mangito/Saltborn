// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// GLOBALs - we're allowed to use these anywhere.

#ifndef CREST_WATER_GLOBALS_H
#define CREST_WATER_GLOBALS_H

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings.Crest.hlsl"

SamplerState LODData_linear_clamp_sampler;
SamplerState LODData_point_clamp_sampler;
SamplerState sampler_Crest_linear_repeat;

CBUFFER_START(CrestPerFrame)
float3 g_Crest_WaterCenter;
float  g_Crest_WaterScale;
float  g_Crest_Time;
float  g_Crest_LodCount;
int    g_Crest_LodChange;
float  g_Crest_MeshScaleLerp;
float  g_Crest_ClipByDefault;
float  g_Crest_LodAlphaBlackPointFade;
float  g_Crest_LodAlphaBlackPointWhitePointFade;

// Hack - due to SV_IsFrontFace occasionally coming through as true for
// backfaces, add a param here that forces water to be in undrwater state. I
// think the root cause here might be imprecision or numerical issues at water
// tile boundaries, although I'm not sure why cracks are not visible in this case.
int    g_Crest_ForceUnderwater;

float3 g_Crest_PrimaryLightDirection;
float3 g_Crest_PrimaryLightIntensity;

float g_Crest_DynamicSoftShadowsFactor;

bool g_Crest_SampleAbsorptionSimulation;
bool g_Crest_SampleScatteringSimulation;

// Motion Vector Parameters
float  g_Crest_WaterScaleChange;
float3 g_Crest_WaterCenterDelta;

// Shifting Origin
#if (CREST_SHIFTING_ORIGIN != 0)
float3 g_Crest_ShiftingOriginOffset;
#endif

// Portals
#if (CREST_PORTALS != 0)
int    _Crest_Portal;
#endif
CBUFFER_END

#endif
