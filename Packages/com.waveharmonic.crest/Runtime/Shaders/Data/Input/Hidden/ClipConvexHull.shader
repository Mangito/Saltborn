// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Renders convex hull to the clip surface texture.

Shader "Hidden/Crest/Inputs/Clip/Convex Hull"
{
    CGINCLUDE
    #pragma vertex Vertex
    #pragma fragment Fragment

    // For SV_IsFrontFace.
    #pragma target 3.0

    #include "UnityCG.cginc"
    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"
    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/InputsDriven.hlsl"
    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Cascade.hlsl"
    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Surface/Geometry.hlsl"

    CBUFFER_START(CrestPerWaterInput)
    bool _Crest_Inverted;
    CBUFFER_END

    m_CrestNameSpace

    struct Attributes
    {
        float3 positionOS : POSITION;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float3 positionWS : TEXCOORD0;
    };

    Varyings Vertex(Attributes input)
    {
        Varyings o;
        o.positionCS = UnityObjectToClipPos(input.positionOS);
        o.positionWS = mul(unity_ObjectToWorld, float4(input.positionOS, 1.0));
        return o;
    }

    float4 Fragment(Varyings input, const bool isFrontFace)
    {
        float3 surfacePositionWS = Cascade::MakeAnimatedWaves(_Crest_LodIndex)
            .SampleDisplacementFromUndisplaced(input.positionWS.xz);

        // Move to sea level.
        surfacePositionWS.y += g_Crest_WaterCenter.y;

        // Clip if above water.
        if (input.positionWS.y > surfacePositionWS.y)
        {
            clip(-1.0);
        }

        // To add clipping, back face must write one and front face must write zero.
        return float4(isFrontFace == _Crest_Inverted ? 1.0 : 0.0, 0.0, 0.0, 1.0);
    }

    m_CrestNameSpaceEnd

    m_CrestVertex
    m_CrestFragmentWithFrontFace(float4)

    ENDCG

    SubShader
    {
        ZWrite Off
        ColorMask R

        Pass
        {
            Cull Front
            // Here so CGINCLUDE works.
            CGPROGRAM
            ENDCG
        }

        Pass
        {
            Cull Back
            // Here so CGINCLUDE works.
            CGPROGRAM
            ENDCG
        }
    }
}
