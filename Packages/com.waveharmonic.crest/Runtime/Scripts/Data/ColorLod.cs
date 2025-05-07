// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// The source of depth color.
    /// </summary>
    public enum ShorelineVolumeColorSource
    {
        /// <summary>
        /// No depth color.
        /// </summary>
        None,

        /// <summary>
        /// Depth color based on water depth.
        /// </summary>
        Depth,

        /// <summary>
        /// Depth color based on shoreline distance.
        /// </summary>
        Distance,
    }

    /// <summary>
    /// Contains shared functionality for <see cref="AbsorptionLod"/> and <see cref="ScatteringLod"/>.
    /// </summary>
    [FilterEnum(nameof(_TextureFormatMode), Filtered.Mode.Exclude, (int)LodTextureFormatMode.Automatic)]
    public abstract partial class ColorLod : Lod
    {
        [@Space(10f)]

        [Tooltip("Source of the shoreline color.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal ShorelineVolumeColorSource _ShorelineColorSource;

        [Tooltip("Color of the shoreline color.")]
        [@Predicated(nameof(_ShorelineColorSource), inverted: false, nameof(ShorelineVolumeColorSource.None), hide: true)]
        [@GenerateAPI(Setter.Custom)]
        [@DecoratedField, SerializeField]
        private protected Color _ShorelineColor;

        [Tooltip("The maximum distance of the shoreline color.\n\nIf using Depth, then it is maximum depth.")]
        [@Predicated(nameof(_ShorelineColorSource), inverted: false, nameof(ShorelineVolumeColorSource.None), hide: true)]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _ShorelineColorMaximumDistance = 10f;

        [Tooltip("Shoreline color falloff value.")]
        [@Predicated(nameof(_ShorelineColorSource), inverted: false, nameof(ShorelineVolumeColorSource.None), hide: true)]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        float _ShorelineColorFalloff = 2f;

        static new class ShaderIDs
        {
            public static readonly int s_ShorelineColor = Shader.PropertyToID("_Crest_ShorelineColor");
            public static readonly int s_ShorelineColorMaximumDistance = Shader.PropertyToID("_Crest_ShorelineColorMaximumDistance");
            public static readonly int s_ShorelineColorFalloff = Shader.PropertyToID("_Crest_ShorelineColorFalloff");
        }

        private protected abstract int GlobalShaderID { get; }
        private protected abstract void SetShorelineColor(Color previous, Color current);
        private protected Vector4 _ShorelineColorValue;
        ShorelineColorInput _ShorelineColorInput;

        private protected override GraphicsFormat RequestedTextureFormat => _TextureFormatMode switch
        {
            LodTextureFormatMode.Manual => _TextureFormat,
            LodTextureFormatMode.Performance => GraphicsFormat.R8G8B8_UNorm,
            LodTextureFormatMode.Precision => GraphicsFormat.R16G16B16_UNorm,
            _ => throw new System.NotImplementedException($"Crest: {_TextureFormatMode} not implemented for {Name}."),
        };

        internal ColorLod()
        {
            // Interpolation banding with lower precision.
            _TextureFormat = GraphicsFormat.R16G16B16_UNorm;
            _TextureFormatMode = LodTextureFormatMode.Precision;
        }

        internal override void Enable()
        {
            base.Enable();

            if (Enabled)
            {
                _ShorelineColorInput ??= new(this);
                // Convert color to value.
                SetShorelineColor(Color.clear, _ShorelineColor);
                Inputs.Add(_ShorelineColorInput.Queue, _ShorelineColorInput);
            }
        }

        internal override void SetGlobals(bool enable)
        {
            base.SetGlobals(enable);

            Helpers.SetGlobalBoolean(GlobalShaderID, enable && Enabled);
        }

        internal override void Disable()
        {
            base.Disable();

            Inputs.Remove(_ShorelineColorInput);
        }

        sealed class ShorelineColorInput : ILodInput
        {
            public bool Enabled => _VolumeColorLod._ShorelineColorSource != ShorelineVolumeColorSource.None &&
                _VolumeColorLod._Water._DepthLod.Enabled;
            public bool IsCompute => true;
            public int Queue => int.MinValue;
            public int Pass => -1;
            public Rect Rect => Rect.zero;
            public MonoBehaviour Component => null;
            public float Filter(WaterRenderer water, int slice) => 1f;

            readonly ColorLod _VolumeColorLod;

            public ShorelineColorInput(ColorLod lod)
            {
                _VolumeColorLod = lod;
            }

            public void Draw(Lod lod, CommandBuffer buffer, RenderTargetIdentifier target, int pass = -1, float weight = 1, int slices = -1)
            {
                var resources = WaterResources.Instance;
                var wrapper = new PropertyWrapperCompute(buffer, resources.Compute._ShorelineColor, 0);

                wrapper.SetVector(ShaderIDs.s_ShorelineColor, _VolumeColorLod._ShorelineColorValue);
                wrapper.SetFloat(ShaderIDs.s_ShorelineColorMaximumDistance, _VolumeColorLod._ShorelineColorMaximumDistance);
                wrapper.SetFloat(ShaderIDs.s_ShorelineColorFalloff, _VolumeColorLod._ShorelineColorFalloff);

                wrapper.SetKeyword(WaterResources.Instance.Keywords.ShorelineColorScattering, lod.GetType() == typeof(ScatteringLod));
                wrapper.SetKeyword(WaterResources.Instance.Keywords.ShorelineColorSourceDistance, _VolumeColorLod._ShorelineColorSource == ShorelineVolumeColorSource.Distance);
                wrapper.SetTexture(Crest.ShaderIDs.s_Target, target);

                var threads = lod.Resolution / k_ThreadGroupSize;
                wrapper.Dispatch(threads, threads, slices);
            }
        }
    }

#if UNITY_EDITOR
    abstract partial class ColorLod
    {
        private protected override void OnChange(string propertyPath, object previousValue)
        {
            base.OnChange(propertyPath, previousValue);

            switch (propertyPath)
            {
                case nameof(_ShorelineColor):
                    SetShorelineColor((Color)previousValue, _ShorelineColor);
                    break;
            }
        }
    }
#endif
}
