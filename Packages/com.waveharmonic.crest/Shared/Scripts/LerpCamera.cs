// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest.Examples
{
#if !CREST_DEBUG
    [AddComponentMenu("")]
#endif
    sealed class LerpCamera : ManagedBehaviour<WaterRenderer>
    {
#pragma warning disable IDE0032 // Use auto property

        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        [SerializeField]
        float _LerpAlpha = 0.1f;

        [SerializeField]
        Transform _Target = null;

        [SerializeField]
        Transform _LookAt = null;

        [SerializeField]
        float _LookAtOffset = 5f;

        [SerializeField]
        float _MinimumHeightAboveWater = 0.5f;

#pragma warning restore IDE0032 // Use auto property

        public Transform Target { get => _Target; set => _Target = value; }
        public Transform LookAt { get => _LookAt; set => _LookAt = value; }

        readonly SampleCollisionHelper _SampleHeightHelper = new();

        private protected override System.Action<WaterRenderer> OnUpdateMethod => OnUpdate;
        void OnUpdate(WaterRenderer water)
        {
            if (_Target == null)
            {
                return;
            }

            _SampleHeightHelper.SampleHeight(transform.position, out var h);

            var targetPos = _Target.position;
            targetPos.y = Mathf.Max(targetPos.y, h + _MinimumHeightAboveWater);

            transform.position = Vector3.Lerp(transform.position, targetPos, _LerpAlpha * water.DeltaTime * 60f);
            transform.LookAt(_LookAt.position + _LookAtOffset * Vector3.up);
        }
    }
}
