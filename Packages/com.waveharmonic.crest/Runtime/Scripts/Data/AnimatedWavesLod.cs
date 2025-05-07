// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace WaveHarmonic.Crest
{
    abstract class BakedWaveData : ScriptableObject
    {
        public abstract ICollisionProvider CreateCollisionProvider();
        public abstract float WindSpeed { get; }
    }

    //
    // Collision
    //

    /// <summary>
    /// The source of collisions (ie water shape).
    /// </summary>
    public enum CollisionSource
    {
        /// <summary>
        /// No collision source. Flat water.
        /// </summary>
        None = 0,

        // GerstnerWavesCPU = 1,

        /// <summary>
        /// Uses AsyncGPUReadback to retrieve data from GPU to CPU.
        /// </summary>
        /// <remarks>
        /// This is the most optimal approach.
        /// </remarks>
        GPU = 2,

        /// <summary>
        /// Computes data entirely on the CPU.
        /// </summary>
        CPU = 3,
    }

    /// <summary>
    /// The pass to render displacement into.
    /// </summary>
    public enum DisplacementPass
    {
        /// <summary>
        /// Displacement that is dependent on an LOD (eg waves).
        /// </summary>
        /// <remarks>
        /// Uses filtering to determine which LOD to write to.
        /// </remarks>
        [Tooltip("Displacement that is dependent on an LOD (eg waves).\n\nUses filtering to determine which LOD to write to.")]
        LodDependent,

        /// <summary>
        /// Renders to all LODs.
        /// </summary>
        [Tooltip("Renders to all LODs.")]
        LodIndependent,

        /// <summary>
        /// Renders to all LODs, but as a separate pass.
        /// </summary>
        /// <remarks>
        /// Typically used to render visual displacement which does not affect collisions.
        /// </remarks>
        [Tooltip("Renders to all LODs, but as a separate pass.\n\nTypically used to render visual displacement which does not affect collisions.")]
        [InspectorName("Lod Independent (Last)")]
        LodIndependentLast,
    }

    /// <summary>
    /// Flags to enable extra collsion layers.
    /// </summary>
    [System.Flags]
    public enum CollisionLayers
    {
        // NOTE: numbers must be in order for defaults to work (everything first).

        /// <summary>
        /// All layers.
        /// </summary>
        [Tooltip("All layers.")]
        Everything = -1,

        /// <summary>
        /// No extra layers (ie single layer).
        /// </summary>
        [Tooltip("No extra layers (ie single layer).")]
        Nothing,

        /// <summary>
        /// Separate layer for dynamic waves.
        /// </summary>
        /// <remarks>
        /// Dynamic waves are normally combined together for efficiency. By enabling this
        /// layer, dynamic waves are combined and added in a separate pass.
        /// </remarks>
        [Tooltip("Separate layer for dynamic waves.\n\nDynamic waves are normally combined together for efficiency. By enabling this layer, dynamic waves are combined and added in a separate pass.")]
        DynamicWaves = 1 << 1,

        /// <summary>
        /// Extra displacement layer for visual displacement.
        /// </summary>
        [Tooltip("Extra displacement layer for visual displacement.")]
        Displacement = 1 << 2,
    }

    /// <summary>
    /// Captures waves/shape that is drawn kinematically - there is no frame-to-frame
    /// state.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>
    ///   A combine pass is done which combines downwards from low detail LODs into
    ///   the high detail LODs.
    /// </item>
    /// <item>
    ///   The LOD data is passed to the water material when the surface is drawn.
    /// <item>
    /// </item>
    ///   <see cref="DynamicWavesLod"/> adds its results into this data. They piggy back
    ///   off the combine pass and subsequent assignment to the water material.
    /// </item>
    /// </list>
    /// The RGB channels are the XYZ displacement from a rest plane at water level to
    /// the corresponding displaced position on the surface.
    /// </remarks>
    [@FilterEnum(nameof(_TextureFormatMode), Filtered.Mode.Exclude, (int)LodTextureFormatMode.Automatic)]
    public sealed partial class AnimatedWavesLod : Lod<ICollisionProvider>
    {
        [@Space(10)]

        [Tooltip("Shifts wavelengths to maintain quality for higher resolutions.\n\nSet this to 2 to improve wave quality. In some cases like flowing rivers, this can make a substantial difference to visual stability. We recommend doubling the Resolution on the WaterRenderer component to preserve detail after making this change.")]
        [@Range(1f, 4f)]
        [@GenerateAPI]
        [SerializeField]
        float _WaveResolutionMultiplier = 1f;

        [Tooltip("How much waves are dampened in shallow water.")]
        [@Range(0f, 1f)]
        [@GenerateAPI]
        [SerializeField]
        float _AttenuationInShallows = 0.95f;

        [Tooltip("Any water deeper than this will receive full wave strength.\n\nThe lower the value, the less effective the depth cache will be at attenuating very large waves. Set to the maximum value (1,000) to disable.")]
        [@Range(1f, 1000f)]
        [@GenerateAPI]
        [SerializeField]
        float _ShallowsMaximumDepth = 1000f;


        [@Heading("Collisions")]

        [Tooltip("Where to obtain water shape on CPU for physics / gameplay.")]
        [@GenerateAPI(Setter.Internal)]
        [@DecoratedField, SerializeField]
        internal CollisionSource _CollisionSource = CollisionSource.GPU;

        [Tooltip("Collision layers to enable.\n\nSome layers will have overhead with CPU, GPU and memory.")]
        [@Predicated(nameof(_CollisionSource), inverted: true, nameof(CollisionSource.GPU))]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal CollisionLayers _CollisionLayers = CollisionLayers.Everything;

        [Tooltip("Maximum number of wave queries that can be performed when using GPU queries.")]
        [@Predicated(nameof(_CollisionSource), true, nameof(CollisionSource.GPU))]
        [@GenerateAPI(Setter.None)]
        [@DecoratedField, SerializeField]
        int _MaximumQueryCount = QueryBase.k_DefaultMaximumQueryCount;

        [@Predicated(nameof(_CollisionSource), true, nameof(CollisionSource.CPU))]
        [@DecoratedField, SerializeField]
        internal BakedWaveData _BakedWaveData;


        internal static new partial class ShaderIDs
        {
            public static readonly int s_WaveBuffer = Shader.PropertyToID("_Crest_WaveBuffer");
            public static readonly int s_DynamicWavesTarget = Shader.PropertyToID("_Crest_DynamicWavesTarget");
            public static readonly int s_AnimatedWavesTarget = Shader.PropertyToID("_Crest_AnimatedWavesTarget");
            public static readonly int s_AttenuationInShallows = Shader.PropertyToID("_Crest_AttenuationInShallows");
        }

        internal static readonly Color s_GizmoColor = new(0f, 1f, 0f, 0.5f);

        /// <summary>
        /// Turn shape combine pass on/off. Debug only - stripped in builds.
        /// </summary>
        internal static bool s_Combine = true;

        internal override string ID => "AnimatedWaves";
        internal override string Name => "Animated Waves";
        internal override Color GizmoColor => s_GizmoColor;
        private protected override bool NeedToReadWriteTextureData => true;
        private protected override Color ClearColor => Color.black;
        internal override int BufferCount => _Water.WriteMotionVectors ? 2 : 1;
        internal override bool RunsInHeadless => true;

        // NOTE: Tried RGB111110Float but errors becomes visible. One option would be to use a UNORM setup.
        private protected override GraphicsFormat RequestedTextureFormat => _TextureFormatMode switch
        {
            LodTextureFormatMode.Performance => GraphicsFormat.R16G16B16A16_SFloat,
            LodTextureFormatMode.Precision => GraphicsFormat.R32G32B32A32_SFloat,
            LodTextureFormatMode.Manual => _TextureFormat,
            _ => throw new System.NotImplementedException(),
        };

        ComputeShader _CombineShader;

        int _KernalShapeCombine = -1;
        int _KernalShapeCombine_DISABLE_COMBINE = -1;
        int _KernalShapeCombine_FLOW_ON = -1;
        int _KernalShapeCombine_FLOW_ON_DISABLE_COMBINE = -1;
        int _KernalShapeCombine_DYNAMIC_WAVE_SIM_ON = -1;
        int _KernalShapeCombine_DYNAMIC_WAVE_SIM_ON_DISABLE_COMBINE = -1;
        int _KernalShapeCombine_FLOW_ON_DYNAMIC_WAVE_SIM_ON = -1;
        int _KernalShapeCombine_FLOW_ON_DYNAMIC_WAVE_SIM_ON_DISABLE_COMBINE = -1;


        internal AnimatedWavesLod()
        {
            _Enabled = true;
            _OverrideResolution = false;
            _TextureFormat = GraphicsFormat.R16G16B16A16_SFloat;
        }

        internal override void Initialize()
        {
            _CombineShader = WaterResources.Instance.Compute._ShapeCombine;
            if (_CombineShader == null)
            {
                _Valid = false;
                return;
            }

            base.Initialize();
        }

        private protected override void Allocate()
        {
            base.Allocate();

            _KernalShapeCombine = _CombineShader.FindKernel("ShapeCombine");
            _KernalShapeCombine_DISABLE_COMBINE = _CombineShader.FindKernel("ShapeCombine_DISABLE_COMBINE");
            _KernalShapeCombine_FLOW_ON = _CombineShader.FindKernel("ShapeCombine_FLOW_ON");
            _KernalShapeCombine_FLOW_ON_DISABLE_COMBINE = _CombineShader.FindKernel("ShapeCombine_FLOW_ON_DISABLE_COMBINE");
            _KernalShapeCombine_DYNAMIC_WAVE_SIM_ON = _CombineShader.FindKernel("ShapeCombine_DYNAMIC_WAVE_SIM_ON");
            _KernalShapeCombine_DYNAMIC_WAVE_SIM_ON_DISABLE_COMBINE = _CombineShader.FindKernel("ShapeCombine_DYNAMIC_WAVE_SIM_ON_DISABLE_COMBINE");
            _KernalShapeCombine_FLOW_ON_DYNAMIC_WAVE_SIM_ON = _CombineShader.FindKernel("ShapeCombine_FLOW_ON_DYNAMIC_WAVE_SIM_ON");
            _KernalShapeCombine_FLOW_ON_DYNAMIC_WAVE_SIM_ON_DISABLE_COMBINE = _CombineShader.FindKernel("ShapeCombine_FLOW_ON_DYNAMIC_WAVE_SIM_ON_DISABLE_COMBINE");
        }

        internal override void BuildCommandBuffer(WaterRenderer water, CommandBuffer buffer)
        {
            buffer.BeginSample(Name);

            FlipBuffers();

            Shader.SetGlobalFloat(ShaderIDs.s_AttenuationInShallows, _AttenuationInShallows);

            // Get temporary buffer to store waves. They will be copied in the combine pass.
            buffer.GetTemporaryRT(ShaderIDs.s_WaveBuffer, DataTexture.descriptor);
            buffer.SetRenderTarget(ShaderIDs.s_WaveBuffer, 0, CubemapFace.Unknown, -1);
            buffer.ClearRenderTarget(false, true, ClearColor);

            // LOD dependent data.
            // Write to per-octave _WaveBuffers. Not the same as _AnimatedWaves.
            // Draw any data with lod preference.
            SubmitDraws(buffer, s_Inputs, ShaderIDs.s_WaveBuffer, (int)DisplacementPass.LodDependent, filter: true);

            var lastSlice = Slices - 1;
            var threadSize = Resolution / k_ThreadGroupSize;

            // Combine the LODs - copy results from biggest LOD down to LOD 0
            {
                var combineShaderKernel = _KernalShapeCombine;
                var combineShaderKernel_lastLOD = _KernalShapeCombine_DISABLE_COMBINE;
                {
                    var isFlowOn = _Water._FlowLod.Enabled;
                    var isDynamicWavesOn = _Water._DynamicWavesLod.Enabled && !_CollisionLayers.HasFlag(CollisionLayers.DynamicWaves);

                    // Set the shader kernels that we will use.
                    if (isFlowOn && isDynamicWavesOn)
                    {
                        combineShaderKernel = _KernalShapeCombine_FLOW_ON_DYNAMIC_WAVE_SIM_ON;
                        combineShaderKernel_lastLOD = _KernalShapeCombine_FLOW_ON_DYNAMIC_WAVE_SIM_ON_DISABLE_COMBINE;
                    }
                    else if (isFlowOn)
                    {
                        combineShaderKernel = _KernalShapeCombine_FLOW_ON;
                        combineShaderKernel_lastLOD = _KernalShapeCombine_FLOW_ON_DISABLE_COMBINE;
                    }
                    else if (isDynamicWavesOn)
                    {
                        combineShaderKernel = _KernalShapeCombine_DYNAMIC_WAVE_SIM_ON;
                        combineShaderKernel_lastLOD = _KernalShapeCombine_DYNAMIC_WAVE_SIM_ON_DISABLE_COMBINE;
                    }
                }

                buffer.BeginSample("Combine");

                // Combine waves.
                for (var slice = lastSlice; slice >= 0; slice--)
                {
                    var kernel = slice < lastSlice && s_Combine
                        ? combineShaderKernel : combineShaderKernel_lastLOD;

                    var wrapper = new PropertyWrapperCompute(buffer, _CombineShader, kernel);

                    // The per-octave wave buffers we read from.
                    wrapper.SetTexture(ShaderIDs.s_WaveBuffer, ShaderIDs.s_WaveBuffer);

                    if (_Water._DynamicWavesLod.Enabled) _Water._DynamicWavesLod.Bind(wrapper);

                    // Set the animated waves texture where we read/write to combine the results. Use
                    // compute suffix to avoid collision as a file already uses the normal name.
                    wrapper.SetTexture(Crest.ShaderIDs.s_Target, DataTexture);
                    wrapper.SetInteger(Lod.ShaderIDs.s_LodIndex, slice);

                    wrapper.Dispatch(threadSize, threadSize, 1);
                }

                buffer.EndSample("Combine");
            }

            buffer.ReleaseTemporaryRT(ShaderIDs.s_WaveBuffer);

            // LOD independent data.
            // Draw any data that did not express a preference for one lod or another.
            var drawn = SubmitDraws(buffer, s_Inputs, DataTexture, (int)DisplacementPass.LodIndependent);

            // Alpha channel is cleared in combine step, but if any inputs draw in post-combine
            // step then alpha may have data.
            var clear = WaterResources.Instance.Compute._Clear;
            if (drawn && clear != null)
            {
                buffer.SetComputeTextureParam(clear, 0, Crest.ShaderIDs.s_Target, DataTexture);
                buffer.SetComputeVectorParam(clear, Crest.ShaderIDs.s_ClearMask, Color.black);
                buffer.SetComputeVectorParam(clear, Crest.ShaderIDs.s_ClearColor, Color.clear);
                buffer.DispatchCompute
                (
                    clear,
                    0,
                    Resolution / k_ThreadGroupSizeX,
                    Resolution / k_ThreadGroupSizeY,
                    Slices
                );
            }

            // Pack height data into alpha channel.
            // We do not add height to displacement directly for better precision and layering.
            var heightShader = WaterResources.Instance.Compute._PackLevel;
            if (_Water._LevelLod.Enabled && heightShader != null)
            {
                buffer.SetComputeTextureParam(heightShader, 0, Crest.ShaderIDs.s_Target, DataTexture);
                buffer.DispatchCompute
                (
                    heightShader,
                    0,
                    Resolution / k_ThreadGroupSizeX,
                    Resolution / k_ThreadGroupSizeY,
                    Slices
                );
            }

            // Query collisions including only Animated Waves.
            // Requires copying the water level.
            Provider.UpdateQueries(_Water, CollisionLayer.AfterAnimatedWaves);

            // Transfer Dynamic Waves to Animated Waves.
            if (_CollisionLayers.HasFlag(CollisionLayers.DynamicWaves) && _Water._DynamicWavesLod.Enabled)
            {
                buffer.BeginSample("Combine");
                // Clearing not required as we overwrite enter texture.
                buffer.GetTemporaryRT(ShaderIDs.s_DynamicWavesTarget, DataTexture.descriptor);

                var wrapper = new PropertyWrapperCompute(buffer, _CombineShader, 8);

                wrapper.SetTexture(ShaderIDs.s_DynamicWavesTarget, ShaderIDs.s_DynamicWavesTarget);

                _Water._DynamicWavesLod.Bind(wrapper);

                // Compute displacement from Dynamic Waves.
                for (var slice = lastSlice; slice >= 0; slice--)
                {
                    wrapper.SetInteger(Lod.ShaderIDs.s_LodIndex, slice);
                    wrapper.Dispatch(threadSize, threadSize, 1);
                }

                // Copy Dynamic Waves displacement into Animated Waves.
                {
                    wrapper = new PropertyWrapperCompute(buffer, _CombineShader, 9);
                    wrapper.SetTexture(ShaderIDs.s_AnimatedWavesTarget, DataTexture);
                    wrapper.SetTexture(ShaderIDs.s_DynamicWavesTarget, ShaderIDs.s_DynamicWavesTarget);
                    wrapper.Dispatch(threadSize, threadSize, Slices);
                }

                buffer.ReleaseTemporaryRT(ShaderIDs.s_DynamicWavesTarget);
                buffer.EndSample("Combine");

                // Query collisions including Dynamic Waves.
                // Does not require copying the water level as they are added with zero alpha.
                _Water.CollisionProvider.UpdateQueries(_Water, CollisionLayer.AfterDynamicWaves);
            }

            if (_CollisionLayers.HasFlag(CollisionLayers.Displacement))
            {
                // LOD independent data.
                // Draw any data that did not express a preference for one lod or another.
                drawn = SubmitDraws(buffer, s_Inputs, DataTexture, (int)DisplacementPass.LodIndependentLast);
            }

            if (_CollisionLayers == CollisionLayers.Nothing || _CollisionLayers.HasFlag(CollisionLayers.Displacement))
            {
                Queryable?.UpdateQueries(_Water);
            }

            if (BufferCount > 1)
            {
                // Update current and previous. Latter for MVs and/or VFX.
                Shader.SetGlobalTexture(_TextureSourceShaderID, _Targets.Previous(1));
                Shader.SetGlobalTexture(_TextureShaderID, DataTexture);
            }

            buffer.EndSample(Name);
        }

        /// <summary>
        /// Provides water shape to CPU.
        /// </summary>
        private protected override ICollisionProvider CreateProvider(bool enable)
        {
            ICollisionProvider result = null;

            Queryable?.CleanUp();

            if (!enable)
            {
                return ICollisionProvider.None;
            }

            switch (_CollisionSource)
            {
                case CollisionSource.None:
                    result = ICollisionProvider.None;
                    break;
                case CollisionSource.GPU:
                    if (_Valid && !_Water.IsRunningWithoutGraphics)
                    {
                        result = new CollisionQueryWithPasses(_Water);
                    }

                    if (_Water.IsRunningWithoutGraphics)
                    {
                        Debug.LogError($"Crest: GPU queries not supported in headless/batch mode. To resolve, assign an Animated Wave Settings asset to the {nameof(WaterRenderer)} component and set the Collision Source to be a CPU option.");
                    }
                    break;
                case CollisionSource.CPU:
                    if (_BakedWaveData != null)
                    {
                        result = _BakedWaveData.CreateCollisionProvider();
                    }
                    break;
            }

            if (result == null)
            {
                // This should not be hit, but can be if compute shaders aren't loaded correctly.
                // They will print out appropriate errors. Don't just return null and have null reference
                // exceptions spamming the logs.
                return ICollisionProvider.None;
            }

            return result;
        }

        //
        // DrawFilter
        //

        internal readonly struct WavelengthFilter
        {
            public readonly float _Minimum;
            public readonly float _Maximum;
            public readonly float _TransitionThreshold;
            public readonly float _ViewerAltitudeLevelAlpha;
            public readonly int _Slice;
            public readonly int _Slices;

            public WavelengthFilter(WaterRenderer water, int slice)
            {
                _Slice = slice;
                _Slices = water.LodLevels;
                _Maximum = water.MaximumWavelength(slice);
                _Minimum = _Maximum * 0.5f;
                _TransitionThreshold = water.MaximumWavelength(_Slices - 1) * 0.5f;
                _ViewerAltitudeLevelAlpha = water.ViewerAltitudeLevelAlpha;
            }
        }

        internal static float FilterByWavelength(WavelengthFilter filter, float wavelength)
        {
            // No wavelength preference - don't draw per-lod
            if (wavelength == 0f)
            {
                return 0f;
            }

            // Too small for this lod
            if (wavelength < filter._Minimum)
            {
                return 0f;
            }

            // If approaching end of lod chain, start smoothly transitioning any large wavelengths across last two lods
            if (wavelength >= filter._TransitionThreshold)
            {
                if (filter._Slice == filter._Slices - 2)
                {
                    return 1f - filter._ViewerAltitudeLevelAlpha;
                }

                if (filter._Slice == filter._Slices - 1)
                {
                    return filter._ViewerAltitudeLevelAlpha;
                }
            }
            else if (wavelength < filter._Maximum)
            {
                // Fits in this lod
                return 1f;
            }

            return 0f;
        }

        internal static float FilterByWavelength(WaterRenderer water, int slice, float wavelength)
        {
            return FilterByWavelength(new(water, slice), wavelength);
        }


        //
        // Inputs
        //

        internal static readonly Utility.SortedList<int, ILodInput> s_Inputs = new(Helpers.DuplicateComparison);
        private protected override Utility.SortedList<int, ILodInput> Inputs => s_Inputs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnLoad()
        {
            s_Inputs.Clear();
        }

#if UNITY_EDITOR
        [@OnChange]
        private protected override void OnChange(string propertyPath, object previousValue)
        {
            base.OnChange(propertyPath, previousValue);

            switch (propertyPath)
            {
                case nameof(_CollisionLayers):
                case nameof(_CollisionSource):
                    if (_Water == null || !_Water.isActiveAndEnabled || !Enabled) return;
                    Queryable?.CleanUp();
                    InitializeProvider(true);
                    break;
            }
        }
#endif
    }
}
