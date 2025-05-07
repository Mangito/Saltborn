// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// FIXME: Broken for BIRP on MacOS. Either platform specific problem or bug in Unity.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.Universal;

namespace WaveHarmonic.Crest
{
    partial class WaterRenderer
    {
        CommandBuffer _WaterLevelDepthBuffer;
        RenderTexture _WaterLevelDepthTexture;
        internal RenderTexture WaterLevelDepthTexture => _WaterLevelDepthTexture;
        RenderTargetIdentifier _WaterLevelDepthTarget;
        Material _WaterLevelDepthMaterial;
        internal readonly Plane[] _CameraFrustumPlanes = new Plane[6];

        const string k_WaterLevelDepthTextureName = "Crest Water Level Depth Texture";

        void RenderWaterSurface(CommandBuffer buffer, Camera camera, Material material)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, _CameraFrustumPlanes);

            // Spends approx 0.2-0.3ms here on 2018 Dell XPS 15.
            foreach (var chunk in Chunks)
            {
                var renderer = chunk.Rend;
                // Can happen in edit mode.
                if (renderer == null) continue;

                var bounds = renderer.bounds;
                if (GeometryUtility.TestPlanesAABB(_CameraFrustumPlanes, bounds))
                {
                    if ((!chunk._WaterDataHasBeenBound) && chunk.enabled)
                    {
                        chunk.Bind(camera);
                    }

                    renderer.SetPropertyBlock(chunk._MaterialPropertyBlock);

                    // Assume correct pass is zero. Use to be k_ShaderPassWaterSurfaceMask.
                    buffer.DrawRenderer(renderer, material, submeshIndex: 0, shaderPass: 0);
                }

                chunk._WaterDataHasBeenBound = false;
            }
        }

        void ExecuteWaterLevelDepthTexture(Camera camera, CommandBuffer buffer)
        {
            Helpers.CreateRenderTargetTextureReference(ref _WaterLevelDepthTexture, ref _WaterLevelDepthTarget);
            _WaterLevelDepthTexture.name = k_WaterLevelDepthTextureName;

            if (_WaterLevelDepthMaterial == null)
            {
                _WaterLevelDepthMaterial = new(Shader.Find("Hidden/Crest/Editor/Water Level (Depth)"));
            }

            var descriptor = new RenderTextureDescriptor(camera.pixelWidth, camera.pixelHeight)
            {
                graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.None,
                depthBufferBits = 32,
            };

            // Depth texture.
            // Always release to handle screen size changes.
            _WaterLevelDepthTexture.Release();
            descriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
            descriptor.depthBufferBits = 0;
            Helpers.SafeCreateRenderTexture(ref _WaterLevelDepthTexture, descriptor);
            _WaterLevelDepthTexture.Create();

            // Depth buffer.
            buffer.GetTemporaryRT(Helpers.ShaderIDs.s_MainTexture, descriptor);
            buffer.SetRenderTarget(Helpers.ShaderIDs.s_MainTexture);
            buffer.ClearRenderTarget(true, false, Color.clear);

            RenderWaterSurface(buffer, camera, _WaterLevelDepthMaterial);

            // Convert.
            Helpers.Blit(buffer, _WaterLevelDepthTarget, Helpers.UtilityMaterial, (int)Helpers.UtilityPass.Copy);

            buffer.ReleaseTemporaryRT(Helpers.ShaderIDs.s_MainTexture);

        }

        void EnableWaterLevelDepthTexture()
        {
            if (Application.isPlaying) return;

#if d_UnityURP
            if (RenderPipelineHelper.IsUniversal)
            {
                WaterLevelDepthTextureURP.Enable();
            }
#endif

#if d_UnityHDRP
            if (RenderPipelineHelper.IsHighDefinition)
            {
                WaterLevelDepthTextureHDRP.Enable();
            }
#endif
        }

        void DisableWaterLevelDepthTexture()
        {
            if (Application.isPlaying) return;

#if d_UnityURP
            WaterLevelDepthTextureURP.Disable();
#endif

#if d_UnityHDRP
            WaterLevelDepthTextureHDRP.Disable();
#endif
        }

        void OnPreRenderWaterLevelDepthTexture(Camera camera)
        {
            if (camera.cameraType != CameraType.SceneView || camera != Viewer)
            {
                return;
            }

            _WaterLevelDepthBuffer ??= new() { name = k_WaterLevelDepthTextureName };
            _WaterLevelDepthBuffer.Clear();

            ExecuteWaterLevelDepthTexture(camera, _WaterLevelDepthBuffer);

            // Both forward and deferred.
            camera.AddCommandBuffer(CameraEvent.BeforeDepthTexture, _WaterLevelDepthBuffer);
            camera.AddCommandBuffer(CameraEvent.BeforeGBuffer, _WaterLevelDepthBuffer);
        }

        void OnPostRenderWaterLevelDepthTexture(Camera camera)
        {
            if (_WaterLevelDepthBuffer != null)
            {
                // Both forward and deferred.
                camera.RemoveCommandBuffer(CameraEvent.BeforeDepthTexture, _WaterLevelDepthBuffer);
                camera.RemoveCommandBuffer(CameraEvent.BeforeGBuffer, _WaterLevelDepthBuffer);
            }
        }

