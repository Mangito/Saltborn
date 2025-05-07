// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.Compilation;
using UnityEngine.Rendering;
using WaveHarmonic.Crest.Editor.Settings;

namespace WaveHarmonic.Crest.Editor
{
    static class ShaderSettingsGenerator
    {
        [DidReloadScripts]
        static void OnReloadScripts()
        {
            EditorApplication.update -= GenerateAfterReloadScripts;
            EditorApplication.update += GenerateAfterReloadScripts;
        }

        static async void GenerateAfterReloadScripts()
        {
            if (EditorApplication.isCompiling)
            {
                return;
            }

            EditorApplication.update -= GenerateAfterReloadScripts;

            // Generate HLSL from C#. Only targets WaveHarmonic.Crest assemblies.
            await ShaderGeneratorUtility.GenerateAll();
            AssetDatabase.Refresh();
        }

        internal static void Generate()
        {
            if (EditorApplication.isCompiling)
            {
                return;
            }

            // Could not ShaderGeneratorUtility.GenerateAll to work without recompiling…
            CompilationPipeline.RequestScriptCompilation();
        }

        sealed class AssetPostProcessor : AssetPostprocessor
        {
            static async void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] movedTo, string[] movedFrom, bool domainReload)
            {
                // Unused.
                _ = deleted; _ = movedTo; _ = movedFrom; _ = domainReload;

                if (EditorApplication.isCompiling)
                {
                    return;
                }

                // Regenerate if file changed like re-importing.
                if (imported.Contains("Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings.Crest.hlsl"))
                {
                    // Generate HLSL from C#. Only targets WaveHarmonic.Crest assemblies.
                    await ShaderGeneratorUtility.GenerateAll();
                    AssetDatabase.Refresh();
                }
            }
        }
    }

    [GenerateHLSL(sourcePath = "Packages/com.waveharmonic.crest/Runtime/Shaders/Library/Settings.Crest")]
    sealed class ShaderSettings
    {
        public static int s_CrestPortals =
#if d_CrestPortals
            1
#else
            0
#endif
        ;

        public static int s_CrestShiftingOrigin =
#if d_WaveHarmonic_Crest_ShiftingOrigin
            1
#else
            0
#endif
        ;

        public static int s_CrestShadowsBuiltInRenderPipeline = ProjectSettings.Instance.BuiltInRendererSampleShadowMaps ? 1 : 0;
        public static int s_CrestFullPrecisionDisplacement = ProjectSettings.Instance.FullPrecisionDisplacementOnHalfPrecisionPlatforms ? 1 : 0;
    }
}
