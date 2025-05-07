// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Registers a custom input to the <see cref="ShadowLod"/>.
    /// </summary>
    /// <remarks>
    /// Attach this objects that you want use to override shadows.
    /// </remarks>
    [@HelpURL("Manual/WaterAppearance.html#shadows-lod")]
    public sealed partial class ShadowLodInput : LodInput
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        internal override LodInputMode DefaultMode => LodInputMode.Renderer;
    }
}
