// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// We do not add height to displacement directly for better precision and layering.

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using WaveHarmonic.Crest.Utility;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Data that gives the water level (water height at rest and gradients).
    /// </summary>
    public sealed partial class LevelLod : Lod
    {
        // Purple/Indigo
        internal static readonly Color s_GizmoColor = new(75f / 255f, 0f, 130f / 255f, 0.5f);

        internal override string ID => "Level";
        internal override Color GizmoColor => s_GizmoColor;
        private protected override Color ClearColor => Color.black;
        private protected override bool NeedToReadWriteTextureData => true;
        internal override bool RunsInHeadless => true;

        private protected override GraphicsFormat RequestedTextureFormat => _TextureFormatMode switch
        {
            LodTextureFormatMode.Automatic => Water == null ? GraphicsFormat.None : Water.AnimatedWavesLod.TextureFormatMode switch
            {
                LodTextureFormatMode.Precision => GraphicsFormat.R32_SFloat,
                _ => GraphicsFormat.R16_SFloat,
            },
            LodTextureFormatMode.Performance => GraphicsFormat.R16_SFloat,
            LodTextureFormatMode.Precision => GraphicsFormat.R32_SFloat,
            LodTextureFormatMode.Manual => _TextureFormat,
            _ => throw new System.NotImplementedException(),
        };

        internal LevelLod()
        {
            _Enabled = false;
            _OverrideResolution = false;
            _TextureFormatMode = LodTextureFormatMode.Automatic;
            _TextureFormat = GraphicsFormat.R16_SFloat;
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
