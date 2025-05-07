// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Registers a custom input to the <see cref="FoamLod"/>.
    /// </summary>
    /// <remarks>
    /// Attach this to objects that you want to influence the foam simulation, such as
    /// depositing foam on the surface.
    /// </remarks>
    [@HelpURL("Manual/WaterAppearance.html#foam-inputs")]
    [@FilterEnum(nameof(_Blend), Filtered.Mode.Include, (int)LodInputBlend.Additive, (int)LodInputBlend.Maximum)]
    public sealed partial class FoamLodInput : LodInput
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

#if d_CrestPaint
        internal override LodInputMode DefaultMode => LodInputMode.Paint;
#else
        internal override LodInputMode DefaultMode => LodInputMode.Renderer;
#endif

        internal override void InferBlend()
        {
            base.InferBlend();

            if (_Mode is LodInputMode.Paint)
            {
                _Blend = LodInputBlend.Maximum;
            }
        }
    }
}
