// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using UnityEditor;
using UnityEngine;
using WaveHarmonic.Crest.Attributes;
using WaveHarmonic.Crest.Editor;

namespace WaveHarmonic.Crest
{
    sealed class HelpBox : Decorator
    {
        // Define our own as Unity's won't be available in builds.
        public enum MessageType
        {
            Info,
            Warning,
            Error,
        }

        public string _Message;
        public MessageType _MessageType;
        public Visibility _Visibility;

        public enum Visibility
        {
            Always,
            PropertyEnabled,
            PropertyDisabled,
        }

        readonly GUIContent _GuiContent;

        public override bool AlwaysVisible => false;

        public HelpBox(string message, MessageType messageType = MessageType.Info, Visibility visibility = Visibility.Always)
        {
            _Message = message;
            _MessageType = messageType;
            _Visibility = visibility;
            _GuiContent = new(message);
        }

        internal override void Decorate(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            if (_Visibility == Visibility.PropertyEnabled && !GUI.enabled || _Visibility == Visibility.PropertyDisabled && GUI.enabled)
            {
                return;
            }

            // Enable rich text in help boxes. Store original so we can revert since this might be a "hack".
            var style = GUI.skin.GetStyle("HelpBox");
            var styleRichText = style.richText;
            style.richText = true;

            var height = style.CalcHeight(_GuiContent, EditorGUIUtility.currentViewWidth);
            if (height <= EditorGUIUtility.singleLineHeight)
            {
                // This gets internal layout of the help box right but breaks down if multiline.
                height += style.padding.horizontal + style.lineHeight;
            }

            // Always get a new control rect so we don't have to deal with positions and offsets.
            position = EditorGUILayout.GetControlRect(true, height, style);
            // + 1 maps our MessageType to Unity's.
            EditorGUI.HelpBox(position, _Message, (UnityEditor.MessageType)_MessageType + 1);

            // Revert skin since it persists.
            style.richText = styleRichText;
        }
    }
}
