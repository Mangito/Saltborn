// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef CREST_MACROS_H
#define CREST_MACROS_H

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings.Crest.hlsl"

#define m_CrestNameSpace namespace WaveHarmonic { namespace Crest {
#define m_CrestNameSpaceEnd } }

#define m_Crest WaveHarmonic::Crest

#define m_FloatMaximum 3.402823466e+38

#if (CREST_FULL_PRECISION_DISPLACEMENT != 0)
#define m_DisplacementTexture(texture, components) texture<float##components>
#else
#define m_DisplacementTexture(texture, components) texture
#endif

#define m_CrestVertex \
m_Crest::Varyings Vertex(m_Crest::Attributes i_Input) \
{ \
    return m_Crest::Vertex(i_Input); \
}

#define m_CrestFragment(type) \
type Fragment(m_Crest::Varyings i_Input) : SV_Target \
{ \
    return m_Crest::Fragment(i_Input); \
}

#define m_CrestFragmentWithFrontFace(type) \
type Fragment(m_Crest::Varyings i_Input, const bool i_IsFrontFace : SV_IsFrontFace) : SV_Target \
{ \
    return m_Crest::Fragment(i_Input, i_IsFrontFace); \
}

#define m_CrestKernel(name) \
void Crest##name(uint3 id : SV_DispatchThreadID) \
{ \
    m_Crest::name(id); \
}

#define m_CrestKernelVariant(name, variant) \
void Crest##name##variant(uint3 id : SV_DispatchThreadID) \
{ \
    m_Crest::name(id); \
}

#define m_CrestKernelDefault(name) \
[numthreads(THREAD_GROUP_SIZE_X, THREAD_GROUP_SIZE_Y, 1)] \
void Crest##name(uint3 id : SV_DispatchThreadID) \
{ \
    m_Crest::name(id); \
}

#define m_CrestInputKernel(name) \
void Crest##name(uint3 id : SV_DispatchThreadID) \
{ \
    m_Crest::name(uint3(id.xy, g_Crest_LodCount - 1 - id.z)); \
}

#define m_CrestInputKernelDefault(name) \
[numthreads(THREAD_GROUP_SIZE_X, THREAD_GROUP_SIZE_Y, 1)] \
void Crest##name(uint3 id : SV_DispatchThreadID) \
{ \
    m_Crest::name(uint3(id.xy, g_Crest_LodCount - 1 - id.z)); \
}

#endif // CREST_MACROS_H
