// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    interface ILodInput
    {
        const int k_QueueMaximumSubIndex = 1000;

        /// <summary>
        /// Draw the input (the render target will be bound)
        /// </summary>
        public void Draw(Lod simulation, CommandBuffer buffer, RenderTargetIdentifier target, int pass = -1, float weight = 1f, int slice = -1);

        float Filter(WaterRenderer water, int slice);

        /// <summary>
        /// Whether to apply this input.
        /// </summary>
        bool Enabled { get; }

        bool IsCompute { get; }

        int Queue { get; }

        int Pass { get; }

        Rect Rect { get; }

        MonoBehaviour Component { get; }

        // Allow sorting within a queue. Callers can pass in things like sibling index to
        // get deterministic sorting.
        int Order => Queue * k_QueueMaximumSubIndex + Mathf.Min(Component.transform.GetSiblingIndex(), k_QueueMaximumSubIndex - 1);

        internal static void Attach(ILodInput input, Utility.SortedList<int, ILodInput> inputs)
        {
            inputs.Remove(input);
            inputs.Add(input.Order, input);
        }

        internal static void Detach(ILodInput input, Utility.SortedList<int, ILodInput> inputs)
        {
            inputs.Remove(input);
        }
    }

    /// <summary>
    /// Base class for scripts that register inputs to the various LOD data types.
    /// </summary>
    [@ExecuteDuringEditMode]
    [@HelpURL("Manual/WaterInputs.html")]
    public abstract partial class LodInput : ManagedBehaviour<WaterRenderer>
    {
        [Tooltip("The mode for this input.\n\nSee the manual for more details about input modes. Use AddComponent(LodInputMode) to set the mode via scripting. The mode cannot be changed after creation.")]
        [@Filtered((int)LodInputMode.Unset)]
        [@GenerateAPI(Setter.None)]
        [SerializeField]
        internal LodInputMode _Mode = LodInputMode.Unset;

        // NOTE:
        // Weight and Feather do not support Depth and Clip as they do not make much sense.
        // For others it is a case of only supporting unsupported mode(s).

        [Tooltip("Scales the input.")]
        [@Predicated(typeof(AlbedoLodInput), inverted: true, hide: true)]
        [@Predicated(typeof(ClipLodInput), inverted: true, hide: true)]
        [@Predicated(typeof(DepthLodInput), inverted: true, hide: true)]
        [@Range(0f, 1f)]
        [@GenerateAPI]
        [SerializeField]
        float _Weight = 1f;

        [Tooltip("The order this input will render.\n\nOrder is Queue plus SiblingIndex")]
        [@GenerateAPI(Setter.Custom)]
        [@DecoratedField, SerializeField]
        int _Queue;

        [Tooltip("How this input blends into existing data.\n\nSimilar to blend operations in shaders. For inputs which have materials, use the blend functionality on the shader/material.")]
        [@Predicated(typeof(AbsorptionLodInput), inverted: true, hide: true)]
        [@Predicated(typeof(AlbedoLodInput), inverted: true, hide: true)]
        [@Predicated(typeof(AnimatedWavesLodInput), inverted: true, hide: true)]
        [@Predicated(typeof(ClipLodInput), inverted: true, hide: true)]
        [@Predicated(typeof(DepthLodInput), inverted: true, hide: true)]
        [@Predicated(typeof(DynamicWavesLodInput), inverted: true, hide: true)]
        [@Predicated(typeof(ScatteringLodInput), inverted: true, hide: true)]
        [@Predicated(typeof(ShadowLodInput), inverted: true, hide: true)]
        [@Predicated(nameof(_Mode), inverted: false, nameof(LodInputMode.Global))]
        [@Predicated(nameof(_Mode), inverted: false, nameof(LodInputMode.Primitive))]
        [@Predicated(nameof(_Mode), inverted: false, nameof(LodInputMode.Renderer))]
        [@Filtered]
        [@GenerateAPI]
        [SerializeField]
        internal LodInputBlend _Blend = LodInputBlend.Additive;

        [@Label("Feather")]
        [Tooltip("The width of the feathering to soften the edges to blend inputs.\n\nInputs that do not support feathering will have this field disabled or hidden in UI.")]
        [@Predicated(typeof(AlbedoLodInput), inverted: true, hide: true)]
        [@Predicated(typeof(AnimatedWavesLodInput), inverted: true, hide: true)]
        [@Predicated(typeof(ClipLodInput), inverted: true, hide: true)]
        [@Predicated(typeof(DepthLodInput), inverted: true, hide: true)]
        [@Predicated(typeof(DynamicWavesLodInput), inverted: true, hide: true)]
        [@Predicated(typeof(LevelLodInput), inverted: true, hide: true)]
        [@Predicated(nameof(_Mode), inverted: false, nameof(LodInputMode.Renderer))]
        [@Predicated(nameof(_Mode), inverted: false, nameof(LodInputMode.Global))]
        [@Predicated(nameof(_Mode), inverted: false, nameof(LodInputMode.Primitive))]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _FeatherWidth = 0.1f;

        [Tooltip("How this input responds to horizontal displacement.\n\nIf false, data will not move horizontally with the waves. Has a small performance overhead when disabled. Only suitable for inputs of small size.")]
        [@Predicated(typeof(ClipLodInput), inverted: true, hide: true)]
        [@Predicated(typeof(FlowLodInput), inverted: true, hide: true)]
        [@Predicated(typeof(LevelLodInput), inverted: true, hide: true)]
        [@Predicated(typeof(ShapeWaves), inverted: true, hide: true)]
        [@Predicated(nameof(_Mode), inverted: false, nameof(LodInputMode.Global))]
        [@Predicated(nameof(_Mode), inverted: false, nameof(LodInputMode.Spline))]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        private protected bool _FollowHorizontalWaveMotion = false;

        [@Heading("Mode")]

        [@Predicated(nameof(_Mode), inverted: false, nameof(LodInputMode.Unset), hide: true)]
        [@Predicated(nameof(_Mode), inverted: false, nameof(LodInputMode.Primitive), hide: true)]
        [@Predicated(nameof(_Mode), inverted: false, nameof(LodInputMode.Global), hide: true)]
        [@Stripped]
        [SerializeReference]
        internal LodInputData _Data;

        // Need always visble for space to appear before foldout instead of inside.
        [@Space(10, isAlwaysVisible: true)]

        [@Group("Debug", order = k_DebugGroupOrder)]

        [@Predicated(nameof(_Mode), inverted: false, nameof(LodInputMode.Global))]
        [@DecoratedField, SerializeField]
        internal bool _DrawBounds;

        internal const int k_DebugGroupOrder = 10;

        internal static class ShaderIDs
        {
            public static int s_Weight = Shader.PropertyToID("_Crest_Weight");
            public static int s_DisplacementAtInputPosition = Shader.PropertyToID("_Crest_DisplacementAtInputPosition");
            public static readonly int s_BlendSource = Shader.PropertyToID("_Crest_BlendSource");
            public static readonly int s_BlendTarget = Shader.PropertyToID("_Crest_BlendTarget");
            public static readonly int s_BlendOperation = Shader.PropertyToID("_Crest_BlendOperation");
        }


        internal abstract Color GizmoColor { get; }
        internal abstract LodInputMode DefaultMode { get; }
        private protected abstract Utility.SortedList<int, ILodInput> Inputs { get; }

        /// <summary>
        /// Disables rendering of input into data, but continues most scripting activities.
        /// </summary>
        public bool ForceRenderingOff { get; set; }

        /// <summary>
        /// Properties specific to <see cref="Mode"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// You will need to cast to a more specific type to change
        /// certain properties. Types derive from and end with <see cref="LodInputData"/>.
        /// Consider using <see cref="GetData{DataType}"/> which will validate and cast.
        /// </para>
        /// <para>
        /// <see cref="LodInputMode.Global"/> and <see cref="LodInputMode.Primitive"/> will
        /// have no associated data and will be null. The rest will have an
        /// <see cref="LodInputData"/> type which will be prefixed with the input type and
        /// then mode (eg <see cref="LodInputMode.Texture"/> mode for
        /// <see cref="FoamLodInput"/> will be <see cref="FoamTextureLodInputData"/>).
        /// </para>
        /// <para>
        /// An exception is <see cref="ShapeGerstner"/> and <see cref="ShapeFFT"/>. They
        /// are derived from <see cref="ShapeWaves"/> and use it as a prefix. (eg <see
        /// cref="ShapeWavesTextureLodInputData"/>).
        /// </para>
        /// </remarks>
        public LodInputData Data { get => _Data; internal set => _Data = value; }

        /// <summary>
        /// Retrieves the typed data and validates the passed type.
        /// </summary>
        /// <remarks>
        /// Validation is stripped from builds.
        /// </remarks>
        /// <typeparam name="T">The data type to cast to.</typeparam>
        /// <returns>The casted data.</returns>
        public T GetData<T>() where T : LodInputData
        {
            if (_Mode is LodInputMode.Global or LodInputMode.Primitive or LodInputMode.Unset)
            {
                Debug.AssertFormat(false, "Crest: {0} has no associated data type.", _Mode);
                return null;
            }

            Debug.AssertFormat(Data is T, "Crest: Incorrect data type ({1}). The data type is {0}.", Data.GetType().BaseType.Name, typeof(T).Name);

            return Data as T;
        }

        internal bool IsCompute => Mode is LodInputMode.Texture or LodInputMode.Paint or LodInputMode.Global or LodInputMode.Primitive;
        internal virtual int Pass => -1;
        internal virtual Rect Rect
        {
            get
            {
                var rect = Rect.zero;
                if (_Data != null)
                {
                    rect = _Data.Rect;
                    rect.center -= _Displacement.XZ();
                }
                return rect;
            }
        }

        readonly SampleCollisionHelper _SampleHeightHelper = new();
        Vector3 _Displacement;
        private protected bool _RecalculateBounds = true;

        internal virtual bool Enabled => enabled && !ForceRenderingOff && Mode switch
        {
            LodInputMode.Unset => false,
            _ => Data?.IsEnabled ?? false,
        };

        // By default do not follow horizontal motion of waves. This means that the water input will appear on the surface at its XZ location, instead
        // of moving horizontally with the waves.
        private protected virtual bool FollowHorizontalMotion => Mode is LodInputMode.Global or LodInputMode.Spline || _FollowHorizontalWaveMotion;


        //
        // Event Methods
        //

        private protected override void Initialize()
        {
            base.Initialize();
            Data?.OnEnable();
            Attach();
        }

        private protected override void OnDisable()
        {
            base.OnDisable();
            Detach();
            Data?.OnDisable();
        }

        private protected override Action<WaterRenderer> OnUpdateMethod => OnUpdate;
        private protected virtual void OnUpdate(WaterRenderer water)
        {
            if (transform.hasChanged)
            {
                _RecalculateBounds = true;
            }

            // Input culling depends on displacement.
            if (!FollowHorizontalMotion)
            {
                _SampleHeightHelper.SampleDisplacement(transform.position, out _Displacement);
            }
            else
            {
                _Displacement = Vector3.zero;
            }

            Data?.OnUpdate();
        }

        private protected override Action<WaterRenderer> OnLateUpdateMethod => OnLateUpdate;
        private protected virtual void OnLateUpdate(WaterRenderer water)
        {
            Data?.OnLateUpdate();

            transform.hasChanged = false;
        }


        //
        // ILodInput
        //

        private protected virtual void Attach()
        {
            _Input ??= new(this);
            ILodInput.Attach(_Input, Inputs);
        }

        private protected virtual void Detach()
        {
            ILodInput.Detach(_Input, Inputs);
        }

        internal virtual void Draw(Lod simulation, CommandBuffer buffer, RenderTargetIdentifier target, int pass = -1, float weight = 1f, int slice = -1)
        {
            if (weight == 0f)
            {
                return;
            }

            // Must use global as weight can change per slice for ShapeWaves.
            var wrapper = new PropertyWrapperBuffer(buffer);
            wrapper.SetFloat(ShaderIDs.s_Weight, weight * _Weight);
            wrapper.SetVector(ShaderIDs.s_DisplacementAtInputPosition, _Displacement);

            Data?.Draw(simulation, this, buffer, target, slice);
        }

        internal virtual float Filter(WaterRenderer water, int slice)
        {
            return 1f;
        }

        /// <summary>
        /// Sets the Blend render state using Blend present.
        /// </summary>
        internal static void SetBlendFromPreset(Material material, LodInputBlend preset)
        {
            // Blend.Additive
            var source = BlendMode.One;
            var target = BlendMode.One;
            var operation = BlendOp.Add;

            switch (preset)
            {
                case LodInputBlend.Off:
                    source = BlendMode.One;
                    target = BlendMode.Zero;
                    break;
                case LodInputBlend.Alpha or LodInputBlend.AlphaClip:
                    source = BlendMode.One; // We apply alpha before blending.
                    target = BlendMode.OneMinusSrcAlpha;
                    break;
                case LodInputBlend.Maximum:
                    operation = BlendOp.Max;
                    break;
                case LodInputBlend.Minimum:
                    operation = BlendOp.Min;
                    break;
            }

            // SetInteger did not appear to work last time. Will need to revisit.
            material.SetInt(ShaderIDs.s_BlendSource, (int)source);
            material.SetInt(ShaderIDs.s_BlendTarget, (int)target);
            material.SetInt(ShaderIDs.s_BlendOperation, (int)operation);
        }

        void SetQueue(int previous, int current)
        {
            if (previous == current) return;
            if (!isActiveAndEnabled) return;
            Attach();
        }

        internal virtual void InferBlend()
        {
            // Correct for most cases.
            _Blend = LodInputBlend.Additive;
        }

        //
        // Editor Only Methods
        //

#if UNITY_EDITOR
        [@OnChange(skipIfInactive: false)]
        void OnChange(string propertyPath, object previousValue)
        {
            switch (propertyPath)
            {
                case nameof(_Queue):
                    SetQueue((int)previousValue, _Queue);
                    break;
                case nameof(_Mode):
                    if (!isActiveAndEnabled) { ChangeMode(Mode); break; }
                    OnDisable();
                    ChangeMode(Mode);
                    UnityEditor.EditorTools.ToolManager.RefreshAvailableTools();
                    OnEnable();
                    break;
                case nameof(_Blend):
                    // TODO: Make compatible with disabled.
                    if (isActiveAndEnabled) Data.OnChange($"../{propertyPath}", previousValue);
                    break;
            }
        }

        internal void ChangeMode(LodInputMode mode)
        {
            _Data = null;

            // Try to infer the mode.
            var types = TypeCache.GetTypesWithAttribute<ForLodInput>();
            var self = GetType();
            foreach (var type in types)
            {
                var attributes = type.GetCustomAttributes<ForLodInput>();
                foreach (var attribute in attributes)
                {
                    if (attribute._Mode != mode) continue;
                    if (!attribute._Type.IsAssignableFrom(self)) continue;
                    _Mode = mode;
                    InferBlend();
                    _Data = (LodInputData)Activator.CreateInstance(type);
                    _Data._Input = this;
                    _Data.InferMode(this, ref _Mode);
                    return;
                }
            }

            _Mode = DefaultMode;
            InferBlend();
        }

        /// <summary>
        /// Called when component attached in edit mode, or when Reset clicked by user.
        /// Besides recovering from Unset default value, also does a nice bit of auto-config.
        /// </summary>
        private protected override void Reset()
        {
            var types = TypeCache.GetTypesWithAttribute<ForLodInput>();
            var self = GetType();

            // Use inferred mode.
            foreach (var type in types)
            {
                var attributes = type.GetCustomAttributes<ForLodInput>();
                foreach (var attribute in attributes)
                {
                    if (!attribute._Type.IsAssignableFrom(self)) continue;

                    var instance = (LodInputData)Activator.CreateInstance(type);
                    instance._Input = this;

                    if (instance.InferMode(this, ref _Mode))
                    {
                        _Data = instance;
                        InferBlend();
                        return;
                    }
                }
            }

            // Use default mode.
            ChangeMode(DefaultMode);

            _Data?.Reset();

            base.Reset();
        }
#endif
    }

    partial class LodInput
    {
        Input _Input;

        sealed class Input : ILodInput
        {
            readonly LodInput _Input;
            public Input(LodInput input) => _Input = input;
            public bool Enabled => _Input.Enabled;
            public bool IsCompute => _Input.IsCompute;
            public int Queue => _Input.Queue;
            public int Pass => _Input.Pass;
            public Rect Rect => _Input.Rect;
            public MonoBehaviour Component => _Input;
            public float Filter(WaterRenderer water, int slice) => _Input.Filter(water, slice);
            public void Draw(Lod lod, CommandBuffer buffer, RenderTargetIdentifier target, int pass = -1, float weight = 1, int slice = -1) => _Input.Draw(lod, buffer, target, pass, weight, slice);
        }
    }
}
