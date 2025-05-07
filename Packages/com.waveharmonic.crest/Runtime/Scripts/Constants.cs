// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

namespace WaveHarmonic.Crest
{
    static class Constants
    {
#if CREST_OCEAN
        const string k_Prefix = "Crest 5 ";
#else
        const string k_Prefix = "Crest ";
#endif
        const string k_MenuScripts = "Crest/";
        public const string k_MenuPrefixScripts = k_MenuScripts + k_Prefix;
        public const string k_MenuPrefixInternal = k_MenuScripts + "Internal/";
        public const string k_MenuPrefixDebug = k_MenuScripts + "Debug/" + k_Prefix;
        public const string k_MenuPrefixInputs = k_MenuScripts + "Inputs/" + k_Prefix;
        public const string k_MenuPrefixTime = k_MenuScripts + "Time/" + k_Prefix;
        public const string k_MenuPrefixSpline = k_MenuScripts + "Spline/" + k_Prefix;
        public const string k_MenuPrefixPhysics = k_MenuScripts + "Physics/" + k_Prefix;
        public const string k_MenuPrefixSample = k_MenuScripts + "Sample/" + k_Prefix;

#if UNITY_EDITOR
        public const int k_FieldGroupOrder = Editor.Inspector.k_FieldGroupOrder;
#else
        public const int k_FieldGroupOrder = 0;
#endif

        // Unity only supports textures up to a size of 16384, even if maxTextureSize returns a larger size.
        // https://docs.unity3d.com/ScriptReference/SystemInfo-maxTextureSize.html
        public const int k_MaximumTextureResolution = 16384;
    }
}
