// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace WaveHarmonic.Crest
{
    // Built-in Render Pipeline
    partial class WaterRenderer
    {
        partial class ShaderIDs
        {
            public static readonly int s_CameraOpaqueTexture = Shader.PropertyToID("_CameraOpaqueTexture");
            public static readonly int s_ShadowMapTexture = Shader.PropertyToID("_ShadowMapTexture");
            public static readonly int s_ScreenSpaceShadowTexture = Shader.PropertyToID("_Crest_ScreenSpaceShadowTexture");
        }

        CommandBuffer _CopyColorTextureBuffer;
        RenderTexture _CameraColorTexture;

        CommandBuffer _ScreenSpaceShadowMapBuffer;
        CommandBuffer _DeferredShadowMapBuffer;

        internal DefaultFormat LikelyFrameBufferFormat
        {
            get
            {
                if (_OverrideRenderHDR)
                {
                    return _RenderHDR ? DefaultFormat.HDR : DefaultFormat.LDR;
                }

#if UNITY_EDITOR
                var target = UnityEditor.EditorUserBuildSettings.activeBuildTarget;
                if (target is UnityEditor.BuildTarget.Android or UnityEditor.BuildTarget.iOS or UnityEditor.BuildTarget.tvOS)
                {
                    return DefaultFormat.LDR;
                }
#endif

#if UNITY_ANDROID || UNITY_IOS || UNITY_TVOS
                return DefaultFormat.LDR;
#else
                return DefaultFormat.HDR;
#endif
            }
        }

        void OnPreRenderCamera(Camera camera)
        {
#if UNITY_EDITOR
            UpdateLastActiveSceneCamera(camera);

            if (!Application.isPlaying)
            {
                OnPreRenderWaterLevelDepthTexture(camera);
            }
#endif

            OnBeginCameraRendering(camera);

            if (!Helpers.MaskIncludesLayer(camera.cullingMask, Layer))
            {
                return;
            }

            XRHelpers.Update(camera);
            XRHelpers.SetInverseViewProjectionMatrix(camera);

            _CopyColorTextureBuffer ??= new() { name = "Crest Copy Color Texture" };
            _DeferredShadowMapBuffer ??= new() { name = "Crest Deferred Shadow Data" };
            _ScreenSpaceShadowMapBuffer ??= new() { name = "Crest Screen-Space Shadow Data" };
            _CopyColorTextureBuffer.Clear();
            _DeferredShadowMapBuffer.Clear();
            _ScreenSpaceShadowMapBuffer.Clear();

            // Create or update RT.
            {
                var descriptor = XRHelpers.GetRenderTextureDescriptor(camera);

                // Format must be correct for CopyTexture to work. Hopefully this is good enough.
                if (camera.allowHDR && QualitySettings.activeColorSpace == ColorSpace.Linear)
                {
                    descriptor.graphicsFormat = SystemInfo.GetGraphicsFormat(LikelyFrameBufferFormat);
                }

                // Occurred in a build and caused a black screen.
                if (descriptor.width <= 0)
                {
                    return;
                }

                if (_CameraColorTexture == null)
                {
                    _CameraColorTexture = new(descriptor);
                }
                else
                {
                    _CameraColorTexture.Release();
                    _CameraColorTexture.descriptor = descriptor;
                }

                _CameraColorTexture.Create();
            }

            var target = new RenderTargetIdentifier(_CameraColorTexture, 0, CubemapFace.Unknown, -1);

            // Use blit instead of CopyTexture as it will smooth out issues with format
            // differences which is very hard to get right for BIRP.
            _CopyColorTextureBuffer.Blit(BuiltinRenderTextureType.CameraTarget, target);
            _CopyColorTextureBuffer.SetGlobalTexture(ShaderIDs.s_CameraOpaqueTexture, _CameraColorTexture);

            camera.AddCommandBuffer(CameraEvent.BeforeForwardAlpha, _CopyColorTextureBuffer);

            if (QualitySettings.shadows != ShadowQuality.Disable && PrimaryLight != null)
            {
                // Make the screen-space shadow texture available for the water shader for caustic occlusion.
                _ScreenSpaceShadowMapBuffer.SetGlobalTexture(ShaderIDs.s_ScreenSpaceShadowTexture, BuiltinRenderTextureType.CurrentActive);
                PrimaryLight.AddCommandBuffer(LightEvent.AfterScreenspaceMask, _ScreenSpaceShadowMapBuffer);
                // Call this regardless of rendering path as it has no negative consequences for forward.
                _DeferredShadowMapBuffer.SetGlobalTexture(ShaderIDs.s_ShadowMapTexture, BuiltinRenderTextureType.CurrentActive);
                PrimaryLight.AddCommandBuffer(LightEvent.AfterShadowMap, _DeferredShadowMapBuffer);
            }

            {
                // Always set these in case shadow maps are disabled in the graphics settings which
                // we cannot check at runtime.
                // Black for shadowed. White for unshadowed.
                if (camera.stereoEnabled && XRHelpers.IsSinglePass)
                {
                    Shader.SetGlobalTexture(ShaderIDs.s_ScreenSpaceShadowTexture, XRHelpers.WhiteTexture);
                }
                else
                {
                    Shader.SetGlobalTexture(ShaderIDs.s_ScreenSpaceShadowTexture, Texture2D.whiteTexture);
                }
            }
        }

        void OnPostRenderCamera(Camera camera)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                OnPostRenderWaterLevelDepthTexture(camera);
            }
#endif

            if (!Helpers.MaskIncludesLayer(camera.cullingMask, Layer))
            {
                return;
            }

            if (_CopyColorTextureBuffer != null)
            {
                camera.RemoveCommandBuffer(CameraEvent.BeforeForwardAlpha, _CopyColorTextureBuffer);
            }

            if (QualitySettings.shadows != ShadowQuality.Disable && PrimaryLight != null)
            {
                if (_ScreenSpaceShadowMapBuffer != null)
                {
                    PrimaryLight.RemoveCommandBuffer(LightEvent.AfterScreenspaceMask, _ScreenSpaceShadowMapBuffer);
                }

                if (_DeferredShadowMapBuffer != null)
                {
                    PrimaryLight.RemoveCommandBuffer(LightEvent.AfterShadowMap, _DeferredShadowMapBuffer);
                }
            }
        }
    }
}
