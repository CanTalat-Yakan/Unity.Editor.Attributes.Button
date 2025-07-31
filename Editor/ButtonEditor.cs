#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace UnityEssentials
{
    /// <summary>
    /// Provides utility methods for managing coroutines in Unity.
    /// </summary>
    /// <remarks>This class serves as a helper for coroutine-related operations and must be attached to a
    /// GameObject in the Unity scene to function properly, as it derives from <see cref="MonoBehaviour"/>.</remarks>
    public class CoroutineHelper : MonoBehaviour { }

    /// <summary>
    /// Provides utility methods and structures for rendering and managing custom buttons in the Unity Editor.
    /// </summary>
    /// <remarks>The <see cref="ButtonEditor"/> class is designed to facilitate the creation and management of
    /// custom buttons in the Unity Editor, particularly for methods decorated with the <see cref="ButtonAttribute"/>.
    /// It includes functionality for grouping buttons, handling method invocation, and managing parameterized
    /// buttons.</remarks>
    public static class ButtonEditor
    {
        /// <summary>
        /// Represents the state of a parameter, including its expansion status and associated values.
        /// </summary>
        /// <remarks>This class is used to store information about a parameter's current state, such as
        /// whether it is expanded and the values associated with it. It is intended for internal use only.</remarks>
        private class ParameterState
        {
            public bool IsExpanded;
            public object[] ParameterValues;
        }

        private static CoroutineHelper _coroutineHelper;
        private static readonly List<List<(ButtonAttribute Attribute, MethodInfo Method)>> _buttonGroups = new();
        private static readonly Dictionary<(MonoBehaviour, MethodInfo), ParameterState> _parameterStates = new();

        [InitializeOnLoadMethod]
        public static void Initialize()
        {
            InspectorHook.AddInitialization(OnInitialize);
            InspectorHook.AddProcessMethod(OnProcessMethod);
        }

        /// <summary>
        /// Initializes and organizes button groups based on methods decorated with the <see cref="ButtonAttribute"/>.
        /// </summary>
        /// <remarks>This method clears any existing button groups and processes all methods retrieved
        /// from the <see cref="InspectorHook.Target"/>. Methods with the <see cref="ButtonAttribute"/> are grouped
        /// according to their specified <see cref="ButtonLayout"/>. Groups are finalized when a method with a <see
        /// cref="ButtonLayout.End"/> attribute is encountered, or when all methods have been processed.</remarks>
        private static void OnInitialize()
        {
            _buttonGroups.Clear();

            if (InspectorHook.Target == null)
                return;

            var currentGroup = new List<(ButtonAttribute, MethodInfo)>();

            InspectorHook.GetAllMethods(InspectorHook.Target.GetType(), out var methods);
            foreach (var method in methods)
            {
                if (method.GetCustomAttribute<ButtonAttribute>() is not ButtonAttribute attribute) continue;

                attribute.Label ??= ObjectNames.NicifyVariableName(method.Name);

                if (attribute.Layout == ButtonLayout.Begin && currentGroup.Count > 0)
                {
                    _buttonGroups.Add(currentGroup);
                    currentGroup = new();
                }

                currentGroup.Add((attribute, method));

                if (attribute.Layout == ButtonLayout.End)
                {
                    _buttonGroups.Add(currentGroup);
                    currentGroup = new();
                }
            }

            if (currentGroup.Count > 0)
                _buttonGroups.Add(currentGroup);
        }

        /// <summary>
        /// Processes the specified method and renders all button groups associated with the current target.
        /// </summary>
        /// <remarks>This method iterates through all button groups, renders them for the current target,
        /// and then clears the button groups collection.</remarks>
        /// <param name="method">The <see cref="MethodInfo"/> instance representing the method to process.</param>
        private static void OnProcessMethod(MethodInfo method)
        {
            if (InspectorHook.IsMethodHandled(method))
                return;

            foreach (var group in _buttonGroups)
                RenderButtonGroup(group, InspectorHook.Target as MonoBehaviour);

            _buttonGroups.Clear();
        }

        /// <summary>
        /// Renders a group of buttons in the Unity Editor, each corresponding to a method decorated with a <see
        /// cref="ButtonAttribute"/>.
        /// </summary>
        /// <remarks>This method dynamically creates buttons in the Unity Editor based on the provided
        /// group of methods and their associated <see cref="ButtonAttribute"/> metadata. Buttons are laid out
        /// horizontally or vertically depending on the layout specified in the attributes. Buttons for methods with
        /// parameters will display a parameter input interface, while buttons for parameterless methods will invoke the
        /// method directly.</remarks>
        /// <param name="group">A list of tuples where each tuple contains a <see cref="ButtonAttribute"/> and the corresponding <see
        /// cref="MethodInfo"/> for the method to be invoked when the button is clicked. The list must not be null.</param>
        /// <param name="target">The <see cref="MonoBehaviour"/> instance on which the methods associated with the buttons will be invoked.
        /// This parameter must not be null.</param>
        private static void RenderButtonGroup(List<(ButtonAttribute Attribute, MethodInfo Method)> group, MonoBehaviour target)
        {
            if (group.Count == 0)
                return;

            EditorGUILayout.Space(8);

            bool useHorizontal = group.Count > 1 || group.Any(button => button.Attribute.Layout != ButtonLayout.None);
            bool single = group.Count == 1;
            GUILayout.BeginHorizontal();
            {
                bool isExpanded = false;
                float totalWeight = group.Sum(button => button.Attribute.Weight);
                for (int i = 0; i < group.Count; i++)
                {
                    var (attribute, method) = group[i];

                    float width = EditorGUIUtility.currentViewWidth - 22; // Full width minus margins
                    //width -= 13; // This is the margin for when the inspectors vertical scrollbar is visible
                    width *= attribute.Weight / totalWeight; // Weight-based width distribution
                    width += i - group.Count; // Group correction
                    width += single ? 1 : 0; // Single button correction

                    var offsetFoldout = i != 0;

                    // Wrap each button (and its parameter fields) in a vertical layout
                    GUILayout.BeginVertical(GUILayout.Width(width));
                    {
                        if (method.GetParameters().Length > 0)
                            DrawParameterButton(target, method, attribute, width, offsetFoldout, out isExpanded);
                        else DrawSimpleButton(target, method, attribute, width);
                    }
                    GUILayout.EndVertical();
                    GUILayout.Space(isExpanded && offsetFoldout ? -3 : 0);
                }
            }
            GUILayout.EndHorizontal();
        }

        private static void DrawSimpleButton(MonoBehaviour target, MethodInfo method, ButtonAttribute attribute, float width)
        {
            var buttonPosition = EditorGUILayout.GetControlRect(GUILayout.Width(width), GUILayout.Height(attribute.Height));
            bool buttonClicked = GUI.Button(buttonPosition, attribute.Label);
            bool keyboardClicked = InspectorFocusHelper.ProcessKeyboardClick(buttonPosition);

            if (buttonClicked || keyboardClicked)
                InvokeMethod(target, method);
        }

        private static GUIStyle _helpBox;
        private static GUIStyle HelpBox => _helpBox ??= new GUIStyle(EditorStyles.helpBox)
        {
            margin = new(20, 0, 0, 0),
            padding = new(0, 0, 0, 0),
        };

        private static void DrawParameterButton(MonoBehaviour target, MethodInfo method, ButtonAttribute attribute, float width, bool offsetFoldout, out bool isExpanded)
        {
            var key = (target, method);
            if (!_parameterStates.TryGetValue(key, out var state))
                _parameterStates[key] = state = CreateParameterState(method);

            float buttonWidth = width;
            if (RenderButtonHeader(attribute, buttonWidth, state, offsetFoldout, out isExpanded))
                InvokeMethod(target, method, state.ParameterValues);

            var scopestyle = offsetFoldout ? HelpBox : EditorStyles.helpBox;
            var layoutWidth = offsetFoldout ? width - 18 : width - 2;

            isExpanded = state.IsExpanded;
            if (isExpanded)
                using (new EditorGUILayout.VerticalScope(scopestyle, GUILayout.Width(layoutWidth)))
                    for (int i = 0; i < method.GetParameters().Length; i++)
                    {
                        var fieldPosition = EditorGUILayout.GetControlRect(GUILayout.Width(layoutWidth - 7));
                        state.ParameterValues[i] = RenderParameterField(fieldPosition, method.GetParameters()[i], state.ParameterValues[i]);
                    }
        }

        /// <summary>
        /// Creates a new <see cref="ParameterState"/> instance initialized with default values for the parameters of
        /// the specified method.
        /// </summary>
        /// <remarks>For parameters with default values, the default value is used. For value type
        /// parameters without default values, an instance of the value type is created. For reference type parameters
        /// without default values, <see langword="null"/> is used.</remarks>
        /// <param name="method">The <see cref="MethodInfo"/> representing the method whose parameters are used to create the state. Must not
        /// be <see langword="null"/>.</param>
        /// <returns>A <see cref="ParameterState"/> object containing default values for each parameter of the specified method.</returns>
        private static ParameterState CreateParameterState(MethodInfo method)
        {
            var parameters = method.GetParameters();
            return new ParameterState
            {
                ParameterValues = parameters.Select(parameter => parameter.HasDefaultValue
                    ? parameter.DefaultValue
                    : parameter.ParameterType.IsValueType
                        ? Activator.CreateInstance(parameter.ParameterType)
                        : null).ToArray()
            };
        }

        /// <summary>
        /// Renders a button with an optional foldout header in the Unity Editor and determines whether the button was
        /// clicked.
        /// </summary>
        /// <remarks>The foldout header can be used to toggle the visibility of additional
        /// controls.</remarks>
        /// <param name="attribute">The <see cref="ButtonAttribute"/> that defines the button's label and height.</param>
        /// <param name="width">The width of the button, in pixels.</param>
        /// <param name="state">The current state of the parameter, including whether the foldout is expanded.</param>
        /// <param name="offsetFoldout">A value indicating whether the foldout header should be offset to align with other controls.</param>
        /// <param name="isExpanded">When this method returns, contains <see langword="true"/> if the button was clicked or activated via
        /// keyboard; otherwise, <see langword="false"/>.</param>
        /// <returns><see langword="true"/> if the button was clicked or activated via keyboard; otherwise, <see
        /// langword="false"/>.</returns>
        private static bool RenderButtonHeader(ButtonAttribute attribute, float width, ParameterState state, bool offsetFoldout, out bool isExpanded)
        {
            var position = EditorGUILayout.GetControlRect(GUILayout.Width(width), GUILayout.Height(attribute.Height));

            var foldoutOffset = offsetFoldout ? 16f : 0f;
            var foldoutPosition = new Rect(position.x + foldoutOffset - 3, position.y, 16, EditorGUIUtility.singleLineHeight);
            state.IsExpanded = EditorGUI.Foldout(foldoutPosition, state.IsExpanded, GUIContent.none);

            var buttonPosition = new Rect(position.x + foldoutOffset, position.y, position.width - foldoutOffset, position.height);
            var buttonClicked = GUI.Button(buttonPosition, attribute.Label);
            var keyboardClicked = InspectorFocusHelper.ProcessKeyboardClick(buttonPosition);
            isExpanded = buttonClicked || keyboardClicked;

            return isExpanded;
        }

        /// <summary>
        /// Renders a field in the Unity Editor for editing the value of a parameter based on its type.
        /// </summary>
        /// <remarks>This method supports rendering fields for common types such as <see langword="int"/>,
        /// <see langword="float"/>, <see langword="bool"/>, <see langword="string"/>, enumerations, and Unity objects.
        /// For unsupported types, the method returns <see langword="null"/> or the default value for the parameter's
        /// type.</remarks>
        /// <param name="position">The screen position and size of the field to render.</param>
        /// <param name="param">The <see cref="ParameterInfo"/> describing the parameter, including its name and type.</param>
        /// <param name="value">The current value of the parameter to be displayed and edited.</param>
        /// <returns>The updated value of the parameter after user interaction. Returns the default value for the parameter's
        /// type if the type is unsupported or if no value is provided.</returns>
        private static object RenderParameterField(Rect position, ParameterInfo param, object value)
        {
            var none = GUIContent.none;
            var label = ObjectNames.NicifyVariableName(param.Name);

            position.x += EditorGUI.indentLevel * 16;
            position.width -= EditorGUI.indentLevel * 16;

            object result = null;
            switch (param.ParameterType)
            {
                case Type t when t == typeof(int):
                    result = EditorGUI.IntField(position, none, value as int? ?? default);
                    break;
                case Type t when t == typeof(float):
                    result = EditorGUI.FloatField(position, none, value as float? ?? default);
                    break;
                case Type t when t == typeof(bool):
                    position.x += 2;
                    result = EditorGUI.Toggle(position, none, value as bool? ?? false);
                    position.x += 24;
                    EditorGUI.LabelField(position, label);
                    break;
                case Type t when t == typeof(string):
                    result = EditorGUI.TextField(position, none, value as string ?? string.Empty);
                    break;
                case Type t when t.IsEnum:
                    result = EditorGUI.EnumPopup(position, none, value as Enum ?? (Enum)Activator.CreateInstance(t));
                    break;
                case Type t when typeof(UnityEngine.Object).IsAssignableFrom(t):
                    result = EditorGUI.ObjectField(position, none, value as UnityEngine.Object, t, true);
                    break;
                default:
                    result = default;
                    break;
            }
            return result;
        }

        /// <summary>
        /// Invokes the specified method on the given <see cref="MonoBehaviour"/> target with the provided parameters.
        /// </summary>
        /// <remarks>If the invoked method returns an <see cref="IEnumerator"/>, it will be started as a
        /// coroutine using a helper object. Any exceptions thrown during the method invocation are logged as
        /// errors.</remarks>
        /// <param name="target">The <see cref="MonoBehaviour"/> instance on which the method will be invoked. Cannot be <see
        /// langword="null"/>.</param>
        /// <param name="method">The <see cref="MethodInfo"/> representing the method to invoke. Cannot be <see langword="null"/>.</param>
        /// <param name="parameters">An array of parameters to pass to the method. Can be <see langword="null"/> if the method does not require
        /// parameters.</param>
        private static void InvokeMethod(MonoBehaviour target, MethodInfo method, params object[] parameters)
        {
            try
            {
                var result = method.Invoke(target, parameters);
                if (result is IEnumerator coroutine)
                    GetCoroutineHelper().StartCoroutine(coroutine);
            }
            catch (Exception ex) { Debug.LogError($"Error invoking {method.Name}: {ex.InnerException?.Message ?? ex.Message}"); }
        }

        /// <summary>
        /// Retrieves a singleton instance of the <see cref="CoroutineHelper"/> class.
        /// </summary>
        /// <remarks>This method ensures that a single instance of <see cref="CoroutineHelper"/> is
        /// created and reused. The instance is attached to a hidden <see cref="GameObject"/> to facilitate coroutine
        /// execution without requiring a MonoBehaviour in the user's code.</remarks>
        /// <returns>The singleton instance of the <see cref="CoroutineHelper"/> class.</returns>
        private static CoroutineHelper GetCoroutineHelper()
        {
            if (_coroutineHelper == null)
                _coroutineHelper = new GameObject("CoroutineHelper") { hideFlags = HideFlags.HideAndDontSave }
                    .AddComponent<CoroutineHelper>();

            return _coroutineHelper;
        }
    }
}
#endif