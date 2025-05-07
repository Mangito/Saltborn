// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

// Linter does not support mixing inheritdoc plus defining own parameters.
#pragma warning disable CS1573 // Parameter has no matching param tag in the XML comment (but other parameters do)

namespace WaveHarmonic.Crest.Internal
{
    /// <summary>
    /// Base class for sample helpers which sample a single point.
    /// </summary>
    /// <remarks>
    /// It is not particularly efficient to sample a single point, but is a fairly
    /// common case.
    /// </remarks>
    public abstract class SampleHelper
    {
        private protected readonly Vector3[] _QueryPosition;
        private protected readonly Vector3[] _QueryResult;

        int _LastFrame = -1;

        private protected SampleHelper(int queryCount = 1)
        {
            _QueryPosition = new Vector3[queryCount];
            _QueryResult = new Vector3[queryCount];
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private protected void Validate(bool allowMultipleCallsPerFrame)
        {
            if (Application.isPlaying && !Time.inFixedTimeStep && !allowMultipleCallsPerFrame && _LastFrame == Time.frameCount)
            {
                var type = GetType().Name;
                Debug.LogWarning($"Crest: {type} sample called multiple times in one frame which is not expected. Each {type} object services a single sample per frame. To perform multiple queries, create multiple {type} objects or use the query provider API directly.");
            }

            _LastFrame = Time.frameCount;
        }

        // The first method is there just to get inheritdoc to work as it does not like
        // inheriting params plus adding additional params.

        /// <remarks>
        /// Only call once per frame.
        /// </remarks>
        /// <param name="position">World space position to sample.</param>
        /// <param name="minimumLength">
        /// The smallest length scale you are interested in. If you are sampling data for boat physics,
        /// pass in the boats width. Larger objects will ignore smaller details in the data.
        /// </param>
        /// <param name="layer">The collision layer to target.</param>
        /// <returns>Whether the query was successful.</returns>
        bool Sample(Vector3 position, float minimumLength, CollisionLayer layer) => false;
    }
}

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Helper to obtain the water surface collision at a single point per frame.
    /// </summary>
    /// <inheritdoc/>
    public sealed class SampleCollisionHelper : Internal.SampleHelper
    {
        readonly Vector3[] _QueryResultNormal = new Vector3[1];
        readonly Vector3[] _QueryResultVelocity = new Vector3[1];

        enum QueryType
        {
            Displacement,
            Height,
        }

        [System.Flags]
        enum QueryOptions
        {
            None,
            Velocity,
            Normal,
            All = Velocity | Normal,
        }

        bool Sample(int id, Vector3 position, out Vector3 displacement, out float height, out Vector3 velocity, out Vector3 normal, QueryType type, QueryOptions options, CollisionLayer layer = CollisionLayer.Everything, float minimumLength = 0f, bool allowMultipleCallsPerFrame = false)
        {
            var water = WaterRenderer.Instance;
            var provider = water == null ? null : water.AnimatedWavesLod.Provider;

            height = 0f;
            displacement = Vector3.zero;
            velocity = Vector3.zero;
            normal = Vector3.up;

            if (provider == null)
            {
                return false;
            }

            var isHeight = type == QueryType.Height;
            var isDisplacement = type == QueryType.Displacement;
            var isVelocity = (options & QueryOptions.Velocity) == QueryOptions.Velocity;
            var isNormal = (options & QueryOptions.Normal) == QueryOptions.Normal;

            Validate(allowMultipleCallsPerFrame);

            _QueryPosition[0] = position;

            var status = provider.Query
            (
                id,
                minimumLength,
                _QueryPosition,
                _QueryResult,
                isNormal ? _QueryResultNormal : null,
                isVelocity ? _QueryResultVelocity : null,
                layer
            );

            if (!provider.RetrieveSucceeded(status))
            {
                height = water.SeaLevel;
                return false;
            }

            if (isHeight) height = _QueryResult[0].y + water.SeaLevel;
            if (isDisplacement) displacement = _QueryResult[0];
            if (isVelocity) velocity = _QueryResultVelocity[0];
            if (isNormal) normal = _QueryResultNormal[0];

            return true;
        }

        internal bool SampleHeight(int id, Vector3 position, out float height, CollisionLayer layer = CollisionLayer.Everything, float minimumLength = 0f, bool allowMultipleCallsPerFrame = false)
        {
            return Sample(id, position, out _, out height, out _, out _, QueryType.Height, QueryOptions.None, layer, minimumLength, allowMultipleCallsPerFrame);
        }

        internal bool SampleHeight(int id, Vector3 position, out float height, out Vector3 velocity, CollisionLayer layer = CollisionLayer.Everything, float minimumLength = 0f, bool allowMultipleCallsPerFrame = false)
        {
            return Sample(id, position, out _, out height, out velocity, out _, QueryType.Height, QueryOptions.Velocity, layer, minimumLength, allowMultipleCallsPerFrame);
        }

