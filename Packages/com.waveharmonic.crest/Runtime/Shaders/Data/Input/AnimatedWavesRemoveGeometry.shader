// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Push water under the geometry. Needs to be rendered into all LODs - set Octave Wave length to 0.

Shader "Crest/Inputs/Animated Waves/Push Water Under Convex Hull"
{
    Properties
    {
        [HideInInspector]
        _Crest_Version("Version", Integer) = 0
    }

    SubShader
    {
        Pass
        {
            BlendOp Min
            Cull Front
            ColorMask G

            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "UnityCG.cginc"

            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/InputsDriven.hlsl"
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Cascade.hlsl"

            CBUFFER_START(CrestPerWaterInput)
            float _Crest_Weight;
            float3 _Crest_DisplacementAtInputPosition;
            CBUFFER_END

            m_CrestNameSpace

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            Varyings Vertex(Attributes input)
            {
                Varyings o;

                o.worldPos = mul(unity_ObjectToWorld, float4(input.positionOS, 1.0)).xyz;
                // Correct for displacement
                o.worldPos.xz -= _Crest_DisplacementAtInputPosition.xz;

                o.positionCS = mul(UNITY_MATRIX_VP, float4(o.worldPos, 1.0));

                return o;
            }

            half4 Fragment(Varyings input)
            {
                // Write displacement to get from sea level of water to the y value of this geometry.
                half seaLevelOffset = Cascade::MakeLevel(_Crest_LodIndex).SampleLevel(input.worldPos.xz);
                return half4(0.0, _Crest_Weight * (input.worldPos.y - g_Crest_WaterCenter.y - seaLevelOffset), 0.0, 0.0);
            }

            m_CrestNameSpaceEnd

            m_CrestVertex
            m_CrestFragment(half4)

            ENDCG
        }
    }
    CustomEditor "WaveHarmonic.Crest.Editor.CustomShaderGUI"
}
