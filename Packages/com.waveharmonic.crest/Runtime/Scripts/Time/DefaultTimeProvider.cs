// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Default time provider - sets the water time to Unity's game time.
    /// </summary>
    sealed class DefaultTimeProvider : ITimeProvider
    {
        public float Time
        {
            get
            {
#if UNITY_EDITOR
                if (UnityEngine.Application.isPlaying)
                {
                    return UnityEngine.Time.time;
                }
                else
                {
                    return WaterRenderer.LastUpdateEditorTime;
                }
#else
                return UnityEngine.Time.time;
#endif
            }
        }

        public float Delta
        {
            get
            {
#if UNITY_EDITOR
                if (UnityEngine.Application.isPlaying)
                {
                    return UnityEngine.Time.deltaTime;
                }
                else
                {
                    return 1f / 20f;
                }
#else
                return UnityEngine.Time.deltaTime;
#endif
                ;
            }

        }
    }
}
