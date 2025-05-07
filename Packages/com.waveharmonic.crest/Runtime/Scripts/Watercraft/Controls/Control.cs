// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest.Watercraft
{
    /// <summary>
    /// Controls provide input whether from the player or otherwise. Extend to
    /// implement a control. See derived classes for examples.
    /// </summary>
    public abstract class Control : MonoBehaviour
    {
        /// <summary>
        /// Provides input for controllers. XYZ is steer, float and drive respectively.
        /// </summary>
        public abstract Vector3 Input { get; }
    }
}
