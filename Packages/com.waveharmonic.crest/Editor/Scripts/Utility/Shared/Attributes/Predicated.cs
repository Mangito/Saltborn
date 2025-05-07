// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using WaveHarmonic.Crest.Attributes;
using WaveHarmonic.Crest.Editor;

namespace WaveHarmonic.Crest
{
    sealed class Predicated : Decorator
    {
        enum Mode
        {
            Property,
            Member,
            Type,
            RenderPipeline,
        }

        readonly Mode _Mode;

        readonly bool _Inverted;
        readonly bool _Hide;

        readonly string _PropertyName;
        readonly object _DisableValue;

        readonly Type _Type;
        readonly MemberInfo _Member;

        readonly RenderPipeline _RenderPipeline;

        /// <summary>
        /// The field with this attribute will be drawn enabled/disabled based on return of method.
        /// </summary>
        /// <param name="type">The type to call the method on. Must be either a static type or the type the field is defined on.</param>
        /// <param name="member">Member name. Method must match signature: bool MethodName(Component component). Can be any visibility and static or instance.</param>
        /// <param name="disableValue"></param>
        /// <param name="inverted">Flip behaviour - for example disable if a bool field is set to true (instead of false).</param>
        /// <param name="hide">Hide this component in the inspector.</param>
        public Predicated(Type type, string member, object disableValue, bool inverted = false, bool hide = false)
        {
            _Mode = Mode.Member;
            _Inverted = inverted;
            _Hide = hide;
            _Type = type;
            _DisableValue = disableValue;
            _Member = _Type.GetMember(member, Helpers.s_AnyMethod)[0];
        }

        /// <summary>
        /// Enable/Disable field depending on the current type of the component.
        /// </summary>
        /// <param name="type">If a component of this type is not attached to this GameObject, disable the GUI (or enable if inverted is true).</param>
        /// <param name="inverted">Flip behaviour - for example disable if a bool field is set to true (instead of false).</param>
        /// <param name="hide">Hide this component in the inspector.</param>
        public Predicated(Type type, bool inverted = false, bool hide = false)
        {
            _Mode = Mode.Type;
            _Inverted = inverted;
            _Hide = hide;
            _Type = type;
        }

        /// <summary>
        /// The field with this attribute will be drawn enabled/disabled based on another field. For example can be used
        /// to disable a field if a toggle is false.
        /// </summary>
        /// <param name="property">The name of the other property whose value dictates whether this field is enabled or not.</param>
        /// <param name="inverted">Flip behaviour - for example disable if a bool field is set to true (instead of false).</param>
        /// <param name="disableValue">If the field has this value, disable the GUI (or enable if inverted is true).</param>
        /// <param name="hide">Hide this component in the inspector.</param>
        public Predicated(string property, bool inverted = false, object disableValue = null, bool hide = false)
        {
            _Mode = Mode.Property;
            _Inverted = inverted;
            _Hide = hide;
            _PropertyName = property;
            _DisableValue = disableValue;
        }

        /// <summary>
        /// Field is predicated (enabled/shown) on which render pipeline is active.
        /// </summary>
        /// <param name="rp">Enable if this render pipeline is active.</param>
        /// <param name="inverted">Invert behaviour.</param>
        /// <param name="hide">Hide instead of disable.</param>
        public Predicated(RenderPipeline rp, bool inverted = false, bool hide = false)
        {
            _Mode = Mode.RenderPipeline;
            _Inverted = inverted;
            _Hide = hide;
            _RenderPipeline = rp;
        }

        public override bool AlwaysVisible => true;

        public bool GUIEnabled(SerializedProperty property)
        {
            return _Inverted != property.propertyType switch
            {
                // Enable GUI if int value of field is not equal to 0, or whatever the disable-value is set to
                SerializedPropertyType.Integer => property.intValue != ((int?)_DisableValue ?? 0),
                // Enable GUI if disable-value is 0 and field is true, or disable-value is not 0 and field is false
                SerializedPropertyType.Boolean => property.boolValue ^ (((int?)_DisableValue ?? 0) != 0),
                SerializedPropertyType.Float => property.floatValue != ((float?)_DisableValue ?? 0),
                SerializedPropertyType.String => property.stringValue != ((string)_DisableValue ?? ""),
                // Must pass nameof enum entry as we are comparing names because index != value.
                SerializedPropertyType.Enum => property.enumNames[property.enumValueIndex] != ((string)_DisableValue ?? ""),
                SerializedPropertyType.ObjectReference => property.objectReferenceValue != null,
                SerializedPropertyType.ManagedReference => property.managedReferenceValue != null,
                _ => throw new ArgumentException($"Crest.Predicated: property type on <i>{property.serializedObject.targetObject}</i> not implemented yet: {property.propertyType}."),
            };
        }

