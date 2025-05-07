// Crest Water System
// Copyright © 2024 Wave Harmonic. All rights reserved.

// Adapted from:
// https://forum.unity.com/threads/drawing-a-field-using-multiple-property-drawers.479377/

// DecoratedProperty renders the field and Decorator decorates said field. The decorator changes the
// GUI state so that the decorated field receives that state. The DecoratedDrawer targets DecoratedProperty,
// calls Decorator.Decorate for each decorator and reverts GUI state.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEditor.Rendering;
using WaveHarmonic.Crest.Attributes;
using WaveHarmonic.Crest.Editor;
using UnityEditor.SceneManagement;

namespace WaveHarmonic.Crest.Attributes
{
    /// <summary>
    /// Renders a property field accommodating decorator properties.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    abstract class DecoratedProperty : PropertyAttribute
    {
        /// <summary>
        /// Override this method to customise the label.
        /// </summary>
        internal virtual GUIContent BuildLabel(GUIContent label) => label;

        /// <summary>
        /// Override this method to make your own IMGUI based GUI for the property.
        /// </summary>
        internal abstract void OnGUI(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer);

        /// <summary>
        /// A new control rectangle is required. Only override as false if the attribute needs to create it itself.
        /// See the embedded attribute as an example.
        /// </summary>
        internal virtual bool NeedsControlRectangle(SerializedProperty property) => true;
    }

