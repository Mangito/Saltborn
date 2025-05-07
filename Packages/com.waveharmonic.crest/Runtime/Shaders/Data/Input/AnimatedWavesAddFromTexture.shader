// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

Shader "Crest/Inputs/Animated Waves/Add From Texture"
{
    Properties
    {
        [MainTexture] _Crest_Texture("Texture", 2D) = "black" {}
        _Crest_Strength( "Strength", float ) = 1

        [Toggle(d_HeightsOnly)]
        _Crest_HeightsOnly("Heights Only", Float) = 1

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

            #pragma shader_feature_local d_HeightsOnly

            #include "UnityCG.cginc"

            Texture2D _Crest_Texture;
            SamplerState sampler_Crest_Texture;

            CBUFFER_START(CrestPerWaterInput)
            float4 _Crest_Texture_ST;
            float _Crest_Strength;
            float _Crest_Weight;
            float3 _Crest_DisplacementAtInputPosition;
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

            half4 Frag(Varyings input) : SV_Target
            {
                half3 texSample = _Crest_Texture.Sample(sampler_Crest_Texture, input.uv).xyz;

                half3 displacement = (half3)0.0;
#if d_HeightsOnly
                displacement.y = texSample.x * _Crest_Strength;
#else
                displacement.xyz = texSample * _Crest_Strength;
#endif

                return _Crest_Weight * half4(displacement, 0.0);
            }

            ENDCG
        }
    }
    CustomEditor "WaveHarmonic.Crest.Editor.CustomShaderGUI"
}
