// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Defines missing inputs.

float4x4 _Crest_InverseViewProjection;
float4x4 _Crest_InverseViewProjectionRight;

#undef UNITY_MATRIX_I_VP

#if defined(STEREO_INSTANCING_ON) || defined(STEREO_MULTIVIEW_ON)
#define UNITY_MATRIX_I_VP (unity_StereoEyeIndex == 0 ? _Crest_InverseViewProjection : _Crest_InverseViewProjectionRight)
#else
#define UNITY_MATRIX_I_VP _Crest_InverseViewProjection
#endif

// Not set and _ScreenParams.zw is "1.0 + 1.0 / _ScreenParams.xy"
#define _ScreenSize float4(_ScreenParams.xy, float2(1.0, 1.0) / _ScreenParams.xy)
