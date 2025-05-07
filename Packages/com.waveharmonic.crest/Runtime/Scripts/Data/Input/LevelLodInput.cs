// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Registers a custom input to the <see cref="LevelLod"/>.
    /// </summary>
    /// <remarks>
    /// Attach this to objects that you want to influence the water height.
    /// </remarks>
    [@HelpURL("Manual/WaterBodies.html#water-bodies")]
    [@FilterEnum(nameof(_Blend), Filtered.Mode.Include, (int)LodInputBlend.Off, (int)LodInputBlend.Additive, (int)LodInputBlend.Minimum, (int)LodInputBlend.Maximum)]
    public sealed partial class LevelLodInput : LodInput
    {
        [@Heading("Water Chunk Culling")]

        [Tooltip("Whether to use the manual \"Height Range\" for water chunk culling.\n\nMandatory for non mesh inputs like \"Texture\".")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _OverrideHeight;

        [Tooltip("The minimum and maximum height value to report for water chunk culling.")]
        [@Predicated(nameof(_OverrideHeight))]
        [@Range(-100, 100, Range.Clamp.None)]
        [@GenerateAPI]
        [SerializeField]
        Vector2 _HeightRange = new(-100, 100);

        LevelLodInput()
        {
            _FollowHorizontalWaveMotion = true;
        }

        // Water level is packed into alpha using the displaced position.
        private protected override bool FollowHorizontalMotion => true;
        internal override LodInputMode DefaultMode => LodInputMode.Geometry;

        internal Rect _Rect;

        internal override void InferBlend()
        {
            base.InferBlend();

            _Blend = LodInputBlend.Off;

            if (_Mode is LodInputMode.Paint or LodInputMode.Texture)
            {
                _Blend = LodInputBlend.Additive;
            }
        }

        private protected override void Initialize()
        {
            base.Initialize();
            _Reporter ??= new(this);
            WaterChunkRenderer.HeightReporters.Add(_Reporter);
        }

        private protected override void OnDisable()
        {
            base.OnDisable();
            WaterChunkRenderer.HeightReporters.Remove(_Reporter);
        }

        bool ReportHeight(ref Rect bounds, ref float minimum, ref float maximum)
        {
            if (!Enabled)
            {
                return false;
            }

            _Rect = Data.Rect;

            // These modes do not provide a height yet.
            if (!Data.HasHeightRange && !_OverrideHeight)
            {
                return false;
            }

            if (bounds.Overlaps(_Rect, false))
            {
                var range = _OverrideHeight ? _HeightRange : Data.HeightRange;
                minimum = range.x;
                maximum = range.y;
                return true;
            }

            return false;
        }
    }

    partial class LevelLodInput
    {
        Reporter _Reporter;

        sealed class Reporter : IReportsHeight
        {
            readonly LevelLodInput _Input;
            public Reporter(LevelLodInput input) => _Input = input;
            public bool ReportHeight(ref Rect bounds, ref float minimum, ref float maximum) => _Input.ReportHeight(ref bounds, ref minimum, ref maximum);
        }
    }

    partial class LevelLodInput : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 1;
#pragma warning restore 414

        /// <inheritdoc/>
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            // Version 1
            // - Implemented blend mode but default value was serialized as Additive.
            if (_Version < 1)
            {
                if (_Mode is LodInputMode.Spline or LodInputMode.Renderer) _Blend = LodInputBlend.Off;
                _Version = 1;
            }
        }

        /// <inheritdoc/>
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            // Empty.
        }
    }
}
