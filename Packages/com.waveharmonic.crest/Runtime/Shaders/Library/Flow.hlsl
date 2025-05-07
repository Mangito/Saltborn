// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef CREST_FLOW_INCLUDED
#define CREST_FLOW_INCLUDED

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"

m_CrestNameSpace

struct Flow
{
    float _Offset0;
    float _Weight0;
    float _Offset1;
    float _Weight1;
    float _Period;
    half2 _Flow;

    static Flow Make
    (
        const half2 i_Flow,
        const float i_Time,
        const float i_Period = 1.0
    )
    {
        const float Period = i_Period;
        const float HalfPeriod = Period * 0.5;
        const float Offset0 = fmod(i_Time, Period);
        float Weight0 = Offset0 / HalfPeriod;
        if (Weight0 > 1.0) Weight0 = 2.0 - Weight0;
        const float Offset1 = fmod(i_Time + HalfPeriod, Period);
        const float Weight1 = 1.0 - Weight0;

        Flow flow;
        flow._Offset0 = Offset0;
        flow._Weight0 = Weight0;
        flow._Offset1 = Offset1;
        flow._Weight1 = Weight1;
        flow._Period = Period;
        flow._Flow = i_Flow;
        return flow;
    }
};

m_CrestNameSpaceEnd

#endif // CREST_FLOW_INCLUDED
