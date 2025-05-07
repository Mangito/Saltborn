// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Generates waves from geometry that is rendered into the water simulation from a top down camera. Expects
// following data on verts:
//   - POSITION: Vert positions as normal.
//   - TEXCOORD0: Axis - direction for waves to travel. "Forward vector" for waves.
//   - TEXCOORD1: X - 0 at start of waves, 1 at end of waves
//
//  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ uv1.x = 0             |
//  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~  |                    |  uv0 - wave direction vector
//  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~  |                   \|/
//  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ uv1.x = 1
//  ------------------- shoreline --------------------
//

Shader "Crest/Inputs/Shape Waves/Add From Geometry"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.BlendMode)]
        _Crest_BlendModeSource("Source Blend Mode", Int) = 1
        [Enum(UnityEngine.Rendering.BlendMode)]
        _Crest_BlendModeTarget("Target Blend Mode", Int) = 1

        // Controls ramp distance over which waves grow/fade as they move forwards
        _Crest_FeatherWaveStart( "Feather wave start (0-1)", Range( 0.0, 10 ) ) = 0.1

        [Toggle(d_Feather)]
        _Crest_Feather("Feather At UV Extents", Float) = 0
        _Crest_FeatherWidth("Feather Width", Range(0.001, 1)) = 0.1

        [HideInInspector]
        _Crest_Version("Version", Integer) = 0
    }

    CGINCLUDE
    #pragma vertex Vertex
    #pragma fragment Fragment
    // #pragma enable_d3d11_debug_symbols

    #pragma shader_feature_local d_Feather

    #include "UnityCG.cginc"

    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"
    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/InputsDriven.hlsl"
    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Cascade.hlsl"
    #include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Helpers.hlsl"

    Texture2DArray _Crest_WaveBuffer;

    CBUFFER_START(CrestPerWaterInput)
    float _Crest_RespectShallowWaterAttenuation;
    int _Crest_WaveBufferSliceIndex;
    float _Crest_AverageWavelength;
    float _Crest_AttenuationInShallows;
    float _Crest_Weight;
    float2 _Crest_AxisX;
    half _Crest_MaximumAttenuationDepth;
    half _Crest_FeatherWidth;
    half _Crest_FeatherWaveStart;
    CBUFFER_END

    m_CrestNameSpace

    struct Attributes
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct Varyings
    {
        float4 vertex : SV_POSITION;
        float2 uv : TEXCOORD0;
        float3 uv_slice : TEXCOORD1;
        float2 axis : TEXCOORD2;
        float3 worldPosScaled : TEXCOORD3;
        float2 worldPosXZ : TEXCOORD5;
    };

    Varyings Vertex(Attributes v)
    {
        Varyings o;

        const float3 positionOS = v.vertex.xyz;
        o.vertex = UnityObjectToClipPos(positionOS);
        const float3 worldPos = mul( unity_ObjectToWorld, float4(positionOS, 1.0) ).xyz;

        // UV coordinate into the cascade we are rendering into
        o.uv_slice = Cascade::MakeAnimatedWaves(_Crest_LodIndex).WorldToUV(worldPos.xz);

        o.worldPosXZ = worldPos.xz;

        o.uv = v.uv;

        // World pos prescaled by wave buffer size, suitable for using as UVs in fragment shader
        const float waveBufferSize = 0.5f * (1 << _Crest_WaveBufferSliceIndex);
        o.worldPosScaled = worldPos / waveBufferSize;

        // Rotate forward axis around y-axis into world space
        o.axis = unity_ObjectToWorld._m00_m20.xy;
        o.axis = _Crest_AxisX.x * o.axis + _Crest_AxisX.y * float2(-o.axis.y, o.axis.x);

        return o;
    }

    float4 Fragment(Varyings input)
    {
        float wt = _Crest_Weight;

        // Feature at away from shore.
        wt *= saturate(input.uv.x / _Crest_FeatherWaveStart);

#if d_Feather
        wt *= FeatherWeightFromUV(input.uv, _Crest_FeatherWidth);
#endif

        float alpha = wt;

        // Attenuate if depth is less than half of the average wavelength
        const half depth = Cascade::MakeDepth(_Crest_LodIndex).SampleSignedDepthFromSeaLevel(input.worldPosXZ) +
            Cascade::MakeLevel(_Crest_LodIndex).SampleLevel(input.worldPosXZ);
        half depth_wt = saturate(2.0 * depth / _Crest_AverageWavelength);
        if (_Crest_MaximumAttenuationDepth < k_Crest_MaximumWaveAttenuationDepth)
        {
            depth_wt = lerp(depth_wt, 1.0, saturate(depth / _Crest_MaximumAttenuationDepth));
        }
        const float attenuationAmount = _Crest_AttenuationInShallows * _Crest_RespectShallowWaterAttenuation;
        wt *= attenuationAmount * depth_wt + (1.0 - attenuationAmount);

        // Quantize wave direction and interpolate waves
        float axisHeading = atan2( input.axis.y, input.axis.x ) + 2.0 * 3.141592654;
        const float dTheta = 0.5*0.314159265;
        float angle0 = axisHeading;
        const float rem = fmod( angle0, dTheta );
        angle0 -= rem;
        const float angle1 = angle0 + dTheta;

        float2 axisX0; sincos( angle0, axisX0.y, axisX0.x );
        float2 axisX1; sincos( angle1, axisX1.y, axisX1.x );
        float2 axisZ0; axisZ0.x = -axisX0.y; axisZ0.y = axisX0.x;
        float2 axisZ1; axisZ1.x = -axisX1.y; axisZ1.y = axisX1.x;

        const float2 uv0 = float2(dot( input.worldPosScaled.xz, axisX0 ), dot( input.worldPosScaled.xz, axisZ0 ));
        const float2 uv1 = float2(dot( input.worldPosScaled.xz, axisX1 ), dot( input.worldPosScaled.xz, axisZ1 ));

        // Sample displacement, rotate into frame
        float3 disp0 = _Crest_WaveBuffer.SampleLevel( sampler_Crest_linear_repeat, float3(uv0, _Crest_WaveBufferSliceIndex), 0 ).xyz;
        float3 disp1 = _Crest_WaveBuffer.SampleLevel( sampler_Crest_linear_repeat, float3(uv1, _Crest_WaveBufferSliceIndex), 0 ).xyz;
        disp0.xz = disp0.x * axisX0 + disp0.z * axisZ0;
        disp1.xz = disp1.x * axisX1 + disp1.z * axisZ1;
        float3 disp = lerp( disp0, disp1, rem / dTheta );

        disp *= wt;

        return float4(disp, alpha);
    }

    m_CrestNameSpaceEnd

    m_CrestVertex
    m_CrestFragment(float4)
    ENDCG

    SubShader
    {
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            // Either additive or alpha blend for geometry waves.
            Blend [_Crest_BlendModeSource] [_Crest_BlendModeTarget]
            CGPROGRAM
            ENDCG
        }

        Pass
        {
            // Subsequent draws need to be additive. We cannot change render state with command
            // buffer and changing on material is not aligned with command buffer usage.
            Blend One One
            CGPROGRAM
            ENDCG
        }
    }
    CustomEditor "WaveHarmonic.Crest.Editor.CustomShaderGUI"
}
