// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Based on Unity's ScriptableSingleton but works with Assets and Packages
// directory. Works in builds except loading from file so it will need a
// reference in the scene for it to instantiate.

using System;
using UnityEditor;
using UnityEngine;

namespace WaveHarmonic.Crest.Utility
{
    [AttributeUsage(AttributeTargets.Class)]
    sealed class FilePath : Attribute
    {
        public readonly string _Path;

        public FilePath(string path)
        {
            _Path = path;
        }
    }

    abstract class ScriptableSingleton<T> : ScriptableObject where T : ScriptableObject
    {
        public static T Instance { get; private set; }

        public ScriptableSingleton()
        {
            // Constructor will be called during run-time if there is a asset reference saved
            // in a scene or preloaded assets.
            // BUG: There appears to be a Unity bug where this will not happen which required
            // recreating the asset. Perhaps after renaming the script.
            Instance = this as T;
        }

#if UNITY_EDITOR
        static string GetFilePath()
        {
            foreach (var attribute in typeof(T).GetCustomAttributes(inherit: true))
            {
                if (attribute is FilePath f)
                {
                    return f._Path;
                }
            }

            return string.Empty;
        }

        internal static void LoadFromAsset()
        {
            if (Instance != null)
            {
                return;
            }

            // This will trigger the constructor and set Instance. But setting it here first
            // prevents exceptions when data changes.
            Instance = AssetDatabase.LoadAssetAtPath<T>(GetFilePath());
        }
#endif
    }
}
