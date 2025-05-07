// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Base class for scripts that provide the time to the water system.
    /// </summary>
    /// <remarks>
    /// See derived classes for examples.
    /// </remarks>
    public interface ITimeProvider
    {
        /// <summary>
        /// Current time.
        /// </summary>
        float Time { get; }

        /// <summary>
        /// Delta time.
        /// </summary>
        float Delta { get; }
    }

    /// <inheritdoc/>
    [@ExecuteDuringEditMode]
    [@HelpURL("Manual/TimeProviders.html")]
    public abstract class TimeProvider : ManagedBehaviour<WaterRenderer>, ITimeProvider
    {
        /// <inheritdoc/>
        public abstract float Time { get; }

        /// <inheritdoc/>
        public abstract float Delta { get; }
    }
}
