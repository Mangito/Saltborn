// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// 0-1 scaling of existing water data using multiplicative blending.

Shader "Crest/Inputs/All/Scale"
{
    Properties
    {
        // Scale the water data. Zero is no data and one leaves data untouched.
        _Crest_Scale("Scale", Range(0, 1)) = 0.35

        // Use the texture instead of the scale value.
        [Toggle(d_Texture)]
        _Crest_ApplyTexture("Apply Texture", Float) = 0
        [MainTexture] _Crest_Texture("Texture", 2D) = "black" {}

        // Inverts the scale value.
        [Toggle(d_Invert)]
        _Crest_Invert("Invert", Float) = 0

        [Header(Feather)]
        // Feather the edges of the mesh using the texture coordinates. Easiest to understand with a plane.
        [Toggle(d_Feather)]
        _Crest_Feather("Feather At UV Extents", Float) = 0
        // How far from edge to feather.
        _Crest_FeatherWidth("Feather Width", Range(0.001, 0.5)) = 0.1

        [HideInInspector]
        _Crest_Version("Version", Integer) = 0
    }

    SubShader
    {
        Pass
        {
            // Multiply
            Blend Zero SrcColor

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local d_Texture
            #pragma shader_feature_local d_Invert
            #pragma shader_feature_local d_Feather

            #include "UnityCG.cginc"

            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/InputsDriven.hlsl"
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Helpers.hlsl"

#if defined(d_Texture) || defined(d_Feather)
#define _NEED_UVS
#endif

#if d_Texture
            Texture2D _Crest_Texture;
            SamplerState sampler_Crest_Texture;
#endif

            CBUFFER_START(CrestPerWaterInput)
            float _Crest_Weight;
            float3 _Crest_DisplacementAtInputPosition;
            float _Crest_Scale;
#if d_Feather
            half _Crest_FeatherWidth;
#endif
#if d_Texture
            float4 _Crest_Texture_ST;
#endif
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
#ifdef _NEED_UVS
                float2 uv : TEXCOORD0;
#endif
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
#ifdef _NEED_UVS
                float2 uv : TEXCOORD0;
#endif
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;

                float3 positionWS = mul(unity_ObjectToWorld, float4(input.positionOS, 1.0)).xyz;
                // Correct for displacement.
                positionWS.xz -= _Crest_DisplacementAtInputPosition.xz;
                o.positionCS = mul(UNITY_MATRIX_VP, float4(positionWS, 1.0));

#ifdef _NEED_UVS
                o.uv = input.uv;
#endif

                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
#if d_Texture
                float scale = _Crest_Texture.Sample(sampler_Crest_Texture, input.uv).r;
#else
                float scale = _Crest_Scale;
#endif

#if d_Invert
                scale = 1.0 - scale;
#endif

#if d_Feather
                scale = lerp(1.0, scale, WaveHarmonic::Crest::FeatherWeightFromUV(input.uv, _Crest_FeatherWidth));
#endif

                return scale * _Crest_Weight;
            }
            ENDCG
        }
    }
    CustomEditor "WaveHarmonic.Crest.Editor.CustomShaderGUI"
}
