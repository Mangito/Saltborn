// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Rendering;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Registers a custom input to the <see cref="DepthLod"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Attach this to objects that you want to use to add water depth.
    /// </para>
    /// <para>
    /// Renders depth every frame and should only be used for dynamic objects. For
    /// static objects, use a <see cref="DepthProbe"/>
    /// </para>
    /// </remarks>
    [@HelpURL("Manual/ShallowsAndShorelines.html#sea-floor-depth")]
    public sealed partial class DepthLodInput : LodInput
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        [@Space(10)]

        [@Label("Relative Height")]
        [Tooltip("Whether the data is relative to the input height.\n\nUseful for procedural placement.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal bool _Relative;

        [@Label("Copy Signed Distance Field")]
        [Tooltip("Whether to copy the signed distance field.")]
        [@GenerateAPI]
        [@DecoratedField, SerializeField]
        internal bool _CopySignedDistanceField;

        internal static new class ShaderIDs
        {
            public static readonly int s_HeightOffset = Shader.PropertyToID("_Crest_HeightOffset");
            public static readonly int s_SDF = Shader.PropertyToID("_Crest_SDF");
        }

        internal override LodInputMode DefaultMode => LodInputMode.Geometry;

        internal override void InferBlend()
        {
            base.InferBlend();
            _Blend = LodInputBlend.Maximum;
        }

        internal override void Draw(Lod simulation, CommandBuffer buffer, RenderTargetIdentifier target, int pass = -1, float weight = 1f, int slice = -1)
        {
            var wrapper = new PropertyWrapperBuffer(buffer);

            wrapper.SetFloat(ShaderIDs.s_HeightOffset, _Relative ? transform.position.y : 0f);

            if (IsCompute)
            {
                wrapper.SetInteger(ShaderIDs.s_SDF, _CopySignedDistanceField ? 1 : 0);
                buffer.SetKeyword(WaterResources.Instance.Compute._DepthTexture, WaterResources.Instance.Keywords.DepthTextureSDF, simulation._Water._DepthLod._EnableSignedDistanceFields);
            }

            base.Draw(simulation, buffer, target, pass, weight, slice);
        }
    }
}
