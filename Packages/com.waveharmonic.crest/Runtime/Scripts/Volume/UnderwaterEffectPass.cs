// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace WaveHarmonic.Crest
{
    sealed class UnderwaterEffectPass
    {
        readonly UnderwaterRenderer _Renderer;

        RTHandle _ColorTexture;

        RTHandle _ColorTarget;
        RTHandle _DepthTarget;

        bool _FirstRender = true;

        readonly System.Action<CommandBuffer> _CopyColorTexture;

        public UnderwaterEffectPass(UnderwaterRenderer renderer)
        {
            _Renderer = renderer;
            _CopyColorTexture = new(CopyColorTexture);
        }

        void CopyColorTexture(CommandBuffer buffer)
        {
            Blitter.BlitCameraTexture(buffer, _ColorTarget, _ColorTexture);
            CoreUtils.SetRenderTarget(buffer, _ColorTarget, _DepthTarget, ClearFlag.None);
        }

        public void Allocate(GraphicsFormat format)
        {
            // TODO: There may other settings we want to set or bring in. Not MSAA since this is a resolved texture.
            _ColorTexture = RTHandles.Alloc
            (
                Vector2.one,
                TextureXR.slices,
                dimension: TextureXR.dimension,
                colorFormat: format,
                depthBufferBits: DepthBits.None,
                useDynamicScale: true,
                wrapMode: TextureWrapMode.Clamp,
                name: "_Crest_UnderwaterCameraColorTexture"
            );
        }

        public void ReAllocate(RenderTextureDescriptor descriptor)
        {
            // Descriptor will not have MSAA bound.
            RenderPipelineCompatibilityHelper.ReAllocateIfNeeded(ref _ColorTexture, descriptor, name: "_Crest_UnderwaterCameraColorTexture");
        }

        public void Release()
        {
            _ColorTexture?.Release();
            _ColorTexture = null;
        }

        public void Execute(Camera camera, CommandBuffer buffer, RTHandle color, RTHandle depth, MaterialPropertyBlock mpb = null)
        {
            _Renderer.UpdateEffectMaterial(camera, _FirstRender);

            _ColorTarget = color;
            _DepthTarget = depth;
            CopyColorTexture(buffer);

            buffer.SetGlobalTexture(UnderwaterRenderer.ShaderIDs.s_CameraColorTexture, _ColorTexture);

            _Renderer.ExecuteEffect(camera, buffer, _CopyColorTexture, mpb);

            // The last pass (uber post) does not resolve the texture.
            // Although, this is wasteful if the pass after this does a resolve.
            // Possibly a bug with Unity?
            buffer.ResolveAntiAliasedSurface(color);

            _FirstRender = false;
        }
    }
}
