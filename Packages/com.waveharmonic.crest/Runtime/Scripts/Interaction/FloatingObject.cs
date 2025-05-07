// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Different physics models for <see cref="FloatingObject"/>
    /// </summary>
    public enum FloatingObjectModel
    {
        /// <summary>
        /// A simple model which aligns the object with the wave normal.
        /// </summary>
        AlignNormal,

        /// <summary>
        /// A more advanced model which samples water at the probes positions.
        /// </summary>
        Probes,
    }

    /// <summary>
    /// Probes for the <see cref="FloatingObject"/> <see cref="FloatingObjectModel.Probes"/> model.
    /// </summary>
    [System.Serializable]
    public struct FloatingObjectProbe
    {
        /// <summary>
        /// How much this probe affects the outcome (not a physical weight).
        /// </summary>
        [SerializeField]
        public float _Weight;

        /// <summary>
        /// The position of the probe.
        /// </summary>
        [SerializeField]
        public Vector3 _Position;
    }

    /// <summary>
    /// Physics including buoyancy and drag.
    /// </summary>
    [@HelpURL("Manual/FloatingObjects.html#physics")]
    [AddComponentMenu(Constants.k_MenuPrefixPhysics + "Floating Object")]
    public sealed partial class FloatingObject : ManagedBehaviour<WaterRenderer>
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414


        [Tooltip("The rigid body to affect.\n\nIt will automatically get the sibling rigid body if not set.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        Rigidbody _RigidBody;

        [Tooltip("The model to use for buoyancy.\n\nAlign Normal is simple and only uses a few queries whilst Probes is more advanced and uses a few queries per probe. Cannot be changed at runtime after Start.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        FloatingObjectModel _Model = FloatingObjectModel.AlignNormal;

        [Tooltip(ICollisionProvider.k_LayerTooltip)]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        CollisionLayer _Layer = CollisionLayer.AfterAnimatedWaves;


        [Header("Buoyancy")]

        [@Label("Force Strength")]
        [Tooltip("Strength of buoyancy force.\n\nFor probes, roughly a mass to force ratio of 100 to 1 to keep the center of mass near the surface. For Align Normal, default value is for a default sphere with a default rigidbody.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _BuoyancyForceStrength = 10f;

        [@Label("Torque Strength")]
        [Tooltip("Strength of torque applied to match boat orientation to water normal.")]
        [@Predicated(nameof(_Model), inverted: true, nameof(FloatingObjectModel.AlignNormal), hide: true)]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _BuoyancyTorqueStrength = 8f;

        [@Label("Maximum Force")]
        [Tooltip("Clamps the buoyancy force to this value.\n\nUseful for handling fully submerged objects.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _MaximumBuoyancyForce = 100f;

        [@Label("Height Offset")]
        [Tooltip("Height offset from transform center to bottom of boat (if any).\n\nDefault value is for a default sphere. Having this value be an accurate measurement from center to bottom is not necessary.")]
        [@Predicated(nameof(_Model), true, nameof(FloatingObjectModel.AlignNormal), hide: true)]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _CenterToBottomOffset = -1f;

        [Tooltip("Approximate hydrodynamics of 'surfing' down waves.")]
        [@Predicated(nameof(_Model), true, nameof(FloatingObjectModel.AlignNormal))]
        [@Range(0, 1)]
        [@GenerateAPI]
        [SerializeField]
        float _AccelerateDownhill;

        [UnityEngine.Space(10)]

        [Tooltip("Query points for buoyancy.\n\nOnly applicable to Probes model.")]
        [@GenerateAPI]
        [SerializeField]
        internal FloatingObjectProbe[] _Probes = new FloatingObjectProbe[] { };


        [Header("Drag")]

        [Tooltip("Drag when in water.\n\nAdditive to the drag declared on the rigid body.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        Vector3 _Drag = new(2f, 3f, 1f);

        [Tooltip("Angular drag when in water.\n\nAdditive to the angular drag declared on the rigid body.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _AngularDrag = 0.2f;

        [Tooltip("Vertical offset for where drag force should be applied.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _ForceHeightOffset;


        [Header("Wave Response")]

        [Tooltip("Width of object for physics purposes.\n\nThe larger this value, the more filtered/smooth the wave response will be. If larger wavelengths cannot be filtered, increase the LOD Levels")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _ObjectWidth = 3f;

        [Tooltip("Computes a separate normal based on boat length to get more accurate orientations.\n\nRequires the cost of an extra collision sample.")]
        [@Predicated(nameof(_Model), true, nameof(FloatingObjectModel.AlignNormal), hide: true)]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _UseObjectLength;

        [Tooltip("Length dimension of boat.\n\nOnly used if Use Boat Length is enabled.")]
        [@Predicated(nameof(_Model), true, nameof(FloatingObjectModel.AlignNormal), hide: true)]
        [@Predicated(nameof(_UseObjectLength))]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _ObjectLength = 3f;

        // Debug
        [UnityEngine.Space(10)]

        [@DecoratedField, SerializeField]
        DebugFields _Debug = new();

        [System.Serializable]
        sealed class DebugFields
        {
            [Tooltip("Draw queries for each force point as gizmos.")]
            [@DecoratedField, SerializeField]
            internal bool _DrawQueries = false;
        }

        /// <summary>
        /// Is any part of this object in water.
        /// </summary>
        public bool InWater { get; private set; }

        readonly SampleCollisionHelper _SampleHeightHelper = new();
        readonly SampleFlowHelper _SampleFlowHelper = new();

        Vector3[] _QueryPoints;
        Vector3[] _QueryResultDisplacements;
        Vector3[] _QueryResultVelocities;
        Vector3[] _QueryResultNormal;

        internal FloatingObjectProbe[] _Probe = new FloatingObjectProbe[] { new() { _Weight = 1f } };

        const float k_WaterDensity = 1000;

        float _TotalWeight;

        bool Advanced => _Model == FloatingObjectModel.Probes;

        private protected override void OnStart()
        {
            base.OnStart();

            if (_RigidBody == null) TryGetComponent(out _RigidBody);

            var points = Advanced ? _Probes : _Probe;
            // Advanced needs an extra spot for the center.
            var length = Advanced ? points.Length + 1 : points.Length;
            _QueryPoints = new Vector3[length];
            _QueryResultDisplacements = new Vector3[length];
            _QueryResultVelocities = new Vector3[length];
            if (!Advanced) _QueryResultNormal = new Vector3[length];
        }

        private protected override System.Action<WaterRenderer> OnFixedUpdateMethod => OnFixedUpdate;
        void OnFixedUpdate(WaterRenderer water)
        {
            UnityEngine.Profiling.Profiler.BeginSample("Buoyancy.FixedUpdate");

            var points = Advanced ? _Probes : _Probe;

            // Queries
            {
                var collisions = water.AnimatedWavesLod.Provider;

                _TotalWeight = 0;

                // Update query points.
                for (var i = 0; i < points.Length; i++)
                {
                    var point = points[i];
                    _TotalWeight += point._Weight;
                    _QueryPoints[i] = transform.TransformPoint(point._Position + new Vector3(0, _RigidBody.centerOfMass.y, 0));
                }

                _QueryPoints[^1] = transform.position + new Vector3(0, _RigidBody.centerOfMass.y, 0);

                collisions.Query(GetHashCode(), _ObjectWidth, _QueryPoints, _QueryResultDisplacements, _QueryResultNormal, _QueryResultVelocities, _Layer);

                if (Advanced && _Debug._DrawQueries)
                {
                    for (var i = 0; i < points.Length; i++)
                    {
                        var query = _QueryPoints[i];
                        query.y = water.SeaLevel + _QueryResultDisplacements[i].y;
                        CollisionAreaVisualizer.DebugDrawCross(query, 1f, Color.magenta);
                    }
                }
            }

            // We could filter the surface velocity as the minimum of the last 2 frames. There
            // is a hard case where a wavelength is turned on/off which generates single frame
            // velocity spikes - because the surface legitimately moves very fast.
            var surfaceVelocity = _QueryResultVelocities[^1];
            _SampleFlowHelper.Sample(transform.position, out var surfaceFlow, minimumLength: _ObjectWidth);
            surfaceVelocity += new Vector3(surfaceFlow.x, 0, surfaceFlow.y);

            if (_Debug._DrawQueries)
            {
                Debug.DrawLine(transform.position + 5f * Vector3.up, transform.position + 5f * Vector3.up + surfaceVelocity, new(1, 1, 1, 0.6f));
            }

            // Buoyancy
            if (Advanced)
            {
                var archimedesForceMagnitude = k_WaterDensity * Mathf.Abs(Physics.gravity.y);
                InWater = false;

                for (var i = 0; i < points.Length; i++)
                {
                    var height = water.SeaLevel + _QueryResultDisplacements[i].y;
                    var difference = height - _QueryPoints[i].y;
                    if (difference > 0)
                    {
                        InWater = true;
                        if (_TotalWeight > 0f)
                        {
                            var force = _BuoyancyForceStrength * points[i]._Weight * archimedesForceMagnitude * difference * Vector3.up / _TotalWeight;
                            if (_MaximumBuoyancyForce < Mathf.Infinity)
                            {
                                force = Vector3.ClampMagnitude(force, _MaximumBuoyancyForce);
                            }
                            _RigidBody.AddForceAtPosition(force, _QueryPoints[i]);
                        }
                    }
                }

                if (!InWater)
                {
                    UnityEngine.Profiling.Profiler.EndSample();
                    return;
                }
            }
            else
            {
                var height = _QueryResultDisplacements[0].y + water.SeaLevel;
                var bottomDepth = height - transform.position.y - _CenterToBottomOffset;
                var normal = _QueryResultNormal[0];

                if (_Debug._DrawQueries)
                {
                    var surfPos = transform.position;
                    surfPos.y = height;
                    CollisionAreaVisualizer.DebugDrawCross(surfPos, normal, 1f, Color.red);
                }

                InWater = bottomDepth > 0f;
                if (!InWater)
                {
                    UnityEngine.Profiling.Profiler.EndSample();
                    return;
                }

                var buoyancy = _BuoyancyForceStrength * bottomDepth * bottomDepth * bottomDepth * -Physics.gravity.normalized;
                if (_MaximumBuoyancyForce < Mathf.Infinity)
                {
                    buoyancy = Vector3.ClampMagnitude(buoyancy, _MaximumBuoyancyForce);
                }
                _RigidBody.AddForce(buoyancy, ForceMode.Acceleration);

                // Approximate hydrodynamics of sliding along water
                if (_AccelerateDownhill > 0f)
                {
                    _RigidBody.AddForce(_AccelerateDownhill * -Physics.gravity.y * new Vector3(normal.x, 0f, normal.z), ForceMode.Acceleration);
                }

                // Orientation
                // Align to water normal. One normal by default, but can use a separate normal
                // based on boat length vs width. This gives varying rotations based on boat
                // dimensions.
                {
                    var normalLatitudinal = normal;
                    var normalLongitudinal = Vector3.up;

                    if (_UseObjectLength)
                    {
                        if (_SampleHeightHelper.SampleHeight(transform.position, out _, out _, out normalLongitudinal, minimumLength: _ObjectLength, _Layer))
                        {
                            var f = transform.forward;
                            f.y = 0f;
                            f.Normalize();
                            normalLatitudinal -= Vector3.Dot(f, normalLatitudinal) * f;

                            var r = transform.right;
                            r.y = 0f;
                            r.Normalize();
                            normalLongitudinal -= Vector3.Dot(r, normalLongitudinal) * r;
                        }
                    }

                    if (_Debug._DrawQueries) Debug.DrawLine(transform.position, transform.position + 5f * normalLatitudinal, Color.green);
                    if (_Debug._DrawQueries && _UseObjectLength) Debug.DrawLine(transform.position, transform.position + 5f * normalLongitudinal, Color.yellow);

                    var torqueWidth = Vector3.Cross(transform.up, normalLatitudinal);
                    _RigidBody.AddTorque(torqueWidth * _BuoyancyTorqueStrength, ForceMode.Acceleration);
                    if (_UseObjectLength)
                    {
                        var torqueLength = Vector3.Cross(transform.up, normalLongitudinal);
                        _RigidBody.AddTorque(torqueLength * _BuoyancyTorqueStrength, ForceMode.Acceleration);
                    }

                    _RigidBody.AddTorque(-_AngularDrag * _RigidBody.angularVelocity);
                }
            }

            // Apply drag relative to water
            if (_Drag != Vector3.zero)
            {
#if UNITY_6000_0_OR_NEWER
                var velocityRelativeToWater = _RigidBody.linearVelocity - surfaceVelocity;
#else
                var velocityRelativeToWater = _RigidBody.velocity - surfaceVelocity;
#endif
                var forcePosition = _RigidBody.worldCenterOfMass + _ForceHeightOffset * Vector3.up;
                _RigidBody.AddForceAtPosition(_Drag.x * Vector3.Dot(transform.right, -velocityRelativeToWater) * transform.right, forcePosition, ForceMode.Acceleration);
                _RigidBody.AddForceAtPosition(_Drag.y * Vector3.Dot(Vector3.up, -velocityRelativeToWater) * Vector3.up, forcePosition, ForceMode.Acceleration);
                _RigidBody.AddForceAtPosition(_Drag.z * Vector3.Dot(transform.forward, -velocityRelativeToWater) * transform.forward, forcePosition, ForceMode.Acceleration);
            }

            UnityEngine.Profiling.Profiler.EndSample();
        }
    }
}
