// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest.Watercraft
{
    /// <summary>
    /// A simple watercraft controlller.
    /// </summary>
    [@HelpURL("Manual/FloatingObjects.html#movement-controller")]
    [AddComponentMenu(Constants.k_MenuPrefixPhysics + "Watercraft Controller")]
    public sealed partial class Controller : ManagedBehaviour<WaterRenderer>
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        [Tooltip("The accompanied buoyancy script.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        FloatingObject _FloatingObject;

        [Tooltip("The accompanied control script to take input from.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        Control _Control;

        [Tooltip("Vertical offset from the center of mass for where move force should be applied.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _ForceHeightOffset;

        [Tooltip("How quickly the watercraft moves from thrust.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _ThrustPower = 10f;

        [Tooltip("How quickly the watercraft turns from steering.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _SteerPower = 1f;

        [Tooltip("Rolls the watercraft when turning.")]
        [@Range(0, 1)]
        [@GenerateAPI]
        [SerializeField]
        float _TurningHeel = 0.35f;

        [Tooltip("Applies a curve to buoyancy changes.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        AnimationCurve _BuoyancyCurveFactor = new(new Keyframe[] { new(0, 0, 0.01267637f, 0.01267637f),
            new(0.6626424f, 0.1791001f, 0.8680198f, 0.8680198f), new(1, 1, 3.38758f, 3.38758f) });

        float _BuoyancyFactor = 1f;

        private protected override void OnStart()
        {
            base.OnStart();

            if (_Control == null) _Control = GetComponent<Control>();
            if (_FloatingObject == null) _FloatingObject = GetComponent<FloatingObject>();
        }

        private protected override System.Action<WaterRenderer> OnFixedUpdateMethod => OnFixedUpdate;
        void OnFixedUpdate(WaterRenderer water)
        {
            if (!_FloatingObject.InWater) return;

            UnityEngine.Profiling.Profiler.BeginSample("WaveHarmonic.Crest.Watercraft.Controller.FixedUpdate");

            var input = _Control.Input;
            var rb = _FloatingObject.RigidBody;

            // Thrust
            var forcePosition = rb.worldCenterOfMass + _ForceHeightOffset * Vector3.up;
            rb.AddForceAtPosition(_ThrustPower * input.z * transform.forward, forcePosition, ForceMode.Acceleration);

            // Steer
            var rotation = transform.up + _TurningHeel * transform.forward;
            rb.AddTorque(_SteerPower * input.x * rotation, ForceMode.Acceleration);

            if (input.y > 0f)
            {
                if (_BuoyancyFactor < 1f)
                {
                    _BuoyancyFactor += Time.deltaTime * 0.1f;
                    _BuoyancyFactor = Mathf.Clamp(_BuoyancyFactor, 0f, 1f);
                    _FloatingObject.BuoyancyForceStrength = _BuoyancyCurveFactor.Evaluate(_BuoyancyFactor);
                }
            }
            else if (input.y < 0f)
            {
                if (_BuoyancyFactor > 0f)
                {
                    _BuoyancyFactor -= Time.deltaTime * 0.1f;
                    _BuoyancyFactor = Mathf.Clamp(_BuoyancyFactor, 0f, 1f);
                    _FloatingObject.BuoyancyForceStrength = _BuoyancyCurveFactor.Evaluate(_BuoyancyFactor);
                }
            }

            UnityEngine.Profiling.Profiler.EndSample();
        }
    }
}
