// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#if d_UnityURP

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WaveHarmonic.Crest
{
    sealed partial class SampleShadowsURP : ScriptableRenderPass
    {
        const string k_Name = "Crest Shadow Data";

        static SampleShadowsURP s_Instance;
        internal static bool Created => s_Instance != null;

        SampleShadowsURP(RenderPassEvent renderPassEvent)
        {
            this.renderPassEvent = renderPassEvent;
        }

        internal static void Enable()
        {
            s_Instance ??= new(RenderPassEvent.AfterRenderingSkybox);

            RenderPipelineManager.beginCameraRendering -= EnqueuePass;
            RenderPipelineManager.beginCameraRendering += EnqueuePass;

            RenderPipelineManager.activeRenderPipelineTypeChanged -= s_Instance.OnActiveRenderPipelineTypeChanged;
            RenderPipelineManager.activeRenderPipelineTypeChanged += s_Instance.OnActiveRenderPipelineTypeChanged;
        }

        internal static void Disable()
        {
            // TODO: Currently on RP change this method can be called with Enable ever being called leading to null
            // exceptions. It can be removed once those problems are sorted.
            if (s_Instance != null)
            {
                RenderPipelineManager.activeRenderPipelineTypeChanged -= s_Instance.OnActiveRenderPipelineTypeChanged;
            }

            RenderPipelineManager.beginCameraRendering -= EnqueuePass;
        }

        void OnActiveRenderPipelineTypeChanged()
        {
            Disable();
        }

        static void EnqueuePass(ScriptableRenderContext context, Camera camera)
        {
            var water = WaterRenderer.Instance;

            if (water == null || !water._ShadowLod.Enabled)
            {
                return;
            }

#if UNITY_EDITOR
            if (!WaterRenderer.IsWithinEditorUpdate || EditorApplication.isPaused)
            {
                return;
            }
#endif

            // Only sample shadows for the main camera.
            if (!ReferenceEquals(water.Viewer, camera))
            {
                return;
            }

            if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var cameraData))
            {
                cameraData.scriptableRenderer.EnqueuePass(s_Instance);
            }
        }

#if UNITY_6000_0_OR_NEWER
        void Execute(ScriptableRenderContext context, CommandBuffer buffer, PassData renderingData)
#else
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
#endif
        {
            var water = WaterRenderer.Instance;

            if (water == null || !water._ShadowLod.Enabled)
            {
                return;
            }

            // TODO: This may not be the same as WaterRenderer._primaryLight. Not certain how to support overriding the
            // main light for shadows yet.
            var mainLightIndex = renderingData.lightData.mainLightIndex;

            if (mainLightIndex == -1)
            {
                return;
            }

            var camera = renderingData.cameraData.camera;

#if !UNITY_6000_0_OR_NEWER
            var buffer = CommandBufferPool.Get(k_Name);
#endif

            // We need to check the mask or it will cause entire pipeline to output black. Appears to only affect URP.
            var isStereoRendering = renderingData.cameraData.xrRendering && XRHelpers.IsSinglePass &&
                camera.stereoTargetEye == StereoTargetEyeMask.Both;

            // Disable for XR SPI otherwise input will not have correct world position.
            if (isStereoRendering)
            {
                buffer.DisableShaderKeyword("STEREO_INSTANCING_ON");
            }

            water._ShadowLod.BuildCommandBuffer(water, buffer);

            // Restore matrices otherwise remaining render will have incorrect matrices. Each pass is responsible for
            // restoring matrices if required.
            buffer.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);

            // Restore XR SPI as we cannot rely on remaining pipeline to do it for us.
            if (isStereoRendering)
            {
                buffer.EnableShaderKeyword("STEREO_INSTANCING_ON");
            }

#if !UNITY_6000_0_OR_NEWER
            context.ExecuteCommandBuffer(buffer);
            CommandBufferPool.Release(buffer);
#endif
        }
    }
}

#endif // d_UnityURP
