// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;

namespace WaveHarmonic.Crest.Editor
{
#if !CREST_DEBUG
    [AddComponentMenu("")]
#endif
    [ExecuteAlways]
    sealed class AmbientLightPatcher : MonoBehaviour
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

#if UNITY_EDITOR
        void OnEnable() => InitializeAmbientLighting();
        void Update() => InitializeAmbientLighting();

        bool _Baked;

        void InitializeAmbientLighting()
        {
            if (_Baked)
            {
                return;
            }

            if (UnityEditor.Lightmapping.isRunning)
            {
                return;
            }

            if (Application.isPlaying)
            {
                return;
            }

            // Throws a warning.
            if (UnityEditor.ShaderUtil.anythingCompiling)
            {
                return;
            }

            // Only do skyboxes for now.
            if (RenderSettings.ambientMode != AmbientMode.Skybox)
            {
                return;
            }

            // NOTE: Cannot use as API introduced in 6000.0.22f1 which cannot be targeted by defines.
            // if (UnityEditor.Lightmapping.bakeOnSceneLoad == UnityEditor.Lightmapping.BakeOnSceneLoadMode.IfMissingLightingData)
            // {
            //     return;
            // }

            var bake = true;
            var probe = RenderSettings.ambientProbe;

            // Check if the ambient probe is effectively empty.
            for (var i = 0; i < 9; i++)
            {
                if (probe[0, i] != 0 || probe[1, i] != 0 || probe[2, i] != 0)
                {
                    bake = false;
                    break;
                }
            }

            if (bake)
            {
#if !UNITY_6000_0_OR_NEWER
                var oldWorkflow = UnityEditor.Lightmapping.giWorkflowMode;
                UnityEditor.Lightmapping.giWorkflowMode = UnityEditor.Lightmapping.GIWorkflowMode.OnDemand;
#endif
                // Only attempt to bake once per scene load.
                _Baked = UnityEditor.Lightmapping.BakeAsync();
#if !UNITY_6000_0_OR_NEWER
                UnityEditor.Lightmapping.giWorkflowMode = oldWorkflow;
#endif

                if (!_Baked)
                {
                    Debug.LogWarning($"Crest: Could not generate scene lighting. Lighting will look incorrect.");
                }
            }
        }
#endif
    }
}
