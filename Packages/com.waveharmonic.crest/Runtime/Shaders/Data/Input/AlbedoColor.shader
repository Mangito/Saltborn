// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

Shader "Crest/Inputs/Albedo/Color"
{
    Properties
    {
        [MainTexture] _Crest_Texture("Albedo", 2D) = "white" {}
        _Crest_Color("Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _Crest_Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        [Enum(UnityEngine.Rendering.BlendMode)]
        _Crest_BlendModeSource("Source Blend Mode", Int) = 5
        [Enum(UnityEngine.Rendering.BlendMode)]
        _Crest_BlendModeTarget("Target Blend Mode", Int) = 10

        [HideInInspector]
        _Crest_Version("Version", Integer) = 0
    }
    SubShader
    {
        Pass
        {
            Blend [_Crest_BlendModeSource] [_Crest_BlendModeTarget]

            ZWrite Off

            CGPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "UnityCG.cginc"

            struct Attributes
            {
                float3 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            CBUFFER_START(CrestPerWaterInput)
            float3 _Crest_DisplacementAtInputPosition;
            CBUFFER_END

            Texture2D _Crest_Texture;
            SamplerState sampler_Crest_Texture;
            float4 _Crest_Texture_ST;

            half4 _Crest_Color;
            half _Crest_Cutoff;

            Varyings Vertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = mul(unity_ObjectToWorld, float4(input.vertex, 1.0)).xyz;
                positionWS.xz -= _Crest_DisplacementAtInputPosition.xz;
                output.vertex = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            fixed4 Fragment(Varyings i) : SV_Target
            {
                fixed4 color = _Crest_Texture.Sample(sampler_Crest_Texture, i.uv) * _Crest_Color;
                clip(color.a - _Crest_Cutoff + 0.0001);
                return color * i.color;
            }
            ENDCG
        }
    }
    CustomEditor "WaveHarmonic.Crest.Editor.CustomShaderGUI"
}
