// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// NOTE: DWP2 depends on this file. Any API changes need to be communicated to the DWP2 authors in advance.

using UnityEngine;

// Linter does not support mixing inheritdoc plus defining own parameters.
#pragma warning disable CS1573 // Parameter has no matching param tag in the XML comment (but other parameters do)

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// A layer/event where queries are executed.
    /// </summary>
    public enum CollisionLayer
    {
        /// <summary>
        /// Include all displacement.
        /// </summary>
        [Tooltip("Include all displacement.")]
        Everything,

        /// <summary>
        /// Only include Animated Waves.
        /// </summary>
        [Tooltip("Only include Animated Waves.")]
        AfterAnimatedWaves,

        /// <summary>
        /// Include Animated Waves and Dynamic Waves.
        /// </summary>
        [Tooltip("Include Animated Waves and Dynamic Waves.")]
        AfterDynamicWaves,
    }

    /// <summary>
    /// Interface for an object that returns water surface displacement and height.
    /// </summary>
    public interface ICollisionProvider : IQueryProvider
    {
        internal const string k_LayerTooltip = "Which water collision layer to target.";

        internal static NoneProvider None { get; } = new();

        /// <summary>
        /// Gives a flat, still water.
        /// </summary>
        internal sealed class NoneProvider : ICollisionProvider
        {
            public int Query(int _0, float _1, Vector3[] _2, Vector3[] result0, Vector3[] result1, Vector3[] result2, CollisionLayer _3 = CollisionLayer.Everything)
            {
                if (result0 != null) System.Array.Fill(result0, Vector3.zero);
                if (result1 != null) System.Array.Fill(result1, Vector3.up);
                if (result2 != null) System.Array.Fill(result2, Vector3.zero);
                return 0;
            }

            public int Query(int _0, float _1, Vector3[] _2, float[] result0, Vector3[] result1, Vector3[] result2, CollisionLayer _3 = CollisionLayer.Everything)
            {
                if (result0 != null) System.Array.Fill(result0, WaterRenderer.Instance.SeaLevel);
                if (result1 != null) System.Array.Fill(result1, Vector3.up);
                if (result2 != null) System.Array.Fill(result2, Vector3.zero);
                return 0;
            }
        }

        /// <summary>
        /// Query water physical data at a set of points. Pass in null to any out parameters that are not required.
        /// </summary>
        /// <param name="heights">Resulting heights (displacement Y + sea level) at the query positions. Pass null if this information is not required.</param>
        /// <param name="normals">Resulting normals at the query positions. Pass null if this information is not required.</param>
        /// <param name="velocities">Resulting velocities at the query positions. Pass null if this information is not required.</param>
        /// <inheritdoc cref="IQueryProvider.Query(int, float, Vector3[], int)" />
        int Query(int hash, float minimumLength, Vector3[] points, float[] heights, Vector3[] normals, Vector3[] velocities, CollisionLayer layer = CollisionLayer.Everything);

        /// <param name="displacements">Resulting displacement vectors at the query positions. Add sea level to Y to get world space height.</param>
        /// <inheritdoc cref="IQueryProvider.Query(int, float, Vector3[], int)" />
        /// <inheritdoc cref="Query(int, float, Vector3[], float[], Vector3[], Vector3[], CollisionLayer)" />
        int Query(int hash, float minimumLength, Vector3[] points, Vector3[] displacements, Vector3[] normals, Vector3[] velocities, CollisionLayer layer = CollisionLayer.Everything);
    }
}
