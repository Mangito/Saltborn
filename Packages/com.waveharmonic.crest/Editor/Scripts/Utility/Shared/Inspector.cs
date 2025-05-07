// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using WaveHarmonic.Crest.Internal;

namespace WaveHarmonic.Crest.Editor
{
    /// <summary>
    /// Base editor. Needed as custom drawers require a custom editor to work.
    /// </summary>
    [CustomEditor(typeof(EditorBehaviour), editorForChildClasses: true)]
    partial class Inspector : UnityEditor.Editor
    {
        public const int k_FieldGroupOrder = 1000;
        const string k_NoPrefabModeSupportWarning = "Prefab mode is not supported. Changes made in prefab mode will not be reflected in the scene view. Save this prefab to see changes.";

        internal static Inspector Current { get; private set; }

        readonly Dictionary<Material, MaterialEditor> _MaterialEditors = new();

        public override bool RequiresConstantRepaint() => TexturePreview.AnyActivePreviews;

        // Set this from a decorator to enable the material editor.
        internal List<Material> _Materials = new();

        readonly Utility.SortedList<int, SerializedProperty> _Properties = new(Helpers.DuplicateComparison);

        public override void OnInspectorGUI()
        {
            // Reset foldout values.
            DecoratedDrawer.s_IsFoldout = false;
            DecoratedDrawer.s_IsFoldoutOpen = false;

            // Make sure we adhere to flags.
            var enabled = GUI.enabled;
            GUI.enabled = (target.hideFlags & HideFlags.NotEditable) == 0;

            RenderBeforeInspectorGUI();
            RenderInspectorGUI();
            RenderValidationMessages();

            EditorGUI.BeginDisabledGroup(target is Behaviour component && !component.isActiveAndEnabled);
            RenderBottomButtons();
            EditorGUI.EndDisabledGroup();

            RenderAfterInspectorGUI();

            GUI.enabled = enabled;
        }

        protected virtual void OnDisable()
        {
            foreach (var (_, editor) in _MaterialEditors)
            {
                Helpers.Destroy(editor);
            }
        }

        protected void RenderBeforeInspectorGUI()
        {
            if (this.target is EditorBehaviour target && target._IsPrefabStageInstance)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(k_NoPrefabModeSupportWarning, MessageType.Warning);
                EditorGUILayout.Space();
            }
        }

        protected virtual void RenderInspectorGUI()
        {
            var previous = Current;
            Current = this;

            _Properties.Clear();

            serializedObject.Update();

            using var iterator = serializedObject.GetIterator();
            if (iterator.NextVisible(true))
            {
                var index = 0;
                var group = 0;
                Type type = null;

                do
                {
                    var property = serializedObject.FindProperty(iterator.name);
                    if (iterator.name == "m_Script")
                    {
#if CREST_DEBUG
                        _Properties.Add(index++, property);
#endif
                        continue;
                    }

                    var field = property.GetFieldInfo(out _);

                    // If field is on a new type (inheritance) then reset the group to prevent group leakage.
                    if (field.DeclaringType != type)
                    {
                        group = 0;
                        type = field.DeclaringType;
                    }

                    // Null checking but there should always be one DecoratedProperty.
                    var order = field.GetCustomAttribute<Attributes.DecoratedProperty>()?.order ?? 0;
                    group = field.GetCustomAttribute<Group>()?.order * k_FieldGroupOrder ?? group;
                    _Properties.Add(order + group + index++, property);
                }
                while (iterator.NextVisible(false));
            }

            foreach (var (_, property) in _Properties)
            {
#if CREST_DEBUG
                using (new EditorGUI.DisabledGroupScope(property.name == "m_Script"))
#endif
                {
                    // Only support top level ordering for now.
                    EditorGUILayout.PropertyField(property, includeChildren: true);
                }
            }

            // Need to call just in case there is no decorated property.
            serializedObject.ApplyModifiedProperties();

            // Restore previous in case this is a nested editor.
            Current = previous;

            // Fixes indented validation etc.
            EditorGUI.indentLevel = 0;
        }

        protected virtual void RenderBottomButtons()
        {

        }

        protected virtual void RenderAfterInspectorGUI()
        {
            foreach (var material in _Materials)
            {
                if (material == null) continue;
                DrawMaterialEditor(material);
            }

            _Materials.Clear();
        }

        // Adapted from: http://answers.unity.com/answers/975894/view.html
        void DrawMaterialEditor(Material material)
        {
            MaterialEditor editor;
            if (!_MaterialEditors.ContainsKey(material))
            {
                editor = (MaterialEditor)CreateEditor(material);
                _MaterialEditors[material] = editor;
            }
            else
            {
                editor = _MaterialEditors[material];
            }

            // Check material again as sometimes null.
            if (editor != null && material != null)
            {
                EditorGUILayout.Space();

                // Draw the material's foldout and the material shader field. Required to call OnInspectorGUI.
                editor.DrawHeader();

                var path = AssetDatabase.GetAssetPath(material);

                // We need to prevent the user from editing Unity's default materials.
                // Check Editor.IsEnabled in Editor.cs for further filtering.
                var isEditable = (material.hideFlags & HideFlags.NotEditable) == 0;

#if !CREST_DEBUG
                // Do not allow editing of our assets.
                isEditable &= !path.StartsWithNoAlloc("Packages/com.waveharmonic");
#endif

                using (new EditorGUI.DisabledGroupScope(!isEditable))
                {
                    // Draw the material properties. Works only if the foldout of DrawHeader is open.
                    editor.OnInspectorGUI();
                }
            }
        }
    }

    // Adapted from:
    // https://gist.github.com/thebeardphantom/1ad9aee0ef8de6271fff39f1a6a3d66d
    static partial class Extensions
    {
        static readonly MethodInfo s_GetFieldInfoFromProperty;

        static Extensions()
        {
            var utility = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ScriptAttributeUtility");
            Debug.Assert(utility != null, "Crest: ScriptAttributeUtility not found!");

            s_GetFieldInfoFromProperty = utility.GetMethod("GetFieldInfoFromProperty", BindingFlags.NonPublic | BindingFlags.Static);
            Debug.Assert(s_GetFieldInfoFromProperty != null, "Crest: GetFieldInfoFromProperty not found!");
        }

        public static FieldInfo GetFieldInfo(this SerializedProperty property, out Type type)
        {
            type = null;
            return (FieldInfo)s_GetFieldInfoFromProperty.Invoke(null, new object[] { property, type });
        }
    }
}
