// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Playables;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// This time provider feeds a Timeline time to the water system, using a Playable Director.
    /// </summary>
    [AddComponentMenu(Constants.k_MenuPrefixTime + "Cutscene Time Provider")]
    [@HelpURL("Manual/TimeProviders.html#timelines-and-cutscenes")]
    public sealed partial class CutsceneTimeProvider : TimeProvider
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

#if d_ModuleUnityDirector
        [Tooltip("Playable Director to take time from.")]
        [@GenerateAPI(symbol: "d_ModuleUnityDirector")]
        [SerializeField]
        internal PlayableDirector _PlayableDirector;
#endif

        [Tooltip("Time offset which will be added to the Timeline time.")]
        [@GenerateAPI]
        [SerializeField]
        float _TimeOffset = 0f;

        [Tooltip("Assign this time provider to the water system when this component becomes active.")]
        [@GenerateAPI]
        [SerializeField]
        bool _AssignToWaterComponentOnEnable = true;

        [Tooltip("Restore the time provider that was previously assigned to water system when this component disables.")]
        [@GenerateAPI]
        [SerializeField]
        bool _RestorePreviousTimeProviderOnDisable = true;

        readonly DefaultTimeProvider _FallbackTimeProvider = new();
        bool _Attached = false;

        private protected override void OnDisable()
        {
            base.OnDisable();

            var water = WaterRenderer.Instance;
            if (_RestorePreviousTimeProviderOnDisable && _Attached && water != null)
            {
                water.TimeProviders.Pop(this);
            }

            _Attached = false;
        }

        private protected override System.Action<WaterRenderer> OnEnableMethod => Attach;
        void Attach(WaterRenderer water)
        {
            if (_Attached) return;

#if d_ModuleUnityDirector
            if (_PlayableDirector == null) return;
#endif

            if (_AssignToWaterComponentOnEnable && water)
            {
                water.TimeProviders.Push(this);
            }

            _Attached = true;
        }

        /// <remarks>
        /// If there is a PlayableDirector which is playing, return its time, otherwise use
        /// the <see cref="TimeProvider"/> being used before this component initialised,
        /// else fallback to a default <see cref="TimeProvider"/>.
        /// </remarks>
        /// <inheritdoc/>
        public override float Time
        {
            get
            {
#if d_ModuleUnityDirector
                if (_PlayableDirector != null
                    && _PlayableDirector.isActiveAndEnabled
                    && (!Application.isPlaying || _PlayableDirector.state == PlayState.Playing))
                {
                    return (float)_PlayableDirector.time + _TimeOffset;
                }
#endif

                // Use a fallback TP
                return _FallbackTimeProvider.Time;
            }
        }

        /// <inheritdoc/>
        public override float Delta => UnityEngine.Time.deltaTime;

#if UNITY_EDITOR
        [@OnChange]
        void OnChange(string propertyPath, object previousValue)
        {
            if (!isActiveAndEnabled) return;
            // Try to attach on change.
            Attach(WaterRenderer.Instance);
        }
#endif
    }
}
