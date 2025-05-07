// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    partial class UnderwaterRenderer
    {
        // Adapted from:
        // Packages/com.unity.render-pipelines.universal/Runtime/UniversalRenderer.cs
#if UNITY_SWITCH || UNITY_ANDROID || UNITY_EMBEDDED_LINUX || UNITY_QNX
        internal const GraphicsFormat k_DepthStencilFormat = GraphicsFormat.D24_UNorm_S8_UInt;
        internal const int k_DepthBufferBits = 24;
        internal const DepthBits k_DepthBits = DepthBits.Depth24;
#else
        internal const GraphicsFormat k_DepthStencilFormat = GraphicsFormat.D32_SFloat_S8_UInt;
        internal const int k_DepthBufferBits = 32;
        internal const DepthBits k_DepthBits = DepthBits.Depth32;
#endif

        internal const int k_ShaderPassWaterSurfaceMask = 0;
        internal const int k_ShaderPassWaterSurfaceDepth = 1;
        internal const int k_ShaderPassWaterHorizonMask = 2;

        // NOTE: Must match CREST_MASK_BELOW_SURFACE in Constants.hlsl.
        const float k_MaskBelowSurface = -1f;
        // NOTE: Must match CREST_MASK_BELOW_SURFACE_CULLED in Constants.hlsl.
        const float k_MaskBelowSurfaceCull = -2f;

        internal const string k_ComputeShaderKernelFillMaskArtefacts = "FillMaskArtefacts";

        static partial class ShaderIDs
        {
            // Local
            public static readonly int s_FarPlaneOffset = Shader.PropertyToID("_Crest_FarPlaneOffset");
            public static readonly int s_MaskBelowSurface = Shader.PropertyToID("_Crest_MaskBelowSurface");

            // Global
            public static readonly int s_WaterMaskTexture = Shader.PropertyToID("_Crest_WaterMaskTexture");
            public static readonly int s_WaterMaskDepthTexture = Shader.PropertyToID("_Crest_WaterMaskDepthTexture");

            public static readonly int s_StencilRef = Shader.PropertyToID("_StencilRef");
        }

        internal Material _MaskMaterial;

        internal RenderTargetIdentifier _MaskTarget;
        internal RenderTargetIdentifier _DepthTarget;

        internal readonly Plane[] _CameraFrustumPlanes = new Plane[6];
        CommandBuffer _MaskCommandBuffer;

        internal RenderTexture _MaskRT;
        RenderTexture _DepthRT;

        ComputeShader _ArtifactsShader;
        bool _ArtifactsShaderInitialized;
        int _ArtifactsKernel;
        uint _ArtifactsThreadGroupSizeX;
        uint _ArtifactsThreadGroupSizeY;

        void SetupMask()
        {
            _MaskCommandBuffer ??= new()
            {
                name = "Crest: Underwater Mask",
            };
        }

        internal void OnEnableMask()
        {
            // Create a reference to handle the RT. The RT properties will be replaced with a descriptor before the
            // native object is created, and since it is lazy it is near zero cost.
            Helpers.CreateRenderTargetTextureReference(ref _MaskRT, ref _MaskTarget);
            _MaskRT.name = "_Crest_WaterMaskTexture";
            Helpers.CreateRenderTargetTextureReference(ref _DepthRT, ref _DepthTarget);
            _DepthRT.name = "_Crest_WaterMaskDepthTexture";

            SetUpArtifactsShader();
        }

        internal void OnDisableMask()
        {
            if (_MaskRT != null) _MaskRT.Release();
            if (_DepthRT != null) _DepthRT.Release();
        }

        internal void SetUpArtifactsShader()
        {
            if (_ArtifactsShaderInitialized)
            {
                return;
            }

            _ArtifactsKernel = _ArtifactsShader.FindKernel(k_ComputeShaderKernelFillMaskArtefacts);
            _ArtifactsShader.GetKernelThreadGroupSizes
            (
                _ArtifactsKernel,
                out _ArtifactsThreadGroupSizeX,
                out _ArtifactsThreadGroupSizeY,
                out _
            );

            _ArtifactsShaderInitialized = true;
        }

        internal void SetUpMaskTextures(RenderTextureDescriptor descriptor)
        {
            if (!Helpers.RenderTargetTextureNeedsUpdating(_MaskRT, descriptor))
            {
                return;
            }

            // This will disable MSAA for our textures as MSAA will break sampling later on. This looks safe to do as
            // Unity's CopyDepthPass does the same, but a possible better way or supporting MSAA is worth looking into.
            descriptor.msaaSamples = 1;

            // @Memory: We could investigate making this an 8-bit texture instead to reduce GPU memory usage.
            // @Memory: We could potentially try a half resolution mask as the mensicus could mask resolution issues.
            // Intel iGPU for Metal and DirectX both had issues with R16. 2021.11.18
            descriptor.colorFormat = Helpers.IsIntelGPU() ? RenderTextureFormat.RFloat : RenderTextureFormat.RHalf;
            descriptor.depthBufferBits = 0;
            descriptor.enableRandomWrite = true;

            _MaskRT.Release();
            _MaskRT.descriptor = descriptor;

            descriptor.colorFormat = RenderTextureFormat.Depth;
            descriptor.depthBufferBits = (int)k_DepthBits;
            descriptor.enableRandomWrite = false;

            _DepthRT.Release();
            _DepthRT.descriptor = descriptor;
        }

        void OnPreRenderMask(Camera camera)
        {
            _MaskCommandBuffer.Clear();

            var descriptor = XRHelpers.GetRenderTextureDescriptor(camera);

            descriptor.useDynamicScale = camera.allowDynamicResolution;

            // Keywords and other things.
            SetUpMaskTextures(descriptor);

#if d_CrestPortals
            // Populate water volume before mask so we can use the stencil.
            if (_Portals.Active)
            {
                _Portals.ReAllocate(descriptor);
                _Portals.RenderMask(camera, _MaskCommandBuffer, _MaskMaterial);
                _Portals.RenderStencil(_MaskCommandBuffer, _DepthRT, _DepthTarget);
            }
#endif

            _MaskCommandBuffer.SetRenderTarget(_MaskTarget, _DepthTarget);
            SetUpMask(_MaskCommandBuffer, _MaskTarget, _DepthTarget);
            PopulateMask(_MaskCommandBuffer, camera);

            FixMaskArtefacts(_MaskCommandBuffer, descriptor, _MaskTarget);
        }

        internal void SetUpMask(CommandBuffer buffer, RenderTargetIdentifier maskTarget, RenderTargetIdentifier depthTarget)
        {
            // When using the stencil we are already clearing depth and do not want to clear the stencil too. Clear
            // color only when using the stencil as the horizon effectively clears it when not using it.
            buffer.ClearRenderTarget(!UseStencilBuffer, UseStencilBuffer, Color.black);
            buffer.SetGlobalTexture(ShaderIDs.s_WaterMaskTexture, maskTarget);
            buffer.SetGlobalTexture(ShaderIDs.s_WaterMaskDepthTexture, depthTarget);
        }

        internal void FixMaskArtefacts(CommandBuffer buffer, RenderTextureDescriptor descriptor, RenderTargetIdentifier target)
        {
            if (_Debug._DisableArtifactCorrection)
            {
                return;
            }

            buffer.SetComputeTextureParam(_ArtifactsShader, _ArtifactsKernel, ShaderIDs.s_WaterMaskTexture, target);
            // XR SPI will have a volume depth of two. If using RTHandles, then set manually as will be two for all cameras.
            _ArtifactsShader.SetKeyword("STEREO_INSTANCING_ON", descriptor.dimension == TextureDimension.Tex2DArray);

            buffer.DispatchCompute
            (
                _ArtifactsShader,
                _ArtifactsKernel,
                // Viewport sizes are not perfect so round up to cover.
                Mathf.CeilToInt((float)descriptor.width / _ArtifactsThreadGroupSizeX),
                Mathf.CeilToInt((float)descriptor.height / _ArtifactsThreadGroupSizeY),
                descriptor.volumeDepth
            );
        }

        // Populates a screen space mask which will inform the underwater postprocess. As a future optimisation we may
        // be able to avoid this pass completely if we can reuse the camera depth after transparents are rendered.
        internal void PopulateMask(CommandBuffer commandBuffer, Camera camera)
        {
            // Render horizon into mask using a fullscreen triangle at the far plane. Horizon must be rendered first or
            // it will overwrite the mask with incorrect values.
            {
                var zBufferParameters = Helpers.GetZBufferParameters(camera);
                // Take 0-1 linear depth and convert non-linear depth.
                _MaskMaterial.SetFloat(ShaderIDs.s_FarPlaneOffset, Helpers.LinearDepthToNonLinear(_FarPlaneMultiplier, zBufferParameters));

                // Render fullscreen triangle with horizon mask pass.
                commandBuffer.DrawProcedural(Matrix4x4.identity, _MaskMaterial, shaderPass: k_ShaderPassWaterHorizonMask, MeshTopology.Triangles, 3, 1);
            }

            GeometryUtility.CalculateFrustumPlanes(camera, _CameraFrustumPlanes);

            // Get all water chunks and render them using cmd buffer, but with mask shader.
            if (!_Debug._DisableMask)
            {
                // Spends approx 0.2-0.3ms here on 2018 Dell XPS 15.
                foreach (var chunk in _Water.Chunks)
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

                        // Handle culled tiles for when underwater is rendered before the transparent pass.
                        chunk._MaterialPropertyBlock.SetFloat(ShaderIDs.s_MaskBelowSurface, !_EnableShaderAPI || renderer.enabled ? k_MaskBelowSurface : k_MaskBelowSurfaceCull);
                        renderer.SetPropertyBlock(chunk._MaterialPropertyBlock);

                        commandBuffer.DrawRenderer(renderer, _MaskMaterial, submeshIndex: 0, shaderPass: k_ShaderPassWaterSurfaceMask);

                        chunk._Visible = true;
                    }
                    chunk._WaterDataHasBeenBound = false;
                }

#if d_CrestPortals
                if (_Portals.Active)
                {
                    _Portals.PopulateMask(commandBuffer, _MaskMaterial);
                }
#endif // d_CrestPortals
            }
        }
    }
}
