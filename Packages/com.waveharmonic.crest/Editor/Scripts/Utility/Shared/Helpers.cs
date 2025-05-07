// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace WaveHarmonic.Crest.Editor
{
    /// <summary>
    /// Provides general helper functions for the editor.
    /// </summary>
    static class EditorHelpers
    {
        internal static ComputeShader s_VisualizeNegativeValuesShader;
        internal static ComputeShader VisualizeNegativeValuesShader
        {
            get
            {
                if (s_VisualizeNegativeValuesShader == null)
                {
                    s_VisualizeNegativeValuesShader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Packages/com.waveharmonic.crest/Editor/Shaders/VisualizeNegativeValues.compute");
                }

                return s_VisualizeNegativeValuesShader;
            }
        }

        public static LayerMask LayerMaskField(string label, LayerMask layerMask)
        {
            // Adapted from: http://answers.unity.com/answers/1387522/view.html
            var temporary = EditorGUILayout.MaskField(
                label,
                UnityEditorInternal.InternalEditorUtility.LayerMaskToConcatenatedLayersMask(layerMask),
                UnityEditorInternal.InternalEditorUtility.layers);
            return UnityEditorInternal.InternalEditorUtility.ConcatenatedLayersMaskToLayerMask(temporary);
        }

        /// <summary>Attempts to get the scene view this camera is rendering.</summary>
        /// <returns>The scene view or null if not found.</returns>
        public static SceneView GetSceneViewFromSceneCamera(Camera camera)
        {
            foreach (SceneView sceneView in SceneView.sceneViews)
            {
                if (sceneView.camera == camera)
                {
                    return sceneView;
                }
            }

            return null;
        }

        /// <summary>Get time passed to animated materials.</summary>
        public static float GetShaderTime()
        {
            // When "Always Refresh" is disabled, Unity passes zero. Also uses realtimeSinceStartup:
            // https://github.com/Unity-Technologies/Graphics/blob/5743e39cdf0795cf7cbeb7ba8ffbbcc7ca200709/Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariablesGlobal.cs#L116
            return !Application.isPlaying && SceneView.lastActiveSceneView != null &&
                !SceneView.lastActiveSceneView.sceneViewState.alwaysRefresh ? 0f : Time.realtimeSinceStartup;
        }

        public static GameObject GetGameObject(SerializedObject serializedObject)
        {
            // We will either get the component or the GameObject it is attached to.
            return serializedObject.targetObject is GameObject
                ? serializedObject.targetObject as GameObject
                : (serializedObject.targetObject as Component).gameObject;
        }

        public static Material CreateSerializedMaterial(string shaderPath, string message)
        {
            var shader = Shader.Find(shaderPath);
            Debug.Assert(shader != null, "Crest: Cannot create required material because shader is null");

            var material = new Material(shader);

            // Record the material and any subsequent changes.
            Undo.RegisterCreatedObjectUndo(material, message);
            Undo.RegisterCompleteObjectUndo(material, message);

            return material;
        }

        public static Material CreateSerializedMaterial(string shaderPath)
        {
            return CreateSerializedMaterial(shaderPath, Undo.GetCurrentGroupName());
        }

        public static Object GetDefaultReference(this SerializedObject self, string property)
        {
            var path = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(self.targetObject as MonoBehaviour));
            var importer = AssetImporter.GetAtPath(path) as MonoImporter;
            return importer.GetDefaultReference(property);
        }

        public static object GetDefiningBoxedObject(this SerializedProperty property)
        {
            object target = property.serializedObject.targetObject;

            if (property.depth > 0)
            {
                // Get the property path so we can find it from the serialized object.
                var path = string.Join(".", property.propertyPath.Split(".", System.StringSplitOptions.None)[0..^1]);
                var other = property.serializedObject.FindProperty(path);
                // Boxed value can handle both managed and generic with caveats:
                // https://docs.unity3d.com/ScriptReference/SerializedProperty-boxedValue.html
                // Not sure if it will be a new or same instance as in the scene.
                target = other.boxedValue;
            }

            return target;
        }

        internal delegate Object CreateInstance(SerializedProperty property);

        internal static Rect AssetField
        (
            System.Type type,
            GUIContent label,
            SerializedProperty property,
            Rect rect,
            string title,
            string defaultName,
            string extension,
            string message,
            CreateInstance create
        )
        {
            var hSpace = 5;
            var buttonWidth = 45;
            var buttonCount = 2;

            rect.width -= buttonWidth * buttonCount + hSpace;
            EditorGUI.PropertyField(rect, property, label);

            var r = new Rect(rect);

            r.x += r.width + hSpace;
            r.width = buttonWidth;
            if (GUI.Button(r, "New", EditorStyles.miniButtonLeft))
            {
                var path = EditorUtility.SaveFilePanelInProject(title, defaultName, extension, message);
                if (!string.IsNullOrEmpty(path))
                {
                    var asset = create(property);
                    if (asset != null)
                    {
                        if (extension == "prefab")
                        {
                            PrefabUtility.SaveAsPrefabAsset(asset as GameObject, path);
                        }
                        else
                        {
                            AssetDatabase.CreateAsset(asset, path);
                        }

                        property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>(path);
                        property.serializedObject.ApplyModifiedProperties();
                    }
                    else
                    {
                        Debug.LogError($"Crest: Could not create file");
                    }
                }
            }

            // Only allow cloning if extensions match. Guards against cloning Shader Graph if
            // using its embedded material.
            var cloneable = property.objectReferenceValue != null;
            cloneable = cloneable && Path.GetExtension(AssetDatabase.GetAssetPath(property.objectReferenceValue)) == $".{extension}";

            EditorGUI.BeginDisabledGroup(!cloneable);
            r.x += r.width;
            if (GUI.Button(r, "Clone", EditorStyles.miniButtonRight))
            {
                var oldPath = AssetDatabase.GetAssetPath(property.objectReferenceValue);
                var newPath = oldPath;
                if (!newPath.StartsWithNoAlloc("Assets")) newPath = Path.Join("Assets", Path.GetFileName(newPath));
                newPath = AssetDatabase.GenerateUniqueAssetPath(newPath);
                AssetDatabase.CopyAsset(oldPath, newPath);
                property.objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>(newPath);
            }
            EditorGUI.EndDisabledGroup();

            return rect;
        }

        internal static void RichTextHelpBox(string message, MessageType type)
        {
            var styleRichText = GUI.skin.GetStyle("HelpBox").richText;
            GUI.skin.GetStyle("HelpBox").richText = true;

            EditorGUILayout.HelpBox(message, type);

            // Revert skin since it persists.
            GUI.skin.GetStyle("HelpBox").richText = styleRichText;
        }

        // Prettify nameof.
        internal static string Pretty(this string text)
        {
            // Regular expression to split on transitions from lower to upper case and keep acronyms together
            return Regex.Replace(text, @"([a-z])([A-Z])|([A-Z])([A-Z][a-z])", "$1$3 $2$4").Replace("_", "");
        }

        internal static string Italic(this string text)
        {
            return $"<i>{text}</i>";
        }

        public static void MarkCurrentStageAsDirty()
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();

            if (stage != null)
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(stage.scene);
            }
            else
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
        }

        static readonly MethodInfo s_ButtonWithDropdownList = typeof(EditorGUI).GetMethod
        (
            "ButtonWithDropdownList",
            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
            null,
            new System.Type[] { typeof(GUIContent), typeof(string[]), typeof(GenericMenu.MenuFunction2), typeof(GUILayoutOption[]) },
            null
        );

        static readonly GUILayoutOption[] s_GUILayoutOptionsZero;

        public static bool ButtonWithDropdownList(GUIContent name, string[] names, GenericMenu.MenuFunction2 callback)
        {
            if (names == null)
            {
                return GUILayout.Button(name);
            }

            return (bool)s_ButtonWithDropdownList.Invoke(null, new object[] { name, names, callback, s_GUILayoutOptionsZero });
        }
    }

    static partial class Extensions
    {
        internal static string GetSubShaderTag([DisallowNull] this Shader shader, ShaderSnippetData snippet, ShaderTagId id)
        {
            var data = ShaderUtil.GetShaderData(shader);
            if (data == null) return null;

            var index = (int)snippet.pass.SubshaderIndex;
            if (index < 0 || index >= shader.subshaderCount) return null;

            var subShader = data.GetSerializedSubshader(index);
            if (subShader == null) return null;

            var tag = subShader.FindTagValue(id);
            if (string.IsNullOrEmpty(tag.name)) return null;

            return tag.name;
        }
    }
}
