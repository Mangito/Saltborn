// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Data storage for for the Geometry input mode.
    /// </summary>
    [System.Serializable]
    public abstract partial class GeometryLodInputData : LodInputData
    {
        [Tooltip("Geometry to render into the simulation.")]
        [@GenerateAPI(Setter.Custom)]
        [@DecoratedField, SerializeField]
        internal Mesh _Geometry;

        private protected abstract Shader GeometryShader { get; }

        internal override bool IsEnabled => _Geometry != null;

        Material _Material;

        internal override void Draw(Lod lod, Component component, CommandBuffer buffer, RenderTargetIdentifier target, int slices)
        {
#if UNITY_EDITOR
            // Weird things happen when hitting save if this is not set here. Hitting save will
            // still flicker the input, but without this it nothing renders.
            if (!Application.isPlaying) LodInput.SetBlendFromPreset(_Material, _Input.Blend);
#endif
            buffer.DrawMesh(_Geometry, component.transform.localToWorldMatrix, _Material);
        }

        internal override void OnEnable()
        {
            if (_Material == null)
            {
                _Material = new Material(GeometryShader);
            }

            LodInput.SetBlendFromPreset(_Material, _Input.Blend);
        }

        internal override void OnDisable()
        {
            // Empty.
        }

        internal override void RecalculateBounds()
        {
            _Bounds = _Input.transform.TransformBounds(_Geometry.bounds);
        }

        internal override void RecalculateRect()
        {
            _Rect = Bounds.RectXZ();
        }

        void SetGeometry(Mesh previous, Mesh current)
        {
            if (previous == current) return;
            RecalculateCulling();
        }

#if UNITY_EDITOR
        internal override void OnChange(string propertyPath, object previousValue)
        {
            if (_Input == null || !_Input.isActiveAndEnabled) return;

            switch (propertyPath)
            {
                case nameof(_Geometry):
                    SetGeometry((Mesh)previousValue, _Geometry);
                    break;
                case "../" + nameof(LodInput._Blend):
                    LodInput.SetBlendFromPreset(_Material, _Input.Blend);
                    break;
            }
        }

        internal override bool InferMode(Component component, ref LodInputMode mode)
        {
            return false;
        }

        internal override void Reset()
        {
            base.Reset();

            _Geometry = Helpers.PlaneMesh;
        }
#endif
    }

    /// <inheritdoc/>
    [ForLodInput(typeof(LevelLodInput), LodInputMode.Geometry)]
    public sealed class LevelGeometryLodInputData : GeometryLodInputData
    {
        private protected override Shader GeometryShader => WaterResources.Instance.Shaders._LevelGeometry;
    }

    /// <inheritdoc/>
    [ForLodInput(typeof(DepthLodInput), LodInputMode.Geometry)]
    public sealed class DepthGeometryLodInputData : GeometryLodInputData
    {
        private protected override Shader GeometryShader => WaterResources.Instance.Shaders._DepthGeometry;
    }
}
