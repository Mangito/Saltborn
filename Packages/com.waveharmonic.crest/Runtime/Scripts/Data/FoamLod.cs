// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using WaveHarmonic.Crest.Utility;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// A persistent foam simulation that moves around with a displacement LOD. The input is fully combined water surface shape.
    /// </summary>
    [FilterEnum(nameof(_TextureFormatMode), Filtered.Mode.Exclude, (int)LodTextureFormatMode.Automatic)]
    public sealed partial class FoamLod : PersistentLod
    {
        [Tooltip("Prewarms the simulation on load and teleports.\n\nResults are only an approximation.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _Prewarm = true;

        [Tooltip("Settings for fine tuning this simulation.")]
        [@Embedded]
        [@GenerateAPI(Getter.Custom)]
        [SerializeField]
        FoamLodSettings _Settings;

        static new class ShaderIDs
        {
            public static readonly int s_MinimumWavesSlice = Shader.PropertyToID("_Crest_MinimumWavesSlice");
            public static readonly int s_FoamMaximum = Shader.PropertyToID("_Crest_FoamMaximum");
            public static readonly int s_FoamFadeRate = Shader.PropertyToID("_Crest_FoamFadeRate");
            public static readonly int s_WaveFoamStrength = Shader.PropertyToID("_Crest_WaveFoamStrength");
            public static readonly int s_WaveFoamCoverage = Shader.PropertyToID("_Crest_WaveFoamCoverage");
            public static readonly int s_ShorelineFoamMaxDepth = Shader.PropertyToID("_Crest_ShorelineFoamMaxDepth");
            public static readonly int s_ShorelineFoamStrength = Shader.PropertyToID("_Crest_ShorelineFoamStrength");
            public static readonly int s_NeedsPrewarming = Shader.PropertyToID("_Crest_NeedsPrewarming");
            public static readonly int s_FoamNegativeDepthPriming = Shader.PropertyToID("_Crest_FoamNegativeDepthPriming");
        }

        internal static readonly Color s_GizmoColor = new(1f, 1f, 1f, 0.5f);

        internal override string ID => "Foam";
        internal override Color GizmoColor => s_GizmoColor;
        private protected override Color ClearColor => Color.black;
        private protected override ComputeShader SimulationShader => WaterResources.Instance.Compute._UpdateFoam;

        private protected override GraphicsFormat RequestedTextureFormat => _TextureFormatMode switch
        {
            LodTextureFormatMode.Performance => GraphicsFormat.R16_SFloat,
            LodTextureFormatMode.Precision => GraphicsFormat.R32_SFloat,
            LodTextureFormatMode.Manual => _TextureFormat,
            _ => throw new System.NotImplementedException(),
        };

        private protected override void SetAdditionalSimulationParameters<T>(T properties)
        {
            base.SetAdditionalSimulationParameters(properties);

            // Prewarm simulation for first frame or teleporting. It will not be the same results as running the
            // simulation for multiple frames - but good enough.
            properties.SetBoolean(ShaderIDs.s_NeedsPrewarming, _Prewarm && _NeedsPrewarmingThisStep);
            properties.SetFloat(ShaderIDs.s_FoamFadeRate, Settings._FoamFadeRate);
            properties.SetFloat(ShaderIDs.s_WaveFoamStrength, Settings._WaveFoamStrength);
            properties.SetFloat(ShaderIDs.s_WaveFoamCoverage, Settings._WaveFoamCoverage);
            properties.SetFloat(ShaderIDs.s_ShorelineFoamMaxDepth, Settings._ShorelineFoamMaximumDepth);
            properties.SetFloat(ShaderIDs.s_ShorelineFoamStrength, Settings._ShorelineFoamStrength);
            properties.SetFloat(ShaderIDs.s_FoamMaximum, Settings.Maximum);
            properties.SetFloat(ShaderIDs.s_FoamNegativeDepthPriming, -Settings._ShorelineFoamPriming);
            properties.SetInteger(ShaderIDs.s_MinimumWavesSlice, Settings.FilterWaves);
        }

        private protected override void GetSubstepData(float timeToSimulate, out int substeps, out float delta)
        {
            substeps = Mathf.FloorToInt(timeToSimulate * _SimulationFrequency);

            delta = substeps > 0 ? (1f / _SimulationFrequency) : 0f;
        }

        internal FoamLod()
        {
            _Enabled = true;
            _TextureFormat = GraphicsFormat.R16_SFloat;
            _SimulationFrequency = 30;
        }

        internal static readonly SortedList<int, ILodInput> s_Inputs = new(Helpers.DuplicateComparison);
        private protected override SortedList<int, ILodInput> Inputs => s_Inputs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnLoad()
        {
            s_Inputs.Clear();
        }
    }
}
