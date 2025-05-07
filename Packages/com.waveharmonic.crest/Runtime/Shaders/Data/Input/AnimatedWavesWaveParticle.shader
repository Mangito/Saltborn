// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

Shader "Crest/Inputs/Animated Waves/Wave Particle"
{
    Properties
    {
        _Crest_Amplitude( "Amplitude", float ) = 1
        _Crest_Radius( "Radius", float) = 3

        [HideInInspector]
        _Crest_Version("Version", Integer) = 0
    }

    SubShader
    {
        Tags { "DisableBatching" = "True" }

        Pass
        {
            Blend One One

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            CBUFFER_START(CrestPerWaterInput)
            float _Crest_Radius;
            float _Crest_Amplitude;
            float _Crest_Weight;
            float3 _Crest_DisplacementAtInputPosition;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 worldOffsetScaledXZ : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;

                float3 worldPos = mul(unity_ObjectToWorld, float4(input.positionOS, 1.0)).xyz;
                float3 centerPos = unity_ObjectToWorld._m03_m13_m23;
                o.worldOffsetScaledXZ = worldPos.xz - centerPos.xz;

                // shape is symmetric around center with known radius - fix the vert positions to perfectly wrap the shape.
                o.worldOffsetScaledXZ = sign(o.worldOffsetScaledXZ);
                float4 newWorldPos = float4(centerPos, 1.0);
                newWorldPos.xz += o.worldOffsetScaledXZ * _Crest_Radius;

                // Correct for displacement
                newWorldPos.xz -= _Crest_DisplacementAtInputPosition.xz;

                o.positionCS = mul(UNITY_MATRIX_VP, newWorldPos);

                return o;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                // power 4 smoothstep - no normalize needed
                // credit goes to stubbe's shadertoy: https://www.shadertoy.com/view/4ldSD2
                float r2 = dot( input.worldOffsetScaledXZ, input.worldOffsetScaledXZ);
                if( r2 > 1.0 )
                    return (float4)0.0;

                r2 = 1.0 - r2;

                float y = r2 * r2 * _Crest_Amplitude;

                return float4(0.0, y * _Crest_Weight, 0.0, 0.0);
            }

            ENDCG
        }
    }
    CustomEditor "WaveHarmonic.Crest.Editor.CustomShaderGUI"
}
