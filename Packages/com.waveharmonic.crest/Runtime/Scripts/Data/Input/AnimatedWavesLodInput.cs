// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Registers a custom input to the <see cref="AnimatedWavesLod"/>.
    /// </summary>
    /// <remarks>
    /// Attach this to objects that you want to render into the displacment textures to
    /// affect the water shape.
    /// </remarks>
    [@HelpURL("Manual/Waves.html#animated-waves-inputs")]
    public sealed partial class AnimatedWavesLodInput : LodInput
    {
        [@Space(10)]

        [Tooltip("When to render the input into the displacement data.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        DisplacementPass _DisplacementPass = DisplacementPass.LodIndependent;

        [Tooltip("Whether to filter this input by wavelength.\n\nIf disabled, it will render to all LODs.")]
        [@Predicated(nameof(_DisplacementPass), inverted: true, nameof(DisplacementPass.LodDependent))]
        [@GenerateAPI]
        [DecoratedField, SerializeField]
        bool _FilterByWavelength;

        [Tooltip("Which octave to render into.\n\nFor example, set this to 2 to render into the 2m-4m octave. These refer to the same octaves as the wave spectrum editor.")]
        [@Predicated(nameof(_DisplacementPass), inverted: true, nameof(DisplacementPass.LodDependent))]
        [@Predicated(nameof(_FilterByWavelength))]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _OctaveWavelength = 512f;


        [Header("Culling")]

        [Tooltip("Inform the system how much this input will displace the water surface vertically.\n\nThis is used to set bounding box heights for the water chunks.")]
        [@GenerateAPI]
        [SerializeField]
        float _MaximumDisplacementVertical = 0f;

        [Tooltip("Inform the system how much this input will displace the water surface horizontally.\n\nThis is used to set bounding box widths for the water chunks.")]
        [@GenerateAPI]
        [SerializeField]
        float _MaximumDisplacementHorizontal = 0f;

        [Tooltip("Use the bounding box of an attached renderer component to determine the maximum vertical displacement.")]
        [@Predicated(nameof(_Mode), inverted: true, nameof(LodInputMode.Renderer))]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        bool _ReportRendererBounds = false;


        internal override LodInputMode DefaultMode => LodInputMode.Renderer;
        internal override int Pass => (int)_DisplacementPass;

        internal AnimatedWavesLodInput() : base()
        {
            _FollowHorizontalWaveMotion = true;
        }

        internal override float Filter(WaterRenderer water, int slice)
        {
            return AnimatedWavesLod.FilterByWavelength(water, slice, _FilterByWavelength ? _OctaveWavelength : 0f);
        }

        private protected override void OnUpdate(WaterRenderer water)
        {
            base.OnUpdate(water);

            if (!Enabled)
            {
                return;
            }

            var maxDispVert = _MaximumDisplacementVertical;

            // let water system know how far from the sea level this shape may displace the surface
            if (_ReportRendererBounds)
            {
                var range = Data.HeightRange;
                var minY = range.x;
                var maxY = range.y;
                var seaLevel = water.SeaLevel;
                maxDispVert = Mathf.Max(maxDispVert, Mathf.Abs(seaLevel - minY), Mathf.Abs(seaLevel - maxY));
            }

            if (_MaximumDisplacementHorizontal > 0f || maxDispVert > 0f)
            {
                water.ReportMaximumDisplacement(_MaximumDisplacementHorizontal, maxDispVert, 0f);
            }
        }
    }

    partial class AnimatedWavesLodInput : ISerializationCallbackReceiver
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 1;
#pragma warning restore 414

        [System.Obsolete("Please use DisplacementPass instead.")]
        [Tooltip("When to render the input into the displacement data.\n\nIf enabled, it renders into all LODs of the simulation after the combine step rather than before with filtering. Furthermore, it will also affect dynamic waves.")]
        [@GenerateAPI(Setter.Custom)]
        [@DecoratedField, SerializeField]
        [HideInInspector]
        bool _RenderPostCombine = true;

        void SetRenderPostCombine(bool previous, bool current, bool force = false)
        {
            if (previous == current && !force) return;
            _DisplacementPass = current ? DisplacementPass.LodIndependent : DisplacementPass.LodDependent;
        }

#pragma warning disable CS0618 // Type or member is obsolete

        /// <inheritdoc/>
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            if (_Version < 1)
            {
                SetRenderPostCombine(_RenderPostCombine, _RenderPostCombine, force: true);
                _Version = 1;
            }
        }

#pragma warning restore CS0618 // Type or member is obsolete

        /// <inheritdoc/>
        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {

        }
    }
}
