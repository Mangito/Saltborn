// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef CREST_CONSTANTS_H
#define CREST_CONSTANTS_H

#define m_CrestBlendNone 0
#define m_CrestBlendAdditive 1
#define m_CrestBlendMinimum 2
#define m_CrestBlendMaximum 3
#define m_CrestBlendAlpha 4

#define m_Crest_PositiveInfinity asfloat(0x7F800000)
#define m_Crest_NegativeInfinity asfloat(0xFF800000)

// Was 0.001, but caused prominent seams for sensitive data like water level.
// 0.00001 worked, but not worth the miniscule saving (in theory). It would require
// testing across various LOD quality settings.
#define m_CrestSampleLodThreshold 0.0

// NOTE: these MUST match the values in PropertyWrapper.cs
#define THREAD_GROUP_SIZE_X 8
#define THREAD_GROUP_SIZE_Y 8

// NOTE: This must match the value in LodDataMgr.cs, as it is used to allow the
// C# code to check if any parameters are within the MAX_LOD_COUNT limits
#define MAX_LOD_COUNT 15

// How light is attenuated deep in water
#define DEPTH_OUTSCATTER_CONSTANT 0.25

// NOTE: Must match k_DepthBaseline in LodDataMgrSeaFloorDepth.cs.
// Bias water floor depth so that default (0) values in texture are not interpreted as shallow and generating foam everywhere
#define CREST_WATER_DEPTH_BASELINE m_Crest_PositiveInfinity
#define k_Crest_MaximumWaveAttenuationDepth 1000.0

// Soft shadows is red, hard shadows is green.
#define CREST_SHADOW_INDEX_SOFT 0
#define CREST_SHADOW_INDEX_HARD 1
#define k_Crest_MaximumShadowJitter 32.0

#define CREST_SSS_MAXIMUM 0.6
#define CREST_SSS_RANGE 0.12

// Note: Must match k_MaskBelowSurfaceCull in UnderwaterRenderer.Mask.cs.
// Fog rendered from below and before transparents and water tile is culled.
#define CREST_MASK_BELOW_SURFACE_CULLED -2.0
// Note: Must match k_MaskBelowSurface in UnderwaterRenderer.Mask.cs.
// Fog rendered from below.
#define CREST_MASK_BELOW_SURFACE        -1.0
// Fog rendered from above.
#define CREST_MASK_ABOVE_SURFACE         1.0
// Normally discard, but keep. Used by negative volumes.
#define CREST_MASK_ABOVE_SURFACE_KEPT    2.0
#define CREST_MASK_BELOW_SURFACE_KEPT   -2.0
// No mask. Used by meniscus when using volumes.
#define CREST_MASK_NONE                  0.0
// No fog. Nicer wording for comparisons.
#define CREST_MASK_NO_FOG                0.0

// The maximum distance the meniscus will be rendered. Only valid when rendering underwater from geometry. The value is
// used to scale the meniscus as it is calculate using a pixel offset which can make the meniscus large at a distance.
#define MENISCUS_MAXIMUM_DISTANCE 15.0


#if defined(STEREO_INSTANCING_ON) || defined(STEREO_MULTIVIEW_ON)
#define CREST_HANDLE_XR 1
#else
#define CREST_HANDLE_XR 0
#endif

#endif // CREST_CONSTANTS_H
