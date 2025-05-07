// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

Shader "Crest/Inputs/Foam/Add From Vertex Colors"
{
    Properties
    {
        _Crest_Strength("Strength", float) = 1

        [HideInInspector]
        _Crest_Version("Version", Integer) = 0
    }

    SubShader
    {
        Blend One One

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            CBUFFER_START(CrestPerWaterInput)
            float _Crest_SimDeltaTime;
            float _Crest_Strength;
            float _Crest_Weight;
            float3 _Crest_DisplacementAtInputPosition;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 col : COLOR0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 col : COLOR0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;

                float3 worldPos = mul(unity_ObjectToWorld, float4(input.positionOS, 1.0)).xyz;
                // Correct for displacement
                worldPos.xz -= _Crest_DisplacementAtInputPosition.xz;
                o.positionCS = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));

                o.col = input.col;

                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _Crest_Strength * input.col.x * _Crest_SimDeltaTime * _Crest_Weight;
            }
            ENDCG
        }
    }
    CustomEditor "WaveHarmonic.Crest.Editor.CustomShaderGUI"
}
