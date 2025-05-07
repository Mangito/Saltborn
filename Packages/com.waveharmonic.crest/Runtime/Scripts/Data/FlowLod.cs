// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using WaveHarmonic.Crest.Utility;

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Simulates horizontal motion of water.
    /// </summary>
    [FilterEnum(nameof(_TextureFormatMode), Filtered.Mode.Exclude, (int)LodTextureFormatMode.Automatic)]
    public sealed partial class FlowLod : Lod<IFlowProvider>
    {
        const string k_FlowKeyword = "CREST_FLOW_ON_INTERNAL";

        internal static readonly Color s_GizmoColor = new(0f, 0f, 1f, 0.5f);

        internal override string ID => "Flow";
        internal override Color GizmoColor => s_GizmoColor;
        private protected override Color ClearColor => Color.black;
        private protected override bool NeedToReadWriteTextureData => true;
        internal override bool RunsInHeadless => true;

        private protected override GraphicsFormat RequestedTextureFormat => _TextureFormatMode switch
        {
            LodTextureFormatMode.Performance => GraphicsFormat.R16G16_SFloat,
            LodTextureFormatMode.Precision => GraphicsFormat.R32G32_SFloat,
            LodTextureFormatMode.Manual => _TextureFormat,
            _ => throw new System.NotImplementedException(),
        };

        internal FlowLod()
        {
            _Resolution = 128;
            _TextureFormat = GraphicsFormat.R16G16_SFloat;
        }

        internal override void Enable()
        {
            base.Enable();

            Shader.EnableKeyword(k_FlowKeyword);
        }

        internal override void Disable()
        {
            base.Disable();

            Shader.DisableKeyword(k_FlowKeyword);
        }

        private protected override IFlowProvider CreateProvider(bool enable)
        {
            Queryable?.CleanUp();
            // Flow is GPU only, and can only be queried using the compute path.
            return enable && Enabled ? new FlowQuery(_Water) : IFlowProvider.None;
        }

        internal static readonly SortedList<int, ILodInput> s_Inputs = new(Helpers.DuplicateComparison);
        private protected override SortedList<int, ILodInput> Inputs => s_Inputs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void OnLoad()
        {
            s_Inputs.Clear();
        }
    }
}
