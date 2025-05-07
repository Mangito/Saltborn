// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#if d_UnityURP

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WaveHarmonic.Crest
{
    sealed partial class UnderwaterMaskPassURP : ScriptableRenderPass
    {
        const string k_Name = "Crest Underwater Mask";
        static UnderwaterMaskPassURP s_Instance;
        UnderwaterRenderer _Renderer;
        UnderwaterMaskPass _UnderwaterMaskPass;

        public UnderwaterMaskPassURP()
        {
            // Will always execute and matrices will be ready.
            renderPassEvent = RenderPassEvent.BeforeRenderingPrePasses;
        }

        public static void Enable(UnderwaterRenderer renderer)
        {
            s_Instance ??= new();
            s_Instance._Renderer = renderer;

            RenderPipelineManager.beginCameraRendering -= s_Instance.EnqueuePass;
            RenderPipelineManager.beginCameraRendering += s_Instance.EnqueuePass;
            RenderPipelineManager.activeRenderPipelineTypeChanged -= Disable;
            RenderPipelineManager.activeRenderPipelineTypeChanged += Disable;
        }

        public static void Disable()
        {
            if (s_Instance != null) RenderPipelineManager.beginCameraRendering -= s_Instance.EnqueuePass;
            RenderPipelineManager.activeRenderPipelineTypeChanged -= Disable;

            s_Instance?._UnderwaterMaskPass?.Release();
            s_Instance = null;
        }

        void EnqueuePass(ScriptableRenderContext context, Camera camera)
        {
            if (!_Renderer.ShouldRender(camera, UnderwaterRenderer.Pass.Mask))
            {
                return;
            }

            var renderer = camera.GetUniversalAdditionalCameraData().scriptableRenderer;

#if UNITY_EDITOR
            if (renderer == null) return;
#endif

            if (_UnderwaterMaskPass == null)
            {
                _UnderwaterMaskPass = new(_Renderer);
                _UnderwaterMaskPass.Allocate();
            }

            // Enqueue the pass. This happens every frame.
            renderer.EnqueuePass(this);
        }

#if UNITY_6000_0_OR_NEWER
        class PassData
        {
            public UniversalCameraData _CameraData;
            public UnderwaterMaskPass _UnderwaterMaskPass;
        }

        public override void RecordRenderGraph(UnityEngine.Rendering.RenderGraphModule.RenderGraph graph, ContextContainer frame)
        {
            using (var builder = graph.AddUnsafePass<PassData>(k_Name, out var data))
            {
                builder.AllowPassCulling(false);

                data._CameraData = frame.Get<UniversalCameraData>();
                data._UnderwaterMaskPass = _UnderwaterMaskPass;

                builder.SetRenderFunc<PassData>((data, context) =>
                {
                    var buffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    data._UnderwaterMaskPass.ReAllocate(data._CameraData.cameraTargetDescriptor);
                    data._UnderwaterMaskPass.Execute(data._CameraData.camera, buffer);
                });
            }
        }

        [System.Obsolete]
#endif
        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            var buffer = CommandBufferPool.Get(k_Name);
            _UnderwaterMaskPass.ReAllocate(data.cameraData.cameraTargetDescriptor);
            _UnderwaterMaskPass.Execute(data.cameraData.camera, buffer);
            context.ExecuteCommandBuffer(buffer);
            CommandBufferPool.Release(buffer);
        }
    }
}

#endif // d_UnityURP
