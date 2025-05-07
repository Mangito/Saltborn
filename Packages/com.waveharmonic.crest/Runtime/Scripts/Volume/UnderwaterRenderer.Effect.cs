// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    partial class UnderwaterRenderer
    {
        const string k_KeywordFullScreenEffect = "_FULL_SCREEN_EFFECT";
        const string k_KeywordDebugVisualizeMask = "_DEBUG_VISUALIZE_MASK";
        const string k_KeywordDebugVisualizeStencil = "_DEBUG_VISUALIZE_STENCIL";
        internal const string k_KeywordUnderwaterObjects = "CREST_UNDERWATER_OBJECTS_PASS";

        static partial class ShaderIDs
        {
            // Local
            public static readonly int s_HorizonNormal = Shader.PropertyToID("_Crest_HorizonNormal");

            // Global
            public static readonly int s_CameraColorTexture = Shader.PropertyToID("_Crest_CameraColorTexture");
            public static readonly int s_WaterVolumeStencil = Shader.PropertyToID("_Crest_WaterVolumeStencil");
            public static readonly int s_AmbientLighting = Shader.PropertyToID("_Crest_AmbientLighting");
            public static readonly int s_ExtinctionMultiplier = Shader.PropertyToID("_Crest_ExtinctionMultiplier");
            public static readonly int s_UnderwaterEnvironmentalLightingWeight = Shader.PropertyToID("_Crest_UnderwaterEnvironmentalLightingWeight");

            // Built-ins
            public static readonly int s_WorldSpaceLightPos0 = Shader.PropertyToID("_WorldSpaceLightPos0");
            public static readonly int s_LightColor0 = Shader.PropertyToID("_LightColor0");
        }


        // These map to passes in the underwater shader.
        internal enum EffectPass
        {
            FullScreen,
            Reflections,
        }

        CommandBuffer _EffectCommandBuffer;
        Material _CurrentWaterMaterial;
        readonly UnderwaterSphericalHarmonicsData _SphericalHarmonicsData = new();
        System.Action<CommandBuffer> _CopyColor;

        RenderTargetIdentifier _ColorTarget = new
        (
            BuiltinRenderTextureType.CameraTarget,
            0,
            CubemapFace.Unknown,
            -1
        );
        RenderTargetIdentifier _DepthStencilTarget = new
        (
            ShaderIDs.s_WaterVolumeStencil,
            0,
            CubemapFace.Unknown,
            -1
        );
        RenderTargetIdentifier _ColorCopyTarget = new
        (
            ShaderIDs.s_CameraColorTexture,
            0,
            CubemapFace.Unknown,
            -1
        );

        sealed class UnderwaterSphericalHarmonicsData
        {
            internal Color[] _AmbientLighting = new Color[1];
            internal Vector3[] _DirectionsSH = { new(0.0f, 0.0f, 0.0f) };
        }

        void CopyColorTexture(CommandBuffer buffer)
        {
            // Use blit instead of CopyTexture as it will smooth out issues with format
            // differences which is very hard to get right for BIRP.
            buffer.Blit(BuiltinRenderTextureType.CameraTarget, _ColorCopyTarget);

            if (UseStencilBuffer)
            {
                _EffectCommandBuffer.SetRenderTarget(_ColorTarget, _DepthStencilTarget);
            }
            else
            {
                _EffectCommandBuffer.SetRenderTarget(_ColorTarget);
            }
        }

        void SetupUnderwaterEffect()
        {
            _EffectCommandBuffer ??= new()
            {
                name = "Underwater Pass",
            };

            _CopyColor ??= new(CopyColorTexture);
        }

        void OnPreRenderUnderwaterEffect(Camera camera)
        {
#if UNITY_EDITOR
            // Do not use this to prevent the mask from rendering due to portals and volumes feature.
            if (!IsFogEnabledForEditorCamera(camera))
            {
                _EffectCommandBuffer?.Clear();
                return;
            }
#endif

            var descriptor = XRHelpers.GetRenderTextureDescriptor(camera);
            descriptor.useDynamicScale = camera.allowDynamicResolution;

            // Format must be correct for CopyTexture to work. Hopefully this is good enough.
            if (camera.allowHDR && QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
                descriptor.graphicsFormat = SystemInfo.GetGraphicsFormat(_Water.LikelyFrameBufferFormat);
            }

            UpdateEffectMaterial(camera, _FirstRender);

            _EffectCommandBuffer.Clear();

            // No need to clear as Blit will overwrite everything.
            _EffectCommandBuffer.GetTemporaryRT(ShaderIDs.s_CameraColorTexture, descriptor);

            var sun = RenderSettings.sun;
            if (sun != null)
            {
                // Unity does not set up lighting for us so we will get the last value which could incorrect.
                // SetGlobalColor is just an alias for SetGlobalVector (no color space conversion like Material.SetColor):
                // https://docs.unity3d.com/2017.4/Documentation/ScriptReference/Shader.SetGlobalColor.html
                _EffectCommandBuffer.SetGlobalVector(ShaderIDs.s_LightColor0, sun.FinalColor());
                _EffectCommandBuffer.SetGlobalVector(ShaderIDs.s_WorldSpaceLightPos0, -sun.transform.forward);
            }

            // Create a separate stencil buffer context by copying the depth texture.
            if (UseStencilBuffer)
            {
                descriptor.colorFormat = RenderTextureFormat.Depth;
                descriptor.depthBufferBits = (int)k_DepthBits;
                // bindMS is necessary in this case for depth.
                descriptor.SetMSAASamples(camera);
                descriptor.bindMS = descriptor.msaaSamples > 1;

                // No need to clear as Blit will overwrite everything.
                _EffectCommandBuffer.GetTemporaryRT(ShaderIDs.s_WaterVolumeStencil, descriptor);

                // Use blit for MSAA. We should be able to use CopyTexture. Might be the following bug:
                // https://issuetracker.unity3d.com/product/unity/issues/guid/1308132
                if (Helpers.IsMSAAEnabled(camera))
                {
                    // Blit with a depth write shader to populate the depth buffer.
                    Helpers.Blit(_EffectCommandBuffer, _DepthStencilTarget, Helpers.UtilityMaterial, (int)Helpers.UtilityPass.CopyDepth);
                }
                else
                {
                    // Copy depth then clear stencil.
                    _EffectCommandBuffer.CopyTexture(BuiltinRenderTextureType.Depth, _DepthStencilTarget);
                    Helpers.Blit(_EffectCommandBuffer, _DepthStencilTarget, Helpers.UtilityMaterial, (int)Helpers.UtilityPass.ClearStencil);
                }
            }

            CopyColorTexture(_EffectCommandBuffer);

            _EffectCommandBuffer.SetGlobalTexture(ShaderIDs.s_CameraColorTexture, _ColorCopyTarget);

            ExecuteEffect(camera, _EffectCommandBuffer, _CopyColor);

            _EffectCommandBuffer.ReleaseTemporaryRT(ShaderIDs.s_CameraColorTexture);
            if (UseStencilBuffer)
            {
                _EffectCommandBuffer.ReleaseTemporaryRT(ShaderIDs.s_WaterVolumeStencil);
            }
        }

        internal void ExecuteEffect(Camera camera, CommandBuffer buffer, System.Action<CommandBuffer> copyColor, MaterialPropertyBlock properties = null)
        {
            if (camera.cameraType == CameraType.Reflection)
            {
                buffer.DrawProcedural
                (
                    Matrix4x4.identity,
                    _VolumeMaterial,
                    shaderPass: (int)EffectPass.Reflections,
                    MeshTopology.Triangles,
                    vertexCount: 3,
                    instanceCount: 1,
                    properties
                );
            }
#if d_CrestPortals
            else if (_Portals.Active && _Portals.Mode != Portals.PortalMode.Tunnel)
            {
                _Portals.RenderEffect(camera, buffer, _VolumeMaterial, copyColor, properties);
            }
#endif
            else
            {
                buffer.DrawProcedural
                (
                    Matrix4x4.identity,
                    _VolumeMaterial,
                    shaderPass: (int)EffectPass.FullScreen,
                    MeshTopology.Triangles,
                    vertexCount: 3,
                    instanceCount: 1,
                    properties
                );
            }
        }

        internal static void UpdateGlobals(Material waterMaterial)
        {
            // We will have the wrong color values if we do not use linear:
            // https://forum.unity.com/threads/fragment-shader-output-colour-has-incorrect-values-when-hardcoded.377657/

            // _CrestAbsorption is already set as global in Water Renderer.
            Shader.SetGlobalColor(WaterRenderer.ShaderIDs.s_Scattering, waterMaterial.GetColor(WaterRenderer.ShaderIDs.s_Scattering).MaybeLinear());
            Shader.SetGlobalFloat(WaterRenderer.ShaderIDs.s_Anisotropy, waterMaterial.GetFloat(WaterRenderer.ShaderIDs.s_Anisotropy));
        }

        internal void UpdateEffectMaterial(Camera camera, bool isFirstRender)
        {
            // Copy water material parameters to underwater material.
            {
                var material = _SurfaceMaterial;

                if (_CopyWaterMaterialParametersEachFrame || isFirstRender || material != _CurrentWaterMaterial)
                {
                    _CurrentWaterMaterial = material;

                    if (material != null)
                    {
                        _VolumeMaterial.CopyMatchingPropertiesFromMaterial(material);

                        if (_EnableShaderAPI)
                        {
                            UpdateGlobals(material);
                        }
                    }
                }
            }

            // Enabling/disabling keywords each frame don't seem to have large measurable overhead
            _VolumeMaterial.SetKeyword(k_KeywordDebugVisualizeMask, _Debug._VisualizeMask);
            _VolumeMaterial.SetKeyword(k_KeywordDebugVisualizeStencil, _Debug._VisualizeStencil);

            // We use this for caustics to get the displacement.
            _VolumeMaterial.SetInteger(Lod.ShaderIDs.s_LodIndex, 0);

            if (!Portaled && camera.cameraType != CameraType.Reflection)
            {
                var seaLevel = _Water.SeaLevel;

                // We don't both setting the horizon value if we know we are going to be having to apply the effect
                // full-screen anyway.
                var forceFullShader = _Water.ViewerHeightAboveWater < -2f;
                if (!forceFullShader)
                {
                    var maxWaterVerticalDisplacement = _Water.MaximumVerticalDisplacement * 0.5f;
                    var cameraYPosition = camera.transform.position.y;
                    float nearPlaneFrustumWorldHeight;
                    {
                        var current = camera.ViewportToWorldPoint(new(0f, 0f, camera.nearClipPlane)).y;
                        float maxY = current, minY = current;

                        current = camera.ViewportToWorldPoint(new(0f, 1f, camera.nearClipPlane)).y;
                        maxY = Mathf.Max(maxY, current);
                        minY = Mathf.Min(minY, current);

                        current = camera.ViewportToWorldPoint(new(1f, 0f, camera.nearClipPlane)).y;
                        maxY = Mathf.Max(maxY, current);
                        minY = Mathf.Min(minY, current);

                        current = camera.ViewportToWorldPoint(new(1f, 1f, camera.nearClipPlane)).y;
                        maxY = Mathf.Max(maxY, current);
                        minY = Mathf.Min(minY, current);

                        nearPlaneFrustumWorldHeight = maxY - minY;
                    }

                    forceFullShader = (cameraYPosition + nearPlaneFrustumWorldHeight + maxWaterVerticalDisplacement) <= seaLevel;
                }

                _VolumeMaterial.SetKeyword(k_KeywordFullScreenEffect, forceFullShader);
            }

            // Project water normal onto camera plane.
            {
                var projectedNormal = new Vector2
                (
                    Vector3.Dot(Vector3.up, camera.transform.right),
                    Vector3.Dot(Vector3.up, camera.transform.up)
                );

                _VolumeMaterial.SetVector(ShaderIDs.s_HorizonNormal, projectedNormal);
            }

            // Compute ambient lighting SH.
            {
                // We could pass in a renderer which would prime this lookup. However it doesnt make sense to use an existing render
                // at different position, as this would then thrash it and negate the priming functionality. We could create a dummy invis GO
                // with a dummy Renderer which might be enough, but this is hacky enough that we'll wait for it to become a problem
                // rather than add a pre-emptive hack.
                UnityEngine.Profiling.Profiler.BeginSample("Crest: Underwater Sample Spherical Harmonics");
                LightProbes.GetInterpolatedProbe(camera.transform.position, null, out var sphericalHarmonicsL2);
                sphericalHarmonicsL2.Evaluate(_SphericalHarmonicsData._DirectionsSH, _SphericalHarmonicsData._AmbientLighting);
                Helpers.SetShaderVector(_VolumeMaterial, ShaderIDs.s_AmbientLighting, _SphericalHarmonicsData._AmbientLighting[0], _EnableShaderAPI);
                UnityEngine.Profiling.Profiler.EndSample();
            }
        }
    }
}
