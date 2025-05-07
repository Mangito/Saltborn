// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using WaveHarmonic.Crest.Editor.Settings;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace WaveHarmonic.Crest.Editor.Build
{
    sealed class LegacyShaderGraphProcessor : IPreprocessShaders, IPostprocessBuildWithReport
    {
        static readonly ShaderTagId s_ShaderGraphShaderShaderTagId = new("ShaderGraphShader");

        public int callbackOrder => -1;

        int _VariantCount;
        int _VariantCountStripped;

        bool LogVariantStripping =>
#if CREST_DEBUG
            true;
#else
            false;
#endif

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            // Not one of our shaders.
            if (!shader.name.StartsWithNoAlloc("Hidden/Crest/") && !shader.name.StartsWithNoAlloc("Crest/"))
            {
                return;
            }

            // Not a Shader Graph.
            if (shader.GetSubShaderTag(snippet, s_ShaderGraphShaderShaderTagId) != "true")
            {
                return;
            }

            // Sub-shader is not targeting the built-in render pipeline.
            if (shader.TryGetRenderPipelineTag(snippet, out _))
            {
                return;
            }

            _VariantCount += data.Count;

            // Strip BIRP sub-shader if not using BIRP, as Unity only strips HDRP/URP sub-shaders.
            if (!RenderPipelineHelper.IsLegacy)
            {
                _VariantCountStripped += data.Count;
                data.Clear();
                return;
            }

            for (var i = data.Count - 1; i >= 0; --i)
            {
                var keywords = data[i].shaderKeywordSet.GetShaderKeywords();
                var isTransparent = keywords.Any(x => x.name == "_BUILTIN_SURFACE_TYPE_TRANSPARENT");

                foreach (var keyword in keywords)
                {
                    var name = keyword.name;
                    var strip =
                        // Main light shadows. Never used.
                        name.StartsWithNoAlloc("_MAIN_LIGHT_") ||
                        // Additional lights. Never used. Although, vertex lighting keyword is set.
                        name.StartsWithNoAlloc("_ADDITIONAL_LIGHT") ||
                        // Never used. Used in deferred pass, but not defined in deferred pass.
                        keyword.name is "LIGHTMAP_SHADOW_MIXING" or "SHADOWS_SHADOWMASK" ||
                        // Never used.
                        keyword.name is "_SCREEN_SPACE_OCCLUSION" or "_SHADOWS_SOFT" or "_CASTING_PUNCTUAL_LIGHT_SHADOW" ||
                        // BIRP does not support this feature (URP does).
                        keyword.name is "_GBUFFER_NORMALS_OCT" ||
                        // TODO: check LightMode instead of pass name.
                        // Shadow keywords are not enabled for transparent objects, except if casting.
                        isTransparent && snippet.passName == "ShadowCaster" && keyword.name.Contains("SHADOW");

                    if (strip)
                    {
                        _VariantCountStripped++;
                        data.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (LogVariantStripping)
            {
                Debug.Log($"Crest: {_VariantCountStripped} / {_VariantCount} stripped from Crest BIRP. Total variants: {_VariantCount - _VariantCountStripped}");
            }
        }
    }

    sealed class BuildProcessor : IPreprocessComputeShaders, IPreprocessShaders, IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        int _VariantCount;
        int _VariantCountStripped;

        ProjectSettings _Settings;
        WaterResources _Resources;

        void Logger(string message)
        {
            Debug.Log(message);
        }

        bool StripShader(Object shader, IList<ShaderCompilerData> data)
        {
            _Settings = ProjectSettings.Instance;
            _Resources = WaterResources.Instance;

            if (!AssetDatabase.GetAssetPath(shader).StartsWithNoAlloc("Packages/com.waveharmonic.crest"))
            {
                return false;
            }

            if (shader.name.StartsWithNoAlloc("Hidden/Crest/Samples/"))
            {
                return false;
            }

            if (_Settings.DebugEnableStrippingLogging)
            {
                Logger($"Shader: '{shader.name}' @ {AssetDatabase.GetAssetPath(shader)}");
            }

            _VariantCount += data.Count;

            if (ShouldStripShader(shader))
            {
                if (_Settings.LogStrippedVariants)
                {
                    Logger($"Stripping Shader: {shader.name}");
                }

                _VariantCountStripped += data.Count;
                data.Clear();
                return false;
            }

            return true;
        }

        bool ShouldStripVariant(Object shader, ShaderCompilerData data, string[] keywords)
        {
            return false;
        }

        bool ShouldStripVariant(ProjectSettings.State state, ShaderCompilerData data, string[] keywords, LocalKeyword keyword, Object shader0, Object shader1)
        {
            if (shader0 != shader1)
            {
                return false;
            }

            return state switch
            {
                ProjectSettings.State.Disabled => data.shaderKeywordSet.IsEnabled(keyword),
                // Strip if keyword is not enabled and appears in one other variant.
                ProjectSettings.State.Enabled => !data.shaderKeywordSet.IsEnabled(keyword) && ArrayUtility.Contains(keywords, keyword.name),
                _ => false,
            };
        }

        bool ShouldStripVariant(ProjectSettings.State state, ShaderCompilerData data, string[] keywords, ShaderKeyword keyword)
        {
            return state switch
            {
                ProjectSettings.State.Disabled => data.shaderKeywordSet.IsEnabled(keyword),
                // Strip if keyword is not enabled and appears in one other variant.
                ProjectSettings.State.Enabled => !data.shaderKeywordSet.IsEnabled(keyword) && ArrayUtility.Contains(keywords, keyword.name),
                _ => false,
            };
        }

        bool ShouldStripVariant(Object shader, ShaderKeyword[] keywords)
        {
            // Strip debug variants.
            if (!EditorUserBuildSettings.development)
            {
                foreach (var keyword in keywords)
                {
                    if (keyword.name.StartsWithNoAlloc("_DEBUG"))
                    {
                        if (_Settings.LogStrippedVariants)
                        {
                            Logger($"Stripping Keyword: {keyword.name}");
                        }

                        return true;
                    }
                }
            }

            return false;
        }

        bool ShouldStripShader(Object shader)
        {
            if (!EditorUserBuildSettings.development)
            {
                if (shader.name.Contains("Debug"))
                {
                    return true;
                }
            }

            return false;
        }

        void StripKeywords(Object shader, IList<ShaderCompilerData> data)
        {
            // Get all keywords for this kernel/stage.
            string[] keywords;
            {
                var set = new HashSet<ShaderKeyword>();
                for (var i = 0; i < data.Count; i++)
                {
                    // Each ShaderCompilerData is a variant which is a combination of keywords. Since each list will be
                    // different, simply getting a list of all keywords is not possible. This also appears to be the only
                    // way to get a list of keywords without trying to extract them from shader property names. Lastly,
                    // shader_feature will be returned only if they are enabled.
                    set.UnionWith(data[i].shaderKeywordSet.GetShaderKeywords());
                }

                keywords = set.Select(x => x.name).ToArray();
            }

            for (var i = data.Count - 1; i >= 0; --i)
            {
                if (_Settings.LogStrippedVariants)
                {
                    Logger($"Keywords: {string.Join(", ", data[i].shaderKeywordSet.GetShaderKeywords())}");
                }

                if (ShouldStripVariant(shader, data[i].shaderKeywordSet.GetShaderKeywords()))
                {
                    _VariantCountStripped++;
                    data.RemoveAt(i);
                    continue;
                }

                if (ShouldStripVariant(shader, data[i], keywords))
                {
                    _VariantCountStripped++;
                    data.RemoveAt(i);
                    continue;
                }

                if (_Settings.LogKeptVariants)
                {
                    Logger($"Keywords: {string.Join(", ", data[i].shaderKeywordSet.GetShaderKeywords())}");
                }
            }
        }

        bool ShouldStripSubShader(Shader shader, ShaderSnippetData snippet)
        {
            if (!shader.name.StartsWithNoAlloc("Crest/") && !shader.name.StartsWithNoAlloc("Hidden/Crest/"))
            {
                return false;
            }

            // There will be at least three sub-shaders if one per render pipeline.
            if (shader.subshaderCount <= 2)
            {
                return false;
            }

            // Strip BIRP sub-shader if not using BIRP as Unity only strips HDRP/URP sub-shaders.
            if (!RenderPipelineHelper.IsLegacy && !shader.TryGetRenderPipelineTag(snippet, out _))
            {
                return true;
            }

            return false;
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            // Fixes point light cookie variant trigger shader compiler error:
            // > Shader error in 'Crest/Water': call to 'texCUBE' is ambiguous at
            // > Buidin/Library/PackageCache/com.unity.shadergraph/Editor/Generation/Targets/BuiltIn/Editor/ShaderGraph/Includes/PBRForwardAddPass.hlsl(58) (on gamecore_scarlett)
            if (ProjectSettings.Instance.StripBrokenVariants && RenderPipelineHelper.IsLegacy && shader.name == "Crest/Water")
            {
                var pointCookie = new LocalKeyword(shader, "POINT_COOKIE");

                for (var i = data.Count - 1; i >= 0; --i)
                {
                    var d = data[i];

                    if (d.buildTarget != BuildTarget.GameCoreXboxSeries && d.shaderCompilerPlatform != ShaderCompilerPlatform.GameCoreXboxSeries)
                    {
                        continue;
                    }

                    if (d.shaderKeywordSet.IsEnabled(pointCookie))
                    {
                        _VariantCountStripped++;
                        data.RemoveAt(i);
                        Debug.Log($"Crest: Removing POINT_COOKIE {shader.name} {d.buildTarget} {d.shaderCompilerPlatform}");
                        continue;
                    }
                }
            }

            if (!StripShader(shader, data))
            {
                return;
            }

            if (ShouldStripSubShader(shader, snippet))
            {
                _VariantCountStripped += data.Count;
                data.Clear();
                return;
            }

            if (_Settings.DebugEnableStrippingLogging)
            {
                Logger($"Pass {snippet.passName} Type {snippet.passType} Stage {snippet.shaderType}");
            }

            // TODO: Add stripping specific to pixel shaders here.

            StripKeywords(shader, data);
        }

        public void OnProcessComputeShader(ComputeShader shader, string kernel, IList<ShaderCompilerData> data)
        {
            if (!StripShader(shader, data))
            {
                return;
            }

            if (_Settings.DebugEnableStrippingLogging)
            {
                Logger($"Kernel {kernel}");
            }

            // TODO: Add stripping specific to compute shaders here.
            StripKeywords(shader, data);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            _Settings = ProjectSettings.Instance;
            _Resources = WaterResources.Instance;

            if (_Settings.DebugEnableStrippingLogging)
            {
                Debug.Log($"Crest: {_VariantCountStripped} / {_VariantCount} stripped from Crest. Total variants: {_VariantCount - _VariantCountStripped}");
            }
        }
    }
}
