// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Approximates the interaction between a sphere and the water.
    /// </summary>
    /// <remarks>
    /// Multiple spheres can be used to model the interaction of a non-spherical shape.
    /// </remarks>
    [AddComponentMenu(Constants.k_MenuPrefixInputs + "Sphere Water Interaction")]
    [@HelpURL("Manual/Waves.html#adding-interaction-forces")]
    public sealed partial class SphereWaterInteraction : ManagedBehaviour<WaterRenderer>
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        [Tooltip("Radius of the sphere that is modelled from which the interaction forces are calculated.")]
        [@Range(0.01f, 50f)]
        [@GenerateAPI]
        [SerializeField]
        internal float _Radius = 1f;

        [Tooltip("Intensity of the forces.\n\nCan be set negative to invert.")]
        [@Range(-40f, 40f)]
        [@GenerateAPI]
        [SerializeField]
        float _Weight = 1f;

        [Tooltip("Intensity of the forces from vertical motion of the sphere.\n\nScales ripples generated from a sphere moving up or down.")]
        [@Range(0f, 2f)]
        [@GenerateAPI]
        [SerializeField]
        float _WeightVerticalMultiplier = 0.5f;

        [Tooltip("Model parameter that can be used to modify the shape of the interaction.\n\nInternally the interaction is modelled by a pair of nested spheres. The forces from the two spheres combine to create the final effect. This parameter scales the effect of the inner sphere and can be tweaked to adjust the shape of the result.")]
        [@Range(0f, 10f)]
        [@GenerateAPI]
        [SerializeField]
        float _InnerSphereMultiplier = 1.55f;

        [Tooltip("Model parameter that can be used to modify the shape of the interaction.\n\nThis parameter controls the size of the inner sphere and can be tweaked to give further control over the result.")]
        [@Range(0f, 1f)]
        [@GenerateAPI]
        [SerializeField]
        float _InnerSphereOffset = 0.109f;

        [Tooltip("Offset in direction of motion to help ripples appear in front of sphere.\n\nThere is some latency between applying a force to the wave simualtion and the resulting waves appearing. Applying this offset can help to ensure the waves do not lag behind the sphere.")]
        [@Range(0f, 2f)]
        [@GenerateAPI]
        [SerializeField]
        internal float _VelocityOffset = 0.04f;

        [Tooltip("How much to correct the position for horizontal wave displacement.\n\nIf set to 0, the input will always be applied at a fixed position before any horizontal displacement from waves. If waves are large then their displacement may cause the interactive waves to drift away from the object. This parameter can be increased to compensate for this displacement and combat this issue. However increasing too far can cause a feedback loop which causes strong 'ring' artifacts to appear in the dynamic waves. This parameter can be tweaked to balance this two effects.")]
        [@Range(0f, 1f)]
        [@GenerateAPI]
        [SerializeField]
        float _CompensateForWaveMotion = 0.45f;

        [Tooltip("Whether to improve visibility in larger LODs.\n\nIf the dynamic waves are not visible far enough in the distance from the camera, this can be used to boost the output.")]
        [@GenerateAPI]
        [SerializeField]
        bool _BoostLargeWaves = false;


        [Header("Limits")]

        [Tooltip("Teleport speed (km/h).\n\nIf the calculated speed is larger than this amount, the object is deemed to have teleported and the computed velocity is discarded.")]
        [@GenerateAPI]
        [SerializeField]
        float _TeleportSpeed = 500f;

        [Tooltip("Outputs a warning to the console on teleport.")]
        [@GenerateAPI]
        [SerializeField]
        bool _WarnOnTeleport = false;

        [Tooltip("Maximum speed clamp (km/h).\n\nUseful for controlling/limiting wake.")]
        [@GenerateAPI]
        [SerializeField]
        float _MaximumSpeed = 100f;

        [Tooltip("Outputs a warning to the console on speed clamp.")]
        [@GenerateAPI]
        [SerializeField]
        bool _WarnOnSpeedClamp = false;

#pragma warning disable 414
        [Header("Debug")]

        [Tooltip("Draws debug lines at each substep position. Editor only.")]
        [SerializeField]
        bool _DebugSubsteps = false;
