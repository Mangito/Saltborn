// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Adaptor layer for XR module. Could be replaced with the following one day:
// com.unity.render-pipelines.core/Runtime/Common/XRGraphics.cs

// Currently, only the horizon line uses it.

// ENABLE_VR is defined if platform support XR.
// d_UnityModuleVR is defined if VR module is installed.
// VR module depends on XR module so we only need to check the VR module.
#if ENABLE_VR && d_UnityModuleVR
#define _XR_ENABLED
#endif

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace WaveHarmonic.Crest
{
    static class XRHelpers
    {
        // NOTE: This is the same value as Unity, but in the future it could be higher.
        const int k_MaximumViews = 2;

#if _XR_ENABLED
        static readonly List<XRDisplaySubsystem> s_DisplayList = new();

        // Unity only supports one display right now.
        static XRDisplaySubsystem Display => IsRunning ? s_DisplayList[0] : null;
#endif

        static Matrix4x4 LeftEyeProjectionMatrix { get; set; }
        static Matrix4x4 RightEyeProjectionMatrix { get; set; }
        static Matrix4x4 LeftEyeViewMatrix { get; set; }
        static Matrix4x4 RightEyeViewMatrix { get; set; }
        static Matrix4x4 LeftInverseViewProjectionMatrixGPU { get; set; }
        static Matrix4x4 RightInverseViewProjectionMatrixGPU { get; set; }

        static class ShaderIDs
        {
            public static readonly int s_InverseViewProjection = Shader.PropertyToID("_Crest_InverseViewProjection");
            public static readonly int s_InverseViewProjectionRight = Shader.PropertyToID("_Crest_InverseViewProjectionRight");
        }

        internal static bool IsRunning
        {
            get
            {
#if _XR_ENABLED
                return XRSettings.enabled;
#else
                return false;
#endif
            }
        }

        public static bool IsSinglePass
        {
            get
            {
#if _XR_ENABLED
                return IsRunning && (XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePassInstanced ||
                    XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePassMultiview);
#else
                return false;
#endif
            }
        }

        static Texture2DArray s_WhiteTexture = null;
        public static Texture2DArray WhiteTexture
        {
            get
            {
                if (s_WhiteTexture == null)
                {
                    s_WhiteTexture = TextureArrayHelpers.CreateTexture2DArray(Texture2D.whiteTexture, k_MaximumViews);
                    s_WhiteTexture.name = "Crest White Texture XR";
                }
                return s_WhiteTexture;
            }
        }

        public static RenderTextureDescriptor GetRenderTextureDescriptor(Camera camera)
        {
#if _XR_ENABLED
            if (camera.stereoEnabled)
            {
                return XRSettings.eyeTextureDesc;
            }
            else
#endif
            {
                // As recommended by Unity, in 2021.2 using SystemInfo.GetGraphicsFormat with DefaultFormat.LDR is
                // necessary or gamma color space texture is returned:
                // https://docs.unity3d.com/ScriptReference/Experimental.Rendering.DefaultFormat.html
                return new(camera.pixelWidth, camera.pixelHeight, SystemInfo.GetGraphicsFormat(UnityEngine.Experimental.Rendering.DefaultFormat.LDR), 0);
            }
        }

        static void SetViewProjectionMatrices(Camera camera, int passIndex)
        {
#if _XR_ENABLED
            if (!XRSettings.enabled || IsSinglePass)
            {
                return;
            }
            // Not going to use cached values here just in case.
            Display.GetRenderPass(passIndex, out var xrPass);
            xrPass.GetRenderParameter(camera, renderParameterIndex: 0, out var xrEye);
            camera.projectionMatrix = xrEye.projection;
#endif
        }

        public static void UpdatePassIndex(ref int passIndex)
        {
            if (IsRunning)
            {
#if _XR_ENABLED
                if (XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.MultiPass)
                {
                    // Alternate between left and right eye.
                    passIndex += 1;
                    passIndex %= 2;
                }
                else
                {
                    passIndex = 0;
                }
#endif
            }
            else
            {
                passIndex = -1;
            }
        }

        public static void SetInverseViewProjectionMatrix(Camera camera)
        {
            // Have to set these explicitly as the built-in transforms aren't in world-space for the blit function.
            if (camera.stereoEnabled && IsSinglePass)
            {
                Shader.SetGlobalMatrix(ShaderIDs.s_InverseViewProjection, LeftInverseViewProjectionMatrixGPU);
                Shader.SetGlobalMatrix(ShaderIDs.s_InverseViewProjectionRight, RightInverseViewProjectionMatrixGPU);
            }
            else
            {
                Shader.SetGlobalMatrix(ShaderIDs.s_InverseViewProjection, LeftInverseViewProjectionMatrixGPU);
            }
        }

        public static void Update(Camera camera)
        {
#if _XR_ENABLED
            SubsystemManager.GetSubsystems(s_DisplayList);
#endif

            if (!camera.stereoEnabled || !IsSinglePass)
            {
                // Built-in renderer does not provide these matrices.
                LeftInverseViewProjectionMatrixGPU = (GL.GetGPUProjectionMatrix(camera.projectionMatrix, false) * camera.worldToCameraMatrix).inverse;
                return;
            }

#if _XR_ENABLED
            // XR SPI only has one pass by definition.
            Display.GetRenderPass(renderPassIndex: 0, out var xrPass);
            // Grab left and right eye.
            xrPass.GetRenderParameter(camera, renderParameterIndex: 0, out var xrLeftEye);
            xrPass.GetRenderParameter(camera, renderParameterIndex: 1, out var xrRightEye);
            // Store all the matrices.
            LeftEyeViewMatrix = xrLeftEye.view;
            RightEyeViewMatrix = xrRightEye.view;
            LeftEyeProjectionMatrix = xrLeftEye.projection;
            RightEyeProjectionMatrix = xrRightEye.projection;
            LeftInverseViewProjectionMatrixGPU = (GL.GetGPUProjectionMatrix(LeftEyeProjectionMatrix, false) * LeftEyeViewMatrix).inverse;
            RightInverseViewProjectionMatrixGPU = (GL.GetGPUProjectionMatrix(RightEyeProjectionMatrix, false) * RightEyeViewMatrix).inverse;
#endif
        }
    }
}
