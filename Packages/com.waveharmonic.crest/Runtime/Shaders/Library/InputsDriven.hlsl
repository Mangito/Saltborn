// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Anything marked as "Source" is from the previous frame.

#ifndef CREST_INPUTS_DRIVEN_H
#define CREST_INPUTS_DRIVEN_H

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Constants.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"

CBUFFER_START(CrestChunkInstanceData)
uint   _Crest_LodIndex;
float  _Crest_ChunkMeshScaleAlpha;
float  _Crest_ChunkMeshScaleAlphaSource;
float  _Crest_ChunkGeometryGridWidth;
float  _Crest_ChunkGeometryGridWidthSource;
float  _Crest_ChunkFarNormalsWeight;
float2 _Crest_ChunkNormalScrollSpeed;
CBUFFER_END

Texture2DArray g_Crest_CascadeAbsorption;
m_DisplacementTexture(Texture2DArray, 4) g_Crest_CascadeAnimatedWaves;
m_DisplacementTexture(Texture2DArray, 4) g_Crest_CascadeAnimatedWavesSource;
Texture2DArray g_Crest_CascadeDepth;
m_DisplacementTexture(Texture2DArray, 4) g_Crest_CascadeLevel;
Texture2DArray g_Crest_CascadeClip;
Texture2DArray g_Crest_CascadeFoam;
Texture2DArray g_Crest_CascadeFoamSource;
Texture2DArray g_Crest_CascadeFlow;
Texture2DArray g_Crest_CascadeDynamicWaves;
Texture2DArray g_Crest_CascadeDynamicWavesSource;
Texture2DArray g_Crest_CascadeScattering;
Texture2DArray g_Crest_CascadeShadow;
Texture2DArray g_Crest_CascadeShadowSource;
Texture2DArray g_Crest_CascadeAlbedo;


CBUFFER_START(CrestLodData)
// Cascade Data:                  Scale, Weight, MaximumWavelength, 0
float4 g_Crest_CascadeData[MAX_LOD_COUNT];
float4 g_Crest_CascadeDataSource[MAX_LOD_COUNT];

// Sampling Parameters:           LodCount, Resolution, OneOverResolution, 0
// Sampling Parameters (Cascade): SnappedPositionX, SnappedPositionZ, TexelWidth, 0
float4 g_Crest_SamplingParametersAbsorption;
float4 g_Crest_SamplingParametersCascadeAbsorption[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersAlbedo;
float4 g_Crest_SamplingParametersCascadeAlbedo[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersAnimatedWaves;
float4 g_Crest_SamplingParametersCascadeAnimatedWaves[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersCascadeAnimatedWavesSource[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersClip;
float4 g_Crest_SamplingParametersCascadeClip[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersDepth;
float4 g_Crest_SamplingParametersCascadeDepth[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersDynamicWaves;
float4 g_Crest_SamplingParametersCascadeDynamicWaves[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersCascadeDynamicWavesSource[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersFlow;
float4 g_Crest_SamplingParametersCascadeFlow[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersFoam;
float4 g_Crest_SamplingParametersCascadeFoam[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersCascadeFoamSource[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersLevel;
float4 g_Crest_SamplingParametersCascadeLevel[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersScattering;
float4 g_Crest_SamplingParametersCascadeScattering[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersShadow;
float4 g_Crest_SamplingParametersCascadeShadow[MAX_LOD_COUNT];
float4 g_Crest_SamplingParametersCascadeShadowSource[MAX_LOD_COUNT];
CBUFFER_END

#endif // CREST_INPUTS_DRIVEN_H
