// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#if d_UnityURP
#if UNITY_6000_0_OR_NEWER

using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace WaveHarmonic.Crest
{
    partial class SampleShadowsURP : ScriptableRenderPass
    {
        class PassData
        {
#pragma warning disable IDE1006 // Naming Styles
            public UniversalCameraData cameraData;
            public UniversalLightData lightData;
            public CullingResults cullResults;
#pragma warning restore IDE1006 // Naming Styles

            public void Init(ContextContainer frameData, IUnsafeRenderGraphBuilder builder = null)
            {
                cameraData = frameData.Get<UniversalCameraData>();
                lightData = frameData.Get<UniversalLightData>();
                cullResults = frameData.Get<UniversalRenderingData>().cullResults;
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

#endif // UNITY_6000_0_OR_NEWER
#endif // d_UnityURP
