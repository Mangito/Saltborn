// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#ifndef CREST_CASCADE_INCLUDED
#define CREST_CASCADE_INCLUDED

// Fix Unity macro leaks.
#undef _Weight

#ifdef SHADER_API_PSSL
#define m_ConstantReturn const
#else
#define m_ConstantReturn
#endif

#define m_SanitizeAbsorption(x) x
#define m_SanitizeAlbedo(x) x
#define m_SanitizeAnimatedWaves(x) x
#define m_SanitizeClip(x) x
// Infinity is unsafe, as it causes NaNs if multiplied by zero.
#define m_SanitizeDepth(x) max(x, -m_FloatMaximum)
#define m_SanitizeDynamicWaves(x) x
#define m_SanitizeFlow(x) x
#define m_SanitizeFoam(x) x
#define m_SanitizeLevel(x) x
#define m_SanitizeScattering(x) x
#define m_SanitizeShadow(x) x

#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Macros.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Globals.hlsl"
#include "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/InputsDriven.hlsl"

#define m__MakeCascade(name, source) \
    static Cascade Make##name##source(uint i_Index) \
    { \
        float4 perType = g_Crest_SamplingParameters##name; \
        float4 perSlice = g_Crest_SamplingParametersCascade##name##source[i_Index]; \
        Cascade result; \
        result._Texture = g_Crest_Cascade##name##source; \
        result._SamplingParameters = g_Crest_SamplingParametersCascade##name##source; \
        result._Index = i_Index; \
        result._PositionSnapped = perSlice.xy; \
        result._Texel = perSlice.z; \
        result._Resolution = perType.y; \
        result._OneOverResolution = perType.z; \
        result._Count = perType.x; \
        return result; \
    } \

#define m_MakeCascadeCopy(name) \
    static Cascade Make##name(uint i_Index, Cascade i_Cascade) \
    { \
        float4 perType = g_Crest_SamplingParameters##name; \
        float4 perSlice = i_Cascade._SamplingParameters[i_Index]; \
        Cascade result; \
        result._Texture = i_Cascade._Texture; \
        result._SamplingParameters = i_Cascade._SamplingParameters; \
        result._Index = i_Index; \
        result._PositionSnapped = perSlice.xy; \
        result._Texel = perSlice.z; \
        result._Resolution = perType.y; \
        result._OneOverResolution = perType.z; \
        result._Count = perType.x; \
        return result; \
    }

#define m__MakeCascadeShared(source) \
    static Cascade Make##source(uint i_Index) \
    { \
        const float4 perAll = g_Crest_CascadeData##source[i_Index]; \
        Cascade result; \
        result._Index = i_Index; \
        result._Scale = perAll.x; \
        result._Weight = perAll.y; \
        result._MaximumWavelength = perAll.z; \
        return result; \
    }

#define m_MakeCascade(name) m__MakeCascade(name,)
#define m_MakeCascadePrevious(name) m__MakeCascade(name, Source)
#define m_MakeCascadeShared m__MakeCascadeShared()
#define m_MakeCascadeSharedPrevious m__MakeCascadeShared(Source)

#define m_Sample(name, type, swizzle) \
    type Sample##name(const float2 i_Position) m_ConstantReturn \
    { \
        type result = Sample(i_Position)swizzle; \
        result = m_Sanitize##name(result); \
        return result; \
    } \
    type Sample##name(const float3 i_UV) m_ConstantReturn \
    { \
        return Sample(i_UV)swizzle; \
    } \
    type Sample##name(const uint2 i_ID) m_ConstantReturn \
    { \
        return Sample(i_ID)swizzle; \
    } \
    type Sample##name##Overflow(const float2 i_Position, const float i_Border) m_ConstantReturn \
    { \
        type result = 0.0; \
        const float3 uv = WorldToUV(i_Position); \
        const half2 r = abs(uv.xy - 0.5); \
        const half rMax = 0.5 - _OneOverResolution * i_Border; \
        if (max(r.x, r.y) <= rMax) \
        { \
            result = Sample##name(uv); \
        } \
        else if (_Index < _Count) \
        { \
            const Cascade cascade = Cascade::Make##name(_Index + 1, this); \
            const float3 uv = cascade.WorldToUV(i_Position); \
            const half2 r = abs(uv.xy - 0.5); \
            const half rMax = 0.5 - cascade._OneOverResolution * i_Border; \
            if (max(r.x, r.y) <= rMax) \
            { \
                result = Sample##name(uv); \
            } \
        } \
        return result; \
    } \
    type Sample##name##Overflow(const float3 i_UV, const float i_Border) m_ConstantReturn \
    { \
        type result = 0.0; \
        const half2 r = abs(i_UV.xy - 0.5); \
        const half rMax = 0.5 - _OneOverResolution * i_Border; \
        if (max(r.x, r.y) <= rMax) \
        { \
            result = Sample##name(i_UV); \
        } \
        else if (_Index < _Count) \
        { \
            const Cascade cascade = Cascade::Make##name(_Index + 1, this); \
            const float3 uv = cascade.WorldToUV(UVToWorld(i_UV)); \
            const half2 r = abs(uv.xy - 0.5); \
            const half rMax = 0.5 - cascade._OneOverResolution * i_Border; \
            if (max(r.x, r.y) <= rMax) \
            { \
                result = Sample##name(uv); \
            } \
        } \
        return result; \
    }

