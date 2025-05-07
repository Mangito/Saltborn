// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace WaveHarmonic.Crest
{
    [AttributeUsage(AttributeTargets.Class)]
    sealed class ForLodInput : Attribute
    {
        public readonly Type _Type;
        public readonly LodInputMode _Mode;

        public ForLodInput(Type type, LodInputMode mode)
        {
            _Type = type;
            _Mode = mode;
        }
    }

    /// <summary>
    /// Data storage for an input, pertinent to the associated input mode.
    /// </summary>
    public abstract class LodInputData
    {
        [SerializeField, HideInInspector]
        internal LodInput _Input;

        private protected Rect _Rect;
        private protected Bounds _Bounds;
        private protected bool _RecalculateRect = true;
        private protected bool _RecalculateBounds = true;

        internal abstract bool IsEnabled { get; }
        internal abstract void OnEnable();
        internal abstract void OnDisable();
        internal abstract void Draw(Lod lod, Component component, CommandBuffer buffer, RenderTargetIdentifier target, int slice);
        internal abstract void RecalculateRect();
        internal abstract void RecalculateBounds();

        internal virtual bool HasHeightRange => true;

        internal Rect Rect
        {
            get
            {
                if (_RecalculateRect)
                {
                    RecalculateRect();
                    _RecalculateRect = false;
                }

                return _Rect;
            }
        }

        internal Bounds Bounds
        {
            get
            {
                if (_RecalculateBounds)
                {
                    RecalculateBounds();
                    _RecalculateBounds = false;
                }

                return _Bounds;
            }
        }

        // Warning: NotImplementedException is thrown for paint and texture types.
        internal Vector2 HeightRange
        {
            get
            {
                if (!HasHeightRange) return Vector2.zero;
                var bounds = Bounds;
                return new(bounds.min.y, bounds.max.y);
            }
        }

        private protected void RecalculateCulling()
        {
            _RecalculateRect = _RecalculateBounds = true;
        }

        internal virtual void OnUpdate()
        {
            if (_Input.transform.hasChanged)
            {
                RecalculateCulling();
            }
        }

        internal virtual void OnLateUpdate()
        {

        }

#if UNITY_EDITOR
        internal abstract void OnChange(string propertyPath, object previousValue);
        internal abstract bool InferMode(Component component, ref LodInputMode mode);
        internal virtual void Reset() { }
#endif
    }

    /// <summary>
    /// Modes that inputs can use. Not all inputs support all modes. Refer to the UI.
    /// </summary>
    public enum LodInputMode
    {
        /// <summary>
        /// Unset is the serialization default.
        /// </summary>
        /// <remarks>
        /// This will be replaced with the default mode automatically. Unset can also be
        /// used if something is invalid.
        /// </remarks>
        Unset = 0,
        /// <summary>
        /// Hand-painted data by the user. Currently unused.
        /// </summary>
        Paint,
        /// <summary>
        /// Driven by a user created spline.
        /// </summary>
        Spline,
        /// <summary>
        /// Attached 'Renderer' (mesh, particle or other) used to drive data.
        /// </summary>
        Renderer,
        /// <summary>
        /// Driven by a mathematical primitive such as a cube or sphere.
        /// </summary>
        Primitive,
        /// <summary>
        /// Covers the entire water area.
        /// </summary>
        Global,
        /// <summary>
        /// Data driven by a user provided texture.
        /// </summary>
        Texture,
        /// <summary>
        /// Renders geometry using a default material.
        /// </summary>
        Geometry,
    }

    /// <summary>
    /// Blend presets for inputs.
    /// </summary>
    public enum LodInputBlend
    {
        /// <summary>
        /// No blending. Overwrites.
        /// </summary>
        Off,

        /// <summary>
        /// Additive blending.
        /// </summary>
        Additive,

        /// <summary>
        /// Takes the minimum value.
        /// </summary>
        Minimum,

        /// <summary>
        /// Takes the maximum value.
        /// </summary>
        Maximum,

        /// <summary>
        /// Applies the inverse weight to the target.
        /// </summary>
        /// <remarks>
        /// Basically overwrites what is already in the simulation.
        /// </remarks>
        Alpha,

        /// <summary>
        /// Same as alpha except anything above zero will overwrite rather than blend.
        /// </summary>
        AlphaClip,
    }

    /// <summary>
    /// Primitive shapes.
    /// </summary>
    // Have this match UnityEngine.PrimitiveType.
    public enum LodInputPrimitive
    {
        /// <summary>
        /// Spheroid.
        /// </summary>
        Sphere = 0,

        /// <summary>
        /// Cuboid.
        /// </summary>
        Cube = 3,

        /// <summary>
        /// Quad.
        /// </summary>
        Quad = 5,
    }
}
