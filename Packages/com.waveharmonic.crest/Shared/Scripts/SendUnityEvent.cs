// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Events;

namespace WaveHarmonic.Crest.Examples
{
#if !CREST_DEBUG
    [AddComponentMenu("")]
#endif
    [ExecuteAlways]
    sealed class SendUnityEvent : MonoBehaviour
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        [SerializeField]
        float _ExecuteUpdateEvery;

        [SerializeField]
        float _StopExecutingUpdateAfter = Mathf.Infinity;

        [SerializeField]
        UnityEvent _OnEnable = new();

        [SerializeField]
        UnityEvent _OnDisable = new();

        [SerializeField]
        UnityEvent<float> _OnUpdate = new();

        [SerializeField]
        UnityEvent _OnLegacyRenderPipeline = new();

        [SerializeField]
        UnityEvent _OnHighDefinitionPipeline = new();

        [SerializeField]
        UnityEvent _OnUniversalRenderPipeline = new();

        float _TimeSinceEnabled;
        float _LastUpdateTime;

        void OnEnable()
        {
            _TimeSinceEnabled = 0f;
            _OnEnable.Invoke();

            if (RenderPipelineHelper.IsHighDefinition)
            {
                _OnHighDefinitionPipeline?.Invoke();
            }
            else if (RenderPipelineHelper.IsUniversal)
            {
                _OnUniversalRenderPipeline?.Invoke();
            }
            else
            {
                _OnLegacyRenderPipeline?.Invoke();
            }
        }

        void OnDisable()
        {
            _OnDisable.Invoke();
        }

        void Update()
        {
            _TimeSinceEnabled += Time.deltaTime;
            _LastUpdateTime += Time.deltaTime;

            if (_LastUpdateTime < _ExecuteUpdateEvery)
            {
                return;
            }

            _LastUpdateTime = 0;

            if (_TimeSinceEnabled > _StopExecutingUpdateAfter)
            {
                return;
            }

            _OnUpdate.Invoke(_TimeSinceEnabled);
        }
    }
}
