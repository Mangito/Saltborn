// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#if d_UnityHDRP

using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace WaveHarmonic.Crest
{
    sealed class SampleShadowsHDRP : CustomPass
    {
        static SampleShadowsHDRP s_Instance;
        static readonly string s_Name = "Sample Shadows";

        // These values come from unity_MatrxVP value in the frame debugger. unity_MatrxVP is marked as legacy and
        // breaks XR SPI. It is defined in:
        // "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/EditorShaderVariables.hlsl"
        static readonly Matrix4x4 s_Matrix = new
        (
            new(2f, 0f, 0f, 0f),
            new(0f, -2f, 0f, 0f),
            new(0f, 0f, 0.00990099f, 0f),
            new(-1f, 1f, 0.990099f, 1f)
        );

        static class ShaderIDs
        {
            public static readonly int s_ViewProjectionMatrix = Shader.PropertyToID("_Crest_ViewProjectionMatrix");
        }

        GameObject _GameObject;
        int _XrTargetEyeIndex = -1;

        protected override void Execute(CustomPassContext context)
        {
            var water = WaterRenderer.Instance;

            if (water == null || !water._ShadowLod.Enabled)
            {
                return;
            }

#if UNITY_EDITOR
            if (!WaterRenderer.IsWithinEditorUpdate || EditorApplication.isPaused)
            {
                return;
            }
#endif

            var camera = context.hdCamera.camera;

            // Custom passes execute for every camera. We only support one camera for now.
            if (!ReferenceEquals(camera, water.Viewer)) return;
            // TODO: bail when not executing for main light or when no main light exists?
            // if (renderingData.lightData.mainLightIndex == -1) return;

            camera.TryGetComponent<HDAdditionalCameraData>(out var cameraData);

            if (cameraData != null && cameraData.xrRendering)
            {
                XRHelpers.UpdatePassIndex(ref _XrTargetEyeIndex);

                // Skip the right eye as data is not stereo.
                if (_XrTargetEyeIndex == 1)
                {
                    return;
                }
            }

            // Disable for XR SPI otherwise input will not have correct world position.
            if (cameraData != null && cameraData.xrRendering && XRHelpers.IsSinglePass)
            {
                context.cmd.DisableShaderKeyword("STEREO_INSTANCING_ON");
            }

            // We cannot seem to override this matrix so a reference manually.
            context.cmd.SetGlobalMatrix(ShaderIDs.s_ViewProjectionMatrix, s_Matrix);
            water._ShadowLod.BuildCommandBuffer(water, context.cmd);

            // Restore matrices otherwise remaining render will have incorrect matrices. Each pass is responsible for
            // restoring matrices if required.
            context.cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);

            // Restore XR SPI as we cannot rely on remaining pipeline to do it for us.
            if (cameraData != null && cameraData.xrRendering && XRHelpers.IsSinglePass)
            {
                context.cmd.EnableShaderKeyword("STEREO_INSTANCING_ON");
            }
        }

        internal static void Enable(WaterRenderer water)
        {
            var gameObject = CustomPassHelpers.CreateOrUpdate
            (
                parent: water.Container.transform,
                s_Name,
                hide: !water._Debug._ShowHiddenObjects
            );

            CustomPassHelpers.CreateOrUpdate
            (
                gameObject,
                ref s_Instance,
                s_Name,
                CustomPassInjectionPoint.BeforeTransparent
            );

            s_Instance._GameObject = gameObject;
        }

        internal static void Disable()
        {
            // It should be safe to rely on this reference for this reference to fail.
            if (s_Instance != null && s_Instance._GameObject != null)
            {
                // Will also trigger Cleanup below.
                s_Instance._GameObject.SetActive(false);
            }
        }
    }
}

#endif // d_UnityHDRP