#if d_UnityURP
        sealed class WaterLevelDepthTextureURP : ScriptableRenderPass
        {
            static WaterLevelDepthTextureURP s_Instance;

            internal WaterLevelDepthTextureURP()
            {
                // Will always execute and matrices will be ready.
                renderPassEvent = RenderPassEvent.BeforeRenderingPrePasses;
            }

            internal static void Enable()
            {
                s_Instance ??= new();

                RenderPipelineManager.beginCameraRendering -= s_Instance.EnqueuePass;
                RenderPipelineManager.beginCameraRendering += s_Instance.EnqueuePass;
                RenderPipelineManager.activeRenderPipelineTypeChanged -= Disable;
                RenderPipelineManager.activeRenderPipelineTypeChanged += Disable;
            }

            internal static void Disable()
            {
                // FIXME: Out of range exception when no null check but shouldn't be necessary.
                if (s_Instance != null) RenderPipelineManager.beginCameraRendering -= s_Instance.EnqueuePass;
                RenderPipelineManager.activeRenderPipelineTypeChanged -= Disable;
            }

            void EnqueuePass(ScriptableRenderContext context, Camera camera)
            {
                if (camera.cameraType != CameraType.SceneView || camera != Instance.Viewer)
                {
                    return;
                }

                // Enqueue the pass. This happens every frame.
                camera.GetUniversalAdditionalCameraData().scriptableRenderer.EnqueuePass(this);
            }

#if UNITY_6000_0_OR_NEWER
            class PassData
            {
                public UniversalCameraData _CameraData;
            }

            public override void RecordRenderGraph(UnityEngine.Rendering.RenderGraphModule.RenderGraph graph, ContextContainer frame)
            {
                using (var builder = graph.AddUnsafePass<PassData>(k_WaterLevelDepthTextureName, out var data))
                {
                    builder.AllowPassCulling(false);

                    data._CameraData = frame.Get<UniversalCameraData>();

                    builder.SetRenderFunc<PassData>((data, context) =>
                    {
                        var buffer = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        Instance.ExecuteWaterLevelDepthTexture(data._CameraData.camera, buffer);
                    });
                }
            }

            [System.Obsolete]
#endif
            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                var buffer = CommandBufferPool.Get(k_WaterLevelDepthTextureName);
                Instance.ExecuteWaterLevelDepthTexture(data.cameraData.camera, buffer);
                context.ExecuteCommandBuffer(buffer);
                CommandBufferPool.Release(buffer);
            }
        }
#endif

#if d_UnityHDRP
        sealed class WaterLevelDepthTextureHDRP : CustomPass
        {
            static WaterLevelDepthTextureHDRP s_Instance;
            GameObject _GameObject;

            internal static void Enable()
            {
                var gameObject = CustomPassHelpers.CreateOrUpdate
                (
                    parent: Instance.Container.transform,
                    k_WaterLevelDepthTextureName,
                    hide: !Instance._Debug._ShowHiddenObjects
                );

                CustomPassHelpers.CreateOrUpdate
                (
                    gameObject,
                    ref s_Instance,
                    k_WaterLevelDepthTextureName,
                    CustomPassInjectionPoint.BeforeRendering
                );

                s_Instance._GameObject = gameObject;
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

            protected override void Execute(CustomPassContext context)
            {
                var camera = context.hdCamera.camera;

                if (camera.cameraType != CameraType.SceneView || camera != Instance.Viewer)
                {
                    return;
                }

                Instance.ExecuteWaterLevelDepthTexture(camera, context.cmd);
            }
        }
#endif
    }
}
