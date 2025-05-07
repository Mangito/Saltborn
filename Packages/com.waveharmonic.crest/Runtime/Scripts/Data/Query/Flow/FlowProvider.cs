// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

// Linter does not support mixing inheritdoc + defining own parameters.
#pragma warning disable CS1573 // Parameter has no matching param tag in the XML comment (but other parameters do)

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Interface for an object that returns water surface displacement and height.
    /// </summary>
    public interface IFlowProvider : IQueryProvider
    {
        internal static NoneProvider None { get; } = new();

        /// <summary>
        /// Gives a stationary water (no horizontal flow).
        /// </summary>
        internal sealed class NoneProvider : IFlowProvider
        {
            public int Query(int _0, float _1, Vector3[] _2, Vector3[] result)
            {
                if (result != null) System.Array.Clear(result, 0, result.Length);
                return 0;
            }
        }

        /// <summary>
        /// Query water flow data (horizontal motion) at a set of points.
        /// </summary>
        /// <param name="results">Water surface flow velocities at the query positions.</param>
        /// <inheritdoc cref="IQueryProvider.Query(int, float, Vector3[], int)" />
        int Query(int hash, float minimumLength, Vector3[] points, Vector3[] results);
    }
}
