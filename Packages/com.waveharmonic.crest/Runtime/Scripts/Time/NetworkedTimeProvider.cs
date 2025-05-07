// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Gives a time to Crest with a custom time offset.
    /// </summary>
    /// <remarks>
    /// Assign this component to the <see cref="WaterRenderer"/> component and set the
    /// <see cref="TimeOffsetToServer"/> property of this component to the delta from
    /// this client's time to the shared server time.
    /// </remarks>
    [AddComponentMenu(Constants.k_MenuPrefixTime + "Networked Time Provider")]
    [@HelpURL("Manual/TimeProviders.html#network-synchronisation")]
    public sealed class NetworkedTimeProvider : TimeProvider
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        /// <summary>
        /// If Time.time on this client is 1.5s ahead of the shared/server Time.time, set
        /// this field to -1.5.
        /// </summary>
        public float TimeOffsetToServer { get; set; }

        readonly DefaultTimeProvider _DefaultTimeProvider = new();

        /// <inheritdoc/>
        public override float Time => _DefaultTimeProvider.Time + TimeOffsetToServer;

        /// <inheritdoc/>
        public override float Delta => _DefaultTimeProvider.Delta;
    }
}
