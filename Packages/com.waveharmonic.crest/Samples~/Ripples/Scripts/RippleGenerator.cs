// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest.Examples
{
    [RequireComponent(typeof(DynamicWavesLodInput))]
    [@ExecuteDuringEditMode]
    [AddComponentMenu(Constants.k_MenuPrefixSample + "Ripple Generator")]
    sealed class RippleGenerator : ManagedBehaviour<WaterRenderer>
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        [SerializeField]
        float _WarmUp = 3f;

        [SerializeField]
        float _OnTime = 0.2f;

        [SerializeField]
        float _Period = 4f;

        DynamicWavesLodInput _DynamicWavesLodInput;

        private protected override void Initialize()
        {
            base.Initialize();
            if (_DynamicWavesLodInput == null) _DynamicWavesLodInput = GetComponent<DynamicWavesLodInput>();
            _DynamicWavesLodInput.ForceRenderingOff = true;
        }

        private protected override System.Action<WaterRenderer> OnUpdateMethod => OnUpdate;
        void OnUpdate(WaterRenderer water)
        {
            if (!water.DynamicWavesLod.Enabled || _DynamicWavesLodInput == null)
            {
                return;
            }

            var time = water.CurrentTime;

            if (time < _WarmUp)
            {
                _DynamicWavesLodInput.ForceRenderingOff = true;
                return;
            }

            time -= _WarmUp;
            time = Mathf.Repeat(time, _Period);
            _DynamicWavesLodInput.ForceRenderingOff = time >= _OnTime;
        }
    }
}
