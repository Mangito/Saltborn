// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using WaveHarmonic.Crest.Utility;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// A dynamic shape simulation that moves around with a displacement LOD.
    /// </summary>
    public sealed partial class DynamicWavesLod : PersistentLod
    {
        [Tooltip("How much waves are dampened in shallow water.")]
        [@Range(0f, 1f)]
        [@GenerateAPI]
        [SerializeField]
        float _AttenuationInShallows = 1f;

        [Tooltip("Settings for fine tuning this simulation.")]
        [@Embedded]
        [@GenerateAPI(Getter.Custom)]
        [SerializeField]
        DynamicWavesLodSettings _Settings;

        const string k_DynamicWavesKeyword = "CREST_DYNAMIC_WAVE_SIM_ON_INTERNAL";

        static new class ShaderIDs
        {
            public static readonly int s_HorizontalDisplace = Shader.PropertyToID("_Crest_HorizontalDisplace");
            public static readonly int s_DisplaceClamp = Shader.PropertyToID("_Crest_DisplaceClamp");
            public static readonly int s_Damping = Shader.PropertyToID("_Crest_Damping");
            public static readonly int s_Gravity = Shader.PropertyToID("_Crest_Gravity");
            public static readonly int s_CourantNumber = Shader.PropertyToID("_Crest_CourantNumber");
        }

        internal static readonly Color s_GizmoColor = new(0f, 1f, 0f, 0.5f);

        internal override string ID => "DynamicWaves";
        internal override string Name => "Dynamic Waves";
        internal override Color GizmoColor => s_GizmoColor;
        private protected override Color ClearColor => Color.black;
        internal override bool RunsInHeadless => true;
        private protected override ComputeShader SimulationShader => WaterResources.Instance.Compute._UpdateDynamicWaves;
        private protected override GraphicsFormat RequestedTextureFormat => _TextureFormatMode switch
        {
            // Try and match Animated Waves format as we copy this simulation into it.
            LodTextureFormatMode.Automatic => Water == null ? GraphicsFormat.None : Water.AnimatedWavesLod.TextureFormatMode switch
            {
                LodTextureFormatMode.Precision => GraphicsFormat.R32G32_SFloat,
                _ => GraphicsFormat.R16G16_SFloat,
            },
            LodTextureFormatMode.Performance => GraphicsFormat.R16G16_SFloat,
            LodTextureFormatMode.Precision => GraphicsFormat.R32G32_SFloat,
            LodTextureFormatMode.Manual => _TextureFormat,
            _ => throw new System.NotImplementedException(),
        };

        internal float TimeLeftToSimulate => _TimeToSimulate;

        internal DynamicWavesLod()
        {
            _OverrideResolution = false;
            _Resolution = 512;
            _TextureFormatMode = LodTextureFormatMode.Automatic;
            _TextureFormat = GraphicsFormat.R16G16_SFloat;
        }

        internal override void Enable()
        {
            base.Enable();

            Shader.EnableKeyword(k_DynamicWavesKeyword);
        }

        internal override void Disable()
        {
            base.Disable();

            Shader.DisableKeyword(k_DynamicWavesKeyword);
        }

        internal override void Bind<T>(T target)
        {
            base.Bind(target);
            target.SetFloat(ShaderIDs.s_HorizontalDisplace, Settings._HorizontalDisplace);
            target.SetFloat(ShaderIDs.s_DisplaceClamp, Settings._DisplaceClamp);
        }

        private protected override void SetAdditionalSimulationParameters<T>(T simMaterial)
        {
            base.SetAdditionalSimulationParameters(simMaterial);

            simMaterial.SetFloat(ShaderIDs.s_Damping, Settings._Damping);
            simMaterial.SetFloat(ShaderIDs.s_Gravity, _Water.Gravity * Settings._GravityMultiplier);
            simMaterial.SetFloat(ShaderIDs.s_CourantNumber, Settings._CourantNumber);
            simMaterial.SetFloat(AnimatedWavesLod.ShaderIDs.s_AttenuationInShallows, _AttenuationInShallows);
        }

        private protected override void GetSubstepData(float timeToSimulate, out int substeps, out float delta)
        {
            substeps = Mathf.FloorToInt(timeToSimulate * _SimulationFrequency);
            delta = substeps > 0 ? (1f / _SimulationFrequency) : 0f;
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
