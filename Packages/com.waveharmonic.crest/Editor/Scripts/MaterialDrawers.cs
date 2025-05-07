// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Repository of custom material property drawers.
// All drawers must be prefixed with Crest as they are global.

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace WaveHarmonic.Crest.Editor
{
    static class MaterialAttributes
    {
        internal sealed class MaterialAttribute
        {
            public Vector2Int _IntegerRange;
        }

        internal static readonly Dictionary<string, MaterialAttribute> s_Common = new()
        {

        };

        internal static readonly Dictionary<string, Dictionary<string, MaterialAttribute>> s_Grouped = new()
        {
            {
                "Crest/Underwater", new()
                {
                    { "_Crest_DataSliceOffset", new() { _IntegerRange = new(0, Lod.k_MaximumSlices - 2) } },
                }
            },
        };
    }

    sealed class CrestIntegerRangeDrawer : MaterialPropertyDrawer
    {
        // Adapted from:
        // https://github.com/Unity-Technologies/UnityCsReference/blob/b44c4cc9e4ce3dfa0bab2fe4bf7efae880c5a175/Editor/Mono/Inspector/MaterialEditor.cs#L1277-L1298
        public override void OnGUI(Rect position, MaterialProperty property, GUIContent label, MaterialEditor editor)
        {
            MaterialEditor.BeginProperty(position, property);

            EditorGUI.BeginChangeCheck();

            // For range properties we want to show the slider so we adjust label width to use default width (setting it to 0)
            // See SetDefaultGUIWidths where we set: EditorGUIUtility.labelWidth = GUIClip.visibleRect.width - EditorGUIUtility.fieldWidth - 17;
            var oldLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 0f;

            var material = editor.target as Material;
            var shader = material.shader;

            var attribute = MaterialAttributes.s_Grouped.GetValueOrDefault(shader.name, null)?.GetValueOrDefault(property.name, null);
            attribute ??= MaterialAttributes.s_Common[property.name];

            var newValue = EditorGUI.IntSlider(position, label, property.intValue, attribute._IntegerRange.x, attribute._IntegerRange.y);

            EditorGUIUtility.labelWidth = oldLabelWidth;

            if (EditorGUI.EndChangeCheck())
            {
                property.intValue = newValue;
            }

            MaterialEditor.EndProperty();
        }
    }
}
