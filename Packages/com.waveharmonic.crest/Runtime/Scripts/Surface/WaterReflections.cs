// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// This script originated from the unity standard assets. It has been modified heavily to be camera-centric (as opposed to
// geometry-centric) and assumes a single main camera which simplifies the code.

using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.Universal;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// What side of the water surface to render planar reflections for.
    /// </summary>
    public enum WaterReflectionSide
    {
        /// <summary>
        /// Both sides. Most expensive.
        /// </summary>
        Both,

        /// <summary>
        /// Above only. Typical for planar reflections.
        /// </summary>
        Above,

        /// <summary>
        /// Below only. For total internal reflections.
        /// </summary>
        Below,
    }

    /// <summary>
    /// Renders reflections for water. Currently on planar reflections.
    /// </summary>
    [Serializable]
    public sealed partial class WaterReflections
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        [@Space(10)]

        [@Label("Enable")]
        [Tooltip("Whether planar reflections are enabled.\n\nAllocates/releases resources if state has changed.")]
        [@GenerateAPI(Setter.Custom)]
        [@DecoratedField, SerializeField]
        internal bool _Enabled;


        [@Heading("Capture")]

        [Tooltip("What side of the water surface to render planar reflections for.")]
        [@GenerateAPI(name: "ReflectionSide")]
        [@DecoratedField, SerializeField]
        internal WaterReflectionSide _Mode = WaterReflectionSide.Above;

        [Tooltip("The layers to rendering into reflections.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        LayerMask _Layers = 1; // Default

        [Tooltip("Resolution of the reflection texture.")]
        [@GenerateAPI]
        [@Delayed, SerializeField]
        int _Resolution = 256;

        [Tooltip("Whether to render to the viewer camera only.\n\nWhen disabled, reflections will render for all cameras rendering the water layer, which currently this prevents Refresh Rate from working. Enabling will unlock the Refresh Rate heading.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal bool _RenderOnlySingleCamera;

        [@Space(10)]

        [Tooltip("Whether to render the sky or fallback to default reflections.\n\nNot rendering the sky can prevent other custom shaders (like tree leaves) from being in the final output. Enable for best compatibility.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal bool _Sky = true;

        [Tooltip("Disables pixel lights (BIRP only).")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _DisablePixelLights = true;

#pragma warning disable 414
        [Tooltip("Disables shadows.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _DisableShadows = true;
#pragma warning restore 414

        [Tooltip("Whether to allow HDR.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _HDR = true;

        [Tooltip("Whether to allow stencil operations.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _Stencil = false;

        [Tooltip("Whether to allow MSAA.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _AllowMSAA = false;

        [@Space(10)]

        [Tooltip("Overrides global quality settings.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        QualitySettingsOverride _QualitySettingsOverride = new()
        {
            _OverrideLodBias = false,
            _LodBias = 0.5f,
            _OverrideMaximumLodLevel = false,
            _MaximumLodLevel = 1,
            _OverrideTerrainPixelError = false,
            _TerrainPixelError = 10,
        };

        [@Heading("Culling")]

        [Tooltip("The near clip plane clips any geometry before it, removing it from reflections.\n\nCan be used to reduce reflection leaks and support varied water level.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _ClipPlaneOffset;

        [Tooltip("Anything beyond the far clip plane is not rendered.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _FarClipPlane = 1000;

        [Tooltip("Disables occlusion culling.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _DisableOcclusionCulling = true;


        [@Heading("Refresh Rate")]

        [Tooltip("Refresh reflection every x frames (one is every frame)")]
        [@Predicated(nameof(_RenderOnlySingleCamera))]
        [@DecoratedField, SerializeField]
        int _RefreshPerFrames = 1;

        [@Predicated(nameof(_RenderOnlySingleCamera))]
        [@DecoratedField, SerializeField]
        int _FrameRefreshOffset = 0;


        [@Heading("Oblique Matrix")]

        [@Label("Enable")]
        [Tooltip("An oblique matrix will clip anything below the surface for free.\n\nDisable if you have problems with certain effects. Disabling can cause other artifacts like objects below the surface to appear in reflections.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _UseObliqueMatrix = true;

        [Tooltip("Planar relfections using an oblique frustum for better performance.\n\nThis can cause depth issues for TIRs, especially near the surface.")]
        [@Predicated(nameof(_UseObliqueMatrix))]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _NonObliqueNearSurface;

        [Tooltip("If within this distance from the surface, disable the oblique matrix.")]
        [@Predicated(nameof(_NonObliqueNearSurface))]
        [@Predicated(nameof(_UseObliqueMatrix))]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _NonObliqueNearSurfaceThreshold = 0.05f;


        [@Space(10)]

        [@DecoratedField, SerializeField]
        DebugFields _Debug = new();

        [Serializable]
        sealed class DebugFields
        {
            [@DecoratedField, SerializeField]
            internal bool _ShowHiddenObjects;

            [Tooltip("Rendering reflections per-camera requires recursive rendering. Check this toggle if experiencing issues. The other downside without it is a one-frame delay.")]
            [@DecoratedField, SerializeField]
            internal bool _DisableRecursiveRendering;
        }


        /// <summary>
        /// What side of the water surface to render planar reflections for.
        /// </summary>
        public WaterReflectionSide Mode { get => _Mode; set => _Mode = value; }


        static class ShaderIDs
        {
            public static int s_ReflectionTexture = Shader.PropertyToID("_Crest_ReflectionTexture");
            public static int s_ReflectionPositionNormal = Shader.PropertyToID("_Crest_ReflectionPositionNormal");
        }

        // Checked in underwater to filter cameras.
        internal static Camera CurrentCamera { get; private set; }

        internal WaterRenderer _Water;
        internal UnderwaterRenderer _UnderWater;

        RenderTexture _ReflectionTexture;
        internal RenderTexture ReflectionTexture => _ReflectionTexture;
        readonly Vector4[] _ReflectionPositionNormal = new Vector4[2];

        Camera _CameraViewpoint;
        Skybox _CameraViewpointSkybox;
        Camera _CameraReflections;
        Skybox _CameraReflectionsSkybox;

        int RefreshPerFrames => _RenderOnlySingleCamera ? _RefreshPerFrames : 1;
        long _LastRefreshOnFrame = -1;

        internal bool SupportsRecursiveRendering =>
#if !UNITY_6000_0_OR_NEWER
            // HDRP cannot recursive render for 2022.
            !RenderPipelineHelper.IsHighDefinition &&
#endif
            !_Debug._DisableRecursiveRendering;

        readonly float[] _CullDistances = new float[32];

        /// <summary>
        /// Invoked when the reflection camera is created.
        /// </summary>
        public static Action<Camera> OnCameraAdded { get; set; }

        internal void OnEnable()
        {
            _CameraViewpoint = _Water.Viewer;
            _CameraViewpointSkybox = _CameraViewpoint.GetComponent<Skybox>();

            // This is called also called every frame, but was required here as there was a
            // black reflection for a frame without this earlier setup call.
            CreateWaterObjects(_CameraViewpoint);
        }

        internal void OnDisable()
        {
            Shader.SetGlobalTexture(ShaderIDs.s_ReflectionTexture, Texture2D.blackTexture);
        }

        internal void OnDestroy()
        {
            if (_CameraReflections)
            {
                Helpers.Destroy(_CameraReflections.gameObject);
                _CameraReflections = null;
            }

            if (_ReflectionTexture)
            {
                _ReflectionTexture.Release();
                Helpers.Destroy(_ReflectionTexture);
                _ReflectionTexture = null;
            }
        }

        internal void OnPreRenderCamera(Camera camera)
        {
            if (SupportsRecursiveRendering)
            {
                if (camera.cameraType is CameraType.Preview or CameraType.Reflection)
                {
                    return;
                }

                if (!Helpers.MaskIncludesLayer(camera.cullingMask, _Water.Layer))
                {
                    return;
                }

                if (_RenderOnlySingleCamera && camera != _Water.Viewer)
                {
                    return;
                }

                _CameraViewpoint = camera;
                LateUpdate();
            }

            if (camera == _CameraViewpoint)
            {
                // TODO: Emit an event instead so WBs can listen.
                Shader.SetGlobalTexture(ShaderIDs.s_ReflectionTexture, _ReflectionTexture);
            }
        }

        internal void OnAfterCameraRender(Camera camera)
        {
            if (SupportsRecursiveRendering && !_RenderOnlySingleCamera) return;
            Shader.SetGlobalTexture(ShaderIDs.s_ReflectionTexture, Texture2D.blackTexture);
        }

        internal void LateUpdate()
        {
            // Frame rate limiter.
            if (_LastRefreshOnFrame > 0 && RefreshPerFrames > 1)
            {
                // Check whether we need to refresh the frame.
                if (Math.Abs(_FrameRefreshOffset) % _RefreshPerFrames != Time.renderedFrameCount % _RefreshPerFrames)
                {
                    return;
                }
            }

            if (_Water == null)
            {
                return;
            }

            if (!SupportsRecursiveRendering)
            {
                _CameraViewpoint = _Water.Viewer;
            }

            if (_CameraViewpoint == null)
            {
                return;
            }

#if UNITY_EDITOR
            // Fix "Screen position out of view frustum" when 2D view activated.
            {
                var sceneView = UnityEditor.SceneView.lastActiveSceneView;
                if (sceneView != null && sceneView.in2DMode && sceneView.camera == _CameraViewpoint)
                {
                    return;
                }
            }
#endif

            CreateWaterObjects(_CameraViewpoint);

            if (!_CameraReflections)
            {
                return;
            }

            UpdateCameraModes();
            ForceDistanceCulling(_FarClipPlane);

            _CameraReflections.targetTexture = _ReflectionTexture;

            // TODO: Do not do this every frame.
            if (_Mode != WaterReflectionSide.Both)
            {
                Helpers.ClearRenderTexture(_ReflectionTexture, Color.clear, depth: false);
            }

            // We do not want the water plane when rendering planar reflections.
            _Water.Root.gameObject.SetActive(false);

            CurrentCamera = _CameraReflections;

            // Optionally disable pixel lights for reflection/refraction
            var oldPixelLightCount = QualitySettings.pixelLightCount;
            if (_DisablePixelLights)
            {
                QualitySettings.pixelLightCount = 0;
            }

            // Optionally disable shadows.
            var oldShadowQuality = QualitySettings.shadows;
            if (_DisableShadows)
            {
                QualitySettings.shadows = UnityEngine.ShadowQuality.Disable;
            }

            _QualitySettingsOverride.Override();

            // Invert culling because view is mirrored. Does not work for HDRP (handled elsewhere).
            var oldCulling = GL.invertCulling;
            GL.invertCulling = !oldCulling;

#if UNITY_EDITOR
            try
#endif
            {
                Render();
            }
#if UNITY_EDITOR
            // Ensure that any global settings are restored.
            finally
#endif
            {
                GL.invertCulling = oldCulling;

                // Restore shadows.
                if (_DisableShadows)
                {
                    QualitySettings.shadows = oldShadowQuality;
                }

                // Restore pixel light count
                if (_DisablePixelLights)
                {
                    QualitySettings.pixelLightCount = oldPixelLightCount;
                }

                _QualitySettingsOverride.Restore();

                CurrentCamera = null;
                _Water.Root.gameObject.SetActive(true);

                // Remember this frame as last refreshed.
                _LastRefreshOnFrame = Time.renderedFrameCount;
            }
        }

        void Render()
        {
            var descriptor = _ReflectionTexture.descriptor;
            descriptor.dimension = TextureDimension.Tex2D;
            descriptor.volumeDepth = 1;
            descriptor.useMipMap = false;
            // No need to clear, as camera clears using the skybox.
            var target = RenderTexture.GetTemporary(descriptor);
            _CameraReflections.targetTexture = target;

            if (_Mode != WaterReflectionSide.Below)
            {
                _ReflectionPositionNormal[0] = ComputeHorizonPositionAndNormal(_CameraReflections, _Water.SeaLevel, 0.05f, false);

                if (_UnderWater._Enabled)
                {
                    // Disable underwater layer. It is the only way to exclude probes.
                    _CameraReflections.cullingMask = _Layers & ~(1 << _UnderWater.Layer);
                }

                RenderCamera(_CameraReflections, Vector3.up, false);
                Graphics.CopyTexture(target, 0, 0, _ReflectionTexture, 0, 0);

                _CameraReflections.ResetProjectionMatrix();
            }

            if (_Mode != WaterReflectionSide.Above)
            {
                _ReflectionPositionNormal[1] = ComputeHorizonPositionAndNormal(_CameraReflections, _Water.SeaLevel, -0.05f, true);

                if (_UnderWater._Enabled)
                {
                    // Enable underwater layer.
                    _CameraReflections.cullingMask = _Layers | (1 << _UnderWater.Layer);
                    // We need the depth texture for underwater.
                    _CameraReflections.depthTextureMode = DepthTextureMode.Depth;
                }

                RenderCamera(_CameraReflections, Vector3.down, _NonObliqueNearSurface);
                Graphics.CopyTexture(target, 0, 0, _ReflectionTexture, 1, 0);

                _CameraReflections.ResetProjectionMatrix();
            }


            RenderTexture.ReleaseTemporary(target);

            _ReflectionTexture.GenerateMips();

            Shader.SetGlobalVectorArray(ShaderIDs.s_ReflectionPositionNormal, _ReflectionPositionNormal);
        }

        void RenderCamera(Camera camera, Vector3 planeNormal, bool nonObliqueNearSurface)
        {
            // Find out the reflection plane: position and normal in world space
            var planePosition = _Water.Root.position;

            var offset = _ClipPlaneOffset;
            {
                var viewpoint = _CameraViewpoint.transform;
                if (offset == 0f && viewpoint.position.y == 0f && viewpoint.rotation.eulerAngles.y == 0f)
                {
                    // Minor offset to prevent "Screen position out of view frustum". Smallest number
                    // to work with both above and below. Smallest number to work with both above and
                    // below. Could be BIRP only.
                    offset = 0.00001f;
                }
            }

            // Reflect camera around reflection plane
            var distance = -Vector3.Dot(planeNormal, planePosition) - offset;
            var reflectionPlane = new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, distance);

            var reflection = Matrix4x4.zero;
            CalculateReflectionMatrix(ref reflection, reflectionPlane);

            camera.worldToCameraMatrix = _CameraViewpoint.worldToCameraMatrix * reflection;

            // Setup oblique projection matrix so that near plane is our reflection
            // plane. This way we clip everything below/above it for free.
            var clipPlane = CameraSpacePlane(camera, planePosition, planeNormal, 1.0f);

            if (_UseObliqueMatrix && (!nonObliqueNearSurface || Mathf.Abs(_CameraViewpoint.transform.position.y - planePosition.y) > _NonObliqueNearSurfaceThreshold))
            {
                camera.projectionMatrix = _CameraViewpoint.CalculateObliqueMatrix(clipPlane);
            }

            // Set custom culling matrix from the current camera
            camera.cullingMatrix = _CameraViewpoint.projectionMatrix * _CameraViewpoint.worldToCameraMatrix;

            camera.transform.position = reflection.MultiplyPoint(_CameraViewpoint.transform.position);
            var euler = _CameraViewpoint.transform.eulerAngles;
            camera.transform.eulerAngles = new(-euler.x, euler.y, euler.z);
            camera.cullingMatrix = camera.projectionMatrix * camera.worldToCameraMatrix;

            if (SupportsRecursiveRendering)
            {
                Helpers.RenderCamera(camera, _Water._Context);
            }
            else
            {
                camera.Render();
            }
        }

        /// <summary>
        /// Limit render distance for reflection camera for first 32 layers
        /// </summary>
        /// <param name="farClipPlane">reflection far clip distance</param>
        void ForceDistanceCulling(float farClipPlane)
        {
            // Cannot use spherical culling with SRPs. Will error.
            if (!RenderPipelineHelper.IsLegacy)
            {
                return;
            }

            for (var i = 0; i < _CullDistances.Length; i++)
            {
                // The culling distance
                _CullDistances[i] = farClipPlane;
            }
            _CameraReflections.layerCullDistances = _CullDistances;
            _CameraReflections.layerCullSpherical = true;
        }

        void UpdateCameraModes()
        {
#if d_UnityHDRP
            if (RenderPipelineHelper.IsHighDefinition)
            {
                if (_CameraReflections.TryGetComponent(out HDAdditionalCameraData additionalCameraData))
                {
                    additionalCameraData.clearColorMode = _Sky ? HDAdditionalCameraData.ClearColorMode.Sky :
                        HDAdditionalCameraData.ClearColorMode.Color;
                }
            }
            else
#endif
            {
                _CameraReflections.clearFlags = _Sky ? CameraClearFlags.Skybox : CameraClearFlags.Color;

                if (_Sky && _CameraViewpoint.TryGetComponent(out _CameraViewpointSkybox))
                {
                    if (_CameraReflectionsSkybox == null)
                    {
                        _CameraReflectionsSkybox = _CameraReflections.gameObject.AddComponent<Skybox>();
                    }

                    _CameraReflectionsSkybox.enabled = _CameraViewpointSkybox.enabled;
                    _CameraReflectionsSkybox.material = _CameraViewpointSkybox.material;
                }
                else
                {
                    // Destroy otherwise skybox will not render if empty.
                    Helpers.Destroy(_CameraViewpointSkybox);
                }
            }

            // Update other values to match current camera.
            // Even if we are supplying custom camera&projection matrices,
            // some of values are used elsewhere (e.g. skybox uses far plane).

            _CameraReflections.farClipPlane = _CameraViewpoint.farClipPlane;
            _CameraReflections.nearClipPlane = _CameraViewpoint.nearClipPlane;
            _CameraReflections.orthographic = _CameraViewpoint.orthographic;
            _CameraReflections.fieldOfView = _CameraViewpoint.fieldOfView;
            _CameraReflections.orthographicSize = _CameraViewpoint.orthographicSize;
            _CameraReflections.allowMSAA = _AllowMSAA;
            _CameraReflections.aspect = _CameraViewpoint.aspect;
            _CameraReflections.useOcclusionCulling = !_DisableOcclusionCulling && _CameraViewpoint.useOcclusionCulling;
            _CameraReflections.depthTextureMode = _CameraViewpoint.depthTextureMode;
        }

        // On-demand create any objects we need for water
        void CreateWaterObjects(Camera currentCamera)
        {
            var format = _HDR ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32;
            var stencil = _Stencil ? 24 : 16;

            // Reflection render texture
            if (!_ReflectionTexture || _ReflectionTexture.width != _Resolution || _ReflectionTexture.format != format || _ReflectionTexture.depth != stencil)
            {
                if (_ReflectionTexture)
                {
                    Helpers.Destroy(_ReflectionTexture);
                }

                Debug.Assert(SystemInfo.SupportsRenderTextureFormat(format), "Crest: The graphics device does not support the render texture format " + format.ToString());
                _ReflectionTexture = new(_Resolution, _Resolution, stencil, format)
                {
                    name = "_Crest_WaterReflection",
                    isPowerOfTwo = true,
                    dimension = TextureDimension.Tex2DArray,
                    volumeDepth = 2,
                    useMipMap = true,
                    autoGenerateMips = false,
                    filterMode = FilterMode.Trilinear,
                };
                _ReflectionTexture.Create();
            }

            // Camera for reflection
            if (!_CameraReflections)
            {
                var go = new GameObject("_Crest_WaterReflectionCamera");
                go.transform.SetParent(_Water.Container.transform, worldPositionStays: true);
                _CameraReflections = go.AddComponent<Camera>();
                _CameraReflections.enabled = false;
                _CameraReflections.cullingMask = _Layers;
                _CameraReflections.cameraType = CameraType.Reflection;
                _CameraReflections.backgroundColor = Color.clear;

                if (RenderPipelineHelper.IsLegacy)
                {
                    _CameraReflections.gameObject.AddComponent<FlareLayer>();
                }

#if d_UnityHDRP
                if (RenderPipelineHelper.IsHighDefinition)
                {
                    var additionalCameraData = _CameraReflections.gameObject.AddComponent<HDAdditionalCameraData>();
                    additionalCameraData.invertFaceCulling = true;
                    additionalCameraData.defaultFrameSettings = FrameSettingsRenderType.RealtimeReflection;
                    additionalCameraData.backgroundColorHDR = Color.clear;
                    additionalCameraData.customRenderingSettings = true;
                    additionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.CustomPass] = true;
                    additionalCameraData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.CustomPass, true);
                }
#endif

#if d_UnityURP
                if (RenderPipelineHelper.IsUniversal)
                {
                    var additionalCameraData = _CameraReflections.gameObject.AddComponent<UniversalAdditionalCameraData>();
                    additionalCameraData.renderShadows = !_DisableShadows;
                    additionalCameraData.requiresColorTexture = false;
                    additionalCameraData.requiresDepthTexture = false;
                }
#endif
                OnCameraAdded?.Invoke(_CameraReflections);
            }

            _CameraReflections.gameObject.hideFlags = _Debug._ShowHiddenObjects ? HideFlags.DontSave : HideFlags.HideAndDontSave;
        }

        // Given position/normal of the plane, calculates plane in camera space.
        Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign)
        {
            var offset = _ClipPlaneOffset;
            {
                var viewpoint = _CameraViewpoint.transform;
                if (offset == 0f && viewpoint.position.y == 0f && viewpoint.rotation.eulerAngles.y == 0f)
                {
                    // Minor offset to prevent "Screen position out of view frustum". Smallest number
                    // to work with both above and below. Smallest number to work with both above and
                    // below. Could be BIRP only.
                    offset = 0.00001f;
                }
            }

            var offsetPos = pos + normal * offset;
            var m = cam.worldToCameraMatrix;
            var cpos = m.MultiplyPoint(offsetPos);
            var cnormal = m.MultiplyVector(normal).normalized * sideSign;
            return new(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
        }

        // Calculates reflection matrix around the given plane
        static void CalculateReflectionMatrix(ref Matrix4x4 reflectionMat, Vector4 plane)
        {
            reflectionMat.m00 = 1F - 2F * plane[0] * plane[0];
            reflectionMat.m01 = -2F * plane[0] * plane[1];
            reflectionMat.m02 = -2F * plane[0] * plane[2];
            reflectionMat.m03 = -2F * plane[3] * plane[0];

            reflectionMat.m10 = -2F * plane[1] * plane[0];
            reflectionMat.m11 = 1F - 2F * plane[1] * plane[1];
            reflectionMat.m12 = -2F * plane[1] * plane[2];
            reflectionMat.m13 = -2F * plane[3] * plane[1];

            reflectionMat.m20 = -2F * plane[2] * plane[0];
            reflectionMat.m21 = -2F * plane[2] * plane[1];
            reflectionMat.m22 = 1F - 2F * plane[2] * plane[2];
            reflectionMat.m23 = -2F * plane[3] * plane[2];

            reflectionMat.m30 = 0F;
            reflectionMat.m31 = 0F;
            reflectionMat.m32 = 0F;
            reflectionMat.m33 = 1F;
        }

        /// <summary>
        /// Compute intersection between the frustum far plane and given plane, and return view space
        /// position and normal for this horizon line.
        /// </summary>
        static Vector4 ComputeHorizonPositionAndNormal(Camera camera, float positionY, float offset, bool flipped)
        {
            var position = Vector2.zero;
            var normal = Vector2.zero;

            // Set up back points of frustum.
            var positionNDC = new NativeArray<Vector3>(4, Allocator.Temp);
            var positionWS = new NativeArray<Vector3>(4, Allocator.Temp);
            try
            {

                var farPlane = camera.farClipPlane;
                positionNDC[0] = new(0f, 0f, farPlane);
                positionNDC[1] = new(0f, 1f, farPlane);
                positionNDC[2] = new(1f, 1f, farPlane);
                positionNDC[3] = new(1f, 0f, farPlane);

                // Project out to world.
                for (var i = 0; i < positionWS.Length; i++)
                {
                    // Eye parameter works for BIRP. With it we could skip setting matrices.
                    // In HDRP it doesn't work for XR MP. And completely breaks horizon in XR SPI.
                    positionWS[i] = camera.ViewportToWorldPoint(positionNDC[i]);
                }

                var intersectionsScreen = new NativeArray<Vector2>(2, Allocator.Temp);
                // This is only used to disambiguate the normal later. Could be removed if we were
                // more careful with point order/indices below.
                var intersectionsWorld = new NativeArray<Vector3>(2, Allocator.Temp);
                try
                {
                    var count = 0;

                    // Iterate over each back point
                    for (var i = 0; i < 4; i++)
                    {
                        // Get next back point, to obtain line segment between them.
                        var next = (i + 1) % 4;

                        // See if one point is above and one point is below sea level - then sign of the two differences
                        // will be different, and multiplying them will give a negative.
                        if ((positionWS[i].y - positionY) * (positionWS[next].y - positionY) < 0f)
                        {
                            // Proportion along line segment where intersection occurs.
                            var proportion = Mathf.Abs((positionY - positionWS[i].y) / (positionWS[next].y - positionWS[i].y));
                            intersectionsScreen[count] = Vector2.Lerp(positionNDC[i], positionNDC[next], proportion);
                            intersectionsWorld[count] = Vector3.Lerp(positionWS[i], positionWS[next], proportion);

                            count++;
                        }
                    }

                    // Two distinct results - far plane intersects water.
                    if (count == 2)
                    {
                        position = intersectionsScreen[0];
                        var tangent = intersectionsScreen[0] - intersectionsScreen[1];
                        normal.x = -tangent.y;
                        normal.y = tangent.x;

                        // Disambiguate the normal. The tangent normal might go from left to right or right
                        // to left since we do not handle ordering of intersection points.
                        if (Vector3.Dot(intersectionsWorld[0] - intersectionsWorld[1], camera.transform.right) > 0f)
                        {
                            normal = -normal;
                        }

                        // Invert the normal if camera is upside down.
                        if (camera.transform.up.y <= 0f)
                        {
                            normal = -normal;
                        }

                        // The above will sometimes produce a normal that is inverted around 90° along the
                        // Z axis. Here we are using world up to make sure that water is world down.
                        {
                            var cameraFacing = Vector3.Dot(camera.transform.right, Vector3.up);
                            var normalFacing = Vector2.Dot(normal, Vector2.right);

                            if (cameraFacing > 0.75f && normalFacing > 0.9f)
                            {
                                normal = -normal;
                            }
                            else if (cameraFacing < -0.75f && normalFacing < -0.9f)
                            {
                                normal = -normal;
                            }
                        }

                        // Minor offset helps.
                        position += normal.normalized * offset;
                    }
                }
                finally
                {
                    intersectionsScreen.Dispose();
                    intersectionsWorld.Dispose();
                }
            }
            finally
            {
                positionNDC.Dispose();
                positionWS.Dispose();
            }

            if (flipped)
            {
                normal = -normal;
            }

            return new(position.x, position.y, normal.x, normal.y);
        }

        void SetEnabled(bool previous, bool current)
        {
            if (previous == current) return;
            if (_Water == null || !_Water.isActiveAndEnabled) return;
            if (_Enabled) OnEnable(); else OnDisable();
        }

#if UNITY_EDITOR
        [@OnChange]
        void OnChange(string propertyPath, object previousValue)
        {
            switch (propertyPath)
            {
                case nameof(_Enabled):
                    SetEnabled((bool)previousValue, _Enabled);
                    break;
            }
        }
#endif
    }
}
