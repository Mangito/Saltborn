// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#if d_UnityURP
#if UNITY_6000_0_OR_NEWER

using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace WaveHarmonic.Crest
{
    partial class UnderwaterEffectPassURP
    {
        class PassData
        {
#pragma warning disable IDE1006 // Naming Styles
            public UniversalCameraData cameraData;
            public RenderGraphHelper.Handle colorTargetHandle;
            public RenderGraphHelper.Handle depthTargetHandle;
#pragma warning restore IDE1006 // Naming Styles

            public void Init(ContextContainer frameData, IUnsafeRenderGraphBuilder builder = null)
            {
                var resources = frameData.Get<UniversalResourceData>();
                cameraData = frameData.Get<UniversalCameraData>();

                if (builder == null)
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    colorTargetHandle = cameraData.renderer.cameraColorTargetHandle;
                    depthTargetHandle = cameraData.renderer.cameraDepthTargetHandle;
#pragma warning restore CS0618 // Type or member is obsolete
                }
                else
                {
                    colorTargetHandle = resources.activeColorTexture;
                    depthTargetHandle = resources.activeDepthTexture;
                    builder.UseTexture(colorTargetHandle, AccessFlags.ReadWrite);
                    builder.UseTexture(depthTargetHandle, AccessFlags.ReadWrite);
                }
            }
        }

        readonly PassData _PassData = new();

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
        {
            using (var builder = graph.AddUnsafePass<PassData>(k_Name, out var data))
            {
                data.Init(frame, builder);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc<PassData>((data, context) =>
                {
                    var buffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    OnSetup(buffer, data);
                    Execute(context.GetRenderContext(), buffer, data);
                });
            }
        }

        [System.Obsolete]
        public override void OnCameraSetup(CommandBuffer buffer, ref RenderingData data)
        {
            _PassData.Init(data.GetFrameData());
        }

        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            _PassData.Init(data.GetFrameData());
            var buffer = CommandBufferPool.Get(k_Name);
            OnSetup(buffer, _PassData);
            Execute(context, buffer, _PassData);
            context.ExecuteCommandBuffer(buffer);
            CommandBufferPool.Release(buffer);
        }

        partial class RenderObjectsWithoutFogPass
        {
            class PassData
            {
#pragma warning disable IDE1006 // Naming Styles
                public UniversalCameraData cameraData;
                public UniversalLightData lightData;
                public UniversalRenderingData renderingData;
                public CullingResults cullResults;
#pragma warning restore IDE1006 // Naming Styles

                public void Init(ContextContainer frameData, IUnsafeRenderGraphBuilder builder = null)
                {
                    cameraData = frameData.Get<UniversalCameraData>();
                    lightData = frameData.Get<UniversalLightData>();
                    renderingData = frameData.Get<UniversalRenderingData>();
                    cullResults = renderingData.cullResults;
                }
            }

            readonly PassData _PassData = new();

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                using (var builder = graph.AddUnsafePass<PassData>(k_Name, out var data))
                {
                    data.Init(frame, builder);
                    builder.AllowPassCulling(false);

                    builder.SetRenderFunc<PassData>((data, context) =>
                    {
                        var buffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        Execute(context.GetRenderContext(), buffer, data);
                    });
                }
            }

            [System.Obsolete]
            public override void OnCameraSetup(CommandBuffer buffer, ref RenderingData data)
            {
                _PassData.Init(data.GetFrameData());
            }

            [System.Obsolete]
            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                _PassData.Init(data.GetFrameData());
                var buffer = CommandBufferPool.Get(k_Name);
                Execute(context, buffer, _PassData);
                context.ExecuteCommandBuffer(buffer);
                CommandBufferPool.Release(buffer);
            }
        }
    }

    partial class CopyDepthBufferPassURP
    {
        class PassData
        {
#pragma warning disable IDE1006 // Naming Styles
            public UniversalCameraData cameraData;
            public RenderGraphHelper.Handle colorTargetHandle;
            public RenderGraphHelper.Handle depthTargetHandle;
#pragma warning restore IDE1006 // Naming Styles

            public void Init(ContextContainer frameData, IUnsafeRenderGraphBuilder builder = null)
            {
                var resources = frameData.Get<UniversalResourceData>();
                cameraData = frameData.Get<UniversalCameraData>();

                if (builder == null)
                {
#pragma warning disable CS0618 // Type or member is obsolete
                    depthTargetHandle = cameraData.renderer.cameraDepthTargetHandle;
#pragma warning restore CS0618 // Type or member is obsolete
                }
                else
                {
                    depthTargetHandle = resources.activeDepthTexture;
                    builder.UseTexture(depthTargetHandle, AccessFlags.ReadWrite);
                }
            }
        }

        readonly PassData _PassData = new();

        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
        {
            using (var builder = graph.AddUnsafePass<PassData>(k_Name, out var data))
            {
                data.Init(frame, builder);
                builder.AllowPassCulling(false);

                builder.SetRenderFunc<PassData>((data, context) =>
                {
                    var buffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                    OnSetup(buffer, data);
                    Execute(context.GetRenderContext(), buffer, data);
                });
            }
        }

        [System.Obsolete]
        public override void OnCameraSetup(CommandBuffer buffer, ref RenderingData data)
        {
            _PassData.Init(data.GetFrameData());
        }

        [System.Obsolete]
        public override void Execute(ScriptableRenderContext context, ref RenderingData data)
        {
            _PassData.Init(data.GetFrameData());
            var buffer = CommandBufferPool.Get(k_Name);
            OnSetup(buffer, _PassData);
            Execute(context, buffer, _PassData);
            context.ExecuteCommandBuffer(buffer);
            CommandBufferPool.Release(buffer);
        }
    }
}

#endif // UNITY_6000_0_OR_NEWER
#endif // d_UnityURP
