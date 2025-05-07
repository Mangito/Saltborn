// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using MonoBehaviour = WaveHarmonic.Crest.Internal.EditorBehaviour;
#endif

namespace WaveHarmonic.Crest.Editor
{
    [ExecuteAlways]
    abstract class RenderPipelinePatcher : MonoBehaviour
    {
#if UNITY_EDITOR
        protected virtual void OnEnable()
        {
            RenderPipelineManager.activeRenderPipelineTypeChanged -= OnActiveRenderPipelineTypeChanged;
            RenderPipelineManager.activeRenderPipelineTypeChanged += OnActiveRenderPipelineTypeChanged;
        }

        protected virtual void OnDisable()
        {
            RenderPipelineManager.activeRenderPipelineTypeChanged -= OnActiveRenderPipelineTypeChanged;
        }

        protected abstract void OnActiveRenderPipelineTypeChanged();
#endif
    }
}
