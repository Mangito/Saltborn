// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Registers a custom input to the <see cref="AbsorptionLod"/>.
    /// </summary>
    /// <remarks>
    /// Attach this to objects that you want to influence the scattering color.
    /// </remarks>
    [@HelpURL("Manual/WaterAppearance.html#volume-color-inputs")]
    public sealed partial class AbsorptionLodInput : LodInput
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
            _Blend = LodInputBlend.Alpha;
        }

        // Looks fine moving around.
        private protected override bool FollowHorizontalMotion => true;
    }
}