#define m_SampleWeighted(name, type) \
    void Sample##name(const float2 i_Position, const float i_Weight, inout type io_##name) m_ConstantReturn \
    { \
        io_##name += Sample##name(i_Position) * i_Weight; \
    } \
    void Sample##name(const float3 i_UV, const float i_Weight, inout type io_##name) m_ConstantReturn \
    { \
        io_##name += Sample##name(i_UV) * i_Weight; \
    } \
    void Sample##name##Overflow(const float2 i_Position, const float i_Border, const float i_Weight, inout type io_##name) m_ConstantReturn \
    { \
        io_##name += Sample##name##Overflow(i_Position, i_Border) * i_Weight; \
    }

m_CrestNameSpace

struct Cascade
{
    Texture2DArray _Texture;
    float _Index;
    float2 _PositionSnapped;
    float _Resolution;
    float _Count;
    float _OneOverResolution;
    float _Texel;
    float _MaximumWavelength;

    float _Scale;
    float _Weight;

    // For copy constructor.
    float4 _SamplingParameters[MAX_LOD_COUNT];

    m_MakeCascadeShared
    m_MakeCascadeSharedPrevious

    static Cascade Make(const uint i_Index, bool i_Previous)
    {
        const float4 perAll = i_Previous ? g_Crest_CascadeDataSource[i_Index] : g_Crest_CascadeData[i_Index];
        Cascade result;
        result._Index = i_Index;
        result._Scale = perAll.x;
        result._Weight = perAll.y;
        result._MaximumWavelength = perAll.z;
        return result;
    }

    m_MakeCascade(Absorption)
    m_MakeCascadeCopy(Absorption)
    m_MakeCascade(Albedo)
    m_MakeCascadeCopy(Albedo)
    m_MakeCascade(AnimatedWaves)
    m_MakeCascadeCopy(AnimatedWaves)
    m_MakeCascadePrevious(AnimatedWaves)
    m_MakeCascade(Clip)
    m_MakeCascadeCopy(Clip)
    m_MakeCascade(Depth)
    m_MakeCascadeCopy(Depth)
    m_MakeCascade(DynamicWaves)
    m_MakeCascadeCopy(DynamicWaves)
    m_MakeCascadePrevious(DynamicWaves)
    m_MakeCascade(Flow)
    m_MakeCascadeCopy(Flow)
    m_MakeCascade(Foam)
    m_MakeCascadeCopy(Foam)
    m_MakeCascadePrevious(Foam)
    m_MakeCascade(Level)
    m_MakeCascadeCopy(Level)
    m_MakeCascade(Scattering)
    m_MakeCascadeCopy(Scattering)
    m_MakeCascade(Shadow)
    m_MakeCascadeCopy(Shadow)
    m_MakeCascadePrevious(Shadow)

    // Convert compute shader id to uv texture coordinates
    float3 IDToUV(const uint2 i_ID) m_ConstantReturn
    {
        return float3((i_ID + 0.5) / _Resolution, _Index);
    }

    float2 UVToWorld(const float3 i_UV) m_ConstantReturn
    {
        return _Texel * _Resolution * (i_UV.xy - 0.5) + _PositionSnapped;
    }

    float3 WorldToUV(const float2 i_Position) m_ConstantReturn
    {
        return float3((i_Position - _PositionSnapped) / (_Texel * _Resolution) + 0.5, _Index);
    }

