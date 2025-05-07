// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using WaveHarmonic.Crest.Internal;
using WaveHarmonic.Crest.Utility;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Overrides the absorption color.
    /// </summary>
    public sealed partial class AbsorptionLod : ColorLod
    {
        // Orange
        internal static readonly Color s_GizmoColor = new(1f, 165f / 255f, 0f, 0.5f);
        internal static readonly Color s_DefaultColor = new(0.342f, 0.695f, 0.85f, 0.102f);

        static new class ShaderIDs
        {
            public static readonly int s_SampleAbsorptionSimulation = Shader.PropertyToID("g_Crest_SampleAbsorptionSimulation");
        }

        internal override string ID => "Absorption";
        internal override string Name => "Absorption";
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
                    color = _Water.Material.GetVector(WaterRenderer.ShaderIDs.s_Absorption);
                    color.a = 0f;
                }

                return color;
            }
        }

        private protected override int GlobalShaderID => ShaderIDs.s_SampleAbsorptionSimulation;

        internal AbsorptionLod()
        {
            _ShorelineColor = (s_DefaultColor * 1.5f).Clamped01();
        }

        private protected override void SetShorelineColor(Color previous, Color current)
        {
            if (previous == current) return;
            _ShorelineColorValue = WaterRenderer.CalculateAbsorptionValueFromColor(current);
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
