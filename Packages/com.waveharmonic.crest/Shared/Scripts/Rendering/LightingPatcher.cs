// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using MonoBehaviour = WaveHarmonic.Crest.Internal.EditorBehaviour;
#endif

namespace WaveHarmonic.Crest.Editor
{
#if !CREST_DEBUG
    [AddComponentMenu("")]
#endif
    [DefaultExecutionOrder(-1000)]
    [ExecuteAlways]
    sealed class LightingPatcher : MonoBehaviour
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

#if !CREST_DEBUG
        [HideInInspector]
#endif
        [@DecoratedField, SerializeField]
        bool _LightsUseLinearIntensity;

#if !CREST_DEBUG
        [HideInInspector]
#endif
        [@DecoratedField, SerializeField]
        bool _LightsUseColorTemperature;

        bool _CurrentLightsUseLinearIntensity;
        bool _CurrentLightsUseColorTemperature;

        void OnEnable()
        {
            // SRP is always linear with temperature.
            if (RenderPipelineHelper.IsLegacy)
            {
                Camera.onPreCull -= OnBeginRendering;
                Camera.onPreCull += OnBeginRendering;
                Camera.onPostRender -= OnEndRendering;
                Camera.onPostRender += OnEndRendering;
            }
            else
            {
                RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
                RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
                RenderPipelineManager.endContextRendering -= OnEndContextRendering;
                RenderPipelineManager.endContextRendering += OnEndContextRendering;
            }

            _CurrentLightsUseLinearIntensity = GraphicsSettings.lightsUseLinearIntensity;
            _CurrentLightsUseColorTemperature = GraphicsSettings.lightsUseColorTemperature;
        }

        void OnDisable()
        {
            if (RenderPipelineHelper.IsLegacy)
            {
                Camera.onPreCull -= OnBeginRendering;
                Camera.onPostRender -= OnEndRendering;
            }
            else
            {
                RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
                RenderPipelineManager.endContextRendering -= OnEndContextRendering;
            }
        }

        void OnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras) => ChangeLighting();
        void OnEndContextRendering(ScriptableRenderContext context, List<Camera> cameras) => RestoreLighting();

        void OnBeginRendering(Camera camera) => ChangeLighting();
        void OnEndRendering(Camera camera) => RestoreLighting();

        void ChangeLighting()
        {
            _CurrentLightsUseLinearIntensity = GraphicsSettings.lightsUseLinearIntensity;
            _CurrentLightsUseColorTemperature = GraphicsSettings.lightsUseColorTemperature;
            GraphicsSettings.lightsUseLinearIntensity = true;
            GraphicsSettings.lightsUseColorTemperature = true;
        }

        void RestoreLighting()
        {
            GraphicsSettings.lightsUseLinearIntensity = _CurrentLightsUseLinearIntensity;
            GraphicsSettings.lightsUseColorTemperature = _CurrentLightsUseColorTemperature;
        }

#if UNITY_EDITOR
        private protected override void Reset()
        {
            _LightsUseLinearIntensity = GraphicsSettings.lightsUseLinearIntensity;
            _LightsUseColorTemperature = GraphicsSettings.lightsUseColorTemperature;

            base.Reset();
        }

        [@OnChange]
        void OnChange(string propertyPath, object previousValue)
        {
            GraphicsSettings.lightsUseLinearIntensity = _LightsUseLinearIntensity;
            GraphicsSettings.lightsUseColorTemperature = _LightsUseColorTemperature;
        }
#endif
    }
}
