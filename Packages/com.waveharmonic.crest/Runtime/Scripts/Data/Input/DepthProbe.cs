// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.Universal;
using WaveHarmonic.Crest.Internal;
using WaveHarmonic.Crest.Utility;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// <see cref="DepthProbe"/>'s update mode.
    /// </summary>
    public enum DepthProbeMode
    {
        /// <summary>
        /// <see cref="DepthProbe"/> is updating in real-time, in accordance to <see cref="DepthProbeRefreshMode"/>.
        /// </summary>
        RealTime,

        /// <summary>
        /// <see cref="DepthProbe"/> is baked in the Editor.
        /// </summary>
        Baked,
    }

    /// <summary>
    /// How the <see cref="DepthProbe"/> refreshes when using <see cref="DepthProbeMode.RealTime"/>.
    /// </summary>
    public enum DepthProbeRefreshMode
    {
        /// <summary>
        /// Populates the <see cref="DepthProbe"/> in Start.
        /// </summary>
        OnStart = 0,

        // EveryFrame = 1,

        /// <summary>
        /// Requires manual updating via <see cref="DepthProbe.Populate"/>.
        /// </summary>
        ViaScripting = 2,
    }

    /// <summary>
    /// How a component is placed in the world.
    /// </summary>
    public enum Placement
    {
        /// <summary>
        /// The component is in a fixed position.
        /// </summary>
        Fixed,

        /// <summary>
        /// The component follows the transform.
        /// </summary>
        Transform,

        /// <summary>
        /// The component follows the viewpoint.
        /// </summary>
        Viewpoint,
    }

    /// <summary>
    /// Captures scene height / water depth, and renders it into the simulation.
    /// </summary>
    /// <remarks>
    /// Caches the operation to avoid rendering it every frame. This should be used for
    /// static geometry, dynamic objects should be tagged with the
    /// <see cref="DepthLodInput"/> component.
    /// </remarks>
    [@ExecuteDuringEditMode]
    [@HelpURL("Manual/ShallowsAndShorelines.html")]
    [AddComponentMenu(Constants.k_MenuPrefixInputs + "Depth Probe")]
    public sealed partial class DepthProbe : ManagedBehaviour<WaterRenderer>
    {
        [Tooltip("Specifies the setup for this probe.")]
        [@GenerateAPI]
        [SerializeField]
        internal DepthProbeMode _Type = DepthProbeMode.RealTime;

        [Tooltip("Controls how the probe is refreshed in the Player.\n\nCall Populate() if scripting.")]
        [@Predicated(nameof(_Type), inverted: true, nameof(DepthProbeMode.RealTime), hide: true)]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal DepthProbeRefreshMode _RefreshMode = DepthProbeRefreshMode.OnStart;


        [@Heading("Capture")]

        [Tooltip("The layers to render into the probe.")]
        [@Predicated(nameof(_Type), inverted: true, nameof(DepthProbeMode.RealTime))]
        [@GenerateAPI(Setter.Dirty)]
        [@DecoratedField, SerializeField]
        internal LayerMask _Layers = 1; // Default

        [Tooltip("The resolution of the probe.\n\nLower will be more efficient.")]
        [@Predicated(nameof(_Type), inverted: true, nameof(DepthProbeMode.RealTime))]
        [@GenerateAPI(Setter.Dirty)]
        [@DecoratedField, SerializeField]
        internal int _Resolution = 512;

        [Tooltip("The far and near plane of the depth probe camera respectively, relative to the transform.\n\nDepth is captured top-down and orthographically. The gizmo will visualize this range as the bottom box.")]
        [@Predicated(nameof(_Type), inverted: true, nameof(DepthProbeMode.RealTime))]
        [@Range(-100f, 100f, Range.Clamp.None)]
        [@GenerateAPI(Setter.Dirty)]
        [SerializeField]
        internal Vector2 _CaptureRange = new(-1000f, 1000f);

        [Tooltip("Fills holes left by the maximum of the capture range.\n\nSetting the maximum capture range lower than the highest point of geometry can be useful for eliminating depth artifacts from overhangs, but the side effect is there will be a hole in the depth data where geometry is clipped by the near plane. This will only capture where the holes are to fill them in. This height is relative to the maximum capture range. Set to zero to skip.")]
        [@Predicated(nameof(_Type), inverted: true, nameof(DepthProbeMode.RealTime))]
        [@Range(0f, 100f, Range.Clamp.Minimum)]
        [UnityEngine.Serialization.FormerlySerializedAs("_MaximumHeight")]
        [@GenerateAPI(Setter.Dirty)]
        [SerializeField]
        internal float _FillHolesCaptureHeight;

        [@Label("Enable Back-Face Inclusion")]
        [Tooltip("Increase coverage by testing mesh back faces within the Fill Holes area.\n\nUses the back-faces to include meshes where the front-face is within the Fill Holes area and the back-face is within the capture area. An example would be an upright cylinder not over a hole but was not captured due to the top being clipped by the near plane.")]
        [@Predicated(nameof(_Type), inverted: true, nameof(DepthProbeMode.RealTime))]
        [@Predicated(nameof(_FillHolesCaptureHeight), inverted: false, 0f)]
        [@GenerateAPI(Setter.Dirty)]
        [@DecoratedField, SerializeField]
        bool _EnableBackFaceInclusion = true;

        [Tooltip("Overrides global quality settings.")]
        [@Predicated(nameof(_Type), inverted: true, nameof(DepthProbeMode.RealTime))]
        [GenerateAPI(Setter.None)]
        [@DecoratedField, SerializeField]
        QualitySettingsOverride _QualitySettingsOverride = new()
        {
            _OverrideLodBias = true,
            _LodBias = Mathf.Infinity,
            _OverrideMaximumLodLevel = true,
            _MaximumLodLevel = 0,
            _OverrideTerrainPixelError = true,
            _TerrainPixelError = 0,
        };

        [@Space(10)]

        [Tooltip("Baked probe.\n\nCan only bake in edit mode.")]
        [@Disabled]
        [@Predicated(nameof(_Type), inverted: true, nameof(DepthProbeMode.Baked), hide: true)]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
#pragma warning disable 649
        internal Texture2D _SavedTexture;
#pragma warning restore 649


        [@Heading("Signed Distance Field")]

        [@Label("Generate")]
        [Tooltip("Generate a signed distance field for the shoreline.")]
        [@Predicated(nameof(_Type), inverted: true, nameof(DepthProbeMode.RealTime))]
        [@GenerateAPI(Setter.Dirty)]
        [@DecoratedField, SerializeField]
        internal bool _GenerateSignedDistanceField = true;

        // Additional rounds of JFA, over the standard log2(resolution), can help reduce
        // innacuracies from JFA, see paper for details.
        [Tooltip("How many additional Jump Flood rounds to use.\n\nThe standard number of rounds is log2(resolution). Additional rounds can reduce innaccuracies.")]
        [@Predicated(nameof(_Type), inverted: true, nameof(DepthProbeMode.RealTime))]
        [@Predicated(nameof(_GenerateSignedDistanceField))]
        [@GenerateAPI(Setter.Dirty)]
        [@DecoratedField, SerializeField]
        int _AdditionalJumpFloodRounds = 7;


        [@Space(10)]

        [@DecoratedField, SerializeField]
        internal DebugFields _Debug = new();

        [System.Serializable]
        internal sealed class DebugFields
        {
            [Tooltip("Will render into the probe every frame. Intended for debugging, will generate garbage.")]
            [@Predicated(nameof(_Type), inverted: true, nameof(DepthProbeMode.RealTime))]
            [@DecoratedField, SerializeField]
            public bool _ForceAlwaysUpdateDebug;

            [Tooltip("Shows hidden objects like the camera which renders into the probe.")]
            [@DecoratedField, SerializeField]
            public bool _ShowHiddenObjects;

#if !CREST_DEBUG
            [HideInInspector]
#endif
            [@DecoratedField, SerializeField]
            public bool _ShowSimulationDataInScene;
        }

        const int k_CopyKernel = 0;
        const int k_FillKernel = 1;

        internal Camera _Camera;
        Rect _Rect;
        bool _RecalculateBounds = true;
        CommandBuffer _CommandBuffer;

        Rect Rect
        {
            get
            {
                if (_RecalculateBounds)
                {
                    _Rect = Managed ? new(Position.XZ() - Scale * 0.5f, Scale) : transform.RectXZ();
                    _RecalculateBounds = false;
                }

                return _Rect;
            }
        }

        internal Texture Texture => _Type == DepthProbeMode.Baked ? SavedTexture : RealtimeTexture;
        internal RenderTexture RealtimeTexture { get; set; }
        internal RenderTexture TargetTexture { get; set; }

        // Allows another component to take control of the transform.
        bool _Managed;
        internal bool Managed
        {
            get => _Managed;

            set
            {
                if (_Managed != value) _RecalculateBounds = true;
                _Managed = value;
            }
        }

        bool _OverridePosition;
        internal bool OverridePosition
        {
            get => _OverridePosition;

            set
            {
                if (Managed && _OverridePosition != value) _RecalculateBounds = true;
                _OverridePosition = value;
            }
        }

        Vector3 _Position;
        internal Vector3 Position
        {
            get
            {
                return Managed && OverridePosition ? _Position.XNZ(transform.position.y) : transform.position;
            }

            set
            {
                if (Managed && OverridePosition && _Position != value) _RecalculateBounds = true;
                _Position = value;
            }
        }

        // Only allow axis-aligned when mananged for now.
        internal Quaternion Rotation => Managed ? Quaternion.identity : Quaternion.Euler(transform.rotation.eulerAngles.NYN());

        Vector2 _Scale;
        internal Vector2 Scale
        {
            get => Managed ? _Scale : transform.lossyScale.XZ();

            set
            {
                if (_Scale != value) _RecalculateBounds = true;
                _Scale = value;
            }
        }


        /// <summary>
        /// Invoked before the <see cref="DepthProbe"/> camera renders.
        /// </summary>
        public static System.Action<DepthProbe> OnBeforeRender { get; set; }

        /// <summary>
        /// Invoked after the <see cref="DepthProbe"/> camera renders.
        /// </summary>
        public static System.Action<DepthProbe> OnAfterRender { get; set; }

        // A background process will listen to this. Allows probe to request a bake with
        // needing assembly reference (which it cannot get due to circular reference).
        internal static System.Action<DepthProbe> OnBakeRequest { get; set; }


        internal static class ShaderIDs
        {
            public static readonly int s_CamDepthBuffer = Shader.PropertyToID("_CamDepthBuffer");
            public static readonly int s_CustomZBufferParams = Shader.PropertyToID("_CustomZBufferParams");
            public static readonly int s_HeightNearHeightFar = Shader.PropertyToID("_HeightNearHeightFar");
            public static readonly int s_HeightOffset = Shader.PropertyToID("_HeightOffset");
            public static readonly int s_CameraDepthBufferBackfaces = Shader.PropertyToID("_Crest_CameraDepthBufferBackfaces");
            public static readonly int s_PreviousPlane = Shader.PropertyToID("_Crest_PreviousPlane");

            // Bind
            public static readonly int s_DepthProbe = Shader.PropertyToID("_Crest_DepthProbe");
            public static readonly int s_DepthProbeHeightOffset = Shader.PropertyToID("_Crest_DepthProbeHeightOffset");
            public static readonly int s_DepthProbeResolution = Shader.PropertyToID("_Crest_DepthProbeResolution");


            // SDF
            public static readonly int s_JumpSize = Shader.PropertyToID("_Crest_JumpSize");
            public static readonly int s_WaterLevel = Shader.PropertyToID("_Crest_WaterLevel");
            public static readonly int s_ProjectionToWorld = Shader.PropertyToID("_Crest_ProjectionToWorld");
            public static readonly int s_VoronoiPingPong0 = Shader.PropertyToID("_Crest_VoronoiPingPong0");
            public static readonly int s_VoronoiPingPong1 = Shader.PropertyToID("_Crest_VoronoiPingPong1");
        }

#if d_UnityHDRP
        static readonly List<FrameSettingsField> s_FrameSettingsFields = new()
        {
            FrameSettingsField.OpaqueObjects,
            FrameSettingsField.TransparentObjects,
            FrameSettingsField.TransparentPrepass,
            FrameSettingsField.TransparentPostpass,
            FrameSettingsField.AsyncCompute,
        };
#endif

        internal void Bind<T>(T wrapper) where T : IPropertyWrapper
        {
            wrapper.SetTexture(ShaderIDs.s_DepthProbe, Texture);
            wrapper.SetFloat(ShaderIDs.s_DepthProbeHeightOffset, transform.position.y);
            wrapper.SetFloat(ShaderIDs.s_DepthProbeResolution, _Resolution);
        }

        /// <inheritdoc/>
        private protected override void OnStart()
        {
            base.OnStart();

            if (_Type == DepthProbeMode.RealTime && _RefreshMode == DepthProbeRefreshMode.OnStart)
            {
                Populate();
            }
        }

        void OnDestroy()
        {
            if (_Camera != null) Helpers.Destroy(_Camera.gameObject);

            _CommandBuffer?.Release();
            _CommandBuffer = null;
        }

        private protected override System.Action<WaterRenderer> OnUpdateMethod => OnUpdate;
        void OnUpdate(WaterRenderer water)
        {
            if (transform.hasChanged)
            {
                _RecalculateBounds = true;
            }
        }

        private protected override System.Action<WaterRenderer> OnLateUpdateMethod => OnLateUpdate;
        void OnLateUpdate(WaterRenderer water)
        {
            transform.hasChanged = false;
        }

        internal bool Outdated => _CurrentStateHash != _RenderedStateHash;

        bool IsTextureOutdated(RenderTexture texture, bool target)
        {
            return texture != null &&
                texture.width != _Resolution ||
                texture.height != _Resolution ||
                texture.format != (target ? RenderTextureFormat.Depth : FinalFormat);
        }

        RenderTextureFormat FinalFormat => _GenerateSignedDistanceField ? RenderTextureFormat.RGFloat : RenderTextureFormat.RFloat;

        void MakeRT(RenderTexture texture, bool target)
        {
            var format = target ? RenderTextureFormat.Depth : FinalFormat;
            var descriptor = texture.descriptor;
            descriptor.colorFormat = format;
            descriptor.width = descriptor.height = _Resolution;
            descriptor.depthBufferBits = target ? 24 : 0;
            descriptor.useMipMap = false;
            // Compute always requires this to write.
            descriptor.enableRandomWrite = !target;
            texture.descriptor = descriptor;
            texture.Create();
            Debug.Assert(SystemInfo.SupportsRenderTextureFormat(format), "Crest: The graphics device does not support the render texture format " + format.ToString());
        }

        bool InitObjects()
        {
            if (RealtimeTexture == null)
            {
                RealtimeTexture = new RenderTexture(0, 0, 0)
                {
                    name = $"_Crest_WaterDepthCache_{gameObject.name}",
                    anisoLevel = 0,
                };
            }
            else if (IsTextureOutdated(RealtimeTexture, target: false))
            {
                RealtimeTexture.Release();
            }

            if (!RealtimeTexture.IsCreated())
            {
                MakeRT(RealtimeTexture, false);
            }

            if (_Layers == 0)
            {
                Debug.LogError("Crest: No valid layers for populating depth probe, aborting.", this);
                return false;
            }

            if (_Camera == null)
            {
                _Camera = new GameObject("_Crest_DepthProbeCamera").AddComponent<Camera>();
                _Camera.transform.parent = transform;
                _Camera.transform.localEulerAngles = 90f * Vector3.right;
                _Camera.transform.localPosition = Vector3.zero;
                _Camera.transform.localScale = Vector3.one;
                _Camera.orthographic = true;
                _Camera.clearFlags = CameraClearFlags.Depth;
                _Camera.enabled = false;
                _Camera.allowMSAA = false;
                _Camera.allowDynamicResolution = false;
                _Camera.depthTextureMode = DepthTextureMode.Depth;
                // Stops behaviour from changing in VR. I tried disabling XR before/after camera render but it makes the editor
                // go bonkers with split windows.
                _Camera.cameraType = CameraType.Reflection;
                // I'd prefer to destroy the camera object, but I found sometimes (on first start of editor) it will fail to render.
                _Camera.gameObject.SetActive(false);

                this.Manage(_Camera.gameObject);

                if (RenderPipelineHelper.IsUniversal)
                {
#if d_UnityURP
                    var additionalCameraData = _Camera.GetUniversalAdditionalCameraData();
                    additionalCameraData.renderShadows = false;
                    additionalCameraData.requiresColorTexture = false;
                    additionalCameraData.requiresDepthTexture = false;
                    additionalCameraData.renderPostProcessing = false;
                    additionalCameraData.allowXRRendering = false;
#endif
                }
                else if (RenderPipelineHelper.IsHighDefinition)
                {
#if d_UnityHDRP
                    var additionalCameraData = _Camera.gameObject.AddComponent<HDAdditionalCameraData>();

                    additionalCameraData.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
                    additionalCameraData.volumeLayerMask = 0;
                    additionalCameraData.probeLayerMask = 0;
                    additionalCameraData.xrRendering = false;

                    // Override camera frame settings to disable most of the expensive rendering for this camera.
                    // Most importantly, disable custom passes and post-processing as third-party stuff might throw
                    // errors because of this camera. Even with excluding a lot of HDRP features, it still does a
                    // lit pass which is not cheap.
                    additionalCameraData.customRenderingSettings = true;

                    foreach (FrameSettingsField frameSetting in System.Enum.GetValues(typeof(FrameSettingsField)))
                    {
                        if (!s_FrameSettingsFields.Contains(frameSetting))
                        {
                            // Enable override and then disable the feature.
                            additionalCameraData.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)frameSetting] = true;
                            additionalCameraData.renderingPathCustomFrameSettings.SetEnabled(frameSetting, false);
                        }
                    }
#endif
                }
            }

            // Always update.
            _Camera.orthographicSize = Mathf.Max(Scale.x * 0.5f, Scale.y * 0.5f);
            _Camera.cullingMask = _Layers;
            _Camera.gameObject.hideFlags = _Debug._ShowHiddenObjects ? HideFlags.DontSave : HideFlags.HideAndDontSave;

            if (TargetTexture == null)
            {
                TargetTexture = new RenderTexture(0, 0, 0)
                {
                    name = $"_Crest_WaterDepthTarget_{gameObject.name}",
                };
            }
            else if (IsTextureOutdated(TargetTexture, target: true))
            {
                TargetTexture.Release();
            }

            if (!TargetTexture.IsCreated())
            {
                MakeRT(TargetTexture, true);
            }

            _Camera.targetTexture = TargetTexture;

            return true;
        }

        /// <summary>
        /// Populates the <see cref="DepthProbe"/> (including re-baking).
        /// </summary>
        /// <remarks>
        /// Call this method if using <see cref="DepthProbeRefreshMode.ViaScripting"/>, or
        /// if needing the probe to be updated re-baked (re-baking editor only).
        /// </remarks>
        public void Populate()
        {
            if (_Type == DepthProbeMode.Baked)
            {
                OnBakeRequest?.Invoke(this);
            }
            else
            {
                ForcePopulate();
            }
        }

        internal void ForcePopulate()
        {
            if (WaterRenderer.RunningWithoutGraphics)
            {
                // Don't bake in headless mode
                Debug.LogWarning("Crest: Depth probe will not be populated at runtime when in batched/headless mode. Please pre-bake the probe in the Editor.");
                return;
            }

            // Make sure we have required objects.
            if (!InitObjects())
            {
                return;
            }

            var oldShadowDistance = 0f;

            if (RenderPipelineHelper.IsLegacy)
            {
                // Stop shadow passes from executing.
                oldShadowDistance = QualitySettings.shadowDistance;
                QualitySettings.shadowDistance = 0f;
            }

            _QualitySettingsOverride.Override();

            OnBeforeRender?.Invoke(this);

#if UNITY_EDITOR
            try
#endif
            {
                // Capture pass.
                RenderDepthIntoProbe(k_CopyKernel, _CaptureRange.y);

                if (_FillHolesCaptureHeight > 0f)
                {
                    // Fill holes pass.
                    RenderDepthIntoProbe(k_FillKernel, _CaptureRange.y + _FillHolesCaptureHeight);
                }
            }
#if UNITY_EDITOR
            // Ensure that any global settings are restored.
            finally
#endif
            {
                _QualitySettingsOverride.Restore();

                // Built-in only.
                if (RenderPipelineHelper.IsLegacy)
                {
                    QualitySettings.shadowDistance = oldShadowDistance;
                }

                OnAfterRender?.Invoke(this);
            }

            if (_GenerateSignedDistanceField)
            {
                RenderSignedDistanceField(inverted: false);
                RenderSignedDistanceField(inverted: true);
            }

            HashState(ref _RenderedStateHash);
        }

        void RenderDepthIntoProbe(int kernel, float height)
        {
            _Camera.transform.position = Position + Vector3.up * height;
            _Camera.farClipPlane = -_CaptureRange.x + height;

            if (Managed)
            {
                _Camera.transform.forward = Vector3.down;
            }
            else
            {
                // Face down maintaining Y rotation.
                var transform = _Camera.transform;
                var rotation = transform.parent == null ? transform.localEulerAngles.y : transform.parent.eulerAngles.y;
                transform.forward = Vector3.down;
                transform.eulerAngles = transform.eulerAngles.XNZ(rotation);
            }

            RenderTexture backFaces = null;
            if (_EnableBackFaceInclusion && kernel == k_FillKernel)
            {
                var target = _Camera.targetTexture;
                // No need to clear as depth is cleared by camera.
                backFaces = RenderTexture.GetTemporary(target.descriptor);
                _Camera.targetTexture = backFaces;

                var oldInvertCulling = GL.invertCulling;
                GL.invertCulling = true;

                // Render scene, saving depths in depth buffer.
#if d_UnityURP
                if (RenderPipelineHelper.IsUniversal)
                {
                    Helpers.RenderCameraWithoutCustomPasses(_Camera);
                }
                else
#endif
                {
                    _Camera.Render();
                }

                _Camera.targetTexture = target;
                GL.invertCulling = oldInvertCulling;
            }

            // Render scene, saving depths in depth buffer.
#if d_UnityURP
            if (RenderPipelineHelper.IsUniversal)
            {
                Helpers.RenderCameraWithoutCustomPasses(_Camera);
            }
            else
#endif
            {
                _Camera.Render();
            }

            var wrapper = new PropertyWrapperComputeStandalone(WaterResources.Instance.Compute._RenderDepthProbe, kernel);

            wrapper.SetFloat(ShaderIDs.s_HeightOffset, transform.position.y);

            // Zbuffer params
            //float4 _ZBufferParams;            // x: 1-far/near,     y: far/near, z: x/far,     w: y/far
            float near = _Camera.nearClipPlane, far = _Camera.farClipPlane;
            wrapper.SetVector(ShaderIDs.s_CustomZBufferParams, new(1f - far / near, far / near, (1f - far / near) / far, (far / near) / far));

            // Altitudes for near and far planes
            var ymax = _Camera.transform.position.y - near;
            var ymin = ymax - far;
            wrapper.SetVector(ShaderIDs.s_HeightNearHeightFar, new(ymax, ymin));

            wrapper.SetTexture(ShaderIDs.s_CamDepthBuffer, _Camera.targetTexture);
            wrapper.SetTexture(Crest.ShaderIDs.s_Target, RealtimeTexture);

            if (_EnableBackFaceInclusion && kernel == k_FillKernel)
            {
                near = _Camera.nearClipPlane;
                far = _CaptureRange.x + _CaptureRange.y;

                // Altitudes for near and far planes.
                ymax = transform.position.y + _CaptureRange.y - near;
                wrapper.SetTexture(ShaderIDs.s_CameraDepthBufferBackfaces, backFaces);
                wrapper.SetFloat(ShaderIDs.s_PreviousPlane, ymax + _Camera.nearClipPlane);
            }

            wrapper.SetKeyword(WaterResources.Instance.Keywords.DepthProbeBackFaceInclusion, _EnableBackFaceInclusion);

            var threads = RealtimeTexture.width / Lod.k_ThreadGroupSize;
            wrapper.Dispatch(threads, threads, 1);

            _Camera.transform.localPosition = Vector3.zero;

            RenderTexture.ReleaseTemporary(backFaces);
        }

        void RenderSignedDistanceField(bool inverted)
        {
            var shader = WaterResources.Instance.Compute._JumpFloodSDF;

            if (shader == null)
            {
                return;
            }

            var cameraToWorldMatrix = _Camera.cameraToWorldMatrix;
            var projectionMatrix = _Camera.projectionMatrix;
            var projectionToWorldMatrix = cameraToWorldMatrix * projectionMatrix.inverse;

            var buffer = _CommandBuffer ??= new();
            buffer.Clear();
            buffer.name = "Jump Flood";
            // Common uniforms.
            buffer.SetComputeFloatParam(shader, DepthLodInput.ShaderIDs.s_HeightOffset, transform.position.y);
            buffer.SetComputeIntParam(shader, Crest.ShaderIDs.s_TextureSize, _Resolution);
            buffer.SetComputeMatrixParam(shader, ShaderIDs.s_ProjectionToWorld, projectionToWorldMatrix);

            // Allow generating without water present.
            {
                var water = WaterRenderer.Instance;
                var height = water != null ? water.SeaLevel : transform.position.y;
                buffer.SetComputeFloatParam(shader, ShaderIDs.s_WaterLevel, height);
                buffer.SetKeyword(shader, WaterResources.Instance.Keywords.JumpFloodStandalone, water == null);
            }

            var descriptor = new RenderTextureDescriptor(_Resolution, _Resolution)
            {
                autoGenerateMips = false,
                colorFormat = RenderTextureFormat.RGHalf,
                useMipMap = false,
                enableRandomWrite = true,
                depthBufferBits = 0,
            };

            var voronoiPingPong0 = ShaderIDs.s_VoronoiPingPong0;
            var voronoiPingPong1 = ShaderIDs.s_VoronoiPingPong1;

            // No need to clear both are always overwritten.
            buffer.GetTemporaryRT(voronoiPingPong0, descriptor);
            buffer.GetTemporaryRT(voronoiPingPong1, descriptor);

            buffer.SetKeyword(shader, WaterResources.Instance.Keywords.JumpFloodInverted, inverted);

            // Initialize.
            {
                var kernel = shader.FindKernel("CrestInitialize");

                buffer.SetComputeTextureParam(shader, kernel, Crest.ShaderIDs.s_Source, RealtimeTexture);
                buffer.SetComputeTextureParam(shader, kernel, Crest.ShaderIDs.s_Target, voronoiPingPong0);
                buffer.DispatchCompute
                (
                    shader,
                    kernel,
                    RealtimeTexture.width / Lod.k_ThreadGroupSize,
                    RealtimeTexture.height / Lod.k_ThreadGroupSize,
                    1
                );
            }

            // Jump Flood.
            {
                var kernel = shader.FindKernel("CrestExecute");

                for (var jumpSize = _Resolution / 2; jumpSize > 0; jumpSize /= 2)
                {
                    ApplyJumpFlood
                    (
                        buffer,
                        shader,
                        kernel,
                        jumpSize,
                        voronoiPingPong0,
                        voronoiPingPong1
                    );
                    (voronoiPingPong0, voronoiPingPong1) = (voronoiPingPong1, voronoiPingPong0);
                }

                for (var roundNum = 0; roundNum < _AdditionalJumpFloodRounds; roundNum++)
                {
                    var jumpSize = 1 << roundNum;
                    ApplyJumpFlood
                    (
                        buffer,
                        shader,
                        kernel,
                        jumpSize,
                        voronoiPingPong0,
                        voronoiPingPong1
                    );
                    (voronoiPingPong0, voronoiPingPong1) = (voronoiPingPong1, voronoiPingPong0);
                }
            }

            // Apply.
            {
                var kernel = shader.FindKernel("CrestApply");
                buffer.SetComputeTextureParam(shader, kernel, Crest.ShaderIDs.s_Source, voronoiPingPong0);
                buffer.SetComputeTextureParam(shader, kernel, Crest.ShaderIDs.s_Target, RealtimeTexture);
                buffer.DispatchCompute
                (
                    shader,
                    kernel,
                    _Resolution / Lod.k_ThreadGroupSize,
                    _Resolution / Lod.k_ThreadGroupSize,
                    1
                );
            }

            Graphics.ExecuteCommandBuffer(buffer);
            buffer.ReleaseTemporaryRT(voronoiPingPong0);
            buffer.ReleaseTemporaryRT(voronoiPingPong1);
        }

        void ApplyJumpFlood
        (
            CommandBuffer buffer,
            ComputeShader shader,
            int kernel,
            int jumpSize,
            RenderTargetIdentifier source,
            RenderTargetIdentifier target
        )
        {
            buffer.SetComputeIntParam(shader, ShaderIDs.s_JumpSize, jumpSize);
            buffer.SetComputeTextureParam(shader, kernel, Crest.ShaderIDs.s_Source, source);
            buffer.SetComputeTextureParam(shader, kernel, Crest.ShaderIDs.s_Target, target);
            buffer.DispatchCompute
            (
                shader,
                kernel,
                _Resolution / Lod.k_ThreadGroupSize,
                _Resolution / Lod.k_ThreadGroupSize,
                1
            );
        }

        void SetDirty<I>(I previous, I current) where I : System.IEquatable<I>
        {
            if (Equals(previous, current)) return;
            HashState(ref _CurrentStateHash);
        }

        // LayerMask does not implement IEquatable.
        void SetDirty(LayerMask previous, LayerMask current)
        {
            if (previous == current) return;
            HashState(ref _CurrentStateHash);
        }
    }

    // LodInput
    partial class DepthProbe
    {
        Input _Input;

        /// <inheritdoc/>
        private protected override void Initialize()
        {
            base.Initialize();
            _Input ??= new(this);
            ILodInput.Attach(_Input, DepthLod.s_Inputs);
            HashState(ref _CurrentStateHash);

#if CREST_DEBUG
            if (_Debug._ShowSimulationDataInScene)
            {
                ILodInput.Attach(_Input, ClipLod.s_Inputs);
            }
#endif
        }

        /// <inheritdoc/>
        private protected override void OnDisable()
        {
            base.OnDisable();
            ILodInput.Detach(_Input, DepthLod.s_Inputs);

#if CREST_DEBUG
            if (_Debug._ShowSimulationDataInScene)
            {
                ILodInput.Detach(_Input, ClipLod.s_Inputs);
            }
#endif
        }

        sealed class Input : ILodInput
        {
            public bool Enabled => _Probe.enabled && _Probe.Texture != null;
            public bool IsCompute => true;
            public int Queue => 0;
            public int Pass => -1;
            public Rect Rect => _Probe.Rect;
            public MonoBehaviour Component => _Probe;
            public float Filter(WaterRenderer water, int slice) => 1f;

            readonly DepthProbe _Probe;

            public Input(DepthProbe probe)
            {
                _Probe = probe;
            }

            public void Draw(Lod lod, CommandBuffer buffer, RenderTargetIdentifier target, int pass = -1, float weight = 1, int slices = -1)
            {
#if CREST_DEBUG
                if (lod is ClipLod)
                {
                    var compute = new PropertyWrapperCompute(buffer, WaterResources.Instance.Compute._ClipPrimitive, 0);

                    compute.SetMatrix(Crest.ShaderIDs.s_Matrix, Matrix4x4.TRS(_Probe.Position, _Probe.Rotation, _Probe.Scale.XNZ(100f)).inverse);

                    // For culling.
                    compute.SetVector(Crest.ShaderIDs.s_Position, _Probe.Position);
                    compute.SetFloat(Crest.ShaderIDs.s_Diameter, _Probe.Scale.x);

                    compute.SetKeyword(WaterResources.Instance.Keywords.ClipPrimitiveInverted, false);
                    compute.SetKeyword(WaterResources.Instance.Keywords.ClipPrimitiveSphere, false);
                    compute.SetKeyword(WaterResources.Instance.Keywords.ClipPrimitiveCube, true);
                    compute.SetKeyword(WaterResources.Instance.Keywords.ClipPrimitiveRectangle, false);
                    compute.SetTexture(Crest.ShaderIDs.s_Target, target);
                    compute.Dispatch(lod.Resolution / Lod.k_ThreadGroupSize, lod.Resolution / Lod.k_ThreadGroupSize, slices);
                    return;
                }
#endif

                var resources = WaterResources.Instance;
                var wrapper = new PropertyWrapperCompute(buffer, resources.Compute._DepthTexture, 0);

                var position = _Probe.Position;
                var matrix = Matrix4x4.TRS(position, _Probe.Rotation, _Probe.Scale.XNZ(1f));

                // Texture Input
                wrapper.SetVector(Crest.ShaderIDs.s_TextureSize, _Probe.Scale);
                wrapper.SetVector(Crest.ShaderIDs.s_TexturePosition, position.XZ());
                wrapper.SetVector(Crest.ShaderIDs.s_TextureRotation, new Vector2(matrix.m20, matrix.m00).normalized);
                wrapper.SetInteger(Crest.ShaderIDs.s_Blend, (int)LodInputBlend.Maximum);
                wrapper.SetTexture(Crest.ShaderIDs.s_Texture, _Probe.Texture);
                wrapper.SetTexture(Crest.ShaderIDs.s_Target, target);

                // Depth Input
                wrapper.SetFloat(DepthLodInput.ShaderIDs.s_HeightOffset, position.y);
                wrapper.SetInteger(DepthLodInput.ShaderIDs.s_SDF, _Probe._GenerateSignedDistanceField ? 1 : 0);
                wrapper.SetKeyword(resources.Keywords.DepthTextureSDF, lod._Water._DepthLod._EnableSignedDistanceFields);

                var threads = lod.Resolution / Lod.k_ThreadGroupSize;
                wrapper.Dispatch(threads, threads, slices);
            }
        }
    }

    sealed partial class DepthProbe
    {
        // Hash is used to notify whether the probe is outdated in the UI.
        int _RenderedStateHash;
        int _CurrentStateHash;

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        void HashState(ref int hash)
        {
            hash = Hash.CreateHash();
            Hash.AddInt(_Layers, ref hash);
            Hash.AddInt(_Resolution, ref hash);
            Hash.AddObject(_CaptureRange, ref hash);
            Hash.AddFloat(_FillHolesCaptureHeight, ref hash);
            Hash.AddObject(_QualitySettingsOverride, ref hash);
            Hash.AddBool(_EnableBackFaceInclusion, ref hash);
            Hash.AddInt(_AdditionalJumpFloodRounds, ref hash);
            Hash.AddBool(_GenerateSignedDistanceField, ref hash);
            Hash.AddObject(Position, ref hash);
            Hash.AddObject(Rotation, ref hash);
            Hash.AddObject(Scale, ref hash);
        }

#if UNITY_EDITOR
        private protected override void OnValidate()
        {
            base.OnValidate();
            // OnChange has a bug (possibly with Unity) with LayerMask where it cannot detect changes.
            HashState(ref _CurrentStateHash);
        }

        [@OnChange]
        void OnChange(string propertyPath, object oldValue)
        {
#if CREST_DEBUG
            ILodInput.Detach(_Input, ClipLod.s_Inputs);
            if (_Debug._ShowSimulationDataInScene)
            {
                ILodInput.Attach(_Input, ClipLod.s_Inputs);
            }
#endif

            if (_Camera == null) return;
            _Camera.gameObject.hideFlags = _Debug._ShowHiddenObjects ? HideFlags.DontSave : HideFlags.HideAndDontSave;
        }

        void Update()
        {
            if (_Debug._ForceAlwaysUpdateDebug)
            {
                Populate();
            }

            if (transform.hasChanged)
            {
                HashState(ref _CurrentStateHash);
            }
        }
#endif
    }

    partial class DepthProbe : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 1;
#pragma warning restore 414

        /// <inheritdoc/>
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            // Version 1 (2024.06.04)
            // - Added new capture options replacing Maximum Height's behaviour.
            if (_Version < 1)
            {
                _CaptureRange.y = _FillHolesCaptureHeight;
                _FillHolesCaptureHeight = 0f;
                _Version = 1;
            }
        }

        /// <inheritdoc/>
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {

        }
    }
}
