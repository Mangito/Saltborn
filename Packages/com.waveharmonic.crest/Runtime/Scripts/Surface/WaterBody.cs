// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Demarcates an AABB area where water is present in the world.
    /// </summary>
    /// <remarks>
    /// If present, water tiles will be culled if they don't overlap any WaterBody.
    /// </remarks>
    [@ExecuteDuringEditMode]
    [AddComponentMenu(Constants.k_MenuPrefixScripts + "Water Body")]
    [@HelpURL("Manual/WaterBodies.html")]
    public sealed partial class WaterBody : ManagedBehaviour<WaterRenderer>
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        [Tooltip("Makes sure this water body is not clipped.\n\nIf clipping is enabled and set to clip everywhere by default, this option will register this water body to ensure its area does not get clipped.")]
        [@GenerateAPI(name: "Clipped")]
        [SerializeField]
        bool _Clip = true;

        [Tooltip("Water chunks that overlap this waterbody area will be assigned this material.\n\nThis is useful for varying water appearance across different water bodies. If no override material is specified, the default material assigned to the WaterRenderer component will be used.")]
        [@AttachMaterialEditor]
        [@GenerateAPI(name: "AboveSurfaceMaterial")]
        [@MaterialField("Crest/Water", name: "Water", title: "Create Water Material"), SerializeField]
        internal Material _Material = null;

        [Tooltip("Overrides the property on the Water Renderer with the same name when the camera is inside the bounds.")]
        [@AttachMaterialEditor]
        [@GenerateAPI]
        [@MaterialField("Crest/Water", name: "Water (Below)", title: "Create Water Material", parent: nameof(_Material)), SerializeField]
        internal Material _BelowSurfaceMaterial;

        [Tooltip("Overrides the Water Renderer's volume material when the camera is inside the bounds.")]
        [@MaterialField("Crest/Underwater", name: "Underwater", title: "Create Underwater Material")]
        [@AttachMaterialEditor]
        [@GenerateAPI]
        [SerializeField]
        internal Material _VolumeMaterial;


        bool _RecalculateRect = true;
        bool _RecalculateBounds = true;


        sealed class ClipInput : ILodInput
        {
            readonly WaterBody _Owner;
            readonly Transform _Transform;

            public bool Enabled => WaterRenderer.Instance._ClipLod._DefaultClippingState == DefaultClippingState.EverythingClipped;
            public bool IsCompute => true;
            public int Pass => -1;

            // TODO: Expose serialized queue.
            public int Queue => 0;
            public MonoBehaviour Component => _Owner;

            public Rect Rect => _Owner.Rect;

            public ClipInput(WaterBody owner)
            {
                _Owner = owner;
                _Transform = owner.transform;
            }

            public void Draw(Lod simulation, CommandBuffer buffer, RenderTargetIdentifier target, int pass = -1, float weight = 1f, int slices = -1)
            {
                var wrapper = new PropertyWrapperCompute(buffer, WaterResources.Instance.Compute._ClipPrimitive, 0);

                wrapper.SetMatrix(ShaderIDs.s_Matrix, _Transform.worldToLocalMatrix);

                // For culling.
                wrapper.SetVector(ShaderIDs.s_Position, _Transform.position);
                wrapper.SetFloat(ShaderIDs.s_Diameter, _Transform.lossyScale.Maximum());

                wrapper.SetKeyword(WaterResources.Instance.Keywords.ClipPrimitiveInverted, true);
                wrapper.SetKeyword(WaterResources.Instance.Keywords.ClipPrimitiveSphere, false);
                wrapper.SetKeyword(WaterResources.Instance.Keywords.ClipPrimitiveCube, false);
                wrapper.SetKeyword(WaterResources.Instance.Keywords.ClipPrimitiveRectangle, true);

                wrapper.SetTexture(ShaderIDs.s_Target, target);

                var threads = simulation.Resolution / Lod.k_ThreadGroupSize;
                wrapper.Dispatch(threads, threads, slices);
            }

            public float Filter(WaterRenderer water, int slice)
            {
                return 1f;
            }
        }

        internal static List<WaterBody> WaterBodies { get; } = new();

        Bounds _Bounds;
        internal Bounds AABB
        {
            get
            {
                if (_RecalculateBounds)
                {
                    CalculateBounds();
                    _RecalculateBounds = false;
                }

                return _Bounds;
            }
        }

        Rect _Rect;
        Rect Rect
        {
            get
            {
                if (_RecalculateRect)
                {
                    _Rect = AABB.RectXZ();
                    _RecalculateRect = false;
                }

                return _Rect;
            }
        }

        internal Material AboveOrBelowSurfaceMaterial => _BelowSurfaceMaterial == null ? _Material : _BelowSurfaceMaterial;

        ClipInput _ClipInput;

        private protected override void Initialize()
        {
            base.Initialize();

            CalculateBounds();

            WaterBodies.Add(this);

            HandleClipInputRegistration();
        }

        private protected override void OnDisable()
        {
            base.OnDisable();

            WaterBodies.Remove(this);

            if (_ClipInput != null)
            {
                ILodInput.Detach(_ClipInput, ClipLod.s_Inputs);

                _ClipInput = null;
            }
        }

        internal void CalculateBounds()
        {
            var bounds = new Bounds();
            bounds.center = transform.position;
            bounds.Encapsulate(transform.TransformPoint(Vector3.right / 2f + Vector3.forward / 2f));
            bounds.Encapsulate(transform.TransformPoint(Vector3.right / 2f - Vector3.forward / 2f));
            bounds.Encapsulate(transform.TransformPoint(-Vector3.right / 2f + Vector3.forward / 2f));
            bounds.Encapsulate(transform.TransformPoint(-Vector3.right / 2f - Vector3.forward / 2f));

            _Bounds = bounds;
        }

        void HandleClipInputRegistration()
        {
            var registered = _ClipInput != null;
            var shouldBeRegistered = _Clip;

            if (registered != shouldBeRegistered)
            {
                if (shouldBeRegistered)
                {
                    _ClipInput = new(this);

                    ILodInput.Attach(_ClipInput, ClipLod.s_Inputs);
                }
                else
                {
                    ILodInput.Detach(_ClipInput, ClipLod.s_Inputs);

                    _ClipInput = null;
                }
            }
        }

        private protected override System.Action<WaterRenderer> OnUpdateMethod => OnUpdate;
        void OnUpdate(WaterRenderer water)
        {
            if (transform.hasChanged)
            {
                _RecalculateRect = _RecalculateBounds = true;
            }
        }

        private protected override System.Action<WaterRenderer> OnLateUpdateMethod => OnLateUpdate;
        void OnLateUpdate(WaterRenderer water)
        {
            transform.hasChanged = false;
        }
    }
}
