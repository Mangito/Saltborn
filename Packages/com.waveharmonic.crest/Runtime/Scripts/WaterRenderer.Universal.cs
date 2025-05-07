// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#if d_UnityURP

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WaveHarmonic.Crest
{
    // Universal Render Pipeline
    partial class WaterRenderer
    {
        sealed class ConfigureUniversalRenderer : ScriptableRenderPass
        {
            readonly WaterRenderer _Water;
            public static ConfigureUniversalRenderer Instance { get; set; }

            public ConfigureUniversalRenderer(WaterRenderer water)
            {
                _Water = water;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
            }

            public static void Enable(WaterRenderer water)
            {
#if UNITY_EDITOR
                var data = water.Viewer != null ? water.Viewer.GetUniversalAdditionalCameraData() : null;

                // Type is internal.
                if (data != null && data.scriptableRenderer.GetType().Name == "Renderer2D")
                {
                    UnityEditor.EditorUtility.DisplayDialog
                    (
                        "Crest Error!",
                        "The project has been detected as a URP 2D project. Crest only supports 3D projects. " +
                        "You may see errors from Crest in the console, and other issues.",
                        "Ok"
                    );
                }
#endif

                Instance = new ConfigureUniversalRenderer(water);
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
                RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            }

            public static void Disable()
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            }

            static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
            {
                // May cause assertions/exceptions for reflection camera.
                if (camera.cameraType == CameraType.Reflection) return;

                if (!Helpers.MaskIncludesLayer(camera.cullingMask, Instance._Water.Layer))
                {
                    return;
                }

                // TODO: Could also check RenderType. Which is better?
                if (!Instance._Water.Material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))
                {
                    return;
                }

                camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(Instance);
            }

#if UNITY_6000_0_OR_NEWER
            sealed class PassData { }

            public override void RecordRenderGraph(UnityEngine.Rendering.RenderGraphModule.RenderGraph graph, ContextContainer frame)
            {
                using (var builder = graph.AddUnsafePass<PassData>("Crest Register Color/Depth Requirements.", out var data))
                {
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc<PassData>((data, context) => { });
                }
            }

            [System.Obsolete]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // Blank
            }
        }

        internal sealed class UniversalCopyWaterSurfaceDepth : ScriptableRenderPass
        {
            readonly WaterRenderer _Water;
            public static UniversalCopyWaterSurfaceDepth Instance { get; set; }

            readonly UnityEngine.Rendering.Universal.Internal.CopyDepthPass _CopyDepthPass;
            readonly Shader _CopyDepthShader;
            readonly Material _CopyDepthMaterial;

            public UniversalCopyWaterSurfaceDepth(WaterRenderer water)
            {
                _Water = water;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;

                _CopyDepthShader = Shader.Find("Hidden/Universal Render Pipeline/CopyDepth");
#if !UNITY_6000_0_OR_NEWER
                _CopyDepthMaterial = new Material(_CopyDepthShader);
#endif

                _CopyDepthPass = new
                (
                    RenderPassEvent.BeforeRenderingPostProcessing,
#if UNITY_6000_0_OR_NEWER
                    _CopyDepthShader,
#else
                    _CopyDepthMaterial,
#endif
                    // Will not work in U6 without it.
                    copyToDepth: true,
                    copyResolvedDepth: RenderingUtils.MultisampleDepthResolveSupported(),
                    shouldClear: false
                );
            }

            public static void Enable(WaterRenderer water)
            {
                Instance = new UniversalCopyWaterSurfaceDepth(water);
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
                RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
            }

            public static void Disable()
            {
                RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            }

            static void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
            {
                // May cause assertions/exceptions for reflection camera.
                if (camera.cameraType == CameraType.Reflection) return;

                if (!Instance._Water._WriteToDepthTexture)
                {
                    return;
                }

                if (!Helpers.MaskIncludesLayer(camera.cullingMask, Instance._Water.Layer))
                {
                    return;
                }

                // TODO: Could also check RenderType. Which is better?
                if (!Instance._Water.Material.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT"))
                {
                    return;
                }

                var renderer = camera.GetUniversalAdditionalCameraData().scriptableRenderer;
                // Needed for OnCameraSetup.
                renderer.EnqueuePass(Instance);

#if UNITY_6000_0_OR_NEWER
                // Copy depth pass does not support RG directly.
                if (GraphicsSettings.GetRenderPipelineSettings<RenderGraphSettings>().enableRenderCompatibilityMode)
#endif
                {
                    renderer.EnqueuePass(Instance._CopyDepthPass);
                }
            }

#if UNITY_6000_0_OR_NEWER
            public override void RecordRenderGraph(UnityEngine.Rendering.RenderGraphModule.RenderGraph graph, ContextContainer frame)
            {
                var resources = frame.Get<UniversalResourceData>();
                var descriptor = resources.cameraDepthTexture.GetDescriptor(graph);
                // Whether we a writing to color or depth format.
                _CopyDepthPass.CopyToDepth = descriptor.colorFormat == UnityEngine.Experimental.Rendering.GraphicsFormat.None;
                _CopyDepthPass.Render(graph, frame, resources.cameraDepthTexture, resources.cameraDepth);
            }

            [System.Obsolete]
#endif
            public override void OnCameraSetup(CommandBuffer buffer, ref RenderingData data)
            {
                var renderer = (UniversalRenderer)data.cameraData.renderer;



                // Also check internal RT because it can be null on Vulkan for some reason.
                if (renderer.cameraDepthTargetHandle?.rt != null && renderer.m_DepthTexture?.rt != null)
                {
                    // Whether we a writing to color or depth format.
                    _CopyDepthPass.CopyToDepth = renderer.m_DepthTexture.rt.graphicsFormat == UnityEngine.Experimental.Rendering.GraphicsFormat.None;
                    _CopyDepthPass.m_CopyResolvedDepth = false;
                    _CopyDepthPass.Setup(renderer.cameraDepthTargetHandle, renderer.m_DepthTexture);
                }
            }

#if UNITY_6000_0_OR_NEWER
            [System.Obsolete]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                // Blank
            }
        }
    }
}

#endif
