// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Registers a custom input to the <see cref="FlowLod"/>.
    /// </summary>
    /// <remarks>
    /// Attach this to objects that you want to influence the horizontal flow of the
    /// water volume.
    /// </remarks>
    [@HelpURL("Manual/TidesAndCurrents.html#flow-inputs")]
    [FilterEnum(nameof(_Blend), Filtered.Mode.Include, (int)LodInputBlend.Additive, (int)LodInputBlend.Alpha)]
    public sealed partial class FlowLodInput : LodInput
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        // Countering will incur thrashing. Previously we allowed the option so the
        // serialized value could be "false".
        private protected override bool FollowHorizontalMotion => true;

#if d_CrestPaint
        internal override LodInputMode DefaultMode => LodInputMode.Paint;
#else
        internal override LodInputMode DefaultMode => LodInputMode.Renderer;
#endif
    }
}
