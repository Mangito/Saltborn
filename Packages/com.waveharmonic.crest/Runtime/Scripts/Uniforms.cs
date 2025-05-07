// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEngine;

namespace WaveHarmonic.Crest
{
    static class ShaderIDs
    {
        public static readonly int s_Blend = Shader.PropertyToID("_Crest_Blend");
        public static readonly int s_Texture = Shader.PropertyToID("_Crest_Texture");
        public static readonly int s_Source = Shader.PropertyToID("_Crest_Source");
        public static readonly int s_Target = Shader.PropertyToID("_Crest_Target");
        public static readonly int s_TargetSlice = Shader.PropertyToID("_Crest_TargetSlice");
        public static readonly int s_Resolution = Shader.PropertyToID("_Crest_Resolution");
        public static readonly int s_ClearMask = Shader.PropertyToID("_Crest_ClearMask");
        public static readonly int s_ClearColor = Shader.PropertyToID("_Crest_ClearColor");
        public static readonly int s_Matrix = Shader.PropertyToID("_Crest_Matrix");
        public static readonly int s_Position = Shader.PropertyToID("_Crest_Position");
        public static readonly int s_Diameter = Shader.PropertyToID("_Crest_Diameter");
        public static readonly int s_TextureSize = Shader.PropertyToID("_Crest_TextureSize");
        public static readonly int s_TexturePosition = Shader.PropertyToID("_Crest_TexturePosition");
        public static readonly int s_TextureRotation = Shader.PropertyToID("_Crest_TextureRotation");
        public static readonly int s_FeatherWidth = Shader.PropertyToID("_Crest_FeatherWidth");
        public static readonly int s_NegativeValues = Shader.PropertyToID("_Crest_NegativeValues");
        public static readonly int s_BoundaryXZ = Shader.PropertyToID("_Crest_BoundaryXZ");
        public static readonly int s_DrawBoundaryXZ = Shader.PropertyToID("_Crest_DrawBoundaryXZ");
    }
}
