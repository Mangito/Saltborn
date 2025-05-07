// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef d_WaveHarmonic_Utility_Macros
#define d_WaveHarmonic_Utility_Macros

#define m_UtilityNameSpace namespace WaveHarmonic { namespace Utility {
#define m_UtilityNameSpaceEnd } }

#define m_Utility WaveHarmonic::Utility

#define m_UtilityVertex \
m_Utility::Varyings Vertex(m_Utility::Attributes i_Input) \
{ \
    return m_Utility::Vertex(i_Input); \
}

#define m_UtilityFragment(type) \
type Fragment(m_Utility::Varyings i_Input) : SV_Target \
{ \
    return m_Utility::Fragment(i_Input); \
}

#define m_UtilityKernel(name) \
void Crest##name(uint3 id : SV_DispatchThreadID) \
{ \
    m_Utility::name(id); \
}

#define m_UtilityKernelVariant(name, variant) \
void Crest##name##variant(uint3 id : SV_DispatchThreadID) \
{ \
    m_Utility::name(id); \
}

#define m_UtilityKernelDefault(name) \
[numthreads(8, 8, 1)] \
void Crest##name(uint3 id : SV_DispatchThreadID) \
{ \
    m_Utility::name(id); \
}

#define m_UtilityKernelDefaultVariant(name, variant) \
[numthreads(8, 8, 1)] \
void Crest##name##variant(uint3 id : SV_DispatchThreadID) \
{ \
    m_Utility::name(id); \
}

#endif // d_WaveHarmonic_Utility_Macros
