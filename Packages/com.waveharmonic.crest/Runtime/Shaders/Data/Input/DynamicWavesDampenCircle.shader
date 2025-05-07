// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

Shader "Crest/Inputs/Dynamic Waves/Dampen Circle"
{
    Properties
    {
        _Crest_Radius("Radius", float) = 3
        _Crest_Strength("Strength", Range(0, 100)) = 10

        [HideInInspector]
        _Crest_Version("Version", Integer) = 0
    }

    SubShader
    {
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "UnityCG.cginc"

            CBUFFER_START(CrestPerWaterInput)
            float _Crest_SimDeltaTime;
            float _Crest_Radius;
            float _Crest_Strength;
            float _Crest_Weight;
            float3 _Crest_DisplacementAtInputPosition;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 worldOffsetScaled : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = UnityObjectToClipPos(input.positionOS);

                float3 worldPos = mul(unity_ObjectToWorld, float4(input.positionOS, 1.0)).xyz;
                float3 centerPos = unity_ObjectToWorld._m03_m13_m23;
                o.worldOffsetScaled.xy = worldPos.xz - centerPos.xz;

                // shape is symmetric around center with known radius - fix the vert positions to perfectly wrap the shape.
                o.worldOffsetScaled.xy = sign(o.worldOffsetScaled.xy);
                float4 newWorldPos = float4(centerPos, 1.0);
                newWorldPos.xz += o.worldOffsetScaled.xy * _Crest_Radius;

                // Correct for displacement
                newWorldPos.xz -= _Crest_DisplacementAtInputPosition.xz;

                o.positionCS = mul(UNITY_MATRIX_VP, newWorldPos);

                return o;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                // power 4 smoothstep - no normalize needed
                // credit goes to stubbe's shadertoy: https://www.shadertoy.com/view/4ldSD2
                float r2 = dot(input.worldOffsetScaled.xy, input.worldOffsetScaled.xy);
                if (r2 > 1.0) return (float4)0.0;
                r2 = 1.0 - r2;
                float val = r2 * r2;

                float weight = val * _Crest_Strength * _Crest_SimDeltaTime * _Crest_Weight;
                return float4(0.0, 0.0, 0.0, weight);
            }
            ENDCG
        }
    }
    CustomEditor "WaveHarmonic.Crest.Editor.CustomShaderGUI"
}
