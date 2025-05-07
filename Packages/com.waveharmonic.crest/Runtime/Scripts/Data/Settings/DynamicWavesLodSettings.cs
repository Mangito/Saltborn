// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Settings for fine tuning the <see cref="DynamicWavesLod"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "DynamicWavesSettings", menuName = "Crest/Simulation Settings/Dynamic Waves")]
    [@HelpURL("Manual/Waves.html#dynamic-waves-settings")]
    public sealed partial class DynamicWavesLodSettings : LodSettings
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        [Header("Simulation")]

        [Tooltip("How much energy is dissipated each frame.\n\nHelps simulation stability, but limits how far ripples will propagate. Set this as large as possible/acceptable. Default is 0.05.")]
        [@Range(0f, 1f)]
        [@GenerateAPI]
        [SerializeField]
        internal float _Damping = 0.05f;

        [Tooltip("Stability control.\n\nLower values means more stable simulation, but may slow down some dynamic waves. This value should be set as large as possible until simulation instabilities/flickering begin to appear. Default is 0.7.")]
        [@Range(0.1f, 1f)]
        [@GenerateAPI]
        [SerializeField]
        internal float _CourantNumber = 0.7f;


        [Header("Displacement Generation")]

        [Tooltip("Induce horizontal displacements to sharpen simulated waves.")]
        [@Range(0f, 20f)]
        [@GenerateAPI]
        [SerializeField]
        internal float _HorizontalDisplace = 3f;

        [Tooltip("Clamp displacement to help prevent self-intersection in steep waves.\n\nZero means unclamped.")]
        [@Range(0f, 1f)]
        [@GenerateAPI]
        [SerializeField]
        internal float _DisplaceClamp = 0.3f;


        [Tooltip("Multiplier for gravity.\n\nMore gravity means dynamic waves will travel faster. Higher values can be a source of instability.")]
        [@Range(0f, 64f)]
        [@GenerateAPI]
        [SerializeField]
        internal float _GravityMultiplier = 1f;
    }
}