#pragma warning restore 414

        static class ShaderIDs
        {
            public static readonly int s_Velocity = Shader.PropertyToID("_Crest_Velocity");
            public static readonly int s_Weight = Shader.PropertyToID("_Crest_Weight");
            public static readonly int s_Radius = Shader.PropertyToID("_Crest_Radius");
            public static readonly int s_InnerSphereOffset = Shader.PropertyToID("_Crest_InnerSphereOffset");
            public static readonly int s_InnerSphereMultiplier = Shader.PropertyToID("_Crest_InnerSphereMultiplier");
            public static readonly int s_LargeWaveMultiplier = Shader.PropertyToID("_Crest_LargeWaveMultiplier");
        }

        internal Vector3 _Velocity;
        Vector3 _VelocityClamped;
        Vector3 _PreviousPosition;
        Vector3 _RelativeVelocity;
        Vector3 _Displacement;

        float _WeightThisFrame;

        readonly SampleCollisionHelper _SampleHeightHelper = new();
        readonly SampleFlowHelper _SampleFlowHelper = new();

        static ComputeShader ComputeShader => WaterResources.Instance.Compute._SphereWaterInteraction;
        Rect Rect => new
        (
            transform.position.XZ() - _Displacement.XZ() * _CompensateForWaveMotion - Vector2.one * (_Radius * 4f * 0.5f),
            Vector2.one * (_Radius * 4f)
        );

        private protected override System.Action<WaterRenderer> OnUpdateMethod => OnUpdate;
        void OnUpdate(WaterRenderer water)
        {
            _SampleHeightHelper.SampleDisplacement(transform.position, out _Displacement, minimumLength: 2f * _Radius);

            LateUpdateComputeVel(water);

            // Velocity relative to water
            _RelativeVelocity = _VelocityClamped;
            {
                _SampleFlowHelper.Sample(transform.position, out var surfaceFlow, minimumLength: 2f * _Radius);
                _RelativeVelocity -= new Vector3(surfaceFlow.x, 0, surfaceFlow.y);

                _RelativeVelocity.y *= _WeightVerticalMultiplier;
            }

            // Use weight from user with a multiplier to make interactions look plausible
            _WeightThisFrame = 3.75f * _Weight;

            var waterHeight = _Displacement.y + water.SeaLevel;
            LateUpdateSphereWeight(waterHeight, ref _WeightThisFrame);

            // Weighting with this value helps keep ripples consistent for different gravity values
            var gravityMul = Mathf.Sqrt(water._DynamicWavesLod.Settings._GravityMultiplier) / 5f;
            _WeightThisFrame *= gravityMul;

            _PreviousPosition = transform.position;
        }

        // Velocity of the sphere, relative to the water. Computes on the fly, discards if teleport detected.
        void LateUpdateComputeVel(WaterRenderer water)
        {
            // Compue vel using finite difference
            _Velocity = (transform.position - _PreviousPosition) / water.DeltaTime;
            if (water.DeltaTime < 0.0001f)
            {
                _Velocity = Vector3.zero;
            }

            var speedKmh = _Velocity.magnitude * 3.6f;
            if (speedKmh > _TeleportSpeed)
            {
                // teleport detected
                _Velocity *= 0f;

                if (_WarnOnTeleport)
                {
                    Debug.LogWarning("Crest: Teleport detected (speed = " + speedKmh.ToString() + "), velocity discarded.", this);
                }

                speedKmh = _Velocity.magnitude * 3.6f;
            }

            if (speedKmh > _MaximumSpeed)
            {
                // limit speed to max
                _VelocityClamped = _Velocity * _MaximumSpeed / speedKmh;

                if (_WarnOnSpeedClamp)
                {
                    Debug.LogWarning("Crest: Speed (" + speedKmh.ToString() + ") exceeded max limited, clamped.", this);
                }
            }
            else
            {
                _VelocityClamped = _Velocity;
            }
        }

        // Weight based on submerged-amount of sphere
        void LateUpdateSphereWeight(float waterHeight, ref float weight)
        {
            var centerDepthInWater = waterHeight - transform.position.y;

            if (centerDepthInWater >= 0f)
            {
                // Center in water - exponential fall off of interaction influence as object gets deeper
                var prop = centerDepthInWater / _Radius;
                prop *= 0.5f;
                weight *= Mathf.Exp(-prop * prop);
            }
            else
            {
                // Center out of water - ramp off with square root, weight goes to 0 when sphere is just touching water
                var height = -centerDepthInWater;
                var heightProp = 1f - Mathf.Clamp01(height / _Radius);
                weight *= Mathf.Sqrt(heightProp);
            }
        }

        private protected override void Initialize()
        {
            base.Initialize();
            _Input ??= new(this);
            ILodInput.Attach(_Input, DynamicWavesLod.s_Inputs);
            _PreviousPosition = transform.position;
        }

        private protected override void OnDisable()
        {
            base.OnDisable();
            ILodInput.Detach(_Input, DynamicWavesLod.s_Inputs);
        }

        void Draw(Lod simulation, CommandBuffer buffer, RenderTargetIdentifier target, int pass = -1, float weight = 1f, int slices = -1)
        {
            var waves = simulation as DynamicWavesLod;
            var timeBeforeCurrentTime = waves.TimeLeftToSimulate;

            var wrapper = new PropertyWrapperCompute(buffer, ComputeShader, 0);

#if UNITY_EDITOR
            // Draw debug lines at each substep position. Alternate colours each frame so that substeps are clearly visible.
            if (_DebugSubsteps)
            {
                var col = 0.7f * (Time.frameCount % 2 == 1 ? Color.green : Color.red);
                var pos = transform.position - _Velocity * (timeBeforeCurrentTime - _VelocityOffset);
                var right = Vector3.Cross(Vector3.up, _Velocity.normalized);
                Debug.DrawLine(pos - right + transform.up, pos + right + transform.up, col, 0.5f);
            }
#endif

            // Reconstruct the position of this input at the current substep time. This produces
            // much smoother interaction shapes for moving objects. Increasing sim freq helps further.
            var offset = _Velocity * (timeBeforeCurrentTime - _VelocityOffset);
            var displacement = _Displacement.XNZ() * _CompensateForWaveMotion;
            wrapper.SetVector(Crest.ShaderIDs.s_Position, transform.position - offset - displacement);

            // Enlarge radius slightly - this tends to help waves 'wrap' the sphere slightly better
            wrapper.SetFloat(ShaderIDs.s_Radius, _Radius * 1.1f);

            wrapper.SetFloat(ShaderIDs.s_Weight, _WeightThisFrame);
            wrapper.SetFloat(ShaderIDs.s_InnerSphereOffset, _InnerSphereOffset);
            wrapper.SetFloat(ShaderIDs.s_InnerSphereMultiplier, _InnerSphereMultiplier);
            wrapper.SetFloat(ShaderIDs.s_LargeWaveMultiplier, _BoostLargeWaves ? 2f : 1f);
            wrapper.SetVector(ShaderIDs.s_Velocity, _RelativeVelocity);

            wrapper.SetTexture(Crest.ShaderIDs.s_Target, target);

            var threads = simulation.Resolution / Lod.k_ThreadGroupSize;
            wrapper.Dispatch(threads, threads, slices);
        }
    }

    partial class SphereWaterInteraction
    {
        Input _Input;

        sealed class Input : ILodInput
        {
            readonly SphereWaterInteraction _Input;
            public Input(SphereWaterInteraction input) => _Input = input;
            public bool Enabled => _Input.enabled;
            public bool IsCompute => true;
            public int Queue => 0;
            public int Pass => -1;
            public Rect Rect => _Input.Rect;
            public MonoBehaviour Component => _Input;
            public float Filter(WaterRenderer water, int slice) => 1f;
            public void Draw(Lod lod, CommandBuffer buffer, RenderTargetIdentifier target, int pass = -1, float weight = 1, int slice = -1) => _Input.Draw(lod, buffer, target, pass, weight, slice);
        }
    }
}
