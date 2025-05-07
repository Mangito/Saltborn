// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#if d_UnityHDRP

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.RendererUtils;

namespace WaveHarmonic.Crest
{
    sealed class UnderwaterEffectPassHDRP : CustomPass
    {
        const string k_Name = "Underwater Effect";

        static UnderwaterRenderer s_Renderer;
        static UnderwaterEffectPass s_UnderwaterEffectPass;
        static UnderwaterEffectPassHDRP s_Instance;
        static CopyDepthBufferPassHDRP s_CopyDepthBufferPassHDRP;

        static ShaderTagId[] s_ForwardShaderTags;

        GameObject _GameObject;

        public static void Enable(UnderwaterRenderer renderer)
        {
            var gameObject = CustomPassHelpers.CreateOrUpdate
            (
                parent: renderer._Water.Container.transform,
                k_Name,
                hide: !renderer._Water._Debug._ShowHiddenObjects
            );

            CustomPassHelpers.CreateOrUpdate
            (
                gameObject,
                ref s_CopyDepthBufferPassHDRP,
                "Copy Depth Buffer",
                CustomPassInjectionPoint.AfterOpaqueDepthAndNormal
            );

            CustomPassHelpers.CreateOrUpdate
            (
                gameObject,
                ref s_Instance,
                k_Name,
                CustomPassInjectionPoint.BeforePostProcess
            );

            s_Instance._GameObject = gameObject;

            s_Renderer = renderer;
            s_UnderwaterEffectPass = new(renderer);

            RenderPipelineManager.beginCameraRendering -= s_Instance.OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += s_Instance.OnBeginCameraRendering;
        }

        public static void Disable()
        {
            // It should be safe to rely on this reference for this reference to fail.
            if (s_Instance != null && s_Instance._GameObject != null)
            {
                // Will also trigger Cleanup below.
                s_Instance._GameObject.SetActive(false);
            }
        }

        void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            s_CopyDepthBufferPassHDRP.enabled = s_Renderer.UseStencilBuffer;
        }

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            var asset = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;

            // Developers have a choice with the color buffer format. There is also a custom buffer buffer format but
            // that is not relevant here. This will not cover the format change when scene filtering as Setup/Cleanup is
            // not executed for this change.
            s_UnderwaterEffectPass.Allocate((GraphicsFormat)asset.currentPlatformRenderPipelineSettings.colorBufferFormat);

            // Taken from:
            // https://github.com/Unity-Technologies/Graphics/blob/778ddac6207ade1689999b95380cd835b0669f2d/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/DrawRenderersCustomPass.cs#L136-L142
            s_ForwardShaderTags ??= new[]
            {
                HDShaderPassNames.s_ForwardName,            // HD Lit shader
                HDShaderPassNames.s_ForwardOnlyName,        // HD Unlit shader
                HDShaderPassNames.s_SRPDefaultUnlitName,    // Cross SRP Unlit shader
            };
        }

        protected override void Cleanup()
        {
            RenderPipelineManager.beginCameraRendering -= s_Instance.OnBeginCameraRendering;

            s_UnderwaterEffectPass?.Release();
        }

        protected override void Execute(CustomPassContext context)
        {
            var camera = context.hdCamera.camera;

            if (!s_Renderer.ShouldRender(camera, UnderwaterRenderer.Pass.Effect))
            {
                return;
            }

            // Create a separate stencil buffer context by using a depth buffer copy if needed.
            var depthBuffer = s_Renderer.UseStencilBuffer
                ? s_CopyDepthBufferPassHDRP._DepthBufferCopy
                : context.cameraDepthBuffer;

            s_UnderwaterEffectPass.Execute(camera, context.cmd, context.cameraColorBuffer, depthBuffer, context.propertyBlock);

            // Renders transparent objects after the underwater effect. Using the correct
            // shader, the above water portion of the object is rendered normally (in the
            // transparent pass), and the below water portion is rendered here with underwater
            // applied.
            // See the following for reference:
            // https://github.com/Unity-Technologies/Graphics/blob/master/Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipeline/RenderPass/DrawRenderersCustomPass.cs
            if (s_Renderer.EnableShaderAPI)
            {
                var renderConfig = HDUtils.GetRendererConfiguration
                (
#if UNITY_6000_0_OR_NEWER
                    context.hdCamera.frameSettings.IsEnabled(FrameSettingsField.AdaptiveProbeVolume),
#else
                    context.hdCamera.frameSettings.IsEnabled(FrameSettingsField.ProbeVolume),
#endif
                    context.hdCamera.frameSettings.IsEnabled(FrameSettingsField.Shadowmask)
                );

                var result = new RendererListDesc(s_ForwardShaderTags, context.cullingResults, context.hdCamera.camera)
                {
                    rendererConfiguration = renderConfig,
                    renderQueueRange = GetRenderQueueRange(RenderQueueType.AllTransparent),
                    sortingCriteria = SortingCriteria.CommonTransparent,
                    excludeObjectMotionVectors = false,
                    layerMask = s_Renderer._TransparentObjectLayers,
                };

                context.cmd.EnableShaderKeyword(UnderwaterRenderer.k_KeywordUnderwaterObjects);
                CoreUtils.DrawRendererList(context.renderContext, context.cmd, context.renderContext.CreateRendererList(result));
                context.cmd.DisableShaderKeyword(UnderwaterRenderer.k_KeywordUnderwaterObjects);
            }
        }
    }

    sealed class CopyDepthBufferPassHDRP : CustomPass
    {
        public RTHandle _DepthBufferCopy;

        protected override void Execute(CustomPassContext context)
        {
            // Multiple cameras could have different settings.
            RenderPipelineCompatibilityHelper.ReAllocateIfNeeded
            (
                ref _DepthBufferCopy,
                context.cameraDepthBuffer.rt.descriptor,
                FilterMode.Point,
                name: "_Crest_UnderwaterCopiedDepthBuffer"
            );

            var buffer = context.cmd;

            buffer.SetRenderTarget(BuiltinRenderTextureType.None, _DepthBufferCopy);
            buffer.ClearRenderTarget(RTClearFlags.Depth, Color.black, 1, 0);

            buffer.CopyTexture(context.cameraDepthBuffer.rt, _DepthBufferCopy.rt);

            // Clear the stencil component just in case.
            buffer.ClearRenderTarget(RTClearFlags.Stencil, Color.black, 1, 0);
        }

        protected override void Cleanup()
        {
            _DepthBufferCopy?.Release();
            _DepthBufferCopy = null;
        }
    }
}

#endif // d_UnityHDRP
