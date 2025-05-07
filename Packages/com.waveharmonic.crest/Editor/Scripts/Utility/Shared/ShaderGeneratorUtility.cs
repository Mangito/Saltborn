// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Exposes a customized version of Unity's shader generator task to only generate
// our source files.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine.Rendering;

namespace WaveHarmonic.Crest.Editor
{
    static class ShaderGeneratorUtility
    {
        static MethodInfo s_GenerateAsync;
        static MethodInfo GenerateAsync => s_GenerateAsync ??= typeof(CSharpToHLSL)
            .GetMethod("GenerateAsync", BindingFlags.Static | BindingFlags.NonPublic);

        static async Task InvokeAsync(this MethodInfo methodInfo, object obj, params object[] parameters)
        {
            dynamic awaitable = methodInfo.Invoke(obj, parameters);
            await awaitable;
        }

        // Adapted from:
        // https://github.com/Unity-Technologies/Graphics/blob/96ba978a240e96adcb2abceb21e90b24caa484a3/Packages/com.unity.render-pipelines.core/Editor/ShaderGenerator/CSharpToHLSL.cs#L18L53
        internal static async Task GenerateAll()
        {
            Dictionary<string, List<ShaderTypeGenerator>> sourceGenerators = null;
            try
            {
                // Store per source file path the generator definitions
                sourceGenerators = DictionaryPool<string, List<ShaderTypeGenerator>>.Get();

                // Extract all types with the GenerateHLSL tag
                foreach (var type in TypeCache.GetTypesWithAttribute<GenerateHLSL>())
                {
                    // Only generate our sources as Unity's will trigger a package refresh.
                    if (!type.FullName.StartsWith("WaveHarmonic.Crest")) continue;

                    var attr = type.GetCustomAttributes(typeof(GenerateHLSL), false).First() as GenerateHLSL;
                    if (!sourceGenerators.TryGetValue(attr.sourcePath, out var generators))
                    {
                        generators = ListPool<ShaderTypeGenerator>.Get();
                        sourceGenerators.Add(attr.sourcePath, generators);
                    }

                    generators.Add(new(type, attr));
                }

                // We need to force the culture to invariant, otherwise generated code can replace characters.
                // For example, Turkish will replace "I" with "İ". This is a bug with GenerateAsync.
                var culture = System.Globalization.CultureInfo.CurrentCulture;
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

                // Generate all files
                await Task.WhenAll(sourceGenerators.Select(async it => await GenerateAsync
                    .InvokeAsync(null, new object[] { $"{it.Key}.hlsl", $"{Path.ChangeExtension(it.Key, "custom")}.hlsl", it.Value })));

                System.Globalization.CultureInfo.CurrentCulture = culture;
            }
            finally
            {
                // Make sure we always release pooled resources
                if (sourceGenerators != null)
                {
                    foreach (var pair in sourceGenerators)
                        ListPool<ShaderTypeGenerator>.Release(pair.Value);
                    DictionaryPool<string, List<ShaderTypeGenerator>>.Release(sourceGenerators);
                }
            }
        }
    }
}
