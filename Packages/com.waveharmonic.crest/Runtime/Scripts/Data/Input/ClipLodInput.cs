// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Registers a custom input to the <see cref="ClipLod"/>.
    /// </summary>
    /// <remarks>
    /// Attach this to objects that you want to use to clip the surface of the water.
    /// </remarks>
    [@HelpURL("Manual/Clipping.html#clip-inputs")]
    public sealed partial class ClipLodInput : LodInput
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        [@Heading("Primitive")]

        [Tooltip("The primitive to render (signed distance) into the simulation.")]
        [@Predicated(nameof(_Mode), inverted: true, nameof(LodInputMode.Primitive), hide: true)]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal LodInputPrimitive _Primitive = LodInputPrimitive.Cube;

        // Only Mode.Primitive SDF supports inverted.
        [Tooltip("Removes clip surface data instead of adding it.")]
        [@Predicated(nameof(_Mode), inverted: true, nameof(LodInputMode.Primitive), hide: true)]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _Inverted;

        [@Heading("Culling")]

        [Tooltip("Prevents inputs from cancelling each other out when aligned vertically.\n\nIt is imperfect so custom logic might be needed for your use case.")]
        [@Predicated(nameof(_Mode), inverted: false, nameof(LodInputMode.Paint), hide: true)]
        [@Predicated(nameof(_Mode), inverted: true, nameof(LodInputMode.Renderer))]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _WaterHeightDistanceCulling = false;

        internal override LodInputMode DefaultMode => LodInputMode.Primitive;
        // The clip surface samples at the displaced position in the water shader, so the displacement correction is not needed.
        private protected override bool FollowHorizontalMotion => true;

        readonly SampleCollisionHelper _SampleHeightHelper = new();

        ComputeShader PrimitiveShader => WaterResources.Instance.Compute._ClipPrimitive;
        static LocalKeyword KeywordInverted => WaterResources.Instance.Keywords.ClipPrimitiveInverted;
        static LocalKeyword KeywordSphere => WaterResources.Instance.Keywords.ClipPrimitiveSphere;
        static LocalKeyword KeywordCube => WaterResources.Instance.Keywords.ClipPrimitiveCube;
        static LocalKeyword KeywordRectangle => WaterResources.Instance.Keywords.ClipPrimitiveRectangle;

        bool _Enabled = true;
        Rect _Rect;

        internal override void InferBlend()
        {
            base.InferBlend();
            _Blend = LodInputBlend.Maximum;
        }

        internal override bool Enabled => _Enabled && Mode switch
        {
            LodInputMode.Primitive => enabled && PrimitiveShader != null,
            _ => base.Enabled,
        };

        internal override Rect Rect
        {
            get
            {
                if (Mode == LodInputMode.Primitive)
                {
                    if (_RecalculateBounds)
                    {
                        // This mode has full transform support so need to get rect from bounds.
                        _Rect = transform.Bounds().RectXZ();
                        _RecalculateBounds = false;
                    }

                    return _Rect;
                }

                return base.Rect;
            }
        }

        internal override void Draw(Lod simulation, CommandBuffer buffer, RenderTargetIdentifier target, int pass = -1, float weight = 1f, int slices = -1)
        {
            if (Mode == LodInputMode.Primitive)
            {
                var wrapper = new PropertyWrapperCompute(buffer, PrimitiveShader, 0);

                wrapper.SetMatrix(Crest.ShaderIDs.s_Matrix, transform.worldToLocalMatrix);

                // For culling.
                wrapper.SetVector(Crest.ShaderIDs.s_Position, transform.position);
                wrapper.SetFloat(Crest.ShaderIDs.s_Diameter, transform.lossyScale.Maximum());

                wrapper.SetKeyword(KeywordInverted, _Inverted);
                wrapper.SetKeyword(KeywordSphere, _Primitive == LodInputPrimitive.Sphere);
                wrapper.SetKeyword(KeywordCube, _Primitive == LodInputPrimitive.Cube);
                wrapper.SetKeyword(KeywordRectangle, _Primitive == LodInputPrimitive.Quad);

                wrapper.SetTexture(Crest.ShaderIDs.s_Target, target);

                var threads = simulation.Resolution / Lod.k_ThreadGroupSize;
                wrapper.Dispatch(threads, threads, slices);
            }
            else
            {
                base.Draw(simulation, buffer, target, pass, weight, slices);
            }
        }

        private protected override void OnUpdate(WaterRenderer water)
        {
            base.OnUpdate(water);

            if (Mode != LodInputMode.Renderer)
            {
                _Enabled = true;
                return;
            }

            if (!base.Enabled)
            {
                return;
            }

            if (Data is not RendererLodInputData data || data._Renderer == null)
            {
                return;
            }

            // Prevents possible conflicts since overlapping doesn't work for every case for convex hull.
            if (_WaterHeightDistanceCulling)
            {
                var position = transform.position;

                if (_SampleHeightHelper.SampleHeight(position, out var waterHeight))
                {
                    position.y = waterHeight;
                    _Enabled = Mathf.Abs(data._Renderer.bounds.ClosestPoint(position).y - waterHeight) < 1;
                }
            }
        }
    }
}
