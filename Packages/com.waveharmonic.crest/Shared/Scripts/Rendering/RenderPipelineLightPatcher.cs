// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace WaveHarmonic.Crest.Editor
{
#if !CREST_DEBUG
    [AddComponentMenu("")]
#endif
    [RequireComponent(typeof(Light))]
    [DefaultExecutionOrder(10)]
    sealed class RenderPipelineLightPatcher : RenderPipelinePatcher
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

#if d_UnityHDRP
        // For 2023.3 onwards, HDAdditionalLightData.intensity is obsolete and returns Light.intensity.
        // It still serializes the old value so grab it via reflection.
        static readonly FieldInfo s_Intensity = typeof(HDAdditionalLightData)
            .GetField("m_Intensity", BindingFlags.Instance | BindingFlags.NonPublic);
#endif

#if UNITY_EDITOR
        private protected override void Awake()
        {
            base.Awake();
            if (!Application.isPlaying) OnActiveRenderPipelineTypeChanged();
        }

        protected override void OnActiveRenderPipelineTypeChanged()
        {
            EditorApplication.update -= OnActiveRenderPipelineTypeChanged;

            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                return;
            }

            // Can happen.
            if (this == null)
            {
                return;
            }

            if (!isActiveAndEnabled)
            {
                return;
            }

#if d_UnityHDRP
            if (RenderPipelineHelper.IsHighDefinition)
            {
                if (TryGetComponent<Light>(out var light) && TryGetComponent<HDAdditionalLightData>(out var data))
                {
                    var intensity = (float)s_Intensity.GetValue(data);

                    if (light.intensity == intensity) return;

                    // HDRP will not restore the correct intensity.
                    light.intensity = intensity;

                    // Execute next frame as revert prefab interferes despite executing afterwards.
                    EditorApplication.update -= OnActiveRenderPipelineTypeChanged;
                    EditorApplication.update += OnActiveRenderPipelineTypeChanged;
                }
            }
#endif
        }
#endif
    }
}
