// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using MonoBehaviour = WaveHarmonic.Crest.Internal.EditorBehaviour;
#else
using MonoBehaviour = UnityEngine.MonoBehaviour;
#endif

namespace WaveHarmonic.Crest.Internal
{
    /// <summary>
    /// Implements logic to smooth out Unity's wrinkles.
    /// </summary>
    public abstract class CustomBehaviour : MonoBehaviour
    {
        bool _AfterStart;

#pragma warning disable 114
        private protected virtual void Awake()
        {
#if UNITY_EDITOR
            base.Awake();
#endif
        }

        /// <summary>
        /// Unity's Start method. Make sure to call base if overriden.
        /// </summary>
        protected void Start()
        {
            _AfterStart = true;

#if UNITY_EDITOR
            base.Start();
            if (!enabled) return;
#endif

            OnStart();
        }
#pragma warning restore 114

        /// <summary>
        /// Called in OnEnable only after Start has ran.
        /// </summary>
        private protected virtual void Initialize()
        {

        }

        /// <summary>
        /// Replaces Start. Only called in the editor if passes validation.
        /// </summary>
        private protected virtual void OnStart()
        {
            Initialize();
        }

        /// <summary>
        /// Unity's OnEnable method. Make sure to call base if overriden.
        /// </summary>
        private protected virtual void OnEnable()
        {
            if (!_AfterStart) return;
            Initialize();
        }

#if UNITY_EDITOR
        [InitializeOnEnterPlayMode]
        static void OnEnterPlayModeInEditor(EnterPlayModeOptions options)
        {
            foreach (var @object in FindObjectsByType<CustomBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                @object._AfterStart = false;
            }
        }
#endif
    }
}