        internal override void Decorate(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            var enabled = true;

            if (_Mode == Mode.Property)
            {
                var propertyPath = _PropertyName;

                if (property.depth > 0)
                {
                    // Get the property path so we can find it from the serialized object.
                    propertyPath = $"{string.Join(".", property.propertyPath.Split(".", StringSplitOptions.None)[0..^1])}.{propertyPath}";
                }

                // Get the other property to be the predicate for the enabled/disabled state of this property.
                // Do not use property.FindPropertyRelative as it does not work with nested properties.
                // Try and find the nested property first and then fallback to the root object.
                var otherProperty = property.serializedObject.FindProperty(propertyPath) ?? property.serializedObject.FindProperty(_PropertyName);
                Debug.AssertFormat(otherProperty != null, "Crest.Predicated: {0} or {1} property on {2} ({3}) could not be found!", propertyPath, _PropertyName, property.serializedObject.targetObject.GetType(), property.name);

                if (otherProperty != null)
                {
                    enabled = GUIEnabled(otherProperty);
                }
            }
            else if (_Mode == Mode.Member)
            {
                // Static is both abstract and sealed: https://stackoverflow.com/a/1175950
                object @object = _Type.IsAbstract && _Type.IsSealed ? null : property.serializedObject.targetObject;

                // If this is a nested type, grab that type. This may not be suitable in all cases.
                if (property.depth > 0)
                {
                    // Get the property path so we can find it from the serialized object.
                    var propertyPath = string.Join(".", property.propertyPath.Split(".", StringSplitOptions.None)[0..^1]);

                    var otherProperty = property.serializedObject.FindProperty(propertyPath);

                    @object = otherProperty.propertyType switch
                    {
                        SerializedPropertyType.ManagedReference => otherProperty.managedReferenceValue,
                        SerializedPropertyType.Generic => otherProperty.boxedValue,
                        _ => @object,
                    };
                }

                if (_Member is PropertyInfo autoProperty)
                {
                    // == operator does not work.
                    enabled = autoProperty.GetValue(@object).Equals(_DisableValue);
                }
                else if (_Member is MethodInfo method)
                {
                    enabled = method.Invoke(@object, new object[] { }).Equals(_DisableValue);
                }

                if (_Inverted) enabled = !enabled;
            }
            else if (_Mode == Mode.Type)
            {
                var type = property.serializedObject.targetObject.GetType();

                // If this is a nested type, grab that type. This may not be suitable in all cases.
                if (property.depth > 0)
                {
                    // Get the property path so we can find it from the serialized object.
                    var propertyPath = string.Join(".", property.propertyPath.Split(".", StringSplitOptions.None)[0..^1]);

                    var otherProperty = property.serializedObject.FindProperty(propertyPath);

                    type = otherProperty.propertyType switch
                    {
                        SerializedPropertyType.ManagedReference => otherProperty.managedReferenceValue.GetType(),
                        SerializedPropertyType.Generic => otherProperty.boxedValue.GetType(),
                        _ => type,
                    };
                }

                var enabledByTypeCheck = _Type.IsAssignableFrom(type);
                if (_Inverted) enabledByTypeCheck = !enabledByTypeCheck;
                enabled = enabledByTypeCheck && enabled;
            }
            else if (_Mode == Mode.RenderPipeline)
            {
                enabled = RenderPipelineHelper.RenderPipeline == _RenderPipeline != _Inverted;
            }

            // Keep current disabled state.
            GUI.enabled &= enabled;

            // Keep previous hidden state.
            DecoratedDrawer.s_HideInInspector = DecoratedDrawer.s_HideInInspector || (_Hide && !enabled);
        }
    }
}
