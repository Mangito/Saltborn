// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Renders water depth - signed distance from sea level to sea floor
Shader "Crest/Inputs/Depth/Water Depth From Geometry"
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
            BlendOp Max
            ColorMask R

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"
            #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Constants.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float terrainHeight : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = UnityObjectToClipPos(input.positionOS);
                o.terrainHeight = mul(unity_ObjectToWorld, float4(input.positionOS, 1.0)).y;
                return o;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                return float4(input.terrainHeight, 0.0, 0.0, 0.0);
            }
            ENDCG
        }
    }
    CustomEditor "WaveHarmonic.Crest.Editor.CustomShaderGUI"
}
