// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Base class for Shape components.
    /// </summary>
    [@ExecuteDuringEditMode(ExecuteDuringEditMode.Include.None)]
    [@HelpURL("Manual/Waves.html#wave-conditions")]
    [@FilterEnum(nameof(_Blend), Filtered.Mode.Include, (int)LodInputBlend.Off, (int)LodInputBlend.Additive, (int)LodInputBlend.Alpha, (int)LodInputBlend.AlphaClip)]
    public abstract partial class ShapeWaves : LodInput
    {
        [@Heading("Waves")]

        [Tooltip("The spectrum that defines the water surface shape.")]
        [@Embedded]
        [@GenerateAPI]
        [SerializeField]
        internal WaveSpectrum _Spectrum;

        [Tooltip("Whether to evaluate the spectrum every frame.\n\nWhen false, the wave spectrum is evaluated once on startup in editor play mode and standalone builds, rather than every frame. This is less flexible, but it reduces the performance cost significantly.")]
        [@GenerateAPI]
        [FormerlySerializedAs("_SpectrumFixedAtRuntime")]
        [SerializeField]
        bool _EvaluateSpectrumAtRunTimeEveryFrame;

        [Tooltip("How much these waves respect the shallow water attenuation.\n\nAttenuation is defined on the Animated Waves. Set to zero to ignore attenuation.")]
        [@Range(0f, 1f)]
        [@GenerateAPI]
        [SerializeField]
        float _RespectShallowWaterAttenuation = 1f;

        [Tooltip("Primary wave direction heading (degrees).\n\nThis is the angle from x axis in degrees that the waves are oriented towards. If a spline is being used to place the waves, this angle is relative to the spline.")]
        [@Predicated(nameof(_Mode), inverted: false, nameof(LodInputMode.Paint))]
        [@Range(-180, 180)]
        [@GenerateAPI]
        [SerializeField]
        private protected float _WaveDirectionHeadingAngle = 0f;

        [Tooltip("Whether to use the wind speed on this component rather than the global wind speed.\n\nGlobal wind speed comes from the Water Renderer component.")]
        [@GenerateAPI]
        [SerializeField]
        bool _OverrideGlobalWindSpeed = false;

        [Tooltip("Wind speed in km/h. Controls wave conditions.")]
        [@ShowComputedProperty(nameof(WindSpeedKPH))]
        [@Predicated(nameof(_OverrideGlobalWindSpeed), hide: true)]
        [@Range(0, 150f, scale: 2f)]
        [@GenerateAPI]
        [SerializeField]
        float _WindSpeed = 20f;


        [Header("Generation Settings")]

        [Tooltip("Resolution to use for wave generation buffers.\n\nLow resolutions are more efficient but can result in noticeable patterns in the shape.")]
        [@Stepped(16, 512, step: 2, power: true)]
        [@GenerateAPI]
        [SerializeField]
        private protected int _Resolution = 128;


        // Debug

        [Tooltip("In Editor, shows the wave generation buffers on screen.")]
        [@DecoratedField(order = k_DebugGroupOrder * Constants.k_FieldGroupOrder), SerializeField]
        internal bool _DrawSlicesInEditor = false;


        private protected static new class ShaderIDs
        {
            public static readonly int s_TransitionalWavelengthThreshold = Shader.PropertyToID("_Crest_TransitionalWavelengthThreshold");
            public static readonly int s_WaveResolutionMultiplier = Shader.PropertyToID("_Crest_WaveResolutionMultiplier");
            public static readonly int s_WaveBufferParameters = Shader.PropertyToID("_Crest_WaveBufferParameters");
            public static readonly int s_AlphaSource = Shader.PropertyToID("_Crest_AlphaSource");
            public static readonly int s_WaveBuffer = Shader.PropertyToID("_Crest_WaveBuffer");
            public static readonly int s_WaveBufferSliceIndex = Shader.PropertyToID("_Crest_WaveBufferSliceIndex");
            public static readonly int s_AverageWavelength = Shader.PropertyToID("_Crest_AverageWavelength");
            public static readonly int s_RespectShallowWaterAttenuation = Shader.PropertyToID("_Crest_RespectShallowWaterAttenuation");
            public static readonly int s_MaximumAttenuationDepth = Shader.PropertyToID("_Crest_MaximumAttenuationDepth");
            public static readonly int s_AxisX = Shader.PropertyToID("_Crest_AxisX");
        }

        static WaveSpectrum s_DefaultSpectrum;
        private protected static WaveSpectrum DefaultSpectrum
        {
            get
            {
                if (s_DefaultSpectrum == null)
                {
                    s_DefaultSpectrum = ScriptableObject.CreateInstance<WaveSpectrum>();
                    s_DefaultSpectrum.name = "Default Waves (instance)";
                    s_DefaultSpectrum.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                }

                return s_DefaultSpectrum;
            }
        }

        private protected abstract int MinimumResolution { get; }
        private protected abstract int MaximumResolution { get; }

        static ComputeShader s_TransferWavesComputeShader;
        static LocalKeyword s_KeywordTexture;
        static LocalKeyword s_KeywordTextureBlend;
        readonly Vector4[] _WaveBufferParameters = new Vector4[Lod.k_MaximumSlices];

        internal static int s_RenderPassOverride = -1;

        private protected WaveSpectrum _ActiveSpectrum = null;
        private protected Vector2 PrimaryWaveDirection => new(Mathf.Cos(Mathf.PI * _WaveDirectionHeadingAngle / 180f), Mathf.Sin(Mathf.PI * _WaveDirectionHeadingAngle / 180f));

        /// <summary>
        /// The wind speed in kilometers per hour (KPH).
        /// </summary>
        /// <remarks>
        /// Wind speed can come from this component or the <see cref="WaterRenderer"/>.
        /// </remarks>
        public float WindSpeedKPH => _OverrideGlobalWindSpeed || WaterRenderer.Instance == null ? _WindSpeed : WaterRenderer.Instance.WindSpeedKPH;

        /// <summary>
        /// The wind speed in meters per second (MPS).
        /// </summary>
        /// /// <remarks>
        /// Wind speed can come from this component or the <see cref="WaterRenderer"/>.
        /// </remarks>
        public float WindSpeedMPS => WindSpeedKPH / 3.6f;

        private protected ShapeWaves()
        {
            _FollowHorizontalWaveMotion = true;
        }

        private protected override void Attach()
        {
            base.Attach();
            _Reporter ??= new(this);
            WaterChunkRenderer.DisplacementReporters.Add(_Reporter);
        }

        private protected override void Detach()
        {
            base.Detach();
            WaterChunkRenderer.DisplacementReporters.Remove(_Reporter);
        }

        internal override void Draw(Lod simulation, CommandBuffer buffer, RenderTargetIdentifier target, int pass = -1, float weight = 1f, int slice = -1)
        {
            if (weight * Weight <= 0f)
            {
                return;
            }

            // Iterating over slices which means this is non compute so pass to graphics draw.
            if (!IsCompute)
            {
                GraphicsDraw(simulation, buffer, target, pass, weight, slice);
                return;
            }

            var lodCount = simulation.Slices;

            var shape = (AnimatedWavesLod)simulation;

            var wrapper = new PropertyWrapperCompute(buffer, s_TransferWavesComputeShader, 0);

            if (_FirstCascade < 0 || _LastCascade < 0)
            {
                return;
            }

            // Write to per-octave _WaveBuffers (ie pre-combined). Not the same as _AnimatedWaves.
            wrapper.SetTexture(Crest.ShaderIDs.s_Target, target);
            // Input weight. Weight for each octave calculated in compute.
            wrapper.SetFloat(LodInput.ShaderIDs.s_Weight, Weight);

            var water = shape._Water;

            for (var lodIdx = lodCount - 1; lodIdx >= lodCount - slice; lodIdx--)
            {
                _WaveBufferParameters[lodIdx] = new(-1, -2, 0, 0);

                var found = false;
                var filter = new AnimatedWavesLod.WavelengthFilter(water, lodIdx);

                for (var i = _FirstCascade; i <= _LastCascade; i++)
                {
                    _Wavelength = MinWavelength(i) / shape.WaveResolutionMultiplier;

                    // Do the weight from scratch because this is the real filter.
                    var w = AnimatedWavesLod.FilterByWavelength(filter, _Wavelength) * Weight;

                    if (w <= 0f)
                    {
                        continue;
                    }

                    if (!found)
                    {
                        _WaveBufferParameters[lodIdx].x = i;
                        found = true;
                    }

                    _WaveBufferParameters[lodIdx].y = i;
                }
            }

            // Set transitional weights.
            _WaveBufferParameters[lodCount - 2].w = 1f - water.ViewerAltitudeLevelAlpha;
            _WaveBufferParameters[lodCount - 1].w = water.ViewerAltitudeLevelAlpha;

            SetRenderParameters(water, wrapper);

            wrapper.SetFloat(ShaderIDs.s_WaveResolutionMultiplier, shape.WaveResolutionMultiplier);
            wrapper.SetFloat(ShaderIDs.s_TransitionalWavelengthThreshold, water.MaximumWavelength(water.LodLevels - 1) * 0.5f);
            wrapper.SetVectorArray(ShaderIDs.s_WaveBufferParameters, _WaveBufferParameters);

            var isTexture = Mode is LodInputMode.Paint or LodInputMode.Texture;
            var isAlphaBlend = Blend is LodInputBlend.Off or LodInputBlend.Alpha or LodInputBlend.AlphaClip;

            wrapper.SetKeyword(s_KeywordTexture, isTexture && !isAlphaBlend);
            wrapper.SetKeyword(s_KeywordTextureBlend, isTexture && isAlphaBlend);

            if (isTexture)
            {
                wrapper.SetInteger(Crest.ShaderIDs.s_Blend, (int)_Blend);
            }

            if (Mode == LodInputMode.Global)
            {
                var threads = shape.Resolution / Lod.k_ThreadGroupSize;
                wrapper.Dispatch(threads, threads, slice);
            }
            else
            {
                base.Draw(simulation, buffer, target, pass, weight, slice);
            }
        }

        void GraphicsDraw(Lod simulation, CommandBuffer buffer, RenderTargetIdentifier target, int pass, float weight, int slice)
        {
            var lod = simulation as AnimatedWavesLod;

            var wrapper = new PropertyWrapperBuffer(buffer);
            SetRenderParameters(simulation._Water, wrapper);

            var isFirst = true;

            for (var i = _FirstCascade; i <= _LastCascade; i++)
            {
                _Wavelength = MinWavelength(i) / lod.WaveResolutionMultiplier;

                // Do the weight from scratch because this is the real filter.
                weight = AnimatedWavesLod.FilterByWavelength(simulation._Water, slice, _Wavelength) * Weight;
                if (weight <= 0f) continue;

                var average = _Wavelength * 1.5f * lod.WaveResolutionMultiplier;
                // We only have one renderer so we need to use global.
                buffer.SetGlobalFloat(ShaderIDs.s_AverageWavelength, average);
                buffer.SetGlobalInt(ShaderIDs.s_WaveBufferSliceIndex, i);

                // Only apply blend mode once per component / LOD. Multiple passes can happen to gather all
                // wavelengths and is incorrect to apply blend mode to those subsequent passes (ie component
                // would be blending against itself).
                if (!isFirst)
                {
                    s_RenderPassOverride = 1;
                }

                isFirst = false;

                base.Draw(simulation, buffer, target, pass, weight, slice);
            }

            // Wavelength must be zero or waves will be filtered beforehand and not be written to every LOD.
            _Wavelength = 0;
            s_RenderPassOverride = -1;
        }

        internal override float Filter(WaterRenderer water, int slice)
        {
            return 1f;
        }

        private protected const int k_CascadeCount = 16;

        // First cascade of wave buffer that has waves and will be rendered.
        private protected int _FirstCascade = -1;
        // Last cascade of wave buffer that has waves and will be rendered.
        // Default to lower than first default to break loops.
        private protected int _LastCascade = -2;

        // Used to populate data on first frame.
        private protected bool _FirstUpdate = true;

        // Wave generation done in Draw. Keeps track to limit to once per frame.
        private protected int _LastGenerateFrameCount = -1;

        internal override bool Enabled => _FirstCascade > -1 && WaterRenderer.Instance.Gravity != 0f && Mode switch
        {
            LodInputMode.Global => enabled && s_TransferWavesComputeShader != null,
            _ => base.Enabled,
        };

        internal override LodInputMode DefaultMode => LodInputMode.Global;
        internal override int Pass => (int)DisplacementPass.LodDependent;

        private protected override bool FollowHorizontalMotion => true;

        float _Wavelength;

        private protected RenderTexture _WaveBuffers;
        internal RenderTexture WaveBuffer => _WaveBuffers;

        internal Rect _Rect;

        private protected Vector2 _MaximumDisplacement;
        private protected float MaximumReportedHorizontalDisplacement { get; set; }
        private protected float MaximumReportedVerticalDisplacement { get; set; }
        private protected float MaximumReportedWavesDisplacement { get; set; }

        static int s_InstanceCount = 0;

        private protected bool UpdateDataEachFrame
        {
            get
            {
                var updateDataEachFrame = _EvaluateSpectrumAtRunTimeEveryFrame;
#if UNITY_EDITOR
                if (!Application.isPlaying) updateDataEachFrame = true;
#endif
                return updateDataEachFrame;
            }
        }

        /// <summary>
        /// Min wavelength for a cascade in the wave buffer. Does not depend on viewpoint.
        /// </summary>
        private protected float MinWavelength(int cascadeIdx)
        {
            var diameter = 0.5f * (1 << cascadeIdx);
            // Matches constant WAVE_SAMPLE_FACTOR in FFTSpectrum.compute
            return diameter / 8f;
        }

        private protected abstract void ReportMaxDisplacement(WaterRenderer water);
        private protected abstract void DestroySharedResources();

        private protected override void OnUpdate(WaterRenderer water)
        {
            base.OnUpdate(water);

            _ActiveSpectrum = _Spectrum != null ? _Spectrum : DefaultSpectrum;

            _FirstUpdate = false;
        }

        private protected virtual void SetRenderParameters<T>(WaterRenderer water, T wrapper) where T : IPropertyWrapper
        {
            wrapper.SetTexture(ShaderIDs.s_WaveBuffer, _WaveBuffers);
            wrapper.SetFloat(ShaderIDs.s_RespectShallowWaterAttenuation, _RespectShallowWaterAttenuation);
            wrapper.SetFloat(ShaderIDs.s_MaximumAttenuationDepth, water._AnimatedWavesLod.ShallowsMaximumDepth);
        }

        private protected override void Awake()
        {
            base.Awake();
            s_InstanceCount++;
        }

        private protected void OnDestroy()
        {
            // Since FFTCompute resources are shared we will clear after last ShapeFFT is destroyed.
            if (--s_InstanceCount <= 0)
            {
                DestroySharedResources();

                if (s_DefaultSpectrum != null)
                {
                    Helpers.Destroy(s_DefaultSpectrum);
                }
            }
        }

        private protected override void Initialize()
        {
            base.Initialize();

            WaterResources.Instance.AfterEnabled -= InitializeResources;
            WaterResources.Instance.AfterEnabled += InitializeResources;
            InitializeResources();

            _FirstUpdate = true;

            // Initialise with spectrum
            if (_Spectrum != null)
            {
                _ActiveSpectrum = _Spectrum;
            }

            if (_ActiveSpectrum == null)
            {
                _ActiveSpectrum = DefaultSpectrum;
            }
        }

        private protected override void OnDisable()
        {
            base.OnDisable();

            WaterResources.Instance.AfterEnabled -= InitializeResources;
        }

        void InitializeResources()
        {
            s_TransferWavesComputeShader = WaterResources.Instance.Compute._ShapeWavesTransfer;
            s_KeywordTexture = WaterResources.Instance.Keywords.AnimatedWavesTransferWavesTexture;
            s_KeywordTextureBlend = WaterResources.Instance.Keywords.AnimatedWavesTransferWavesTextureBlend;
        }

        bool ReportDisplacement(ref Rect bounds, ref float horizontal, ref float vertical)
        {
            if (Mode == LodInputMode.Global || !Enabled)
            {
                return false;
            }

            _Rect = Data.Rect;

            if (bounds.Overlaps(_Rect, false))
            {
                horizontal = MaximumReportedHorizontalDisplacement;
                vertical = MaximumReportedVerticalDisplacement;
                return true;
            }

            return false;
        }
    }

    partial class ShapeWaves
    {
        Reporter _Reporter;

        sealed class Reporter : IReportsDisplacement
        {
            readonly ShapeWaves _Input;
            public Reporter(ShapeWaves input) => _Input = input;
            public bool ReportDisplacement(ref Rect bounds, ref float horizontal, ref float vertical) => _Input.ReportDisplacement(ref bounds, ref horizontal, ref vertical);
        }
    }

    partial class ShapeWaves
    {
        [HideInInspector, SerializeField]
        AlphaSource _AlphaSource;
        enum AlphaSource { AlwaysOne, FromZero, FromZeroNormalized }

        private protected int MigrateV1(int version)
        {
            // Version 1
            // - Merge Alpha Source into Blend.
            // - Rename and invert Spectrum Fixed at Run-Time
            if (version < 1)
            {
                if (_Blend == LodInputBlend.Alpha)
                {
                    _Blend = _AlphaSource switch
                    {
                        AlphaSource.AlwaysOne => LodInputBlend.Off,
                        AlphaSource.FromZero => LodInputBlend.Alpha,
                        AlphaSource.FromZeroNormalized => LodInputBlend.AlphaClip,
                        _ => _Blend, // Linter complained (linter has one off error).
                    };
                }

                _EvaluateSpectrumAtRunTimeEveryFrame = !_EvaluateSpectrumAtRunTimeEveryFrame;

                version = 1;
            }

            return version;
        }
    }
}