    float2 IDToWorld(const uint2 i_ID) m_ConstantReturn
    {
        return UVToWorld(IDToUV(i_ID));
    }

    bool IsOutOfBounds(float2 uv, float offset) m_ConstantReturn
    {
        const half2 r = abs(uv - 0.5);
        const half rMax = 0.5 - _OneOverResolution * offset;
        return max(r.x, r.y) > rMax;
    }

    half4 Sample(const float3 i_UV) m_ConstantReturn
    {
        return _Texture.SampleLevel(LODData_linear_clamp_sampler, i_UV, 0.0);
    }

    half4 Sample(const Texture2DArray i_Texture, const float3 i_UV) m_ConstantReturn
    {
        return i_Texture.SampleLevel(LODData_linear_clamp_sampler, i_UV, 0.0);
    }

    half4 Sample(const float2 i_Position) m_ConstantReturn
    {
        return Sample(WorldToUV(i_Position));
    }

    half4 Sample(const Texture2DArray i_Texture, const float2 i_Position) m_ConstantReturn
    {
        return Sample(i_Texture, WorldToUV(i_Position));
    }

    half4 Sample(const uint2 i_ID) m_ConstantReturn
    {
        return Sample(IDToUV(i_ID));
    }

    half4 Sample(const Texture2DArray i_Texture, const uint2 i_ID) m_ConstantReturn
    {
        return Sample(i_Texture, IDToUV(i_ID));
    }

    float3 Internal_WrapToNextSlice(float3 i_uv, float i_overflowed) m_ConstantReturn
    {
        // Next slice is twice the size so half the coordinates to match position.
        float overflow = 0.5 * i_overflowed;
        i_uv = float3((i_uv.xy - overflow) * (1.0 - overflow) + overflow, i_uv.z + i_overflowed);
        return i_uv;
    }

    // Wraps to next slice if coordinates outside of range.
    float3 WrapToNextSlice(float3 i_uv) m_ConstantReturn
    {
        return Internal_WrapToNextSlice(i_uv, any(i_uv.xy > 1.0) || any(i_uv.xy < 0.0));
    }

    // Wraps to next slice if coordinates outside of range.
    float3 WrapToNextSlice(float3 i_uv, float i_depth) m_ConstantReturn
    {
        return Internal_WrapToNextSlice(i_uv, any(i_uv.xy > 1.0) || any(i_uv.xy < 0.0) && i_uv.z + 1.0 < i_depth);
    }

    m_Sample(Absorption, half3, .xyz)
    m_SampleWeighted(Absorption, half3)
    m_Sample(Albedo, half4, )
    m_SampleWeighted(Albedo, half4)
    m_Sample(AnimatedWaves, half4, )
    m_SampleWeighted(AnimatedWaves, float4) // Use float because parameter is position
    m_Sample(Clip, half, .x)
    m_SampleWeighted(Clip, half)
    m_Sample(Depth, half2, .xy)
    m_SampleWeighted(Depth, half2)
    m_Sample(DynamicWaves, half2, .xy)
    m_Sample(Flow, half2, .xy)
    m_SampleWeighted(Flow, half2)
    m_Sample(Foam, half, .x)
    m_SampleWeighted(Foam, half)
    m_Sample(Level, half, .x)
    m_SampleWeighted(Level, half)
    m_Sample(Scattering, half3, .xyz)
    m_SampleWeighted(Scattering, half3)
    m_Sample(Shadow, half2, .xy)
    m_SampleWeighted(Shadow, half2)

    float3 SampleDisplacement(const float2 i_Position) m_ConstantReturn
    {
        float4 position = SampleAnimatedWaves(i_Position);
        position.y += position.w;
        return position.xyz;
    }

    void SampleDisplacement(const float2 i_Position, const float i_Weight, inout float3 io_Position) m_ConstantReturn
    {
        io_Position += SampleDisplacement(i_Position).xyz * i_Weight;
    }

    half3 SampleWaveDisplacement(const float2 i_Position) m_ConstantReturn
    {
        return SampleAnimatedWaves(i_Position).xyz;
    }

    half3 SampleWaveDisplacement(const float3 i_UV) m_ConstantReturn
    {
        return SampleAnimatedWaves(i_UV).xyz;
    }

