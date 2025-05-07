// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#pragma target 4.5

// Low appears good enough as it has filtering which is necessary when close to a shadow.
#define SHADOW_LOW
#define AREA_SHADOW_LOW

// In shared SG code we target the forward pass to avoid shader compilation errors.
#define CREST_HDRP 1
#define SHADERPASS SHADERPASS_FORWARD
#define CREST_HDRP_FORWARD_PASS 1
#define CREST_SHADERGRAPH_CONSTANTS_H

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
#include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"
