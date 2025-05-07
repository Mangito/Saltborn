// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Helper to trace a ray against the water surface.
    /// </summary>
    /// <remarks>
    /// Works by sampling at a set of points along the ray and interpolating the
    /// intersection location.
    /// </remarks>
    public sealed class RayCastHelper : Internal.SampleHelper
    {
        readonly float _RayStepSize;
        readonly float _MinimumLength;

        /// <summary>
        /// The length of the ray and the step size must be given here. The smaller the step size, the greater the accuracy.
        /// </summary>
        /// <param name="rayLength">Length of the ray.</param>
        /// <param name="rayStepSize">Size of the step. With length the number of steps is computed.</param>
        public RayCastHelper(float rayLength, float rayStepSize = 2f) : base(ComputeQueryCount(rayLength, ref rayStepSize))
        {
            _RayStepSize = rayStepSize;
            // Waves go max double along min length. Thats too much - only allow half of a wave per step.
            _MinimumLength = _RayStepSize * 4f;
        }

        static int ComputeQueryCount(float rayLength, ref float rayStepSize)
        {
            Debug.Assert(rayLength > 0f);
            Debug.Assert(rayStepSize > 0f);

            var stepCount = Mathf.CeilToInt(rayLength / rayStepSize) + 1;

            var maxStepCount = 128;
            if (stepCount > maxStepCount)
            {
                stepCount = maxStepCount;
                rayStepSize = rayLength / (stepCount - 1f);
                Debug.LogWarning($"Crest: RayTraceHelper: ray steps exceed maximum ({maxStepCount}), step size increased to {rayStepSize} to reduce step count.");
            }

            return stepCount;
        }

        /// <summary>
        /// Call this once each frame to do the query.
        /// </summary>
        /// <param name="origin">World space position of ray origin</param>
        /// <param name="direction">World space ray direction</param>
        /// <param name="distance">The distance along the ray to the first intersection with the water surface.</param>
        /// <param name="layer">The layer this ray targets.</param>
        /// <returns>True if the results have come back from the GPU, and if the ray intersects the water surface.</returns>
        public bool RayCast(Vector3 origin, Vector3 direction, out float distance, CollisionLayer layer = CollisionLayer.Everything)
        {
            distance = -1f;

            Validate(allowMultipleCallsPerFrame: false);

            var water = WaterRenderer.Instance;
            var provider = water == null ? null : water.AnimatedWavesLod.Provider;
            if (provider == null) return false;

            for (var i = 0; i < _QueryPosition.Length; i++)
            {
                _QueryPosition[i] = origin + i * _RayStepSize * direction;
            }

            var status = provider.Query(GetHashCode(), _MinimumLength, _QueryPosition, _QueryResult, null, null, layer);

            if (!provider.RetrieveSucceeded(status))
            {
                return false;
            }

            // Now that data is available, compare the height of the water to the height of each point of the ray. If
            // the ray crosses the surface, the distance to the intersection is interpolated from the heights.
            for (var i = 1; i < _QueryPosition.Length; i++)
            {
                var height0 = _QueryResult[i - 1].y + water.SeaLevel - _QueryPosition[i - 1].y;
                var height1 = _QueryResult[i].y + water.SeaLevel - _QueryPosition[i].y;

                if (Mathf.Sign(height0) != Mathf.Sign(height1))
                {
                    var prop = Mathf.Abs(height0) / (Mathf.Abs(height0) + Mathf.Abs(height1));
                    distance = (i - 1 + prop) * _RayStepSize;
                    break;
                }
            }

            return distance >= 0f;
        }
    }
}
