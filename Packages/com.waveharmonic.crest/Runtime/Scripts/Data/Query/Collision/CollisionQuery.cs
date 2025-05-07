// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Samples water surface shape - displacement, height, normal, velocity.
    /// </summary>
    sealed class CollisionQuery : QueryBase, ICollisionProvider
    {
        public CollisionQuery(WaterRenderer water) : base(water) { }
        protected override int Kernel => 0;

        public int Query(int ownerHash, float minSpatialLength, Vector3[] queryPoints, Vector3[] resultDisplacements, Vector3[] resultNormals, Vector3[] resultVelocities, CollisionLayer layer = CollisionLayer.Everything)
        {
            var result = (int)QueryStatus.OK;

            if (!UpdateQueryPoints(ownerHash, minSpatialLength, queryPoints, resultNormals != null ? queryPoints : null))
            {
                result |= (int)QueryStatus.PostFailed;
            }

            if (!RetrieveResults(ownerHash, resultDisplacements, null, resultNormals))
            {
                result |= (int)QueryStatus.RetrieveFailed;
            }

            if (resultVelocities != null)
            {
                result |= CalculateVelocities(ownerHash, resultVelocities);
            }

            return result;
        }

        public int Query(int ownerHash, float minimumSpatialLength, Vector3[] queryPoints, float[] resultHeights, Vector3[] resultNormals, Vector3[] resultVelocities, CollisionLayer layer = CollisionLayer.Everything)
        {
            var result = (int)QueryStatus.OK;

            if (!UpdateQueryPoints(ownerHash, minimumSpatialLength, queryPoints, resultNormals != null ? queryPoints : null))
            {
                result |= (int)QueryStatus.PostFailed;
            }

            if (!RetrieveResults(ownerHash, null, resultHeights, resultNormals))
            {
                result |= (int)QueryStatus.RetrieveFailed;
            }

            if (resultVelocities != null)
            {
                result |= CalculateVelocities(ownerHash, resultVelocities);
            }

            return result;
        }
    }

    sealed class CollisionQueryWithPasses : ICollisionProvider, IQueryable
    {
        readonly CollisionQuery _AnimatedWaves;
        readonly CollisionQuery _DynamicWaves;
        readonly CollisionQuery _Displacement;
        readonly WaterRenderer _Water;

        public int ResultGuidCount => _AnimatedWaves.ResultGuidCount + _DynamicWaves.ResultGuidCount + _Displacement.ResultGuidCount;
        public int RequestCount => _AnimatedWaves.RequestCount + _DynamicWaves.RequestCount + _Displacement.RequestCount;
        public int QueryCount => _AnimatedWaves.QueryCount + _DynamicWaves.QueryCount + _Displacement.QueryCount;

        public CollisionQueryWithPasses(WaterRenderer water)
        {
            _Water = water;
            _AnimatedWaves = new(water);
            _DynamicWaves = new(water);
            _Displacement = new(water);
        }

        // Gets the provider for the given layer, falling back to previous layer when needed.
        CollisionQuery GetProvider(CollisionLayer layer)
        {
            var layers = _Water.AnimatedWavesLod._CollisionLayers;

            if (layers == CollisionLayers.Nothing)
            {
                return _Displacement;
            }

            if (layer == CollisionLayer.Everything)
            {
                if (layers.HasFlag(CollisionLayers.Displacement))
                {
                    return _Displacement;
                }
            }

            if (layer >= CollisionLayer.AfterDynamicWaves)
            {
                if (layers.HasFlag(CollisionLayers.DynamicWaves) && _Water.DynamicWavesLod.Enabled)
                {
                    return _DynamicWaves;
                }
            }

            return _AnimatedWaves;
        }

        public int Query(int hash, float minimumLength, Vector3[] points, float[] heights, Vector3[] normals, Vector3[] velocities, CollisionLayer layer = CollisionLayer.Everything)
        {
            return GetProvider(layer).Query(hash, minimumLength, points, heights, normals, velocities);
        }

        public int Query(int hash, float minimumLength, Vector3[] points, Vector3[] displacements, Vector3[] normals, Vector3[] velocities, CollisionLayer layer = CollisionLayer.Everything)
        {
            return GetProvider(layer).Query(hash, minimumLength, points, displacements, normals, velocities);
        }

        public void UpdateQueries(WaterRenderer water, CollisionLayer layer)
        {
            switch (layer)
            {
                case CollisionLayer.Everything: _Displacement.UpdateQueries(water); break;
                case CollisionLayer.AfterAnimatedWaves: _AnimatedWaves.UpdateQueries(water); break;
                case CollisionLayer.AfterDynamicWaves: _DynamicWaves.UpdateQueries(water); break;
            }
        }

        public void UpdateQueries(WaterRenderer water)
        {
            _Displacement.UpdateQueries(water);
        }

        public void CleanUp()
        {
            _AnimatedWaves.CleanUp();
            _DynamicWaves.CleanUp();
            _Displacement.CleanUp();
        }
    }

    static partial class Extensions
    {
        public static void UpdateQueries(this ICollisionProvider self, WaterRenderer water, CollisionLayer layer) => (self as CollisionQueryWithPasses)?.UpdateQueries(water, layer);
        public static void UpdateQueries(this ICollisionProvider self, WaterRenderer water) => (self as IQueryable)?.UpdateQueries(water);
        public static void CleanUp(this ICollisionProvider self) => (self as IQueryable)?.CleanUp();
    }
}
