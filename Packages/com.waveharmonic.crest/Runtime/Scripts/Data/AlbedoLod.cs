// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using WaveHarmonic.Crest.Utility;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// A color layer that can be composited onto the water surface.
    /// </summary>
    [FilterEnum(nameof(_TextureFormatMode), Filtered.Mode.Include, (int)LodTextureFormatMode.Performance, (int)LodTextureFormatMode.Manual)]
    public sealed partial class AlbedoLod : Lod
    {
        internal static readonly Color s_GizmoColor = new(1f, 0f, 1f, 0.5f);

        internal override string ID => "Albedo";
        internal override Color GizmoColor => s_GizmoColor;
        private protected override Color ClearColor => Color.clear;
        private protected override bool NeedToReadWriteTextureData => false;

        private protected override GraphicsFormat RequestedTextureFormat => _TextureFormatMode switch
        {
            LodTextureFormatMode.Manual => _TextureFormat,
            _ => GraphicsFormat.R8G8B8A8_UNorm,
        };

        internal AlbedoLod()
        {
            _Resolution = 768;
            _TextureFormat = GraphicsFormat.R8G8B8A8_UNorm;
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
