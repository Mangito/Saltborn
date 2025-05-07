// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

Shader "Crest/Inputs/Scattering/Color"
{
    Properties
    {
        _Crest_Scattering("Scattering", Color) = (0.0, 0.09803921729326248, 0.20000000298023225, 0.0)

        [Toggle(d_Feather)]
        _Crest_Feather("Feather At UV Extents", Float) = 0
        _Crest_FeatherWidth("Feather Width", Range(0.001, 0.5)) = 0.1

        [HideInInspector]
        _Crest_Version("Version", Integer) = 0
    }

    SubShader
    {
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ColorMask RGB

            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #pragma shader_feature_local d_Feather

            #include "UnityCG.cginc"

            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Helpers.hlsl"

            struct Attributes
            {
                float3 vertex : POSITION;
#if d_Feather
                float2 uv : TEXCOORD0;
#endif
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
#if d_Feather
                float2 uv : TEXCOORD0;
#endif
            };

            CBUFFER_START(CrestPerWaterInput)
            half4 _Crest_Scattering;
            half _Crest_FeatherWidth;
            CBUFFER_END

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = mul(unity_ObjectToWorld, float4(input.vertex, 1.0)).xyz;
                output.vertex = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));
#if d_Feather
                output.uv = input.uv;
#endif
                return output;
            }

            float4 Fragment(Varyings input) : SV_Target
            {
                half4 color = _Crest_Scattering;
                color.a = 1.0;
#if d_Feather
                color.a *= WaveHarmonic::Crest::FeatherWeightFromUV(input.uv, _Crest_FeatherWidth);
#endif

                return color;
            }
            ENDCG
        }
    }
    CustomEditor "WaveHarmonic.Crest.Editor.CustomShaderGUI"
}
