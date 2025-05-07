// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace WaveHarmonic.Crest
{
    sealed class UnderwaterMaskPass
    {
        readonly UnderwaterRenderer _Renderer;
#if d_CrestPortals
        readonly Portals.PortalRenderer _Portals;
#endif

        RTHandle _MaskTexture;
        RTHandle _DepthTexture;
        RenderTargetIdentifier _MaskTarget;
        RenderTargetIdentifier _DepthTarget;

        public UnderwaterMaskPass(UnderwaterRenderer renderer)
        {
            _Renderer = renderer;
#if d_CrestPortals
            _Portals = renderer._Portals;
#endif
        }

        public void Allocate()
        {
            _MaskTexture = RTHandles.Alloc
            (
                scaleFactor: Vector2.one,
                slices: TextureXR.slices,
                dimension: TextureXR.dimension,
                depthBufferBits: DepthBits.None,
                colorFormat: GraphicsFormat.R16_SFloat,
                enableRandomWrite: true,
                useDynamicScale: true,
                name: "_Crest_WaterMask"
            );

            _MaskTarget = new(_MaskTexture, mipLevel: 0, CubemapFace.Unknown, depthSlice: -1);

            _DepthTexture = RTHandles.Alloc
            (
                scaleFactor: Vector2.one,
                slices: TextureXR.slices,
                dimension: TextureXR.dimension,
                depthBufferBits: UnderwaterRenderer.k_DepthBits,
                colorFormat: GraphicsFormat.None,
                enableRandomWrite: false,
                useDynamicScale: true,
                name: "_Crest_WaterMaskDepth"
            );

#if d_CrestPortals
            // For HDRP we cannot allocate in OnEnable as RTHandle will complain.
            if (_Portals.Active)
            {
                _Portals.Allocate();
            }
#endif

            _DepthTarget = new(_DepthTexture, mipLevel: 0, CubemapFace.Unknown, depthSlice: -1);

            _Renderer.SetUpArtifactsShader();
        }

        // We should not have to reallocate, but URP will raise errors when an option like HDR is changed if we do not.
        public void ReAllocate(RenderTextureDescriptor descriptor)
        {
            // Shared settings. Enabling MSAA might be a good idea except cannot enable random writes. Having a raster
            // shader to remove artifacts is a workaround.
            descriptor.bindMS = false;
            descriptor.msaaSamples = 1;

            descriptor.graphicsFormat = GraphicsFormat.None;

            if (RenderPipelineCompatibilityHelper.ReAllocateIfNeeded(ref _DepthTexture, descriptor, name: "_Crest_WaterMaskDepth"))
            {
                _DepthTarget = new(_DepthTexture, mipLevel: 0, CubemapFace.Unknown, depthSlice: -1);
            }

#if d_CrestPortals
            if (_Portals.Active)
            {
                _Portals.ReAllocate(descriptor);
            }
#endif

            descriptor.graphicsFormat = GraphicsFormat.R16_SFloat;
            descriptor.enableRandomWrite = true;
            descriptor.depthBufferBits = 0;

            if (RenderPipelineCompatibilityHelper.ReAllocateIfNeeded(ref _MaskTexture, descriptor, name: "_Crest_WaterMask"))
            {
                _MaskTarget = new(_MaskTexture, mipLevel: 0, CubemapFace.Unknown, depthSlice: -1);
            }
        }

        public void Release()
        {
            _MaskTexture?.Release();
            _DepthTexture?.Release();

#if d_CrestPortals
            if (_Portals.Active)
            {
                _Portals.Release();
            }
#endif
        }

        public void Execute(Camera camera, CommandBuffer buffer)
        {
#if d_CrestPortals
            // Populate water volume before mask so we can use the stencil.
            if (_Portals.Active)
            {
                _Portals.RenderMask(camera, buffer, _Renderer._MaskMaterial);
                _Portals.RenderStencil(buffer, _DepthTexture);
            }
#endif

            // For HDRP software dynamic scaling to work.
            CoreUtils.SetRenderTarget(buffer, _MaskTexture, _DepthTexture);
            Helpers.ScaleViewport(camera, buffer, _MaskTexture);

            _Renderer.SetUpMask(buffer, _MaskTarget, _DepthTarget);
            _Renderer.PopulateMask(buffer, camera);

            var size = _MaskTexture.GetScaledSize(_MaskTexture.rtHandleProperties.currentViewportSize);
            var descriptor = _MaskTexture.rt.descriptor;
            descriptor.width = size.x; descriptor.height = size.y;
            _Renderer.FixMaskArtefacts(buffer, descriptor, _MaskTarget);
        }
    }
}
