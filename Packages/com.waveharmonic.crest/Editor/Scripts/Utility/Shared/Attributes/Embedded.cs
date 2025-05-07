// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEditor;
using UnityEngine;
using WaveHarmonic.Crest.Attributes;
using WaveHarmonic.Crest.Editor;

namespace WaveHarmonic.Crest
{
    sealed class Embedded : DecoratedProperty
    {
        internal EmbeddedAssetEditor _Editor;
        readonly int _BottomMargin;

        public Embedded(int margin = 0)
        {
            _Editor = new();
            _BottomMargin = margin;
        }

        internal override void OnGUI(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            _Editor.DrawEditorCombo(label, drawer, property, "asset", _BottomMargin);
        }

        internal override bool NeedsControlRectangle(SerializedProperty property) => false;
    }
}
