// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEditor;
using UnityEngine;
using WaveHarmonic.Crest.Attributes;
using WaveHarmonic.Crest.Editor;

namespace WaveHarmonic.Crest
{
    sealed class Layer : DecoratedProperty
    {
        internal override void OnGUI(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            property.intValue = EditorGUI.LayerField(position, label, property.intValue);
        }
    }
}
