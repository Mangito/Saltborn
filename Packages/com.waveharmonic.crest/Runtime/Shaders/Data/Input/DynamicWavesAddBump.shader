// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

Shader "Crest/Inputs/Dynamic Waves/Add Bump"
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
        Pass
        {
            Blend One One

            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "UnityCG.cginc"

            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Cascade.hlsl"
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Helpers.hlsl"

            CBUFFER_START(CrestPerWaterInput)
            float _Crest_Radius;
            float _Crest_SimDeltaTime;
            float _Crest_Amplitude;
            float3 _Crest_DisplacementAtInputPosition;
            CBUFFER_END

            m_CrestNameSpace

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 worldOffsetScaled : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings o;
                o.positionCS = UnityObjectToClipPos(input.positionOS);

                float3 worldPos = mul(unity_ObjectToWorld, float4(input.positionOS, 1.0));
                float3 centerPos = unity_ObjectToWorld._m03_m13_m23;
                o.worldOffsetScaled.xy = worldPos.xz - centerPos.xz;

                // shape is symmetric around center with known radius - fix the vert positions to perfectly wrap the shape.
                o.worldOffsetScaled.xy = sign(o.worldOffsetScaled.xy);
                float4 newWorldPos = float4(centerPos, 1.0);
                newWorldPos.xz += o.worldOffsetScaled.xy * _Crest_Radius;

                // Correct for displacement
                newWorldPos.xz -= _Crest_DisplacementAtInputPosition.xz;

                o.positionCS = mul(UNITY_MATRIX_VP, newWorldPos);
                o.uv = Cascade::MakeDynamicWaves(_Crest_LodIndex).WorldToUV(newWorldPos.xz);

                return o;
            }

            float4 Fragment(Varyings input)
            {
                // power 4 smoothstep - no normalize needed
                // credit goes to stubbe's shadertoy: https://www.shadertoy.com/view/4ldSD2
                float r2 = dot(input.worldOffsetScaled.xy, input.worldOffsetScaled.xy);
                if (r2 > 1.0)
                    return (float4)0.0;

                r2 = 1.0 - r2;

                float y = r2 * r2;
                y = pow(y, 0.05);
                y *= _Crest_Amplitude;

                y /= g_Crest_LodCount;

                // Feather edges to reduce streaking without introducing reflections.
                y *= FeatherWeightFromUV(input.uv, 0.1);

                // accelerate velocities
                return float4(0.0, _Crest_SimDeltaTime * y, 0.0, 0.0);
            }

            m_CrestNameSpaceEnd

            m_CrestVertex
            m_CrestFragment(float4)

            ENDCG
        }
    }
}
