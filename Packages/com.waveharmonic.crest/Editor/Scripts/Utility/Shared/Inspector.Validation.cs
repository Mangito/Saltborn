// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace WaveHarmonic.Crest.Editor
{
    partial class Inspector
    {
        static readonly bool s_GroupMessages = false;
        static GUIContent s_JumpButtonContent = null;
        static GUIContent s_FixButtonContent = null;

        protected virtual void RenderValidationMessages()
        {
            // Enable rich text in help boxes. Store original so we can revert since this might be a "hack".
            var styleRichText = GUI.skin.GetStyle("HelpBox").richText;
            GUI.skin.GetStyle("HelpBox").richText = true;

            // This is a static list so we need to clear it before use. Not sure if this will ever be a threaded
            // operation which would be an issue.
            foreach (var messages in ValidatedHelper.s_Messages)
            {
                messages.Clear();
            }

            ValidatedHelper.ExecuteValidators(target, ValidatedHelper.HelpBox);

            // We only want space before and after the list of help boxes. We don't want space between.
            var needsSpaceAbove = true;
            var needsSpaceBelow = false;

            // We loop through in reverse order so errors appears at the top.
            for (var messageTypeIndex = 0; messageTypeIndex < ValidatedHelper.s_Messages.Length; messageTypeIndex++)
            {
                var messages = ValidatedHelper.s_Messages[messageTypeIndex];

                if (messages.Count > 0)
                {
                    if (needsSpaceAbove)
                    {
                        // Double space looks good at top.
                        EditorGUILayout.Space();
                        // EditorGUILayout.Space();
                        needsSpaceAbove = false;
                    }

                    needsSpaceBelow = true;

                    // Map Validated.MessageType to HelpBox.MessageType.
                    var messageType = (MessageType)ValidatedHelper.s_Messages.Length - messageTypeIndex;

                    if (s_GroupMessages)
                    {
                        // We join the messages together to reduce vertical space since HelpBox has padding, borders etc.
                        var joinedMessage = messages[0]._Message;
                        // Format as list if we have more than one message.
                        if (messages.Count > 1) joinedMessage = $"- {joinedMessage}";

                        for (var messageIndex = 1; messageIndex < messages.Count; messageIndex++)
                        {
                            joinedMessage += $"\n- {messages[messageIndex]}";
                        }

                        EditorGUILayout.HelpBox(joinedMessage, messageType);
                    }
                    else
                    {
                        foreach (var message in messages)
                        {
                            EditorGUILayout.BeginHorizontal();

                            var fixDescription = message._FixDescription;
                            var originalGUIEnabled = GUI.enabled;

                            if (message._Action != null)
                            {
                                fixDescription += " Click the fix/repair button on the right to fix.";

                                if ((message._Action == ValidatedHelper.FixAddMissingMathPackage || message._Action == ValidatedHelper.FixAddMissingBurstPackage) && PackageManagerHelpers.IsBusy)
                                {
                                    GUI.enabled = false;
                                }
                            }

                            EditorGUILayout.HelpBox($"{message._Message} {fixDescription}", messageType);

                            // Jump to object button.
                            if (message._Object != null)
                            {
                                // Selection.activeObject can be message._object.gameObject instead of the component
                                // itself. We soft cast to MonoBehaviour to get the gameObject for comparison.
                                // Alternatively, we could always pass gameObject instead of "this".
                                var casted = message._Object as MonoBehaviour;

                                if (Selection.activeObject != message._Object && (casted == null || casted.gameObject != Selection.activeObject))
                                {
                                    s_JumpButtonContent ??= new(EditorGUIUtility.FindTexture("scenepicking_pickable_hover@2x"), "Jump to object to resolve issue");

                                    if (GUILayout.Button(s_JumpButtonContent, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true)))
                                    {
                                        Selection.activeObject = message._Object;
                                    }
                                }
                            }

                            // Fix the issue button.
                            if (message._Action != null)
                            {
                                s_FixButtonContent ??= new(EditorGUIUtility.FindTexture("SceneViewTools"));

                                if (message._FixDescription != null)
                                {
                                    var sanitisedFixDescr = Regex.Replace(message._FixDescription, @"<[^<>]*>", "'");
                                    s_FixButtonContent.tooltip = $"Apply fix: {sanitisedFixDescr}";
                                }
                                else
                                {
                                    s_FixButtonContent.tooltip = "Fix issue";
                                }

                                if (GUILayout.Button(s_FixButtonContent, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(true)))
                                {
                                    // Run fix function
                                    var serializedObject = new SerializedObject(message._Object);
                                    // Property is optional.
                                    var property = message._PropertyPath != null ? serializedObject?.FindProperty(message._PropertyPath) : null;
                                    var oldValue = property?.boxedValue;
                                    message._Action.Invoke(serializedObject, property);
                                    if (serializedObject.ApplyModifiedProperties())
                                    {
                                        // SerializedObject does this for us, but gives the history item a nicer label.
                                        Undo.RecordObject(message._Object, s_FixButtonContent.tooltip);
                                        DecoratedDrawer.OnChange(property, oldValue);
                                    }
                                }
                            }

                            GUI.enabled = originalGUIEnabled;

                            EditorGUILayout.EndHorizontal();
                        }
                    }
                }
            }

            if (needsSpaceBelow)
            {
                // EditorGUILayout.Space();
            }

            // Revert skin since it persists.
            GUI.skin.GetStyle("HelpBox").richText = styleRichText;
        }
    }
}
