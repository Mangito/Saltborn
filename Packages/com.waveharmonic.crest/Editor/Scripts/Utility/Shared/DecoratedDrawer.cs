// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Adapted from:
// https://forum.unity.com/threads/drawing-a-field-using-multiple-property-drawers.479377/

// This class draws all the attributes which inherit from DecoratedProperty. This class may need to be
// extended to handle reseting GUI states as we need them.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using WaveHarmonic.Crest.Attributes;

namespace WaveHarmonic.Crest.Editor
{
    [CustomPropertyDrawer(typeof(DecoratedProperty), true)]
    sealed class DecoratedDrawer : PropertyDrawer
    {
        internal static bool s_HideInInspector = false;
        public static bool s_IsFoldout = false;
        public static bool s_IsFoldoutOpen = false;

        public static bool s_TemporaryColor;
        public static Color s_PreviousColor;

        List<object> _Decorators = null;
        List<object> Decorators
        {
            get
            {
                // Populate list with decorators.
                _Decorators ??= fieldInfo
                    .GetCustomAttributes(typeof(Decorator), false)
                    .ToList();

                return _Decorators;
            }
        }

        List<Attributes.Validator> _Validators = null;
        List<Attributes.Validator> Validators => _Validators ??= fieldInfo
            .GetCustomAttributes(typeof(Attributes.Validator), false)
            .Cast<Attributes.Validator>()
            .ToList();

        static List<(MethodInfo, OnChange)> s_OnChangeHandlers;
        static List<(MethodInfo, OnChange)> OnChangeHandlers => s_OnChangeHandlers ??= TypeCache
            .GetMethodsWithAttribute<OnChange>()
            .Select(x => (x, x.GetCustomAttribute<OnChange>()))
            .ToList();

        [InitializeOnLoadMethod]
        static void OnDomainReload()
        {
            s_OnChangeHandlers = null;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // Make original control rectangle be invisible because we always create our own. Zero still adds a little
            // height which becomes noticeable once multiple properties are hidden. This could be some GUI style
            // property but could not find which one.
            return -2f;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Store the original GUI state so it can be reset later.
            var originalColor = GUI.color;
            var originalEnabled = GUI.enabled;

            if (s_TemporaryColor) GUI.color = s_PreviousColor;

            // Execute all non visual attributes like Predicated. If these change any IMGUI
            // properties they might be overriden with EditorGUI.GetPropertyHeight call.
            for (var index = 0; index < Decorators.Count; index++)
            {
                var attribute = (Decorator)Decorators[index];
                if (!attribute.AlwaysVisible) continue;
                attribute.Decorate(position, property, label, this);
            }

            if (!s_HideInInspector && (!s_IsFoldout || s_IsFoldoutOpen))
            {
                // Execute all visual attributes.
                for (var index = 0; index < Decorators.Count; index++)
                {
                    var attribute = (Decorator)Decorators[index];
                    if (attribute.AlwaysVisible) continue;
                    label = attribute.BuildLabel(label);
                    attribute.Decorate(position, property, label, this);
                }

                var a = (DecoratedProperty)attribute;
                position = a.NeedsControlRectangle(property)
                    ? EditorGUILayout.GetControlRect(true, EditorGUI.GetPropertyHeight(property, label, true))
                    : position;

                // Call for labels again as EditorGUI.GetPropertyHeight will revert them.
                for (var index = 0; index < Decorators.Count; index++)
                {
                    var attribute = (Decorator)Decorators[index];
                    if (attribute.AlwaysVisible) continue;
                    label = attribute.BuildLabel(label);
                }

                var skipChange = fieldInfo.FieldType == typeof(UnityEvent);

                if (!skipChange)
                {
                    EditorGUI.BeginChangeCheck();
                }

                var oldValue = skipChange ? null : property.boxedValue;
                a.OnGUI(position, property, label, this);

                for (var index = 0; index < Validators.Count; index++)
                {
                    Validators[index].Validate(position, property, label, this, oldValue);
                }

                // Guard against foldouts triggering change check due to changing isExpanded.
                if (!skipChange && EditorGUI.EndChangeCheck() && oldValue != property.boxedValue)
                {
                    // Apply any changes.
                    property.serializedObject.ApplyModifiedProperties();

                    OnChange(property, oldValue);
                }

                for (var index = 0; index < Decorators.Count; index++)
                {
                    var attribute = (Decorator)Decorators[index];
                    if (attribute.AlwaysVisible) continue;
                    attribute.DecorateAfter(position, property, label, this);
                }
            }

            if (!s_IsFoldout || s_IsFoldoutOpen)
            {
                for (var index = 0; index < Decorators.Count; index++)
                {
                    var attribute = (Decorator)Decorators[index];
                    if (!attribute.AlwaysVisible) continue;
                    attribute.DecorateAfter(position, property, label, this);
                }
            }

            // Handle resetting the GUI state.
            s_HideInInspector = false;
            GUI.color = originalColor;
            GUI.enabled = originalEnabled;
        }

        public static void OnChange(SerializedProperty property, object oldValue)
        {
            var target = property.serializedObject.targetObject;
            // This is the type the field is declared on so it will work for nested objects.
            var targetType = target.GetType();

            var isActive = true;
            if (property.serializedObject.targetObject is Internal.EditorBehaviour c && !c.isActiveAndEnabled)
            {
                isActive = false;
            }

            // Send event to target.
            foreach (var (method, attribute) in OnChangeHandlers)
            {
                if (attribute.SkipIfInactive && !isActive) continue;
                var type = attribute.Type ?? method.DeclaringType;
                if (!type.IsAssignableFrom(targetType)) continue;

                if (attribute.Type == null)
                {
                    method.Invoke(target, new object[] { property.propertyPath, oldValue });
                }
                else
                {
                    method.Invoke(null, new object[] { target, property.propertyPath, oldValue, property });
                }
            }

            // Propagate event to nested classes.
            for (var i = 0; i < property.depth; i++)
            {
                var chunks = property.propertyPath.Split(".");
                var nestedProperty = property.serializedObject.FindProperty(string.Join(".", chunks[..(i + 1)]));
                if (nestedProperty.propertyType != SerializedPropertyType.ManagedReference) continue;
                var relativePath = string.Join(".", chunks[(i + 1)..]);

                var nestedTarget = nestedProperty.managedReferenceValue;
                var nestedTargetType = nestedTarget.GetType();

                foreach (var (method, attribute) in OnChangeHandlers)
                {
                    if (attribute.SkipIfInactive && !isActive) continue;
                    var type = attribute.Type ?? method.DeclaringType;
                    if (!type.IsAssignableFrom(nestedTargetType)) continue;

                    if (attribute.Type == null)
                    {
                        method.Invoke(nestedTarget, new object[] { relativePath, oldValue });
                    }
                    else
                    {
                        method.Invoke(null, new object[] { nestedTarget, relativePath, oldValue, property });
                    }
                }
            }
        }
    }
}
