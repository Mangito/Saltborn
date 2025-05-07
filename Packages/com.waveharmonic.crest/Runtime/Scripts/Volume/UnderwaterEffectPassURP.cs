// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#if d_UnityURP

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WaveHarmonic.Crest
{
    sealed partial class UnderwaterEffectPassURP : ScriptableRenderPass
    {
        const string k_Name = "Crest Underwater Effect";

        UnderwaterRenderer _Renderer;

        static UnderwaterEffectPassURP s_Instance;
        RenderObjectsWithoutFogPass _ApplyFogToTransparentObjects;
        UnderwaterEffectPass _UnderwaterEffectPass;
        CopyDepthBufferPassURP _CopyDepthBufferPass;

        RTHandle _ColorBuffer;
        RTHandle _DepthBuffer;

        public UnderwaterEffectPassURP()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
        }

        public static void Enable(UnderwaterRenderer renderer)
        {
            if (s_Instance == null)
            {
                s_Instance = new();
                s_Instance._Renderer = renderer;
                s_Instance._CopyDepthBufferPass = new(RenderPassEvent.AfterRenderingOpaques);
                s_Instance._ApplyFogToTransparentObjects = new();
            }

            RenderPipelineManager.beginCameraRendering -= s_Instance.EnqueuePass;
            RenderPipelineManager.beginCameraRendering += s_Instance.EnqueuePass;
            RenderPipelineManager.activeRenderPipelineTypeChanged -= Disable;
            RenderPipelineManager.activeRenderPipelineTypeChanged += Disable;
        }

        public static void Disable()
        {
            if (s_Instance != null) RenderPipelineManager.beginCameraRendering -= s_Instance.EnqueuePass;
            RenderPipelineManager.activeRenderPipelineTypeChanged -= Disable;

            s_Instance?._UnderwaterEffectPass?.Release();
            s_Instance?._CopyDepthBufferPass?.Release();
            s_Instance = null;
        }

        void EnqueuePass(ScriptableRenderContext context, Camera camera)
        {
            if (!_Renderer.ShouldRender(camera, UnderwaterRenderer.Pass.Effect))
            {
                return;
            }

            var renderer = camera.GetUniversalAdditionalCameraData().scriptableRenderer;

#if UNITY_EDITOR
            if (renderer == null) return;
#endif

            // Copy the depth buffer to create a new depth/stencil context.
            if (_Renderer.UseStencilBuffer)
            {
                renderer.EnqueuePass(_CopyDepthBufferPass);
            }

            // Set up internal pass which houses shared code for SRPs.
            _UnderwaterEffectPass ??= new(_Renderer);

            renderer.EnqueuePass(s_Instance);

            if (_Renderer.EnableShaderAPI)
            {
                renderer.EnqueuePass(_ApplyFogToTransparentObjects);
            }
        }

#if UNITY_6000_0_OR_NEWER
        void OnSetup(CommandBuffer buffer, PassData data)
        {
            _ColorBuffer = data.colorTargetHandle.Texture;
            _DepthBuffer = data.depthTargetHandle.Texture;

            // TODO: renderingData.cameraData.cameraTargetDescriptor?
            _UnderwaterEffectPass.ReAllocate(_ColorBuffer.rt.descriptor);
        }

        void Execute(ScriptableRenderContext context, CommandBuffer buffer, PassData data)
        {
            if (_Renderer.UseStencilBuffer)
            {
                _DepthBuffer = _CopyDepthBufferPass._DepthBufferCopy;
            }

            _UnderwaterEffectPass.Execute(data.cameraData.camera, buffer, _ColorBuffer, _DepthBuffer);
        }
#else
        public override void OnCameraSetup(CommandBuffer buffer, ref RenderingData data)
        {
            _ColorBuffer = data.cameraData.renderer.cameraColorTargetHandle;
            _DepthBuffer = data.cameraData.renderer.cameraDepthTargetHandle;

            // TODO: renderingData.cameraData.cameraTargetDescriptor?
            _UnderwaterEffectPass.ReAllocate(_ColorBuffer.rt.descriptor);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            var buffer = CommandBufferPool.Get(k_Name);

            if (_Renderer.UseStencilBuffer)
            {
                _DepthBuffer = _CopyDepthBufferPass._DepthBufferCopy;
            }

            _UnderwaterEffectPass.Execute(data.cameraData.camera, buffer, _ColorBuffer, _DepthBuffer);

            context.ExecuteCommandBuffer(buffer);
            CommandBufferPool.Release(buffer);
        }
#endif

        // Renders transparent objects after the underwater effect. Using the correct
        // shader, the above water portion of the object is rendered normally (in the
        // transparent pass), and the below water portion is rendered here with underwater
        // applied.
        sealed partial class RenderObjectsWithoutFogPass : ScriptableRenderPass
        {
            FilteringSettings _FilteringSettings;

            static readonly List<ShaderTagId> s_ShaderTagIdList = new()
            {
                new("SRPDefaultUnlit"),
                new("UniversalForward"),
                new("UniversalForwardOnly"),
                new("LightweightForward"),
            };

            public RenderObjectsWithoutFogPass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                _FilteringSettings = new(RenderQueueRange.transparent, 0);
            }

#if UNITY_6000_0_OR_NEWER
            void Execute(ScriptableRenderContext context, CommandBuffer buffer, PassData renderingData)
#else
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
#endif
            {
                _FilteringSettings.layerMask = s_Instance._Renderer._TransparentObjectLayers;

#if !UNITY_6000_0_OR_NEWER
                var buffer = CommandBufferPool.Get("Crest Underwater Objects");
#endif

                // Disable Unity's fog keywords as there is no option to ignore fog for the Shader Graph.
                if (RenderSettings.fog)
                {
                    switch (RenderSettings.fogMode)
                    {
                        case FogMode.Exponential:
                            buffer.DisableShaderKeyword("FOG_EXP");
                            break;
                        case FogMode.Linear:
                            buffer.DisableShaderKeyword("FOG_LINEAR");
                            break;
                        case FogMode.ExponentialSquared:
                            buffer.DisableShaderKeyword("FOG_EXP2");
                            break;
                    }
                }

                buffer.EnableShaderKeyword(UnderwaterRenderer.k_KeywordUnderwaterObjects);
                // If we want anything to apply to DrawRenderers, it has to be executed before:
                // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.DrawRenderers.html
                context.ExecuteCommandBuffer(buffer);
                buffer.Clear();

#if UNITY_6000_0_OR_NEWER
                var drawingSettings = RenderingUtils.CreateDrawingSettings
                (
                    s_ShaderTagIdList,
                    renderingData.renderingData,
                    renderingData.cameraData,
                    renderingData.lightData,
                    SortingCriteria.CommonTransparent
                );

                var parameters = new RendererListParams(renderingData.cullResults, drawingSettings, _FilteringSettings);
                var list = context.CreateRendererList(ref parameters);

                buffer.DrawRendererList(list);
#else
                var drawingSettings = CreateDrawingSettings
                (
                    s_ShaderTagIdList,
                    ref renderingData,
                    SortingCriteria.CommonTransparent
                );

                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref _FilteringSettings);
#endif

                // Revert fog keywords.
                if (RenderSettings.fog)
                {
                    switch (RenderSettings.fogMode)
                    {
                        case FogMode.Exponential:
                            buffer.EnableShaderKeyword("FOG_EXP");
                            break;
                        case FogMode.Linear:
                            buffer.EnableShaderKeyword("FOG_LINEAR");
                            break;
                        case FogMode.ExponentialSquared:
                            buffer.EnableShaderKeyword("FOG_EXP2");
                            break;
                    }
                }

                buffer.DisableShaderKeyword(UnderwaterRenderer.k_KeywordUnderwaterObjects);

#if !UNITY_6000_0_OR_NEWER
                context.ExecuteCommandBuffer(buffer);
                CommandBufferPool.Release(buffer);
#endif
            }
        }
    }

    // Copies the depth buffer to avoid conflicts when using the stencil buffer.
    sealed partial class CopyDepthBufferPassURP : ScriptableRenderPass
    {
        const string k_Name = "Crest Copy Depth Buffer";
        RTHandle _DepthBuffer;
        public RTHandle _DepthBufferCopy;

        public CopyDepthBufferPassURP(RenderPassEvent @event)
        {
            renderPassEvent = @event;
        }

#if UNITY_6000_0_OR_NEWER
        void OnSetup(CommandBuffer buffer, PassData data)
#else
        public override void OnCameraSetup(CommandBuffer buffer, ref RenderingData data)
#endif
        {
            var descriptor = data.cameraData.cameraTargetDescriptor;
            descriptor.graphicsFormat = GraphicsFormat.None;
            descriptor.bindMS = descriptor.msaaSamples > 1;
#if UNITY_6000_0_OR_NEWER
            RenderingUtils.ReAllocateHandleIfNeeded(ref _DepthBufferCopy, descriptor, FilterMode.Point, name: "Crest Copied Depth Buffer");
            _DepthBuffer = data.depthTargetHandle;
#else
            RenderingUtils.ReAllocateIfNeeded(ref _DepthBufferCopy, descriptor, FilterMode.Point, name: "Crest Copied Depth Buffer");
            _DepthBuffer = data.cameraData.renderer.cameraDepthTargetHandle;
#endif
        }

#if UNITY_6000_0_OR_NEWER
        void Execute(ScriptableRenderContext context, CommandBuffer buffer, PassData data)
#else
        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
#endif
        {
#if !UNITY_6000_0_OR_NEWER
            var buffer = CommandBufferPool.Get(k_Name);
#endif

            // Must clear even though we are overwriting or there will be strange artifacts on new writes.
            // This could be a Unity bug and may be worth reporting.
            buffer.SetRenderTarget(BuiltinRenderTextureType.None, _DepthBufferCopy);
            buffer.ClearRenderTarget(RTClearFlags.Depth, Color.black, 1, 0);

            buffer.CopyTexture(_DepthBuffer.rt, _DepthBufferCopy.rt);

            // Clear the stencil component just in case.
            buffer.ClearRenderTarget(RTClearFlags.Stencil, Color.black, 1, 0);

#if !UNITY_6000_0_OR_NEWER
            context.ExecuteCommandBuffer(buffer);
            CommandBufferPool.Release(buffer);
#endif
        }

        public void Release()
        {
            _DepthBuffer = null;
            _DepthBufferCopy?.Release();
        }
    }
}

#endif // d_UnityURP
