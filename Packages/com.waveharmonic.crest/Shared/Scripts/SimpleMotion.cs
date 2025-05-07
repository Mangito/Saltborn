// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest.Examples
{
    /// <summary>
    /// Moves this transform.
    /// </summary>
#if !CREST_DEBUG
    [AddComponentMenu("")]
#endif
    sealed class SimpleMotion : MonoBehaviour
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        [SerializeField]
        bool _ResetOnDisable;

        [SerializeField]
        bool _IsLocal;

        [Header("Translation")]
        [SerializeField]
        Vector3 _Velocity;

        [Header("Rotation")]
        [SerializeField]
        Vector3 _AngularVelocity;

        Vector3 _OldPosition;
        Quaternion _OldRotation;

        void OnEnable()
        {
            _OldPosition = transform.position;
            _OldRotation = transform.rotation;
        }

        void OnDisable()
        {
            if (_ResetOnDisable)
            {
                transform.SetPositionAndRotation(_OldPosition, _OldRotation);
            }
        }

        void Update()
        {
            // Translation
            {
                transform.position += (_IsLocal ? transform.TransformDirection(_Velocity) : _Velocity) * Time.deltaTime;
            }

            // Rotation
            {
                transform.rotation *= Quaternion.Euler(_AngularVelocity * Time.deltaTime);
            }
        }
    }
}
