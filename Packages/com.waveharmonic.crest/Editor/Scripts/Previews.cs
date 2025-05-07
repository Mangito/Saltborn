// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEditor;
using UnityEngine;

namespace WaveHarmonic.Crest.Editor
{
    //
    // Lod
    //

    abstract class LodPreview : TexturePreview
    {
        protected abstract Lod Lod { get; }
        protected abstract bool VisualizeNegatives { get; }
        protected virtual bool ForceAlpha => false;
        public override GUIContent GetPreviewTitle() => new(Lod.Name);
        protected RenderTexture _TemporaryTexture;
        protected override Texture OriginalTexture
        {
            get
            {
                if (!Lod.Enabled || !((WaterRenderer)target).isActiveAndEnabled) return null;

                var texture = Lod.DataTexture;

                if (texture == null)
                {
                    return null;
                }

                return texture;
            }
        }

        protected override Texture ModifiedTexture => _TemporaryTexture;

        public override void OnPreviewSettings()
        {
            base.OnPreviewSettings();
            // OnPreviewSettings is called after OnPreviewGUI so release here.
            RenderTexture.ReleaseTemporary(_TemporaryTexture);
            _TemporaryTexture = null;
        }

        public override void OnPreviewGUI(Rect rect, GUIStyle background)
        {
            var texture = Lod.DataTexture;
            var descriptor = texture.descriptor;
            _TemporaryTexture = RenderTexture.GetTemporary(descriptor);
            _TemporaryTexture.name = "Crest Preview (Temporary)";
            Graphics.CopyTexture(texture, _TemporaryTexture);

            if (VisualizeNegatives)
            {
                var wrapper = new PropertyWrapperComputeStandalone(EditorHelpers.VisualizeNegativeValuesShader, 1);
                wrapper.SetTexture(ShaderIDs.s_Target, _TemporaryTexture);
                wrapper.Dispatch
                (
                    Lod.Resolution / Lod.k_ThreadGroupSizeX,
                    Lod.Resolution / Lod.k_ThreadGroupSizeY,
                    Lod.Slices
                );
            }

            ModifyTexture();

            if (ForceAlpha)
            {
                // Set alpha to one otherwise it shows nothing when set to RGB.
                var clear = WaterResources.Instance.Compute._Clear;
                if (clear != null)
                {
                    clear.SetTexture(0, ShaderIDs.s_Target, _TemporaryTexture);
                    clear.SetVector(ShaderIDs.s_ClearMask, Color.black);
                    clear.SetVector(ShaderIDs.s_ClearColor, Color.black);
                    clear.Dispatch
                    (
                        0,
                        Lod.Resolution / Lod.k_ThreadGroupSizeX,
                        Lod.Resolution / Lod.k_ThreadGroupSizeY,
                        Lod.Slices
                    );
                }
            }

            base.OnPreviewGUI(rect, background);
        }


        protected virtual void ModifyTexture()
        {

        }

        public override void Cleanup()
        {
            base.Cleanup();
            RenderTexture.ReleaseTemporary(_TemporaryTexture);
        }

        // FIXME: Without constructor Unity complains:
        // WaveHarmonic.Crest.Editor.LodPreview does not contain a default constructor, it
        // will not be registered as a preview handler. Use the Initialize function to set
        // up your object instead.
        public LodPreview() { }
    }

    [CustomPreview(typeof(WaterRenderer))]
    sealed class AbsorptionLodPreview : LodPreview
    {
        protected override Lod Lod => (target as WaterRenderer)._AbsorptionLod;
        protected override bool VisualizeNegatives => false;
    }

    [CustomPreview(typeof(WaterRenderer))]
    sealed class AlbedoLodPreview : LodPreview
    {
        protected override Lod Lod => (target as WaterRenderer)._AlbedoLod;
        protected override bool VisualizeNegatives => false;
    }

    [CustomPreview(typeof(WaterRenderer))]
    sealed class AnimatedWavesLodPreview : LodPreview
    {
        protected override Lod Lod => (target as WaterRenderer)._AnimatedWavesLod;
        protected override bool VisualizeNegatives => true;
        protected override bool ForceAlpha => true;
    }

    [CustomPreview(typeof(WaterRenderer))]
    sealed class ClipLodPreview : LodPreview
    {
        protected override Lod Lod => (target as WaterRenderer)._ClipLod;
        protected override bool VisualizeNegatives => false;
    }

