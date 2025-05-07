// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest.Examples
{
    /// <summary>
    /// Places the game object on the water surface by moving it vertically.
    /// </summary>
    [AddComponentMenu(Constants.k_MenuPrefixSample + "Sample Height Demo")]
    sealed class SampleHeightDemo : ManagedBehaviour<WaterRenderer>
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        [Tooltip(ICollisionProvider.k_LayerTooltip)]
        [SerializeField]
        CollisionLayer _Layer;

        readonly SampleCollisionHelper _SampleHeightHelper = new();

        private protected override System.Action<WaterRenderer> OnUpdateMethod => OnUpdate;
        void OnUpdate(WaterRenderer water)
        {
            // Assume a primitive like a sphere or box.
            var r = transform.lossyScale.magnitude;

            if (_SampleHeightHelper.SampleHeight(transform.position, out var height, minimumLength: 2f * r, _Layer))
            {
                var pos = transform.position;
                pos.y = height;
                transform.position = pos;
            }
        }
    }
}
