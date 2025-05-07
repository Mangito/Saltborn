// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Data storage for for the Texture input mode.
    /// </summary>
    [System.Serializable]
    public abstract partial class TextureLodInputData : LodInputData
    {
        [Tooltip("Texture to render into the simulation.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal Texture _Texture;

        private protected abstract ComputeShader TextureShader { get; }
        internal override bool IsEnabled => _Texture != null;
        internal override bool HasHeightRange => false;

        internal override void RecalculateRect()
        {
            _Rect = _Input.transform.RectXZ();
        }

        internal override void RecalculateBounds()
        {
            throw new System.NotImplementedException();
        }

        internal override void Draw(Lod lod, Component component, CommandBuffer buffer, RenderTargetIdentifier target, int slices)
        {
            var transform = component.transform;
            var wrapper = new PropertyWrapperCompute(buffer, TextureShader, 0);
            var rotation = new Vector2(transform.localToWorldMatrix.m20, transform.localToWorldMatrix.m00).normalized;
            wrapper.SetVector(ShaderIDs.s_TextureSize, transform.lossyScale.XZ());
            wrapper.SetVector(ShaderIDs.s_TexturePosition, transform.position.XZ());
            wrapper.SetVector(ShaderIDs.s_TextureRotation, rotation);
            wrapper.SetVector(ShaderIDs.s_Resolution, new(_Texture.width, _Texture.height));
            wrapper.SetFloat(ShaderIDs.s_FeatherWidth, _Input.FeatherWidth);
            wrapper.SetTexture(ShaderIDs.s_Texture, _Texture);
            wrapper.SetInteger(ShaderIDs.s_Blend, (int)_Input.Blend);
            wrapper.SetTexture(ShaderIDs.s_Target, target);

            if (this is LevelTextureLodInputData height)
            {
                wrapper.SetKeyword(WaterResources.Instance.Keywords.LevelTextureCatmullRom, height._UseCatmullRomFiltering);
            }

            if (this is DirectionalTextureLodInputData data)
            {
                wrapper.SetBoolean(ShaderIDs.s_NegativeValues, data._NegativeValues);
            }

            var threads = lod.Resolution / Lod.k_ThreadGroupSize;
            wrapper.Dispatch(threads, threads, slices);
        }

        internal override void OnEnable()
        {
            // Empty.
        }

        internal override void OnDisable()
        {
            // Empty.
        }

#if UNITY_EDITOR
        internal override void OnChange(string propertyPath, object previousValue)
        {

        }

        internal override bool InferMode(Component component, ref LodInputMode mode)
        {
            return false;
        }
#endif
    }

    /// <inheritdoc/>
    [System.Serializable]
    public abstract partial class DirectionalTextureLodInputData : TextureLodInputData
    {
        [Tooltip("Whether the texture supports negative values.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal bool _NegativeValues;
    }

    /// <inheritdoc/>
    partial class LevelTextureLodInputData
    {
        [Label("Filtering (High Quality)")]
        [Tooltip("Helps with staircase aliasing.")]
        [@DecoratedField, SerializeField]
        internal bool _UseCatmullRomFiltering;
    }
}
