// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

#pragma warning disable

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.ShaderGraph;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;

[assembly: InternalsVisibleTo("WaveHarmonic.Crest.Editor")]

namespace WaveHarmonic.Crest.Editor
{
    internal static class ShaderGraphPropertyDrawers
    {
        static Dictionary<GraphInputData, bool> s_CompoundPropertyFoldoutStates = new();
        static Dictionary<string, string> s_Tooltips;
        static readonly GUIContent s_Label = new();

        public static void DrawShaderGraphGUI(MaterialEditor materialEditor, IEnumerable<MaterialProperty> properties, Dictionary<string, string> tooltips)
        {
            s_Tooltips = tooltips;
            Material m = materialEditor.target as Material;
            Shader s = m.shader;
            string path = AssetDatabase.GetAssetPath(s);
            ShaderGraphMetadata metadata = null;
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (obj is ShaderGraphMetadata meta)
                {
                    metadata = meta;
                    break;
                }
            }

            if (metadata != null)
                DrawShaderGraphGUI(materialEditor, properties, metadata.categoryDatas);
            else
                PropertiesDefaultGUI(materialEditor, properties);
        }

        static void PropertiesDefaultGUI(MaterialEditor materialEditor, IEnumerable<MaterialProperty> properties)
        {
            foreach (var property in properties)
            {
                if ((property.flags & (MaterialProperty.PropFlags.HideInInspector | MaterialProperty.PropFlags.PerRendererData)) != 0)
                    continue;

                float h = materialEditor.GetPropertyHeight(property, property.displayName);
                Rect r = EditorGUILayout.GetControlRect(true, h, EditorStyles.layerMaskField);

                s_Label.text = property.displayName;
                s_Label.tooltip = s_Tooltips.GetValueOrDefault(property.name, null);
                materialEditor.ShaderProperty(r, property, s_Label);
            }
        }

        static Rect GetRect(MaterialProperty prop)
        {
            return EditorGUILayout.GetControlRect(true, MaterialEditor.GetDefaultPropertyHeight(prop));
        }

        static MaterialProperty FindProperty(string propertyName, IEnumerable<MaterialProperty> properties)
        {
            foreach (var prop in properties)
            {
                if (prop.name == propertyName)
                {
                    return prop;
                }
            }

            return null;
        }

        public static void DrawShaderGraphGUI(MaterialEditor materialEditor, IEnumerable<MaterialProperty> properties, IEnumerable<MinimalCategoryData> categoryDatas)
        {
            foreach (MinimalCategoryData mcd in categoryDatas)
            {
                DrawCategory(materialEditor, properties, mcd);
            }
        }

        static void DrawCategory(MaterialEditor materialEditor, IEnumerable<MaterialProperty> properties, MinimalCategoryData minimalCategoryData)
        {
            if (minimalCategoryData.categoryName.Length > 0)
            {
                minimalCategoryData.expanded = EditorGUILayout.BeginFoldoutHeaderGroup(minimalCategoryData.expanded, minimalCategoryData.categoryName);
            }
            else
            {
                // force draw if no category name to do foldout on
                minimalCategoryData.expanded = true;
            }

            if (minimalCategoryData.expanded)
            {
                foreach (var propData in minimalCategoryData.propertyDatas)
                {
                    if (propData.isCompoundProperty == false)
                    {
                        MaterialProperty prop = FindProperty(propData.referenceName, properties);
                        if (prop == null) continue;
                        DrawMaterialProperty(materialEditor, prop, propData.propertyType, propData.isKeyword, propData.keywordType);
                    }
                    else
                    {
                        DrawCompoundProperty(materialEditor, properties, propData);
                    }
                }
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        static void DrawCompoundProperty(MaterialEditor materialEditor, IEnumerable<MaterialProperty> properties, GraphInputData compoundPropertyData)
        {
            EditorGUI.indentLevel++;

            bool foldoutState = true;
            var exists = s_CompoundPropertyFoldoutStates.ContainsKey(compoundPropertyData);
            if (!exists)
                s_CompoundPropertyFoldoutStates.Add(compoundPropertyData, true);
            else
                foldoutState = s_CompoundPropertyFoldoutStates[compoundPropertyData];

            foldoutState = EditorGUILayout.Foldout(foldoutState, compoundPropertyData.referenceName);
            if (foldoutState)
            {
                EditorGUI.indentLevel++;
                foreach (var subProperty in compoundPropertyData.subProperties)
                {
                    var property = FindProperty(subProperty.referenceName, properties);
                    if (property == null) continue;
                    DrawMaterialProperty(materialEditor, property, subProperty.propertyType);
                }
                EditorGUI.indentLevel--;
            }

            if (exists)
                s_CompoundPropertyFoldoutStates[compoundPropertyData] = foldoutState;
            EditorGUI.indentLevel--;
        }

        static void DrawMaterialProperty(MaterialEditor materialEditor, MaterialProperty property, PropertyType propertyType, bool isKeyword = false, KeywordType keywordType = KeywordType.Boolean)
        {
            if (!isKeyword)
            {
                switch (propertyType)
                {
                    case PropertyType.SamplerState:
                    case PropertyType.Matrix4:
                    case PropertyType.Matrix3:
                    case PropertyType.Matrix2:
                    case PropertyType.VirtualTexture:
                    case PropertyType.Gradient:
                        return;
                    case PropertyType.Vector3:
                        DrawVector3Property(materialEditor, property);
                        return;
                    case PropertyType.Vector2:
                        DrawVector2Property(materialEditor, property);
                        return;
                }
            }

            s_Label.text = property.displayName;
            s_Label.tooltip = s_Tooltips.GetValueOrDefault(property.name, null);
            materialEditor.ShaderProperty(property, s_Label);
        }

        static void DrawVector2Property(MaterialEditor materialEditor, MaterialProperty property)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = property.hasMixedValue;
            Vector2 newValue = EditorGUI.Vector2Field(GetRect(property), property.displayName, new Vector2(property.vectorValue.x, property.vectorValue.y));
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                property.vectorValue = newValue;
            }
        }

        static void DrawVector3Property(MaterialEditor materialEditor, MaterialProperty property)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = property.hasMixedValue;
            Vector3 newValue = EditorGUI.Vector3Field(GetRect(property), property.displayName, new Vector3(property.vectorValue.x, property.vectorValue.y, property.vectorValue.z));
            EditorGUI.showMixedValue = false;
            if (EditorGUI.EndChangeCheck())
            {
                property.vectorValue = newValue;
            }
        }
    }
}
