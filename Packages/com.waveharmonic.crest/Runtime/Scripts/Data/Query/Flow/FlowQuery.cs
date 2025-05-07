// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Samples horizontal motion of water volume
    /// </summary>
    sealed class FlowQuery : QueryBase, IFlowProvider
    {
        public FlowQuery(WaterRenderer water) : base(water) { }
        protected override int Kernel => 1;
    }
}
