// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// The mode for <see cref="WatertightHull"/>.
    /// </summary>
    /// <remarks>
    /// Each mode has its strengths and weaknesses.
    /// </remarks>
    public enum WatertightHullMode
    {
        /// <summary>
        /// Uses <see cref="AnimatedWavesLod"/> to remove water.
        /// </summary>
        [Tooltip("Use displacement to remove water.\n\nUsing displacement will also affect the underwater and can nest bouyant objects. Requires the displacement layer to be enabled.")]
        Displacement,

        /// <summary>
        /// Uses <see cref="ClipLod"/> to remove water.
        /// </summary>
        [Tooltip("Clips the surface to remove water.\n\nThis option is more precise and can be submerged.")]
        Clip,
    }

    /// <summary>
    /// Removes water from a provided hull using the clip simulation.
    /// </summary>
    [@ExecuteDuringEditMode]
    [AddComponentMenu(Constants.k_MenuPrefixInputs + "Watertight Hull")]
    [@HelpURL("Manual/Clipping.html#watertight-hull")]
    public sealed partial class WatertightHull : ManagedBehaviour<WaterRenderer>
    {
        [@Label("Convex Hull")]
        [Tooltip("The convex hull to keep water out.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal Mesh _Mesh;

        [Tooltip("Order this input will render.\n\nQueue is 'Queue + SiblingIndex'")]
        [@GenerateAPI(Setter.Custom)]
        [@DecoratedField, SerializeField]
        int _Queue;

        [@Space(10)]

        [Tooltip("Which mode to use.")]
        [@GenerateAPI(Setter.Custom)]
        [@DecoratedField, SerializeField]
        WatertightHullMode _Mode = WatertightHullMode.Displacement;

        [Tooltip("Inverts the effect to remove clipping (ie add water).")]
        [@Predicated(nameof(_Mode), inverted: true, nameof(WatertightHullMode.Clip), hide: true)]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _Inverted;

        [@Label("Use Clip")]
        [Tooltip("Whether to also to clip the surface when using displacement mode.\n\nDisplacement mode can have a leaky hull by allowing chop top push waves across the hull boundaries slightly. Clipping the surface will remove these interior leaks.")]
        [@Predicated(nameof(_Mode), inverted: true, nameof(WatertightHullMode.Displacement), hide: true)]
        [@GenerateAPI(Setter.Custom)]
        [@DecoratedField, SerializeField]
        bool _UseClipWithDisplacement = true;

        [@Space(10)]

        [@DecoratedField, SerializeField]
        internal DebugFields _Debug = new();

        [System.Serializable]
        internal sealed class DebugFields
        {
            [@DecoratedField, SerializeField]
            public bool _DrawBounds;
        }

        Material _ClipMaterial;
        Material _AnimatedWavesMaterial;

        internal bool Enabled => enabled && _Mesh != null;

        bool _RecalculateBounds = true;
        Rect _Rect;

        internal Rect Rect
        {
            get
            {
                if (_RecalculateBounds)
                {
                    _Rect = transform.TransformBounds(_Mesh.bounds).RectXZ();
                    _RecalculateBounds = false;
                }

                return _Rect;
            }
        }

        readonly SampleCollisionHelper _SampleCollisionHelper = new();
        Vector3 _Displacement;

        internal bool UsesClip => _Mode == WatertightHullMode.Clip || _UseClipWithDisplacement;
        internal bool UsesDisplacement => _Mode == WatertightHullMode.Displacement;

        static class ShaderIDs
        {
            public static int s_Inverted = Shader.PropertyToID("_Crest_Inverted");
        }

        private protected override void Initialize()
        {
            base.Initialize();

            if (UsesClip)
            {
                _ClipInput ??= new(this);
                _ClipMaterial = new(WaterResources.Instance.Shaders._ClipConvexHull);
                ILodInput.Attach(_ClipInput, ClipLod.s_Inputs);
            }

            if (UsesDisplacement)
            {
                _AnimatedWavesInput ??= new(this);
                _AnimatedWavesMaterial = new(Shader.Find("Crest/Inputs/Animated Waves/Push Water Under Convex Hull"));
                _AnimatedWavesMaterial.SetFloat(LodInput.ShaderIDs.s_Weight, 1f);
                ILodInput.Attach(_AnimatedWavesInput, AnimatedWavesLod.s_Inputs);
            }
        }

        private protected override void OnDisable()
        {
            base.OnDisable();
            Helpers.Destroy(_ClipMaterial);
            ILodInput.Detach(_ClipInput, ClipLod.s_Inputs);
            Helpers.Destroy(_AnimatedWavesMaterial);
            ILodInput.Detach(_AnimatedWavesInput, AnimatedWavesLod.s_Inputs);
        }

        private protected override System.Action<WaterRenderer> OnUpdateMethod => OnUpdate;
        void OnUpdate(WaterRenderer water)
        {
            if (_Mode == WatertightHullMode.Displacement)
            {
                _SampleCollisionHelper.SampleDisplacement(transform.position, out _Displacement);
            }

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

        void DrawClip(Lod simulation, CommandBuffer buffer, RenderTargetIdentifier target, int pass = -1, float weight = 1, int slice = -1)
        {
            _ClipMaterial.SetBoolean(ShaderIDs.s_Inverted, _Inverted);
            buffer.DrawMesh(_Mesh, transform.localToWorldMatrix, _ClipMaterial);
        }

        void DrawDisplacement(Lod simulation, CommandBuffer buffer, RenderTargetIdentifier target, int pass = -1, float weight = 1, int slice = -1)
        {
            _AnimatedWavesMaterial.SetVector(LodInput.ShaderIDs.s_DisplacementAtInputPosition, _Displacement);
            buffer.DrawMesh(_Mesh, transform.localToWorldMatrix, _AnimatedWavesMaterial);
        }

        void SetQueue(int previous, int current)
        {
            if (previous == current) return;
            if (_ClipInput == null || !isActiveAndEnabled) return;
            if (UsesClip) ILodInput.Attach(_ClipInput, ClipLod.s_Inputs);
            if (UsesDisplacement) ILodInput.Attach(_AnimatedWavesInput, AnimatedWavesLod.s_Inputs);
        }

        void SetMode(WatertightHullMode previous, WatertightHullMode current)
        {
            if (previous == current) return;
            OnDisable(); OnEnable();
        }

        void SetUseClipWithDisplacement(bool previous, bool current)
        {
            if (previous == current) return;
            OnDisable(); OnEnable();
        }

#if UNITY_EDITOR
        [@OnChange]
        void OnChange(string propertyPath, object previousValue)
        {
            switch (propertyPath)
            {
                case nameof(_Queue):
                    SetQueue((int)previousValue, _Queue);
                    break;
                case nameof(_Mode):
                    SetMode((WatertightHullMode)previousValue, _Mode);
                    break;
                case nameof(_UseClipWithDisplacement):
                    SetUseClipWithDisplacement((bool)previousValue, _UseClipWithDisplacement);
                    break;
            }
        }
#endif
    }

    partial class WatertightHull
    {
        ClipInput _ClipInput;

        sealed class ClipInput : ILodInput
        {
            readonly WatertightHull _Input;
            public ClipInput(WatertightHull input) => _Input = input;
            public bool Enabled => _Input.Enabled;
            public bool IsCompute => false;
            public int Queue => _Input.Queue;
            public int Pass => -1;
            public Rect Rect => _Input.Rect;
            public MonoBehaviour Component => _Input;
            public float Filter(WaterRenderer water, int slice) => 1f;
            public void Draw(Lod lod, CommandBuffer buffer, RenderTargetIdentifier target, int pass = -1, float weight = 1, int slice = -1) => _Input.DrawClip(lod, buffer, target, pass, weight, slice);
        }
    }

    partial class WatertightHull
    {
        DisplacementInput _AnimatedWavesInput;

        sealed class DisplacementInput : ILodInput
        {
            readonly WatertightHull _Input;
            public DisplacementInput(WatertightHull input) => _Input = input;
            public bool Enabled => _Input.Enabled;
            public bool IsCompute => false;
            public int Queue => _Input.Queue;
            public int Pass => (int)DisplacementPass.LodIndependentLast;
            public Rect Rect => _Input.Rect;
            public MonoBehaviour Component => _Input;
            public float Filter(WaterRenderer water, int slice) => 1f;
            public void Draw(Lod lod, CommandBuffer buffer, RenderTargetIdentifier target, int pass = -1, float weight = 1, int slice = -1) => _Input.DrawDisplacement(lod, buffer, target, pass, weight, slice);
        }
    }

    partial class WatertightHull : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 1;
#pragma warning restore 414

        /// <inheritdoc/>
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (_Version < 1)
            {
                // Keep clip for existing.
                _Mode = WatertightHullMode.Clip;
                _Version = 1;
            }
        }

        /// <inheritdoc/>
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {

        }
    }
}