    /// <summary>
    /// Decorates a decorator field by changing GUI state.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    abstract class Decorator : PropertyAttribute
    {
        public abstract bool AlwaysVisible { get; }

        /// <summary>
        /// Override this method to customise the label.
        /// </summary>
        internal virtual GUIContent BuildLabel(GUIContent label) => label;

        /// <summary>
        /// Override this method to additively change the appearance of a decorated field.
        /// </summary>
        internal virtual void Decorate(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {

        }

        internal virtual void DecorateAfter(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {

        }
    }

    /// <summary>
    /// An OnValidate replacement.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    abstract class Validator : PropertyAttribute
    {
        internal abstract void Validate(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer, object previous);
    }
}

namespace WaveHarmonic.Crest
{
    /// <summary>
    /// Renders the property using EditorGUI.PropertyField.
    /// </summary>
    sealed class DecoratedField : DecoratedProperty
    {
        public readonly bool _CustomFoldout;

        public DecoratedField(bool isCustomFoldout = false)
        {
            _CustomFoldout = isCustomFoldout;
        }

        internal override void OnGUI(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            var isCustomFoldout = _CustomFoldout && (property.propertyType == SerializedPropertyType.Generic || property.propertyType == SerializedPropertyType.ManagedReference);

            if (isCustomFoldout)
            {
                // Draw top border.
                var rect = position;
                rect.xMin = 0;
                rect.xMax = 100000;
                rect.height = 1;
                EditorGUI.DrawRect(rect, new(0.1f, 0.1f, 0.1f, 1f));

                // Draw background.
                var background = position;
                background.xMin = 0;
                background.xMax = 100000;
                background.yMin += 1;
                background.yMax = background.yMin + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing * 2;
                EditorGUI.DrawRect(background, new(0.1f, 0.1f, 0.1f, 0.2f));

                position.yMin += EditorGUIUtility.standardVerticalSpacing + 1;
                position.yMax -= EditorGUIUtility.standardVerticalSpacing + 1;
            }

            // FIXME: InvalidOperationException: Stack empty.
            // System.Collections.Generic.Stack`1[T].Pop()(at<e307bbb467104258887a104f6151f183>:0)
            EditorGUI.PropertyField(position, property, label, true);

            if (isCustomFoldout)
            {
                var background = position;
                background.xMin = 0;
                background.xMax = 100000;
                background.yMin += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                background.yMax = background.yMin + 1;
                EditorGUI.DrawRect(background, property.isExpanded ? new(0.1f, 0.1f, 0.1f, 0.5f) : new(0.1f, 0.1f, 0.1f, 1f));
                EditorGUILayout.Space(1);

                if (property.isExpanded)
                {
                    EditorGUILayout.Space(EditorGUIUtility.standardVerticalSpacing * 3);
                }
            }
        }
    }

    /// <summary>
    /// Renders foldout without the foldout.
    /// </summary>
    sealed class Stripped : DecoratedProperty
    {
        internal override bool NeedsControlRectangle(SerializedProperty property) => false;

        internal override void OnGUI(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            property.isExpanded = true;
            DecoratedDrawer.s_TemporaryColor = true;
            DecoratedDrawer.s_PreviousColor = GUI.color;

            GUI.color = new(0, 0, 0, 0);

            EditorGUI.indentLevel -= 1;
            EditorGUI.PropertyField(position, property, label, true);
            EditorGUI.indentLevel += 1;
        }
    }

    /// <summary>
    /// Renders the property using EditorGUI.Delayed*.
    /// </summary>
    sealed class Delayed : DecoratedProperty
    {
        internal override void OnGUI(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    EditorGUI.DelayedFloatField(position, property, label);
                    break;
                case SerializedPropertyType.Integer:
                    EditorGUI.DelayedIntField(position, property, label);
                    break;
                case SerializedPropertyType.String:
                    EditorGUI.DelayedTextField(position, property, label);
                    break;
                default:
                    EditorGUI.LabelField(position, label.text, "Delayed: must be float, integer, or string.");
                    break;
            }
        }
    }

    sealed class Group : Decorator
    {
        public enum Style
        {
            None,
            Foldout,
            Accordian,
        }

        readonly GUIContent _Title;
        readonly bool _IsEnd;
        readonly bool _CustomFoldout;

        string _IsExpandedKey;

        public static bool s_Foldout = false;

        public override bool AlwaysVisible => true;

        readonly Style _Style;

        /// <summary>
        /// Begins (and subsequently ends) a group of fields. They can be ordered and styled.
        /// </summary>
        /// <param name="title">Title of the group.</param>
        /// <param name="style">The appearance of the group.</param>
        /// <param name="isCustomFoldout">Pass "true" if this field is an object which would normally be a foldout but you want accordian styling. Also set this value on the DecoratedField attribute. Other parameters are ignored with this set.</param>
        public Group(string title = null, Style style = Style.Foldout, bool isCustomFoldout = false)
        {
            _IsEnd = title == null || style == Style.None;
            _Title = _IsEnd ? null : new(title);
            _CustomFoldout = isCustomFoldout;
            _Style = _IsEnd ? Style.None : style;
        }

        internal override void Decorate(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            if (DecoratedDrawer.s_IsFoldoutOpen)
            {
                --EditorGUI.indentLevel;

                if (!_IsEnd || _CustomFoldout)
                {
                    EditorGUILayout.GetControlRect(false, EditorGUIUtility.standardVerticalSpacing * 3);
                }
            }
            else if (_Style == Style.Accordian || _CustomFoldout)
            {
                // HACK: Otherwise anything groups following an embedded will be indented.
                EditorGUI.indentLevel = 0;
            }

            DecoratedDrawer.s_IsFoldout = DecoratedDrawer.s_IsFoldoutOpen = false;

            if (_CustomFoldout)
            {
                return;
            }

            if (_IsEnd)
            {
                EditorGUILayout.GetControlRect(false, EditorGUIUtility.standardVerticalSpacing * 3);
                return;
            }

            DecoratedDrawer.s_IsFoldout = true;

            Rect rect;
            Rect background;

            if (_Style == Style.Accordian)
            {
                // Draw top border.
                rect = EditorGUILayout.GetControlRect(GUILayout.Height(1));
                rect.xMin = 0;
                rect.xMax = 100000;
                rect.height = 1;
                EditorGUI.DrawRect(rect, new(0.1f, 0.1f, 0.1f, 1f));

                rect = EditorGUILayout.GetControlRect(true);

                // Draw background.
                background = rect;
                background.xMin = 0;
                background.xMax = 100000;
                background.yMin -= 2;
                background.yMax += 2;
                EditorGUI.DrawRect(background, new(0.1f, 0.1f, 0.1f, 0.2f));
            }
            else
            {
                rect = EditorGUILayout.GetControlRect();
                background = new Rect();
            }

            // Cannot use "property.isExpanded" as this will be used by nested built-in foldouts.
            _IsExpandedKey ??= $"{property.serializedObject.targetObject.GetType().FullName}.{property.propertyPath}";
            var isExpanded = SessionState.GetBool(_IsExpandedKey, false);

            isExpanded = EditorGUI.Foldout(rect, isExpanded, _Title, toggleOnLabelClick: true);
            DecoratedDrawer.s_IsFoldoutOpen = isExpanded;

            SessionState.SetBool(_IsExpandedKey, isExpanded);

            if (_Style != Style.Accordian)
            {
                EditorGUI.indentLevel++;
                return;
            }

            if (isExpanded)
            {
                // Draw bottom border (lighter when open).
                rect = EditorGUILayout.GetControlRect(GUILayout.Height(1));
                rect.xMin = 0;
                rect.xMax = 100000;
                rect.height = 1;
                EditorGUI.DrawRect(rect, new(0.1f, 0.1f, 0.1f, 0.5f));

                ++EditorGUI.indentLevel;

                EditorGUILayout.GetControlRect(false, EditorGUIUtility.standardVerticalSpacing * 2);
            }
            else
            {
                // Draw bottom border. This will have same position as top border for next foldout.
                background.yMax += 1;
                background.yMin = background.yMax - 1;
                EditorGUI.DrawRect(background, new(0.1f, 0.1f, 0.1f, 1f));
            }
        }
    }

    sealed class Stepped : DecoratedProperty
    {
        readonly int _Minimum;
        readonly int _Maximum;
        readonly int _Step;
        readonly bool _Power;

        public Stepped(int minimum, int maximum, int step = 1, bool power = false)
        {
            _Minimum = minimum;
            _Maximum = maximum;
            _Step = step;
            _Power = power;
        }

        internal override void OnGUI(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            if (property.propertyType == SerializedPropertyType.Integer)
            {
                property.intValue = EditorGUI.IntSlider(position, label, property.intValue, _Minimum, _Maximum);
                property.intValue = _Power
                    ? Mathf.ClosestPowerOfTwo(property.intValue)
                    : property.intValue / _Step * _Step;
                property.intValue = property.intValue;
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use Stepped with int.");
            }
        }
    }

    sealed class Minimum : Attributes.Validator
    {
        readonly float _Minimum;

        public Minimum(float minimum)
        {
            _Minimum = minimum;
        }

        internal override void Validate(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer, object previous)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    property.floatValue = Mathf.Max(_Minimum, property.floatValue);
                    break;
                case SerializedPropertyType.Integer:
                    property.floatValue = Mathf.Max((int)_Minimum, property.intValue);
                    break;
                case SerializedPropertyType.Vector2:
                    var vector2Value = property.vector2Value;
                    vector2Value.x = Mathf.Max(_Minimum, vector2Value.x);
                    vector2Value.y = Mathf.Max(_Minimum, vector2Value.y);
                    property.vector2Value = vector2Value;
                    break;
                case SerializedPropertyType.Vector2Int:
                    var vector2IntValue = property.vector2IntValue;
                    vector2IntValue.x = Mathf.Max((int)_Minimum, vector2IntValue.x);
                    vector2IntValue.y = Mathf.Max((int)_Minimum, vector2IntValue.y);
                    property.vector2Value = vector2IntValue;
                    break;
                default:
                    EditorGUI.LabelField(position, label.text, "Minimum: must be float, integer, or string.");
                    break;
            }
        }
    }

    sealed class Maximum : Attributes.Validator
    {
        readonly float _Maximum;

        public Maximum(float maximum)
        {
            _Maximum = maximum;
        }

        internal override void Validate(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer, object previous)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    property.floatValue = Mathf.Min(_Maximum, property.floatValue);
                    break;
                case SerializedPropertyType.Integer:
                    property.intValue = Mathf.Min((int)_Maximum, property.intValue);
                    break;
                case SerializedPropertyType.Vector2:
                    var vector2Value = property.vector2Value;
                    vector2Value.x = Mathf.Min(_Maximum, vector2Value.x);
                    vector2Value.y = Mathf.Min(_Maximum, vector2Value.y);
                    property.vector2Value = vector2Value;
                    break;
                case SerializedPropertyType.Vector2Int:
                    var vector2IntValue = property.vector2IntValue;
                    vector2IntValue.x = Mathf.Min((int)_Maximum, vector2IntValue.x);
                    vector2IntValue.y = Mathf.Min((int)_Maximum, vector2IntValue.y);
                    property.vector2IntValue = vector2IntValue;
                    break;
                default:
                    EditorGUI.LabelField(position, label.text, "Maximum: must be float, integer, or string.");
                    break;
            }
        }
    }

    /// <summary>
    /// Renders the property using EditorGUI.Slider.
    /// </summary>
    sealed class Range : DecoratedProperty
    {
        readonly float _Minimum;
        readonly float _Maximum;
        readonly float _PowerScale;
        readonly int _Step;
        readonly bool _Power;
        readonly bool _Delayed;
        readonly Clamp _Clamp;

        [Flags]
        public enum Clamp
        {
            None = 0,
            Minimum = 1,
            Maximum = 2,
            Both = Minimum | Maximum,
        }

        public Range(float minimum, float maximum, Clamp clamp = Clamp.Both, float scale = 1f, bool delayed = false, int step = 0, bool power = false)
        {
            _Minimum = minimum;
            _Maximum = maximum;
            _Step = step;
            _Power = power;
            _PowerScale = scale;
            _Clamp = clamp;
            _Delayed = delayed;
        }

        static readonly FieldInfo s_RecycledEditor = typeof(EditorGUI).GetField("s_RecycledEditor", BindingFlags.NonPublic | BindingFlags.Static);
        static readonly MethodInfo s_DoFloatField = typeof(EditorGUI).GetMethod("DoFloatField", BindingFlags.NonPublic | BindingFlags.Static, null, new Type[] { Assembly.GetAssembly(typeof(EditorGUI)).GetType("UnityEditor.EditorGUI+RecycledTextEditor"), typeof(Rect), typeof(Rect), typeof(int), typeof(float), typeof(string), typeof(GUIStyle), typeof(bool), typeof(float) }, null);
        static readonly object[] s_Arguments = new object[] { null, null, null, null, null, "g7", null, true, null };

        // For label dragging.
        float FloatField(Rect position, Rect dragHotZone, float value, float minimum, float maximum)
        {
            s_Arguments[0] = s_RecycledEditor.GetValue(null);
            s_Arguments[1] = position;
            s_Arguments[2] = dragHotZone;
            s_Arguments[3] = GUIUtility.GetControlID("EditorTextField".GetHashCode(), FocusType.Keyboard, position);
            s_Arguments[4] = value;
            s_Arguments[6] = EditorStyles.numberField;
            s_Arguments[8] = Math.Abs(maximum - minimum) / 100f * 0.03f;
            return (float)s_DoFloatField.Invoke(null, s_Arguments);
        }

        static MethodInfo s_PowerSliderMethod;

        internal static void PowerSlider(Rect position, SerializedProperty property, float minimum, float maximum, float power, GUIContent label)
        {
            if (s_PowerSliderMethod == null)
            {
                // Grab the internal PowerSlider method.
                s_PowerSliderMethod = typeof(EditorGUI).GetMethod
                (
                    name: "PowerSlider",
                    bindingAttr: BindingFlags.NonPublic | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(Rect), typeof(GUIContent), typeof(float), typeof(float), typeof(float), typeof(float) },
                    modifiers: null
                );
            }

            // Render slider and apply value to SerializedProperty.
            label = EditorGUI.BeginProperty(position, label, property);
            EditorGUI.BeginChangeCheck();
            var newValue = (float)s_PowerSliderMethod.Invoke(null, new object[] { position, label, property.floatValue, minimum, maximum, power });
            if (EditorGUI.EndChangeCheck())
            {
                property.floatValue = newValue;
            }
            EditorGUI.EndProperty();
        }

        internal override void OnGUI(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            var isVector = property.propertyType is SerializedPropertyType.Vector2 or SerializedPropertyType.Vector2Int;
            var isInteger = property.propertyType is SerializedPropertyType.Integer or SerializedPropertyType.Vector2Int;

            if (property.propertyType != SerializedPropertyType.Float && !isVector && !isInteger)
            {
                EditorGUI.LabelField(position, label.text, "Range: must be float, integer, or vector2.");
            }

            // Power provided so use PowerSlider.
            if (_PowerScale != 1f)
            {
                if (property.propertyType != SerializedPropertyType.Float)
                {
                    // We could fallback to Slider, but better to raise an issue.
                    EditorGUI.LabelField(position, label.text, "Range: must be float if power is provided.");
                    return;
                }

                PowerSlider(position, property, _Minimum, _Maximum, _PowerScale, label);
                return;
            }

            label = EditorGUI.BeginProperty(position, label, property);

            var dragHotZone = position;
            position = EditorGUI.PrefixLabel(position, label);
            dragHotZone.xMax = position.xMin;

            // Otherwise fields will have indentation.
            var indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            if (isVector)
            {
                var range = isInteger ? property.vector2IntValue : property.vector2Value;

                var xMax = position.xMax;
                position.width = EditorGUIUtility.fieldWidth;

                range.x = _Delayed
                    ? EditorGUI.DelayedFloatField(position, range.x)
                    : EditorGUI.FloatField(position, range.x);
                range.x = Mathf.Min(range.x, range.y);

                position.xMin = position.xMax + 6f;
                position.xMax = xMax - (EditorGUIUtility.fieldWidth + 6f);
                position.width = Mathf.Max(position.width, 19f);

                EditorGUI.MinMaxSlider(position, ref range.x, ref range.y, _Minimum, _Maximum);

                position.xMin = position.xMax + 5f;
                position.width = EditorGUIUtility.fieldWidth;

                range.y = _Delayed
                    ? EditorGUI.DelayedFloatField(position, range.y)
                    : EditorGUI.FloatField(position, range.y);
                range.y = Mathf.Max(range.x, range.y);

                if (_Clamp.HasFlag(Clamp.Minimum)) range.x = Mathf.Max(range.x, _Minimum);
                if (_Clamp.HasFlag(Clamp.Maximum)) range.y = Mathf.Min(range.y, _Maximum);

                if (isInteger) property.vector2IntValue = Vector2Int.RoundToInt(range);
                else property.vector2Value = range;
            }
            else
            {
                var range = isInteger ? property.intValue : property.floatValue;

                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    position.xMax -= EditorGUIUtility.fieldWidth + 6f;
                    position.width = Mathf.Max(position.width, 19f);

                    // If we go outside of the range then the thumb control will disappear.
                    var clamped = Mathf.Clamp(range, _Minimum, _Maximum);
                    clamped = GUI.HorizontalSlider(position, clamped, _Minimum, _Maximum);
                    if (check.changed) range = clamped;
                }

                position.xMin = position.xMax + 5f;
                position.width = EditorGUIUtility.fieldWidth;

                // There does not seem to be a functional difference with using integer vs float
                // fields since we handle rounding ourselves.
                range = _Delayed
                    ? EditorGUI.DelayedFloatField(position, range)
#if UNITY_6000_0_OR_NEWER
                    : EditorGUI.FloatField(position, range);
#else
                    : FloatField(position, dragHotZone, range, _Minimum, _Maximum);
#endif

                if (_Step > 0)
                {
                    var integer = Mathf.RoundToInt(range);
                    range = _Power ? Mathf.ClosestPowerOfTwo(integer) : integer / _Step * _Step;
                }

                if (_Clamp.HasFlag(Clamp.Minimum)) range = Mathf.Max(range, _Minimum);
                if (_Clamp.HasFlag(Clamp.Maximum)) range = Mathf.Min(range, _Maximum);

                if (isInteger) property.intValue = Mathf.RoundToInt(range);
                else property.floatValue = range;
            }

            EditorGUI.indentLevel = indent;
            EditorGUI.EndProperty();
        }
    }

    /// <summary>
    /// Allows an enum to render only a subset of options in subclasses.
    /// </summary>
    sealed class Filtered : DecoratedProperty
    {
        public enum Mode
        {
            Include,
            Exclude,
        }

        bool _Initialized;

        readonly bool _HasUnset = false;
        readonly int _Unset = -1;

        bool _Invalid;
        readonly List<int> _InvalidValues = new();

        public Filtered()
        {
        }

        public Filtered(int unset)
        {
            _Unset = unset;
            _HasUnset = true;
        }

        GUIContent[] _Labels;
        int[] _Values;
        bool _UnsetHidden = false;

        readonly GUIContent _InvalidLabel = new("Invalid");

        internal override void OnGUI(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            if (property.propertyType != SerializedPropertyType.Enum)
            {
                EditorGUI.LabelField(position, label.text, "Filtered: must be an enum.");
                return;
            }

            var attributes = property
                .GetDefiningBoxedObject()
                .GetType()
                .GetCustomAttributes<FilterEnum>(true)
                .Where(x => x._Property == property.name);

            if (attributes.Count() == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            Debug.AssertFormat(attributes.Count() == 1, "Crest: {0}.{1} has a subclass with too many DynamicEnumFilters",
                drawer.fieldInfo.FieldType, property.name);

            var attribute = attributes.First();
            var rebuild = false;
            rebuild |= !_Initialized;
            rebuild |= _HasUnset && _UnsetHidden != (property.intValue != _Unset);
            rebuild |= _Invalid != _InvalidValues.Contains(property.intValue);

            if (rebuild)
            {
                var labels = Enum.GetNames(drawer.fieldInfo.FieldType).Select(x => new GUIContent(x)).ToList();
                var values = ((int[])Enum.GetValues(drawer.fieldInfo.FieldType)).ToList();

                _Invalid = false;
                _UnsetHidden = false;

                // Filter enum entries.
                for (var i = 0; i < labels.Count; i++)
                {
                    // If this enum has an "unset" value, and "unset" is not the current value, filter it out.
                    if (_HasUnset && values[i] == _Unset && property.intValue != _Unset)
                    {
                        labels.RemoveAt(i);
                        values.RemoveAt(i);
                        i--;
                        _UnsetHidden = true;
                        continue;
                    }

                    if (attribute._Mode == Mode.Exclude && attribute._Values.Contains(values[i]) ||
                        attribute._Mode == Mode.Include && !attribute._Values.Contains(values[i]))
                    {
                        if (!_Initialized)
                        {
                            _InvalidValues.Add(values[i]);
                        }

                        if (property.intValue == values[i])
                        {
                            _Invalid = true;
                            labels[i] = _InvalidLabel;
                        }
                        else
                        {
                            labels.RemoveAt(i);
                            values.RemoveAt(i);
                            i--;
                        }
                    }
                }

                _Labels = labels.ToArray();
                _Values = values.ToArray();
                _Initialized = true;
            }

            property.intValue = EditorGUI.IntPopup(position, label, property.intValue, _Labels, _Values);
        }
    }

    /// <summary>
    /// Marks which enum options this subclass wants to use. Companion to Filtered.
    /// Usage: [FilterEnum("_mode", Filtered.Mode.Include, (int)Mode.One, (int)Mode.Two)]
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    sealed class FilterEnum : Attribute
    {
        public string _Property;
        public Filtered.Mode _Mode;
        internal int[] _Values;

        public FilterEnum(string property, Filtered.Mode mode, params int[] values)
        {
            _Mode = mode;
            _Values = values;
            _Property = property;
        }
    }

    sealed class ShowComputedProperty : Decorator
    {
        readonly string _PropertyName;
        PropertyInfo _PropertyInfo;
        object _Target;
        Array _EnumValues;

        public override bool AlwaysVisible => true;

        public ShowComputedProperty(string name)
        {
            _PropertyName = name;
        }

        internal override void DecorateAfter(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            if (!DecoratedDrawer.s_HideInInspector)
            {
                return;
            }

            // Do not execute for now as some components are not active in prefab stage.
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                return;
            }

            _Target ??= property.GetDefiningBoxedObject();
            _PropertyInfo ??= _Target.GetType().GetProperty(_PropertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);

            EditorGUI.BeginDisabledGroup(true);

            // Have to do this manually as PropertyField requires changing the property value
            // but will mess with OnChange check.
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    EditorGUILayout.FloatField(label, (float)_PropertyInfo.GetValue(_Target));
                    break;
                case SerializedPropertyType.Enum:
                    _EnumValues ??= Enum.GetValues(_PropertyInfo.PropertyType);
                    EditorGUILayout.Popup(label, Array.IndexOf(_EnumValues, _PropertyInfo.GetValue(_Target)), property.enumDisplayNames);
                    break;
            }

            EditorGUI.EndDisabledGroup();
        }
    }

    /// <summary>
    /// Manually provide a label (ie rename) for fields.
    /// </summary>
    sealed class Label : Decorator
    {

        readonly string _Label;

        public override bool AlwaysVisible => false;

        public Label(string label)
        {
            _Label = label;
        }

        internal override GUIContent BuildLabel(GUIContent label)
        {
            label = base.BuildLabel(label);
            label.text = _Label;
            return label;
        }

        internal override void Decorate(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            // Empty
        }
    }

    /// <summary>
    /// Drop-in replacement for Header. Use when hiding property with Predicated.
    /// </summary>
    sealed class Heading : Decorator
    {
        readonly GUIContent _Text;
        readonly string _HelpLink;
        readonly Style _Style;
        readonly bool _AlwaysVisible;
        readonly bool _AlwaysEnabled;

        public enum Style
        {
            Normal,
            Settings
        }

        public Heading(string heading, Style style = Style.Normal, bool alwaysVisible = false, bool alwaysEnabled = false, string helpLink = null)
        {
            _Text = EditorGUIUtility.TrTextContent(heading);
            _HelpLink = helpLink;
            _Style = style;
            _AlwaysVisible = alwaysVisible;
            _AlwaysEnabled = alwaysEnabled;
        }

        public override bool AlwaysVisible => _AlwaysVisible || _Style == Style.Settings;

        internal override void Decorate(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            var enabled = GUI.enabled;
            if (_AlwaysEnabled) GUI.enabled = true;

            switch (_Style)
            {
                case Style.Normal:
                    // Register margin with IMGUI so subsequent spacing is correct.
                    EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight * 0.5f);
                    GUI.Label(EditorGUI.IndentedRect(EditorGUILayout.GetControlRect(true)), _Text, EditorStyles.boldLabel);
                    break;
                case Style.Settings:
                    // Draws the section header found in SRP global settings files.
                    CoreEditorUtils.DrawSectionHeader(_Text, _HelpLink);
                    break;
            }

            GUI.enabled = enabled;
        }
    }

    /// <summary>
    /// Drop-in replacement for Space but supports our Decorator system.
    /// </summary>
    sealed class Space : Decorator
    {
        readonly float _Height;
        readonly bool _AlwaysVisible;

        public Space(float height, bool isAlwaysVisible = false)
        {
            _Height = height;
            _AlwaysVisible = isAlwaysVisible;
        }

        public override bool AlwaysVisible => _AlwaysVisible;

        internal override void Decorate(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            EditorGUILayout.GetControlRect(false, _Height);
        }
    }

    sealed class PrefabField : DecoratedProperty
    {
        readonly EditorHelpers.CreateInstance _CreateInstance;
        readonly string _Title;
        readonly string _DefaultName;

        public PrefabField(string title, string name)
        {
            _CreateInstance = x => x.serializedObject.GetDefaultReference(x.name);
            _DefaultName = name;
            _Title = title;
        }

        internal override void OnGUI(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            EditorHelpers.AssetField
            (
                typeof(GameObject),
                label,
                property,
                position,
                _Title,
                _DefaultName,
                "prefab",
                string.Empty,
                _CreateInstance
            );
        }
    }

    sealed class MaterialField : DecoratedProperty
    {
        readonly EditorHelpers.CreateInstance _CreateInstance;
        readonly string _Title;
        readonly string _DefaultName;
        readonly string _MaterialVariantPropertyName;

        public MaterialField(string shader, string title = "Create Material", string name = "Material", string parent = null)
        {
            _CreateInstance = x => new Material(Shader.Find(shader));
            _DefaultName = name;
            _Title = title;
            _MaterialVariantPropertyName = parent;
        }

        internal override void OnGUI(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            var old = property.objectReferenceValue;

            EditorHelpers.AssetField
            (
                typeof(Material),
                label,
                property,
                position,
                _Title,
                _DefaultName,
                "mat",
                string.Empty,
                _CreateInstance
            );

            // If we just created the material then parent.
            if (old != property.objectReferenceValue && _MaterialVariantPropertyName != null && property.objectReferenceValue != null)
            {
                var parent = (Material)property.serializedObject.FindProperty(_MaterialVariantPropertyName).objectReferenceValue;
                if (parent == null) return;
                var child = (Material)property.objectReferenceValue;
                child.parent = parent;
                // After parenting it will have overrides.
                child.RevertAllPropertyOverrides();
            }
        }
    }

    sealed class AttachMaterialEditor : Decorator
    {
        public override bool AlwaysVisible => true;

        internal override void Decorate(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            Inspector.Current._Materials.Add((Material)property.objectReferenceValue);
        }
    }

    sealed class Disabled : Decorator
    {
        public override bool AlwaysVisible => false;

        internal override void Decorate(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer)
        {
            GUI.enabled = false;
        }
    }

    sealed class WarnIfAbove : Attributes.Validator
    {
        readonly float _Maximum;

        public WarnIfAbove(float maximum)
        {
            _Maximum = maximum;
        }

        internal override void Validate(Rect position, SerializedProperty property, GUIContent label, DecoratedDrawer drawer, object previous)
        {
            var warn = false;

            if (previous == property.boxedValue)
            {
                return;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    var newValue = property.floatValue;
                    var oldValue = (float)previous;
                    warn = newValue > _Maximum && newValue > oldValue && oldValue <= _Maximum;
                    break;
                case SerializedPropertyType.Integer:
                    warn = property.intValue > _Maximum && property.intValue > (int)previous;
                    break;
                default:
                    EditorGUI.LabelField(position, label.text, "Maximum: must be float or integer.");
                    break;
            }

            if (warn)
            {
                var revert = EditorUtility.DisplayDialog
                (
                    "Warning!",
                    $"The entered value ({property.boxedValue}) is about to exceed the recommended maximum ({_Maximum}). " +
                    "A value this large could potentially freeze or even crash your computer.",
                    "Revert",
                    "Continue"
                );

                if (revert)
                {
                    property.boxedValue = previous;
                }
            }
        }
    }
}
