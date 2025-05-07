// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WaveHarmonic.Crest.Editor
{
#if !CREST_DEBUG
    [AddComponentMenu("")]
#endif
    sealed class RevertPrefabOnRenderPipelineChange : RenderPipelinePatcher
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

#if UNITY_EDITOR
        private protected override void Awake()
        {
            base.Awake();
            if (!Application.isPlaying) OnActiveRenderPipelineTypeChanged();
        }

        protected override void OnActiveRenderPipelineTypeChanged()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                return;
            }

            if (!isActiveAndEnabled)
            {
                return;
            }

            foreach (var item in gameObject.GetComponents<Component>())
            {
                if (item is Transform) continue;
                if (item == null) continue; // Can happen if missing packages/scripts.
                if (!PrefabUtility.IsPartOfPrefabInstance(item)) continue;
                PrefabUtility.RevertObjectOverride(item, InteractionMode.AutomatedAction);
            }
        }
#endif
    }
}
