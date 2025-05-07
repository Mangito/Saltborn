// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace WaveHarmonic.Crest.Editor.Settings
{
    [FilePath(k_Path, FilePathAttribute.Location.ProjectFolder)]
    sealed class ProjectSettings : ScriptableSingleton<ProjectSettings>
    {
        [SerializeField, HideInInspector]
#pragma warning disable 414
        int _Version = 0;
#pragma warning restore 414

#pragma warning disable IDE0032 // Use auto property

        [@Heading("Variant Stripping", Heading.Style.Settings)]

        [@Group]

        [@DecoratedField, SerializeField]
        bool _DebugEnableStrippingLogging;

        [@Predicated(nameof(_DebugEnableStrippingLogging))]
        [@DecoratedField, SerializeField]
        bool _DebugOnlyLogRemainingVariants;

        [Tooltip("Whether to strip broken variants.\n\nCurrently, the only known case is the point cookie variant being broken on Xbox.")]
        [@DecoratedField, SerializeField]
        bool _StripBrokenVariants = true;

        [@Heading("Features", Heading.Style.Settings)]

        [@Group]

        [Tooltip("Whether to use full precision sampling for half precision platforms (typically mobile).\n\nThis will solve rendering artifacts like minor bumps and staircasing.")]
        [@DecoratedField, SerializeField]
        bool _FullPrecisionDisplacementOnHalfPrecisionPlatforms = true;

        [Tooltip("Whether to sample shadow maps for built-in renderer.")]
        [@DecoratedField, SerializeField]
        bool _BuiltInRendererSampleShadowMaps = true;

#pragma warning restore IDE0032 // Use auto property

        internal const string k_Path = "ProjectSettings/Packages/com.waveharmonic.crest/Settings.asset";

        internal enum State
        {
            Dynamic,
            Disabled,
            Enabled,
        }

        internal static ProjectSettings Instance => instance;

        internal bool StripBrokenVariants => _StripBrokenVariants;
        internal bool DebugEnableStrippingLogging => _DebugEnableStrippingLogging;
        internal bool LogStrippedVariants => _DebugEnableStrippingLogging && !_DebugOnlyLogRemainingVariants;
        internal bool LogKeptVariants => _DebugEnableStrippingLogging && _DebugOnlyLogRemainingVariants;
        internal bool FullPrecisionDisplacementOnHalfPrecisionPlatforms => _FullPrecisionDisplacementOnHalfPrecisionPlatforms;
        internal bool BuiltInRendererSampleShadowMaps => _BuiltInRendererSampleShadowMaps;


        void OnEnable()
        {
            // Fixes not being editable.
            hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
        }


        internal static void Save()
        {
            instance.Save(saveAsText: true);
        }

        [@OnChange(skipIfInactive: false)]
        void OnChange(string path, object previous)
        {
            switch (path)
            {
                case nameof(_BuiltInRendererSampleShadowMaps):
                case nameof(_FullPrecisionDisplacementOnHalfPrecisionPlatforms):
                    ShaderSettingsGenerator.Generate();
                    break;
            }
        }
    }

    sealed class SettingsProvider : UnityEditor.SettingsProvider
    {
        static readonly string[] s_ShaderGraphs = new string[]
        {
            "Packages/com.waveharmonic.crest/Runtime/Shaders/Surface/Water.shadergraph",
            "Packages/com.waveharmonic.crest/Shared/Shaders/Lit.shadergraph",
            "Packages/com.waveharmonic.crest.paint/Samples/Colorado/Shaders/SpeedTree8_PBRLit.shadergraph",
            "Packages/com.waveharmonic.crest.paint/Samples/Colorado/Shaders/Environment (Splat Map).shadergraph",
        };

        UnityEditor.Editor _Editor;

        SettingsProvider(string path, SettingsScope scope = SettingsScope.User) : base(path, scope)
        {
            // Empty
        }

        static bool IsSettingsAvailable()
        {
            return File.Exists(ProjectSettings.k_Path);
        }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            base.OnActivate(searchContext, rootElement);
            _Editor = UnityEditor.Editor.CreateEditor(ProjectSettings.Instance);
            Undo.undoRedoPerformed -= OnUndoRedo;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();
            Helpers.Destroy(_Editor);
            Undo.undoRedoPerformed -= OnUndoRedo;
        }

        void OnUndoRedo()
        {
            ProjectSettings.Save();
        }

        public override void OnGUI(string searchContext)
        {
            if (_Editor.target == null)
            {
                Helpers.Destroy(_Editor);
                _Editor = UnityEditor.Editor.CreateEditor(ProjectSettings.Instance);
                return;
            }

            // Reset foldout values.
            DecoratedDrawer.s_IsFoldout = false;
            DecoratedDrawer.s_IsFoldoutOpen = false;

            EditorGUI.BeginChangeCheck();

            // Pad similar to settings header.
            var style = new GUIStyle();
            style.padding.left = 8;

            // Same label with as other settings.
            EditorGUIUtility.labelWidth = 251;

            EditorGUILayout.BeginVertical(style);
            _Editor.OnInspectorGUI();
            EditorGUILayout.EndVertical();

            // Commit all changes. Normally settings are written when user hits save or exits
            // without any undo/redo entry and dirty state. No idea how to do the same.
            // SaveChanges and hasUnsavedChanges on custom editor did not work.
            // Not sure if hooking into EditorSceneManager.sceneSaving is correct.
            if (EditorGUI.EndChangeCheck())
            {
                ProjectSettings.Save();
            }

            GUILayout.Space(10 * 2);

            if (GUILayout.Button("Repair Shaders"))
            {
                foreach (var path in s_ShaderGraphs)
                {
                    if (!File.Exists(path)) continue;
                    AssetDatabase.ImportAsset(path);
                }
            }
        }

        [SettingsProvider]
        static UnityEditor.SettingsProvider Create()
        {
            if (ProjectSettings.Instance)
            {
                var provider = new SettingsProvider("Project/Crest", SettingsScope.Project);
                provider.keywords = GetSearchKeywordsFromSerializedObject(new(ProjectSettings.Instance));
                return provider;
            }

            // Settings Asset doesn't exist yet; no need to display anything in the Settings window.
            return null;
        }
    }
}
