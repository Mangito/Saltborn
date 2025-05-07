// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Restores "Lighting > Environment" settings after switching from HDRP. "Lighting > Other Settings" do not need
// restoring. We only need to restore the skybox as we use the default values for everything else.

using UnityEngine;

namespace WaveHarmonic.Crest.Editor
{
#if !CREST_DEBUG
    [AddComponentMenu("")]
#endif
    sealed class RenderPipelineSettingsPatcher : RenderPipelinePatcher
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

        [@AttachMaterialEditor]
        [@DecoratedField, SerializeField]
        Material _SkyBox;

#if UNITY_EDITOR
        private protected override void Reset()
        {
            _SkyBox = RenderSettings.skybox;

            base.Reset();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            OnActiveRenderPipelineTypeChanged();
        }

        protected override void OnActiveRenderPipelineTypeChanged()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (RenderPipelineHelper.IsLegacy || RenderPipelineHelper.IsUniversal)
            {
                RenderSettings.skybox = _SkyBox;
            }
        }
#endif
    }
}
