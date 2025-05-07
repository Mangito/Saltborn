// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Provides utility passes for rendering like clearing the stencil buffer.

Shader "Hidden/Crest/Utility/Blit"
{
    HLSLINCLUDE
    #pragma vertex Vertex
    #pragma fragment Fragment

    #include "UnityCG.cginc"

    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Legacy/Core.hlsl"
    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Legacy/InputsDriven.hlsl"

    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Utility/Depth.hlsl"

    struct Attributes
    {
        uint id : SV_VertexID;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float2 uv : TEXCOORD0;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    Varyings Vertex(Attributes input)
    {
        // This will work for all pipelines.
        Varyings output = (Varyings)0;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        output.positionCS = GetFullScreenTriangleVertexPosition(input.id);
        output.uv = GetFullScreenTriangleTexCoord(input.id);
        return output;
    }
    ENDHLSL

    SubShader
    {
        Cull Off ZWrite On ZTest Always

        Pass
        {
            // Copies the color texture.
            Name "Copy Color"

            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            TEXTURE2D_X(_CameraColorTexture);

            float4 Fragment(Varyings input) : SV_Target
            {
                // We need this when sampling a screenspace texture.
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return LOAD_TEXTURE2D_X(_CameraColorTexture, input.positionCS.xy);
            }
            ENDHLSL
        }

        Pass
        {
            // Copies the depth from the camera depth texture. Clears the stencil for convenience.
            Name "Copy Depth / Clear Stencil"

            ZWrite On
            ZTest Always
            Cull Off

            Stencil
            {
                Ref 0
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            TEXTURE2D_X(_CameraDepthTexture);
            float Fragment(Varyings input) : SV_Depth
            {
                // We need this when sampling a screenspace texture.
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return LOAD_DEPTH_TEXTURE_X(_CameraDepthTexture, input.positionCS.xy);
            }
            ENDHLSL
        }

        Pass
        {
            // Clears the depth buffer without clearing the stencil.
            Name "Clear Depth"

            ZWrite On
            ZTest Always
            Cull Off

            HLSLPROGRAM
            float Fragment(Varyings input) : SV_Depth
            {
                return 0.0;
            }
            ENDHLSL
        }

        Pass
        {
            // Clears the stencil buffer without clearing depth
            Name "Clear Stencil"

            ZWrite Off
            ZTest Always
            Cull Off
            Blend Zero One

            Stencil
            {
                Ref 0
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            float Fragment(Varyings input) : SV_Target
            {
                return 0.0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Copy"
            Blend Off

            HLSLPROGRAM
            TEXTURE2D(_Utility_MainTexture);
            SAMPLER(sampler_Utility_MainTexture);
            float4 Fragment(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_Utility_MainTexture, sampler_Utility_MainTexture, input.uv);
            }
            ENDHLSL
        }
    }
}
