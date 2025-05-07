// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

Shader "Crest/Inputs/Flow/Add From Texture"
{
    Properties
    {
        [MainTexture] _Crest_Texture("Flow Map", 2D) = "white" {}
        _Crest_Strength( "Strength", float ) = 1

        [Toggle(d_FlipX)]
        _Crest_FlipX("Flip X", Float) = 0
        [Toggle(d_FlipZ)]
        _Crest_FlipZ("Flip Z", Float) = 0
        [Toggle(d_NegativeValues)]
        _Crest_NegativeValues("Has Negative Values", Float) = 0

        [Toggle(d_Feather)]
        _Crest_Feather("Feather At UV Extents", Float) = 0
        _Crest_FeatherWidth("Feather Width", Range(0.001, 0.5)) = 0.1

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

            #pragma shader_feature_local d_FlipX
            #pragma shader_feature_local d_FlipZ
            #pragma shader_feature_local d_Feather
            #pragma shader_feature_local d_NegativeValues

            #include "UnityCG.cginc"

            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/InputsDriven.hlsl"
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Helpers.hlsl"

            Texture2D _Crest_Texture;
            SamplerState sampler_Crest_Texture;

            CBUFFER_START(CrestPerWaterInput)
            float4 _Crest_Texture_ST;
            float _Crest_Strength;
            float _Crest_Weight;
            float3 _Crest_DisplacementAtInputPosition;
            half _Crest_FeatherWidth;
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
                float2 flow = _Crest_Texture.Sample(sampler_Crest_Texture, input.uv).xy;

#if !d_NegativeValues
                // From 0..1 to -1..1.
                flow = flow * 2.0 - 1.0;
#endif

#if d_Feather
                flow *= WaveHarmonic::Crest::FeatherWeightFromUV(input.uv, _Crest_FeatherWidth);
#endif

#if d_FlipX
                flow.x *= -1.0;
#endif
#if d_FlipZ
                flow.y *= -1.0;
#endif

                return float4(flow * _Crest_Strength * _Crest_Weight, 0.0, 0.0);
            }

            ENDCG
        }
    }
    CustomEditor "WaveHarmonic.Crest.Editor.CustomShaderGUI"
}
