// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering.Universal;

namespace WaveHarmonic.Crest
{
    enum RenderPipeline
    {
        Legacy,
        HighDefinition,
        Universal,
    }

    sealed class RenderPipelineHelper
    {
        public static RenderPipeline RenderPipeline => GraphicsSettings.currentRenderPipeline switch
        {
#if d_UnityHDRP
            HDRenderPipelineAsset => RenderPipeline.HighDefinition,
#endif
#if d_UnityURP
            UniversalRenderPipelineAsset => RenderPipeline.Universal,
#endif
            _ => RenderPipeline.Legacy,
        };

        // GraphicsSettings.currentRenderPipeline could be from the graphics setting or current quality level.
        public static bool IsLegacy => GraphicsSettings.currentRenderPipeline == null;

        public static bool IsUniversal
        {
            get
            {
#if d_UnityURP
                return GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;
#else
                return false;
#endif
            }
        }

        public static bool IsHighDefinition
        {
            get
            {
#if d_UnityHDRP
                return GraphicsSettings.currentRenderPipeline is HDRenderPipelineAsset;
#else
                return false;
#endif
            }
        }
    }
}