        internal bool SampleHeight(int id, Vector3 position, out float height, out Vector3 velocity, out Vector3 normal, CollisionLayer layer = CollisionLayer.Everything, float minimumLength = 0f, bool allowMultipleCallsPerFrame = false)
        {
            return Sample(id, position, out _, out height, out velocity, out normal, QueryType.Height, QueryOptions.All, layer, minimumLength, allowMultipleCallsPerFrame);
        }

        internal bool SampleDisplacement(int id, Vector3 position, out Vector3 displacement, out Vector3 velocity, out Vector3 normal, CollisionLayer layer = CollisionLayer.Everything, float minimumLength = 0f, bool allowMultipleCallsPerFrame = false)
        {
            return Sample(id, position, out displacement, out _, out velocity, out normal, QueryType.Displacement, QueryOptions.All, layer, minimumLength, allowMultipleCallsPerFrame);
        }

        internal bool SampleDisplacement(int id, Vector3 position, out Vector3 displacement, out Vector3 velocity, CollisionLayer layer = CollisionLayer.Everything, float minimumLength = 0f, bool allowMultipleCallsPerFrame = false)
        {
            return Sample(id, position, out displacement, out _, out velocity, out _, QueryType.Displacement, QueryOptions.Velocity, layer, minimumLength, allowMultipleCallsPerFrame);
        }

        internal bool SampleDisplacement(int id, Vector3 position, out Vector3 displacement, CollisionLayer layer = CollisionLayer.Everything, float minimumLength = 0f, bool allowMultipleCallsPerFrame = false)
        {
            return Sample(id, position, out displacement, out _, out _, out _, QueryType.Displacement, QueryOptions.None, layer, minimumLength, allowMultipleCallsPerFrame);
        }

        // The first method is there just to get inheritdoc to work as it does not like
        // inheriting params plus adding additional params.

        /// <summary>
        /// Sample displacement data.
        /// </summary>
        /// <param name="displacement">The water surface displacement to point.</param>
        /// <param name="height">The water surface height.</param>
        /// <param name="velocity">The velocity of the water surface excluding flow velocity.</param>
        /// <param name="normal">The water surface normal.</param>
        /// <inheritdoc cref="Internal.SampleHelper.Sample" />
        bool Sample(Vector3 position, float height, Vector3 displacement, Vector3 normal, Vector3 velocity, float minimumLength = 0f, CollisionLayer layer = CollisionLayer.Everything) => false;

        /// <inheritdoc cref="Sample(Vector3, float, Vector3, Vector3, Vector3, float, CollisionLayer)" />
        [System.Obsolete("Please use SampleDisplacement instead. Be wary that the new API has switch the normal parameter with velocity.")]
        public bool Sample(Vector3 position, out Vector3 displacement, out Vector3 normal, out Vector3 velocity, float minimumLength = 0f, CollisionLayer layer = CollisionLayer.Everything) => SampleDisplacement(GetHashCode(), position, out displacement, out velocity, out normal, layer, minimumLength);

        /// <inheritdoc cref="Sample(Vector3, float, Vector3, Vector3, Vector3, float, CollisionLayer)" />
        [System.Obsolete("Please use SampleHeight instead. Be wary that the new API has switch the normal parameter with velocity.")]
        public bool Sample(Vector3 position, out float height, out Vector3 normal, out Vector3 velocity, float minimumLength = 0f, CollisionLayer layer = CollisionLayer.Everything) => SampleHeight(GetHashCode(), position, out height, out velocity, out normal, layer, minimumLength);

        /// <inheritdoc cref="Sample(Vector3, float, Vector3, Vector3, Vector3, float, CollisionLayer)" />
        [System.Obsolete("Please use SampleHeight instead. Be wary that the new API has switch the normal parameter with velocity.")]
        public bool Sample(Vector3 position, out float height, out Vector3 normal, float minimumLength = 0f, CollisionLayer layer = CollisionLayer.Everything) => SampleHeight(GetHashCode(), position, out height, out _, out normal, layer, minimumLength);

        /// <inheritdoc cref="Sample(Vector3, float, Vector3, Vector3, Vector3, float, CollisionLayer)" />
        [System.Obsolete("Please use SampleHeight instead. Be wary that the new API has switch the normal parameter with velocity.")]
        public bool Sample(Vector3 position, out float height, float minimumLength = 0f, CollisionLayer layer = CollisionLayer.Everything) => SampleHeight(GetHashCode(), position, out height, layer, minimumLength);


        /// <inheritdoc cref="Sample(Vector3, float, Vector3, Vector3, Vector3, float, CollisionLayer)" />
        public bool SampleDisplacement(Vector3 position, out Vector3 displacement, out Vector3 velocity, out Vector3 normal, float minimumLength = 0f, CollisionLayer layer = CollisionLayer.Everything) => SampleDisplacement(GetHashCode(), position, out displacement, out velocity, out normal, layer, minimumLength);

