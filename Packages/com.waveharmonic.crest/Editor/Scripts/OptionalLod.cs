// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Collections.Generic;

namespace WaveHarmonic.Crest.Editor
{
    class OptionalLod
    {
        internal string _MaterialProperty;

        internal string PropertyName { get; private set; }
        internal string PropertyLabel { get; private set; }
        internal System.Type Dependency { get; private set; }

        internal Lod GetLod(WaterRenderer water) => PropertyName switch
        {
            nameof(WaterRenderer._AbsorptionLod) => water.AbsorptionLod,
            nameof(WaterRenderer._AlbedoLod) => water.AlbedoLod,
            nameof(WaterRenderer._AnimatedWavesLod) => water.AnimatedWavesLod,
            nameof(WaterRenderer._ClipLod) => water.ClipLod,
            nameof(WaterRenderer._DepthLod) => water.DepthLod,
            nameof(WaterRenderer._DynamicWavesLod) => water.DynamicWavesLod,
            nameof(WaterRenderer._FlowLod) => water.FlowLod,
            nameof(WaterRenderer._FoamLod) => water.FoamLod,
            nameof(WaterRenderer._LevelLod) => water.LevelLod,
            nameof(WaterRenderer._ScatteringLod) => water.ScatteringLod,
            nameof(WaterRenderer._ShadowLod) => water.ShadowLod,
            _ => throw new System.NotImplementedException(),
        };


        // Optional. Not all simulations will have a corresponding keyword.
        internal bool HasMaterialToggle => !string.IsNullOrEmpty(MaterialProperty);

        // Needed as clip surface material toggle is Alpha Clipping.
        internal virtual string MaterialProperty => _MaterialProperty;
        internal virtual string MaterialPropertyPath => $"{PropertyLabel} > Enabled";
        internal virtual string MaterialKeyword => $"{MaterialProperty}_ON";

        internal static OptionalLod Get(System.Type type)
        {
            return s_Lods.GetValueOrDefault(s_Mapping.GetValueOrDefault(type, type), null);
        }

        static readonly Dictionary<System.Type, OptionalLod> s_Lods = new()
        {
            {
                typeof(AbsorptionLod), new ColorOptionLod()
                {
                    PropertyLabel = "Absorption",
                    PropertyName  = nameof(WaterRenderer._AbsorptionLod),
                }
            },
            {
                typeof(AlbedoLod), new()
                {
                    PropertyLabel = "Albedo",
                    PropertyName  = nameof(WaterRenderer._AlbedoLod),
                    _MaterialProperty = "_Crest_AlbedoEnabled",
                }
            },
            {
                typeof(AnimatedWavesLod), new()
                {
                    PropertyLabel = "Animate Waves",
                    PropertyName  = nameof(WaterRenderer._AnimatedWavesLod),
                }
            },
            {
                typeof(ClipLod), new ClipOptionalLod()
                {
                    PropertyLabel = "Clip Surface",
                    PropertyName  = nameof(WaterRenderer._ClipLod),
                }
            },
            {
                typeof(DepthLod), new()
                {
                    PropertyLabel = "Water Depth",
                    PropertyName  = nameof(WaterRenderer._DepthLod),
                }
            },
            {
                typeof(DynamicWavesLod), new()
                {
                    PropertyLabel = "Dynamic Waves",
                    PropertyName  = nameof(WaterRenderer._DynamicWavesLod),
                    Dependency = typeof(AnimatedWavesLod),
                }
            },
            {
                typeof(FlowLod), new()
                {
                    PropertyLabel = "Flow",
                    PropertyName  = nameof(WaterRenderer._FlowLod),
                    _MaterialProperty = "CREST_FLOW",
                }
            },
            {
                typeof(FoamLod), new()
                {
                    PropertyLabel = "Foam",
                    PropertyName  = nameof(WaterRenderer._FoamLod),
                    _MaterialProperty = "_Crest_FoamEnabled",
                }
            },
            {
                typeof(LevelLod), new()
                {
                    PropertyLabel = "Water Level",
                    PropertyName  = nameof(WaterRenderer._LevelLod),
                    _MaterialProperty = "_Crest_LevelEnabled",
                    Dependency = typeof(AnimatedWavesLod),

                }
            },
            {
                typeof(ScatteringLod), new ColorOptionLod()
                {
                    PropertyLabel = "Scattering",
                    PropertyName  = nameof(WaterRenderer._ScatteringLod),
                }
            },
            {
                typeof(ShadowLod), new()
                {
                    PropertyLabel = "Shadow",
                    PropertyName  = nameof(WaterRenderer._ShadowLod),
                    _MaterialProperty = "_Crest_ShadowsEnabled",
                }
            },
        };

        static readonly Dictionary<System.Type, System.Type> s_Mapping = new()
        {
            { typeof(AbsorptionLodInput), typeof(AbsorptionLod) },
            { typeof(AlbedoLodInput), typeof(AlbedoLod) },
            { typeof(AnimatedWavesLodInput), typeof(AnimatedWavesLod) },
            { typeof(ClipLodInput), typeof(ClipLod) },
            { typeof(DepthLodInput), typeof(DepthLod) },
            { typeof(DynamicWavesLodInput), typeof(DynamicWavesLod) },
            { typeof(FlowLodInput), typeof(FlowLod) },
            { typeof(FoamLodInput), typeof(FoamLod) },
            { typeof(LevelLodInput), typeof(LevelLod) },
            { typeof(ScatteringLodInput), typeof(ScatteringLod) },
            { typeof(ShadowLodInput), typeof(ShadowLod) },
        };
    }

    sealed class ClipOptionalLod : OptionalLod
    {
        // BIRP SG has prefixes for Unity properties but other RPs do not. These prefixes
        // are for serialisation only and are not used in the shader.
        internal override string MaterialPropertyPath => "Alpha Clipping";
        internal override string MaterialProperty => (RenderPipelineHelper.IsLegacy ? "_BUILTIN" : "") + "_AlphaClip";
        internal override string MaterialKeyword => (RenderPipelineHelper.IsLegacy ? "_BUILTIN" : "") + "_ALPHATEST_ON";
    }

    sealed class ColorOptionLod : OptionalLod
    {
        internal override string MaterialPropertyPath => $"Volume Lighting > Sample {PropertyLabel} Simulation";
    }
}
