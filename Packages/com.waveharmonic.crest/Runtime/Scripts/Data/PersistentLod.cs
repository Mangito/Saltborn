// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// A persistent simulation that moves around with a displacement LOD.
    /// </summary>
    public abstract partial class PersistentLod : Lod
    {
        [Tooltip("Frequency to run the simulation, in updates per second.\n\nLower frequencies are more efficient but may lead to visible jitter or slowness.")]
        [@Range(15, 200, order = -1000)]
        [@GenerateAPI]
        [SerializeField]
        private protected int _SimulationFrequency = 60;

        static new class ShaderIDs
        {
            public static readonly int s_SimDeltaTime = Shader.PropertyToID("_Crest_SimDeltaTime");
            public static readonly int s_SimDeltaTimePrev = Shader.PropertyToID("_Crest_SimDeltaTimePrev");
            public static readonly int s_TemporaryPersistentTarget = Shader.PropertyToID("_Crest_TemporaryPersistentTarget");
        }

        private protected override bool NeedToReadWriteTextureData => true;
        internal override int BufferCount => 2;

        float _PreviousSubstepDeltaTime = 1f / 60f;

        // Is this the first step since being enabled?
        private protected bool _NeedsPrewarmingThisStep = true;

        // This is how far the simulation time is behind Unity's time.
        private protected float _TimeToSimulate = 0f;

        internal int LastUpdateSubstepCount { get; private set; }

        private protected abstract ComputeShader SimulationShader { get; }
        private protected abstract void GetSubstepData(float timeToSimulate, out int substeps, out float delta);

        internal override void Initialize()
        {
            if (SimulationShader == null)
            {
                _Valid = false;
                return;
            }

            base.Initialize();

            _NeedsPrewarmingThisStep = true;
        }

        internal override void ClearLodData()
        {
            base.ClearLodData();
            _Targets.RunLambda(x => Clear(x));
        }

        internal override void BuildCommandBuffer(WaterRenderer water, CommandBuffer buffer)
        {
            buffer.BeginSample(Name);

            FlipBuffers();

            var slices = water.LodLevels;

            // How far are we behind.
            _TimeToSimulate += water.DeltaTime;

            // Do a set of substeps to catch up.
            GetSubstepData(_TimeToSimulate, out var substeps, out var delta);

            LastUpdateSubstepCount = substeps;

            // Even if no steps were needed this frame, the simulation still needs to advect to
            // compensate for camera motion / water scale changes, so do a trivial substep.
            // This could be a specialised kernel that only advects, or the simulation shader
            // could have a branch for 0 delta time.
            if (substeps == 0)
            {
                substeps = 1;
                delta = 0f;
            }

            if (substeps > 1)
            {
                // No need to clear, as the update dispatch overwrites every pixel, but finding
                // artifacts if not and there is a renderer input. Happens for foam and dynamic
                // waves. Confusing/concerning.
                buffer.GetTemporaryRT(ShaderIDs.s_TemporaryPersistentTarget, DataTexture.descriptor);
                buffer.SetRenderTarget(ShaderIDs.s_TemporaryPersistentTarget, 0, CubemapFace.Unknown, -1);
                buffer.ClearRenderTarget(RTClearFlags.Color, ClearColor, 0, 0);
            }

            var target = new RenderTargetIdentifier(DataTexture);
            var source = new RenderTargetIdentifier(ShaderIDs.s_TemporaryPersistentTarget);
            var current = target;

            for (var substep = 0; substep < substeps; substep++)
            {
                var isFirstStep = substep == 0;
                var frame = isFirstStep ? 1 : 0;
                var wrapper = new PropertyWrapperCompute(buffer, SimulationShader, 0);

                // Record how much we caught up
                _TimeToSimulate -= delta;

                // Buffers are already flipped, but we need to ping-pong for subsequent substeps.
                if (!isFirstStep)
                {
                    // Use temporary target for ping-pong instead of flipping buffer. We do not want
                    // to buffer substeps as they will not match buffered cascade data etc. Each buffer
                    // entry must be for a single frame and substeps are "sub-frame".
                    (source, target) = (target, source);
                }
                else
                {
                    // We only want to handle teleports for the first step.
                    _NeedsPrewarmingThisStep = _NeedsPrewarmingThisStep || _Water._HasTeleportedThisFrame;
                }

                // Both simulation update and input draws need delta time.
                buffer.SetGlobalFloat(ShaderIDs.s_SimDeltaTime, delta);
                buffer.SetGlobalFloat(ShaderIDs.s_SimDeltaTimePrev, _PreviousSubstepDeltaTime);

                wrapper.SetTexture(Crest.ShaderIDs.s_Target, target);
                wrapper.SetTexture(_TextureSourceShaderID, isFirstStep ? _Targets.Previous(1) : source);

                // Compute which LOD data we are sampling source data from. if a scale change has
                // happened this can be any LOD up or down the chain. This is only valid on the
                // first update step, after that the scale source/target data are in the right
                // places.
                wrapper.SetFloat(Lod.ShaderIDs.s_LodChange, isFirstStep ? _Water.ScaleDifferencePower2 : 0);

                wrapper.SetVectorArray(WaterRenderer.ShaderIDs.s_CascadeDataSource, _Water._CascadeData.Previous(frame));
                wrapper.SetVectorArray(_SamplingParametersCascadeSourceShaderID, _SamplingParameters.Previous(frame));

                SetAdditionalSimulationParameters(wrapper);

                var threads = Resolution / k_ThreadGroupSize;
                wrapper.Dispatch(threads, threads, Slices);

                // Only add forces if we did a step.
                if (delta > 0f)
                {
                    SubmitDraws(buffer, Inputs, target);
                }

                // The very first step since being enabled.
                _NeedsPrewarmingThisStep = false;
                _PreviousSubstepDeltaTime = delta;
            }

            // Swap textures if needed.
            if (target != current)
            {
                buffer.CopyTexture(target, DataTexture);
            }

            if (substeps > 1)
            {
                buffer.ReleaseTemporaryRT(ShaderIDs.s_TemporaryPersistentTarget);
            }

            // Set the target texture as to make sure we catch the 'pong' each frame.
            Shader.SetGlobalTexture(_TextureShaderID, DataTexture);

            buffer.EndSample(Name);
        }

        /// <summary>
        /// Set any simulation specific shader parameters.
        /// </summary>
        private protected virtual void SetAdditionalSimulationParameters<T>(T properties) where T : IPropertyWrapper
        {
        }
    }
}