    half4 __SampleDisplacements(const float2 i_Position, out float3 o_DisplacementX, out float3 o_DisplacementZ) m_ConstantReturn
    {
        const float3 uv = WorldToUV(i_Position);
        const half4 displacement = SampleAnimatedWaves(uv);
        const float3 dd = float3(_OneOverResolution, 0.0, _Texel);
        o_DisplacementX = dd.zyy + SampleWaveDisplacement(uv + dd.xyy);
        o_DisplacementZ = dd.yyz + SampleWaveDisplacement(uv + dd.yxy);
        return displacement;
    }

    void SampleDisplacement(const float2 i_Position, const float i_Weight, inout float3 io_Position, inout half2 io_Derivatives, inout half io_LevelOffset) m_ConstantReturn
    {
        float3 uv = WorldToUV(i_Position);
        float4 position = SampleAnimatedWaves(uv);
        io_LevelOffset += position.w * i_Weight;
        io_Position += position.xyz * i_Weight;

        // Derivatives
        {
            // Compute derivative of water level - needed to get base normal of water. Water
            // normal, normal map etc is then added to base normal.
            const float2 dd = float2(_OneOverResolution, 0.0);
            const float xOffset = SampleAnimatedWaves(uv + dd.xyy).w;
            const float zOffset = SampleAnimatedWaves(uv + dd.yxy).w;

            // TODO: Is weight in correct position?
            io_Derivatives.x += i_Weight * (xOffset - position.w) / _Texel;
            io_Derivatives.y += i_Weight * (zOffset - position.w) / _Texel;
        }
    }

    void SampleDisplacement(const float2 i_Position, const float i_Weight, inout float3 io_Position, inout half2 io_Derivatives) m_ConstantReturn
    {
        half offset = 0.0;
        SampleDisplacement(i_Position, i_Weight, io_Position, io_Derivatives, offset);
        io_Position.y += offset;
    }

    half3 SampleDisplacement(const float2 i_Position, out half o_Determinent) m_ConstantReturn
    {
        float3 xDisplacement; float3 zDisplacement;
        half4 displacement = __SampleDisplacements(i_Position, xDisplacement, zDisplacement);
        o_Determinent = __ComputeDisplacementDeterminant(displacement.xyz, xDisplacement, zDisplacement);
        displacement.y += displacement.w;
        return displacement.xyz;
    }

    half __ComputeDisplacementDeterminant(half3 i_Displacement, float3 i_DisplacementX, float3 i_DisplacementZ) m_ConstantReturn
    {
        const float2x2 jacobian = (float4(i_DisplacementX.xz, i_DisplacementZ.xz) - i_Displacement.xzxz) / _Texel;
        // Determinant is < 1 for pinched, < 0 for overlap/inversion and > 1 for stretched.
        return determinant(jacobian);
    }

    half2 __ComputeDisplacementNormals(half3 i_Displacement, float3 i_DisplacementX, float3 i_DisplacementZ) m_ConstantReturn
    {
        float3 xProduct = cross(i_DisplacementZ - i_Displacement, i_DisplacementX - i_Displacement);

        // Situation could arise where cross returns 0, prob when arguments are two aligned vectors. This
        // resulted in NaNs and flashing screen in HDRP. Force normal to point upwards as the only time
        // it should point downwards is for underwater (handled elsewhere) or in surface inversions which
        // should not happen for well tweaked waves, and look broken anyway.
        xProduct.y = max(xProduct.y, 0.0001);

        return normalize(xProduct).xz;
    }

    // TODO: Rename
    void SampleNormals(const float2 i_Position, const float i_Weight, inout half2 io_Normal, inout half io_Determinant) m_ConstantReturn
    {
        float3 xDisplacement; float3 zDisplacement;
        half3 displacement = __SampleDisplacements(i_Position, xDisplacement, zDisplacement).xyz;
        io_Normal += __ComputeDisplacementNormals(displacement, xDisplacement, zDisplacement) * i_Weight;
        io_Determinant += __ComputeDisplacementDeterminant(displacement, xDisplacement, zDisplacement) * i_Weight;
    }

    half SampleSceneHeight(const float2 i_Position) m_ConstantReturn
    {
        return SampleDepth(i_Position).x;
    }

    void SampleSceneHeight(const float2 i_Position, const float i_Weight, inout half io_Height) m_ConstantReturn
    {
        io_Height += SampleSceneHeight(i_Position) * i_Weight;
    }

    half SampleShorelineDistance(const float2 i_Position) m_ConstantReturn
    {
        return Sample(i_Position).y;
    }

