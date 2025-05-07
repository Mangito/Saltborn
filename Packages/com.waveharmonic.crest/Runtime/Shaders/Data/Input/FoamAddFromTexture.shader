// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

Shader "Crest/Inputs/Foam/Add From Texture"
{
    Properties
    {
        [MainTexture] _Crest_Texture("Texture", 2D) = "white" {}
        _Crest_Strength( "Strength", float ) = 1

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

            Texture2D _Crest_Texture;
            SamplerState sampler_Crest_Texture;
            float _Crest_Strength;

            CBUFFER_START(CrestPerWaterInput)
            float _Crest_SimDeltaTime;
            float4 _Crest_Texture_ST;
            float3 _Crest_DisplacementAtInputPosition;
            half _Crest_Weight;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;

                float3 worldPos = mul(unity_ObjectToWorld, float4(input.positionOS, 1.0)).xyz;
                // Correct for displacement
                worldPos.xz -= _Crest_DisplacementAtInputPosition.xz;
                o.positionCS = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));

                o.uv = TRANSFORM_TEX(input.uv, _Crest_Texture);
                return o;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return _Crest_Texture.Sample(sampler_Crest_Texture, input.uv) * _Crest_Weight * _Crest_Strength * _Crest_SimDeltaTime;
            }

            ENDCG
        }
    }
    CustomEditor "WaveHarmonic.Crest.Editor.CustomShaderGUI"
}
