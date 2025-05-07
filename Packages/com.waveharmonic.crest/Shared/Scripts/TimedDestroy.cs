// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest.Examples
{
    /// <summary>
    /// Simple utility script to destroy the gameobject after a set time.
    /// </summary>
#if !CREST_DEBUG
    [AddComponentMenu("")]
#endif
    sealed class TimedDestroy : MonoBehaviour
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        [SerializeField]
        float _LifeTime = 2.0f;

        // this seems to make motion stutter?
        // [SerializeField]
        // float _ScaleToOneDuration = 0.1f;

        [SerializeField]
        float _ScaleToZeroDuration = 0.0f;

        Vector3 _Scale;
        float _BirthTime;

        void Start()
        {
            _BirthTime = Time.time;
            _Scale = transform.localScale;
        }

        void Update()
        {
            var age = Time.time - _BirthTime;

            if (age >= _LifeTime)
            {
                Helpers.Destroy(gameObject);
            }
            else if (age > _LifeTime - _ScaleToZeroDuration)
            {
                transform.localScale = _Scale * (1.0f - (age - (_LifeTime - _ScaleToZeroDuration)) / _ScaleToZeroDuration);
            }
            /*else if (age < _ScaleToOneDuration && _ScaleToOneDuration > 0.0f)
            {
                transform.localScale = _Scale * age / _ScaleToOneDuration;
            }*/
            else
            {
                transform.localScale = _Scale;
            }
        }
    }
}
