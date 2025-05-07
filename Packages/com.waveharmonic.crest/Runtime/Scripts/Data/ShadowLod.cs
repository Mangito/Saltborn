// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// BIRP fallback not really tested yet - shaders need fixing up.

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WaveHarmonic.Crest.Internal;
using WaveHarmonic.Crest.Utility;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Stores shadowing data to use during water shading.
    /// </summary>
    /// <remarks>
    /// Shadowing is persistent and supports sampling across many frames and jittered
    /// sampling for (very) soft shadows. Soft shadows is red, hard shadows is green.
    /// </remarks>
    [FilterEnum(nameof(_TextureFormatMode), Filtered.Mode.Exclude, (int)LodTextureFormatMode.Automatic)]
    public sealed partial class ShadowLod : Lod
    {
        [@Space(10)]

        [Tooltip("Whether to vary soft shadow jitter by scattering/absorption density.")]
        [@DecoratedField, SerializeField]
        bool _DynamicSoftShadows = true;

        [Tooltip("Factor control for dynamic soft jitter.")]
        [@Predicated(nameof(_DynamicSoftShadows), hide: true)]
        [@Range(0f, 1f, Range.Clamp.Minimum)]
        [SerializeField]
        float _SoftJitterExtinctionFactor = 0.75f;

        [Tooltip("Jitter diameter for soft shadows, controls softness of this shadowing component.")]
        [@Predicated(nameof(_DynamicSoftShadows), hide: true, inverted: true)]
        [@Range(0f, k_MaximumJitter)]
        [@GenerateAPI]
        [SerializeField]
        internal float _JitterDiameterSoft = 15f;

        [Tooltip("Current frame weight for accumulation over frames for soft shadows.\n\nRoughly means 'responsiveness' for soft shadows.")]
        [@Range(0f, 1f)]
        [@GenerateAPI]
        [SerializeField]
        internal float _CurrentFrameWeightSoft = 0.03f;

        [Tooltip("Jitter diameter for hard shadows, controls softness of this shadowing component.")]
        [@Range(0f, k_MaximumJitter)]
        [@GenerateAPI]
        [SerializeField]
        internal float _JitterDiameterHard = 0.6f;

        [Tooltip("Current frame weight for accumulation over frames for hard shadows.\n\nRoughly means 'responsiveness' for hard shadows.")]
        [@Range(0f, 1f)]
        [@GenerateAPI]
        [SerializeField]
        internal float _CurrentFrameWeightHard = 0.15f;

        [@Space(10)]

        [Tooltip("Whether to disable the null light warning, use this if you assign it dynamically and expect it to be null at points")]
        [@DecoratedField, SerializeField]
        internal bool _AllowNullLight = false;

        [Tooltip("Whether to disable the no shadows warning. Use this if you toggle the shadows on the primary light dynamically.")]
        [@DecoratedField, SerializeField]
        internal bool _AllowNoShadows = false;

        static new class ShaderIDs
        {
            public static readonly int s_DynamicSoftShadowsFactor = Shader.PropertyToID("g_Crest_DynamicSoftShadowsFactor");
            public static readonly int s_SampleColorMap = Shader.PropertyToID("_Crest_SampleColorMap");
            public static readonly int s_CenterPos = Shader.PropertyToID("_Crest_CenterPos");
            public static readonly int s_Scale = Shader.PropertyToID("_Crest_Scale");
            public static readonly int s_JitterDiameters_CurrentFrameWeights = Shader.PropertyToID("_Crest_JitterDiameters_CurrentFrameWeights");
            public static readonly int s_MainCameraProjectionMatrix = Shader.PropertyToID("_Crest_MainCameraProjectionMatrix");
            public static readonly int s_SimDeltaTime = Shader.PropertyToID("_Crest_SimDeltaTime");

            // BIRP only.
            public static readonly int s_ShadowCastersPresent = Shader.PropertyToID("_Crest_ShadowCastersPresent");
            public static readonly int s_ClearShadows = Shader.PropertyToID("_Crest_ClearShadows");
        }

        const float k_MaximumJitter = 32f;

        internal static readonly Color s_GizmoColor = new(0f, 0f, 0f, 0.5f);
        internal static bool s_ProcessData = true;

        internal override string ID => "Shadow";
        internal override string Name => "Shadows";
        internal override Color GizmoColor => s_GizmoColor;
        private protected override Color ClearColor => Color.black;
        private protected override bool NeedToReadWriteTextureData => true;
        internal override int BufferCount => 2;

        private protected override GraphicsFormat RequestedTextureFormat => _TextureFormatMode switch
        {
            LodTextureFormatMode.Performance => GraphicsFormat.R8G8_UNorm,
            LodTextureFormatMode.Precision => GraphicsFormat.R16G16_UNorm,
            LodTextureFormatMode.Manual => _TextureFormat,
            _ => throw new System.NotImplementedException(),
        };

        Light _Light;

        // SRP version needs access to this externally, hence internal get.
        internal CommandBuffer CopyShadowMapBuffer { get; private set; }
        PropertyWrapperMaterial[] _RenderMaterial;

        enum Error
        {
            None,
            NoLight,
            NoShadows,
            IncorrectLightType,
        }

        Error _Error;

        internal override void Initialize()
        {
            if (WaterResources.Instance.Shaders._UpdateShadow == null)
            {
                _Valid = false;
                return;
            }

            var isShadowsDisabled = false;

            if (RenderPipelineHelper.IsLegacy)
            {
                if (QualitySettings.shadows == UnityEngine.ShadowQuality.Disable)
                {
                    isShadowsDisabled = true;
                }
            }
            else if (RenderPipelineHelper.IsUniversal)
            {
#if d_UnityURP
                var asset = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

                // TODO: Support single casacades as it is possible.
                if (asset && asset.shadowCascadeCount < 2)
                {
                    Debug.LogError("Crest shadowing requires shadow cascades to be enabled on the pipeline asset.", asset);
                    _Valid = false;
                    return;
                }

                if (asset.mainLightRenderingMode == LightRenderingMode.Disabled)
                {
                    Debug.LogError("Crest: Main Light must be enabled to enable water shadowing.", _Water);
                    _Valid = false;
                    return;
                }

                isShadowsDisabled = !asset.supportsMainLightShadows;
#endif
            }

            if (isShadowsDisabled)
            {
                Debug.LogError("Crest: Shadows must be enabled in the quality settings to enable water shadowing.", _Water);
                _Valid = false;
                return;
            }

            base.Initialize();
        }

        internal override void SetGlobals(bool enable)
        {
            base.SetGlobals(enable);
            Shader.SetGlobalFloat(ShaderIDs.s_DynamicSoftShadowsFactor, 1f);

            if (RenderPipelineHelper.IsLegacy)
            {
                Helpers.SetGlobalBoolean(ShaderIDs.s_ShadowCastersPresent, true);
            }
        }

        internal override void Enable()
        {
            base.Enable();

            if (RenderPipelineHelper.IsLegacy)
            {
                Camera.onPreCull -= OnPreCullCamera;
                Camera.onPreCull += OnPreCullCamera;
                Camera.onPostRender -= OnPostRenderCamera;
                Camera.onPostRender += OnPostRenderCamera;
            }

            CleanUpShadowCommandBuffers();

            if (RenderPipelineHelper.IsHighDefinition)
            {
#if d_UnityHDRP
                SampleShadowsHDRP.Enable(_Water);
#endif
            }
            else if (RenderPipelineHelper.IsUniversal)
            {
#if d_UnityURP
                SampleShadowsURP.Enable();
#endif
            }
        }

        internal override void Disable()
        {
            base.Disable();

            CleanUpShadowCommandBuffers();
            Shader.SetGlobalFloat(ShaderIDs.s_DynamicSoftShadowsFactor, 1f);

            Camera.onPreCull -= OnPreCullCamera;
            Camera.onPostRender -= OnPostRenderCamera;

#if d_UnityHDRP
            SampleShadowsHDRP.Disable();
#endif

#if d_UnityURP
            SampleShadowsURP.Disable();
#endif
        }

        internal override void Destroy()
        {
            base.Destroy();

            for (var index = 0; index < _RenderMaterial.Length; index++)
            {
                Helpers.Destroy(_RenderMaterial[index].Material);
            }
        }

        private protected override void Allocate()
        {
            base.Allocate();

            _Targets.RunLambda(buffer => Clear(buffer));

            {
                _RenderMaterial = new PropertyWrapperMaterial[Slices];
                var shader = WaterResources.Instance.Shaders._UpdateShadow;
                for (var i = 0; i < _RenderMaterial.Length; i++)
                {
                    _RenderMaterial[i] = new(shader);
                    _RenderMaterial[i].SetInteger(Lod.ShaderIDs.s_LodIndex, i);
                }
            }

            // Enable sample shadows custom pass.
            if (RenderPipelineHelper.IsHighDefinition)
            {
#if d_UnityHDRP
                SampleShadowsHDRP.Enable(_Water);
#endif
            }
            else if (RenderPipelineHelper.IsUniversal)
            {
#if d_UnityURP
                SampleShadowsURP.Enable();
#endif
            }
        }

        internal override void ClearLodData()
        {
            base.ClearLodData();
            _Targets.RunLambda(buffer => Clear(buffer));
        }

        void OnPreCullCamera(Camera camera)
        {
#if UNITY_EDITOR
            // Do not execute when editor is not active to conserve power and prevent possible leaks.
            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
            {
                CopyShadowMapBuffer?.Clear();
                return;
            }

            if (!WaterRenderer.IsWithinEditorUpdate)
            {
                CopyShadowMapBuffer?.Clear();
                return;
            }
#endif

            var water = _Water;

            if (water == null)
            {
                return;
            }

            if (!Helpers.MaskIncludesLayer(camera.cullingMask, water.Layer))
            {
                return;
            }

            if (camera == water.Viewer && CopyShadowMapBuffer != null)
            {
                // Calling this in OnPreRender was too late to be executed in the same frame.
                AddCommandBufferToPrimaryLight();

                // Disable for XR SPI otherwise input will not have correct world position.
                if (camera.stereoEnabled && XRHelpers.IsSinglePass)
                {
                    CopyShadowMapBuffer.DisableShaderKeyword("STEREO_INSTANCING_ON");
                }

                BuildCommandBuffer(water, CopyShadowMapBuffer);

                // Restore XR SPI as we cannot rely on remaining pipeline to do it for us.
                if (camera.stereoEnabled && XRHelpers.IsSinglePass)
                {
                    CopyShadowMapBuffer.EnableShaderKeyword("STEREO_INSTANCING_ON");
                }
            }
        }

        void OnPostRenderCamera(Camera camera)
        {
#if UNITY_EDITOR
            // Do not execute when editor is not active to conserve power and prevent possible leaks.
            if (!UnityEditorInternal.InternalEditorUtility.isApplicationActive)
            {
                CopyShadowMapBuffer?.Clear();
                return;
            }

            if (!WaterRenderer.IsWithinEditorUpdate)
            {
                CopyShadowMapBuffer?.Clear();
                return;
            }
#endif

            var water = _Water;

            if (water == null)
            {
                return;
            }

            if (!Helpers.MaskIncludesLayer(camera.cullingMask, water.Layer))
            {
                return;
            }

            if (camera == water.Viewer)
            {
                // CBs added to a light are executed for every camera, but the LOD data is only supports a single
                // camera. Removing the CB after the camera renders restricts the CB to one camera.
                RemoveCommandBufferFromPrimaryLight();
            }
        }

        internal void AddCommandBufferToPrimaryLight()
        {
            if (_Light == null || CopyShadowMapBuffer == null) return;
            _Light.RemoveCommandBuffer(LightEvent.BeforeScreenspaceMask, CopyShadowMapBuffer);
            _Light.AddCommandBuffer(LightEvent.BeforeScreenspaceMask, CopyShadowMapBuffer);
        }

        internal void RemoveCommandBufferFromPrimaryLight()
        {
            if (_Light == null || CopyShadowMapBuffer == null) return;
            _Light.RemoveCommandBuffer(LightEvent.BeforeScreenspaceMask, CopyShadowMapBuffer);
        }

        /// <summary>
        /// Validates the primary light.
        /// </summary>
        /// <returns>
        /// Whether the light is valid. An invalid light should be treated as a developer error and not recoverable.
        /// </returns>
        bool ValidateLight()
        {
            if (_Light == null)
            {
                if (!_AllowNullLight)
                {
                    if (_Error != Error.NoLight)
                    {
                        Debug.LogWarning($"Crest: Primary light must be specified on {nameof(WaterRenderer)} script to enable shadows.", _Water);
                        _Error = Error.NoLight;
                    }
                    return false;
                }

                return true;
            }

            if (_Light.shadows == LightShadows.None)
            {
                if (!_AllowNoShadows)
                {
                    if (_Error != Error.NoShadows)
                    {
                        Debug.LogWarning("Crest: Shadows must be enabled on primary light to enable water shadowing (types Hard and Soft are equivalent for the water system).", _Light);
                        _Error = Error.NoShadows;
                    }
                    return false;
                }
            }

            if (_Light.type != LightType.Directional)
            {
                if (_Error != Error.IncorrectLightType)
                {
                    Debug.LogError("Crest: Primary light must be of type Directional.", _Light);
                    _Error = Error.IncorrectLightType;
                }
                return false;
            }

            _Error = Error.None;
            return true;
        }

        /// <summary>
        /// Stores the primary light.
        /// </summary>
        /// <returns>
        /// Whether there is a light that casts shadows.
        /// </returns>
        bool SetUpLight()
        {
            if (_Light == null)
            {
                _Light = _Water.PrimaryLight;

                if (_Light == null)
                {
                    return false;
                }
            }

            if (_Light.shadows == LightShadows.None)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// May happen if scenes change etc
        /// </summary>
        void ClearBufferIfLightChanged()
        {
            if (_Light != _Water.PrimaryLight)
            {
                _Targets.RunLambda(buffer => Clear(buffer));
                CleanUpShadowCommandBuffers();
                _Light = null;
            }
        }

        void CleanUpShadowCommandBuffers()
        {
            if (!RenderPipelineHelper.IsLegacy)
            {
                return;
            }

            CopyShadowMapBuffer?.Release();
            CopyShadowMapBuffer = null;
        }

        void Update()
        {
            // If disabled then we hit a failure state. Try and recover in edit mode by proceeding.
            if (!_Valid && Application.isPlaying)
            {
                return;
            }

            ClearBufferIfLightChanged();

            var hasShadowCastingLight = SetUpLight();
            // If in play mode, and this becomes false, then we hit a failed state and will not recover.
            _Valid = ValidateLight();

            if (!s_ProcessData || !_Valid || !hasShadowCastingLight)
            {
                if (CopyShadowMapBuffer != null)
                {
                    // If we have a command buffer, then there is likely shadow data so we need to clear it.
                    _Targets.RunLambda(buffer => Clear(buffer));
                    CleanUpShadowCommandBuffers();
                }

                return;
            }

            CopyShadowMapBuffer ??= new() { name = "Crest Shadow Data" };
            CopyShadowMapBuffer.Clear();

            FlipBuffers();

            // clear the shadow collection. it will be overwritten with shadow values IF the shadows render,
            // which only happens if there are (nontransparent) shadow receivers around. this is only reliable
            // in play mode, so don't do it in edit mode.
#if UNITY_EDITOR
            if (Application.isPlaying)
#endif
            {
                Clear(DataTexture);
            }
        }

        internal override void BuildCommandBuffer(WaterRenderer water, CommandBuffer buffer)
        {
            var isSimulationBuffer = buffer == _Water.SimulationBuffer;

            if (isSimulationBuffer)
            {
                var skip = true;
                if (RenderPipelineHelper.IsLegacy)
                {
                    // If no shadow casters are present, BIRP will not execute the command buffer
                    // leaving outdated shadows in data. We set a flag to determine if the command
                    // buffer was executed.
                    skip = Helpers.GetGlobalBoolean(ShaderIDs.s_ShadowCastersPresent);
                    Helpers.SetGlobalBoolean(ShaderIDs.s_ShadowCastersPresent, false);
                }

                Update();

                // Only do a partial update when called by WaterRenderer as we want to execute
                // with the camera's command buffer (in frame).
                if (skip) return;
            }

            // NOTE: FlipBuffers called elsewhere.

            // Cache the camera for further down.
            var camera = water.Viewer;

#pragma warning disable 618
            using (new ProfilingSample(buffer, isSimulationBuffer ? "Shadows" : "Crest.Shadows"))
#pragma warning restore 618
            {
                var jitter = new Vector4
                (
                    _JitterDiameterSoft,
                    _JitterDiameterHard,
                    _CurrentFrameWeightSoft,
                    _CurrentFrameWeightHard
                );

                var waterMaterial = _Water.Material;
                var hasColor = waterMaterial.HasVector(WaterRenderer.ShaderIDs.s_Absorption) && waterMaterial.HasProperty(WaterRenderer.ShaderIDs.s_Scattering);
                var absorption = hasColor ? waterMaterial.GetVector(WaterRenderer.ShaderIDs.s_Absorption).XYZ() : Vector3.zero;
                var scattering = hasColor ? ((Vector4)waterMaterial.GetColor(WaterRenderer.ShaderIDs.s_Scattering).MaybeLinear()).XYZ() : Vector3.zero;
                var sampleAbsorption = _Water.AbsorptionLod.Enabled;
                var sampleScattering = _Water.ScatteringLod.Enabled;
                var sampleColor = sampleAbsorption || sampleScattering;

                if (_DynamicSoftShadows && hasColor && !sampleColor)
                {
                    // This approximates varying of soft shadowing by volume scattering/absorption density.
                    var extinction = absorption + scattering;
                    var factor = Mathf.Clamp01(Mathf.Min(Mathf.Min(extinction.x, extinction.y), extinction.z) * _SoftJitterExtinctionFactor);
                    jitter.x = (1f - factor) * k_MaximumJitter;
                }

                Shader.SetGlobalFloat(ShaderIDs.s_DynamicSoftShadowsFactor, _DynamicSoftShadows ? _SoftJitterExtinctionFactor : 1f);

                buffer.BeginSample("Sample");

                for (var slice = Slices - 1; slice >= 0; slice--)
                {
                    _RenderMaterial[slice].SetVector(ShaderIDs.s_CenterPos, _Cascades[slice]._SnappedPosition.XNZ(_Water.SeaLevel));
                    var scale = water.CalcLodScale(slice);
                    _RenderMaterial[slice].SetVector(ShaderIDs.s_Scale, new(scale, 1f, scale));
                    _RenderMaterial[slice].SetVector(ShaderIDs.s_JitterDiameters_CurrentFrameWeights, jitter);
                    _RenderMaterial[slice].SetMatrix(ShaderIDs.s_MainCameraProjectionMatrix, GL.GetGPUProjectionMatrix(camera.projectionMatrix, renderIntoTexture: true) * camera.worldToCameraMatrix);
                    _RenderMaterial[slice].SetFloat(ShaderIDs.s_SimDeltaTime, Time.deltaTime);

                    // Dynamic Soft Shadows.
                    _RenderMaterial[slice].SetBoolean(ShaderIDs.s_SampleColorMap, _DynamicSoftShadows && sampleColor);
                    if (_DynamicSoftShadows && sampleColor)
                    {
                        _RenderMaterial[slice].SetVector(WaterRenderer.ShaderIDs.s_Absorption, absorption);
                        _RenderMaterial[slice].SetVector(WaterRenderer.ShaderIDs.s_Scattering, scattering);
                    }

                    _RenderMaterial[slice].SetTexture(_TextureSourceShaderID, _Targets.Previous(1));

#if UNITY_EDITOR
                    // On recompiles this becomes unset even though we run over the code path to set it again...
                    _RenderMaterial[slice].SetInteger(Lod.ShaderIDs.s_LodIndex, slice);
#endif

                    if (RenderPipelineHelper.IsLegacy)
                    {
                        // If we are executing the simulation buffer, then we are clearing.
                        _RenderMaterial[slice].SetBoolean(ShaderIDs.s_ClearShadows, isSimulationBuffer);
                    }

                    Helpers.Blit(buffer, DataTexture, _RenderMaterial[slice].Material, depthSlice: slice);
                }

                buffer.EndSample("Sample");

                // BUG: These draw calls will "leak" and be duplicated before the above blit. They are executed at
                // the beginning of this CB before any commands are applied.
                SubmitDraws(buffer, s_Inputs, DataTexture);

                // Set the target texture as to make sure we catch the 'pong' each frame
                Shader.SetGlobalTexture(_TextureShaderID, DataTexture);

                if (RenderPipelineHelper.IsLegacy && !isSimulationBuffer)
                {
                    buffer.SetGlobalBoolean(ShaderIDs.s_ShadowCastersPresent, true);
                }
            }
        }

        internal ShadowLod()
        {
            _Enabled = true;
            _TextureFormat = GraphicsFormat.R8G8_UNorm;
        }

        internal static SortedList<int, ILodInput> s_Inputs = new(Helpers.DuplicateComparison);
        private protected override SortedList<int, ILodInput> Inputs => s_Inputs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnLoad()
        {
            s_Inputs.Clear();
        }
    }
}
