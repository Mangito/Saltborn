// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest
{
    sealed class DepthQuery : QueryBase, IDepthProvider
    {
        public DepthQuery(WaterRenderer water) : base(water) { }
        protected override int Kernel => 2;

        public override int Query(int hash, float minimumSpatialLength, Vector3[] queries, Vector3[] results)
        {
            var id = base.Query(hash, minimumSpatialLength, queries, results);

            // Infinity will become NaN. Convert back to infinity.
            // Negative infinity should not happen.
            for (var i = 0; i < results.Length; i++)
            {
                var value = results[i];
                if (float.IsNaN(value.x)) value.x = float.PositiveInfinity;
                if (float.IsNaN(value.y)) value.y = float.PositiveInfinity;
                results[i] = value;
            }

            return id;
        }
    }
}