    half SampleShorelineDistance(const float3 i_UV) m_ConstantReturn
    {
        return Sample(i_UV).y;
    }

    half SampleShorelineDistance(const uint2 i_ID) m_ConstantReturn
    {
        return Sample(i_ID).y;
    }

    void SampleShorelineDistance(const float2 i_Position, const float i_Weight, inout half io_Distance) m_ConstantReturn
    {
        io_Distance += SampleShorelineDistance(i_Position) * i_Weight;
    }

    half SampleSignedDepthFromSeaLevel(const float2 i_Position) m_ConstantReturn
    {
        return g_Crest_WaterCenter.y - SampleSceneHeight(i_Position);
    }

    half2 SampleSignedDepthFromSeaLevelAndDistance(const float2 i_Position) m_ConstantReturn
    {
        half2 value = SampleDepth(i_Position);
        value.x = g_Crest_WaterCenter.y - value.x;
        return value;
    }

    void SampleSignedDepthFromSeaLevel(const float2 i_Position, const float i_Weight, inout half io_Depth) m_ConstantReturn
    {
        io_Depth += (g_Crest_WaterCenter.y - SampleSceneHeight(i_Position)) * i_Weight;
    }

    // Perform iteration to invert the displacement vector field - find position that displaces to query position.
    float2 SampleInvertedDisplacement(const float2 i_Position) m_ConstantReturn
    {
        float2 inverted = i_Position;
        for (uint i = 0; i < 4; i++)
        {
            const float2 displacement = SampleAnimatedWaves(inverted).xz;
            const float2 error = (inverted + displacement) - i_Position;
            inverted -= error;
        }

        return inverted;
    }

    half3 SampleDisplacementFromUndisplaced(const float2 i_Position) m_ConstantReturn
    {
        return SampleDisplacement(SampleInvertedDisplacement(i_Position));
    }

    half3 SampleDynamicWavesDisplacement(const float2 i_Position, const float i_HorizontalDisplace, const float i_DisplaceClamp) m_ConstantReturn
    {
        const float3 uv = WorldToUV(i_Position);
        return SampleDynamicWavesDisplacement(uv, i_HorizontalDisplace, i_DisplaceClamp);
    }

    half3 SampleDynamicWavesDisplacement(const float3 i_UV, const float i_HorizontalDisplace, const float i_DisplaceClamp) m_ConstantReturn
    {
        const float3 uv = i_UV;

        half3 displacement = 0.0;
        displacement.y = Sample(uv).x;

        const float2 invRes = float2(_OneOverResolution, 0.0);
        const half waveSimY_px = Sample(uv + invRes.xyy).x;
        const half waveSimY_nx = Sample(uv - invRes.xyy).x;
        const half waveSimY_pz = Sample(uv + invRes.yxy).x;
        const half waveSimY_nz = Sample(uv - invRes.yxy).x;
        // Compute displacement from gradient of water surface - discussed in issue #18 and then in issue #47.

        // For gerstner waves, horizontal displacement is proportional to derivative of
        // vertical displacement multiplied by the wavelength.
        const float wavelength_mid = 2.0 * _Texel * 1.5;
        const float wavevector = 2.0 * 3.14159 / wavelength_mid;
        const float2 dydx = (float2(waveSimY_px, waveSimY_pz) - float2(waveSimY_nx, waveSimY_nz)) / (2.0 * _Texel);
        displacement.xz = i_HorizontalDisplace * dydx / wavevector;

        const float maxDisp = _Texel * i_DisplaceClamp;
        displacement.xz = clamp(displacement.xz, -maxDisp, maxDisp);

        return displacement;
    }
};

float2 DataIDToInputUV
(
    const float2 i_ID,
    const Cascade i_Cascade,
    const float2 i_Position,
    const float2 i_Rotation,
    const float2 i_Size
)
{
    const float2 position = i_Cascade.IDToWorld(i_ID);
    float2 uv = (position - i_Position) / i_Size;

    // Clockwise transform rotation.
    uv = uv.x * float2(i_Rotation.y, -i_Rotation.x) + uv.y * i_Rotation;
    uv += 0.5;

    return uv;
}

m_CrestNameSpaceEnd

#undef m__MakeCascade
#undef m_MakeCascade
#undef m_MakeCascadePrevious
#undef m_Sample
#undef m_SampleWeighted
#undef m_ComputeDepth

#endif // CREST_CASCADE_INCLUDED
