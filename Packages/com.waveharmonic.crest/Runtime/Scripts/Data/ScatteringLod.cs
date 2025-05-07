// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using WaveHarmonic.Crest.Internal;
using WaveHarmonic.Crest.Utility;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Overrides the main scattering color.
    /// </summary>
    public sealed partial class ScatteringLod : ColorLod
    {
        // Orange
        internal static readonly Color s_GizmoColor = new(1f, 165f / 255f, 0f, 0.5f);
        internal static readonly Color s_DefaultColor = new(0f, 0.098f, 0.2f, 1f);

        static new class ShaderIDs
        {
            public static readonly int s_SampleScatteringSimulation = Shader.PropertyToID("g_Crest_SampleScatteringSimulation");
        }

        internal override string ID => "Scattering";
        internal override string Name => "Scattering";
        internal override Color GizmoColor => s_GizmoColor;
        private protected override bool NeedToReadWriteTextureData => true;
        private protected override bool RequiresClearBorder => true;
        private protected override bool AlwaysClear => true;
        private protected override Color ClearColor
        {
            get
            {
                var color = Color.clear;

                if (_Water.Material != null)
                {
                    color = _Water.Material.GetColor(WaterRenderer.ShaderIDs.s_Scattering).MaybeLinear();
                    color.a = 0f;
                }

                return color;
            }
        }

        private protected override int GlobalShaderID => ShaderIDs.s_SampleScatteringSimulation;

        internal ScatteringLod()
        {
            _ShorelineColor = (s_DefaultColor * 6f).Clamped01();
        }

        private protected override void SetShorelineColor(Color previous, Color current)
        {
            if (previous == current) return;
            _ShorelineColorValue = current.MaybeLinear();
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
