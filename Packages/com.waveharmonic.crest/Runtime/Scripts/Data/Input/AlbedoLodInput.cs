// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Registers a custom input to the <see cref="AlbedoLod"/>.
    /// </summary>
    /// <remarks>
    /// Attach this to objects that you want to influence the surface color.
    /// </remarks>
    [@HelpURL("Manual/WaterAppearance.html#albedo-inputs")]
    public sealed partial class AlbedoLodInput : LodInput
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        internal override LodInputMode DefaultMode => LodInputMode.Renderer;
    }
}