    [CustomPreview(typeof(WaterRenderer))]
    sealed class DepthLodPreview : LodPreview
    {
        protected override Lod Lod => (target as WaterRenderer)._DepthLod;
        protected override bool VisualizeNegatives => true;
    }

    [CustomPreview(typeof(WaterRenderer))]
    sealed class DynamicWavesLodPreview : LodPreview
    {
        protected override Lod Lod => (target as WaterRenderer)._DynamicWavesLod;
        // Negatives do not visualize well, and obscure positives too much.
        protected override bool VisualizeNegatives => false;
    }

    [CustomPreview(typeof(WaterRenderer))]
    sealed class FlowLodPreview : LodPreview
    {
        protected override Lod Lod => (target as WaterRenderer)._FlowLod;
        protected override bool VisualizeNegatives => true;
    }

    [CustomPreview(typeof(WaterRenderer))]
    sealed class FoamLodPreview : LodPreview
    {
        protected override Lod Lod => (target as WaterRenderer)._FoamLod;
        protected override bool VisualizeNegatives => false;
    }

    [CustomPreview(typeof(WaterRenderer))]
    sealed class LevelLodPreview : LodPreview
    {
        protected override Lod Lod => (target as WaterRenderer)._LevelLod;
        protected override bool VisualizeNegatives => true;
    }

    [CustomPreview(typeof(WaterRenderer))]
    sealed class ScatteringLodPreview : LodPreview
    {
        protected override Lod Lod => (target as WaterRenderer)._ScatteringLod;
        protected override bool VisualizeNegatives => false;
        protected override bool ForceAlpha => true;
    }

    [CustomPreview(typeof(WaterRenderer))]
    sealed class ShadowLodPreview : LodPreview
    {
        protected override Lod Lod => (target as WaterRenderer)._ShadowLod;
        protected override bool VisualizeNegatives => false;
    }


    //
    // LodInput
    //

    // Adding abstract causes exception:
    // does not contain a default constructor, it will not be registered as a preview
    // handler. Use the Initialize function to set up your object instead.
    class ShapeWavesPreview : TexturePreview
    {
        public override GUIContent GetPreviewTitle() => new($"{target.GetType().Name}: Wave Buffer");
        protected override Texture OriginalTexture => (target as ShapeWaves).WaveBuffer;
    }

    [CustomPreview(typeof(ShapeFFT))]
    sealed class ShapeFFTPreview : ShapeWavesPreview
    {
    }

    [CustomPreview(typeof(ShapeGerstner))]
    sealed class ShapeGerstnerPreview : ShapeWavesPreview
    {
    }

    [CustomPreview(typeof(DepthProbe))]
    sealed class DepthProbePreview : TexturePreview
    {
        public override GUIContent GetPreviewTitle() => new("Depth Probe");
        protected override Texture OriginalTexture => (target as DepthProbe).Texture;
    }

#if CREST_DEBUG
    [CustomPreview(typeof(DepthProbe))]
    sealed class DepthProbeCameraPreview : TexturePreview
    {
        public override GUIContent GetPreviewTitle() => new("Depth Probe: Camera");
        protected override Texture OriginalTexture
        {
            get
            {
                var target = this.target as DepthProbe;
                if (target._Camera == null) return null;
                return target._Camera.targetTexture;
            }
        }
    }
#endif


    //
    // Other
    //

#if CREST_DEBUG
    [CustomPreview(typeof(WaterRenderer))]
    sealed class WaterLevelDepthPreview : TexturePreview
    {
        public override GUIContent GetPreviewTitle() => new("Water Level Screen-Space Depth");
        protected override Texture OriginalTexture => (target as WaterRenderer).WaterLevelDepthTexture;
    }

    [CustomPreview(typeof(WaterRenderer))]
    sealed class WaterVolumeMaskPreview : TexturePreview
    {
        public override GUIContent GetPreviewTitle() => new("Water Volume Mask");
        protected override Texture OriginalTexture => RenderPipelineHelper.IsLegacy && (target as WaterRenderer)._Underwater._MaskRT && (target as WaterRenderer)._Underwater._MaskRT.width > 0 ? (target as WaterRenderer)._Underwater._MaskRT : null;
    }
#endif

    [CustomPreview(typeof(WaterRenderer))]
    sealed class ReflectionPreview : TexturePreview
    {
        public override GUIContent GetPreviewTitle() => new("Water Reflections");
        protected override Texture OriginalTexture => (target as WaterRenderer)._Reflections._Enabled ? (target as WaterRenderer)._Reflections.ReflectionTexture : null;
    }
}