        /// <inheritdoc cref="Sample(Vector3, float, Vector3, Vector3, Vector3, float, CollisionLayer)" />
        public bool SampleDisplacement(Vector3 position, out Vector3 displacement, out Vector3 velocity, float minimumLength = 0f, CollisionLayer layer = CollisionLayer.Everything) => SampleDisplacement(GetHashCode(), position, out displacement, out velocity, layer, minimumLength);

        /// <inheritdoc cref="Sample(Vector3, float, Vector3, Vector3, Vector3, float, CollisionLayer)" />
        public bool SampleDisplacement(Vector3 position, out Vector3 displacement, float minimumLength = 0f, CollisionLayer layer = CollisionLayer.Everything) => SampleDisplacement(GetHashCode(), position, out displacement, layer, minimumLength);

        /// <inheritdoc cref="Sample(Vector3, float, Vector3, Vector3, Vector3, float, CollisionLayer)" />
        public bool SampleHeight(Vector3 position, out float height, out Vector3 velocity, out Vector3 normal, float minimumLength = 0f, CollisionLayer layer = CollisionLayer.Everything) => SampleHeight(GetHashCode(), position, out height, out velocity, out normal, layer, minimumLength);

        /// <inheritdoc cref="Sample(Vector3, float, Vector3, Vector3, Vector3, float, CollisionLayer)" />
        public bool SampleHeight(Vector3 position, out float height, out Vector3 velocity, float minimumLength = 0f, CollisionLayer layer = CollisionLayer.Everything) => SampleHeight(GetHashCode(), position, out height, out velocity, layer, minimumLength);

        /// <inheritdoc cref="Sample(Vector3, float, Vector3, Vector3, Vector3, float, CollisionLayer)" />
        public bool SampleHeight(Vector3 position, out float height, float minimumLength = 0f, CollisionLayer layer = CollisionLayer.Everything) => SampleHeight(GetHashCode(), position, out height, layer, minimumLength);
    }

    /// <summary>
    /// Helper to obtain the flow data (horizontal water motion) at a single point.
    /// </summary>
    /// <inheritdoc/>
    public sealed class SampleFlowHelper : Internal.SampleHelper
    {
        /// <summary>
        /// Sample flow data.
        /// </summary>
        /// <param name="flow">Filled with resulting flow.</param>
        /// <returns>Whether the query was successful.</returns>
        /// <inheritdoc cref="Internal.SampleHelper.Sample" />
        public bool Sample(Vector3 position, out Vector2 flow, float minimumLength = 0f)
        {
            var water = WaterRenderer.Instance;
            var flowProvider = WaterRenderer.Instance == null ? null : water.FlowLod.Provider;

            if (flowProvider == null)
            {
                flow = Vector2.zero;
                return false;
            }

            Validate(false);

            _QueryPosition[0] = position;

            var status = flowProvider.Query(GetHashCode(), minimumLength, _QueryPosition, _QueryResult);

            if (!flowProvider.RetrieveSucceeded(status))
            {
                flow = Vector2.zero;
                return false;
            }

            // We don't support float2 queries unfortunately, so unpack from float3
            flow.x = _QueryResult[0].x;
            flow.y = _QueryResult[0].z;

            return true;
        }
    }

    /// <summary>
    /// Helper to obtain the depth data at a single point.
    /// </summary>
    public sealed class SampleDepthHelper : Internal.SampleHelper
    {
        bool Sample(Vector3 position, out Vector2 result)
        {
            var water = WaterRenderer.Instance;
            var depthProvider = WaterRenderer.Instance == null ? null : water.DepthLod.Provider;

            if (depthProvider == null)
            {
                result = Vector2.zero;
                return false;
            }

            Validate(false);

            _QueryPosition[0] = position;

            var status = depthProvider.Query(GetHashCode(), minimumLength: 0, _QueryPosition, _QueryResult);
            if (!depthProvider.RetrieveSucceeded(status))
            {
                result = Vector2.zero;
                return false;
            }

            result = _QueryResult[0];
            return true;
        }

        /// <summary>
        /// Sample both the water depth and water edge distance.
        /// </summary>
        /// <param name="depth">Filled by the water depth at the query position.</param>
        /// <param name="distance">Filled by the distance to water edge at the query position.</param>
        /// <inheritdoc cref="Internal.SampleHelper.Sample" />
        bool Sample(Vector3 position, out float depth, out float distance)
        {
            var success = Sample(position, out var result);
            depth = result.x;
            distance = result.y;
            return success;
        }

        /// <summary>Sample water depth.</summary>
        /// <inheritdoc cref="Sample(Vector3, out float, out float)"/>
        bool SampleWaterDepth(Vector3 position, out float depth)
        {
            var success = Sample(position, out var result);
            depth = result.x;
            return success;
        }

        /// <summary>Sample water edge distance.</summary>
        /// <inheritdoc cref="Sample(Vector3, out float, out float)"/>
        public bool SampleDistanceToWaterEdge(Vector3 position, out float distance)
        {
            var success = Sample(position, out var result);
            distance = result.y;
            return success;
        }
    }
}
