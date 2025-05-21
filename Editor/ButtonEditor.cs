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
    public class CoroutineHelper : MonoBehaviour { }

    public static class ButtonEditor
    {
        public readonly struct HorizontalGroup : IDisposable
        {
            readonly bool _isActive;

            public HorizontalGroup(bool shouldUse)
            {
                if (_isActive = shouldUse)
                    GUILayout.BeginHorizontal();
            }

            public void Dispose()
            {
                if (_isActive)
                    GUILayout.EndHorizontal();
            }
        }

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

        private static void OnInitialize()
        {
            _buttonGroups.Clear();

            if (InspectorHook.Target == null)
                return;

            InspectorHook.GetAllMethods(out var methods);

            var currentGroup = new List<(ButtonAttribute, MethodInfo)>();

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

        private static void OnProcessMethod(MethodInfo method)
        {
            foreach (var group in _buttonGroups)
                RenderButtonGroup(group, InspectorHook.Target);

            _buttonGroups.Clear();
        }

        private static void RenderButtonGroup(List<(ButtonAttribute Attribute, MethodInfo Method)> group, MonoBehaviour target)
        {
            if (group.Count == 0)
                return;

            EditorGUILayout.Space(8);

            bool useHorizontal = group.Count > 1 || group.Any(button => button.Attribute.Layout != ButtonLayout.None);
            using (new HorizontalGroup(useHorizontal))
            {
                float totalWeight = group.Sum(button => button.Attribute.Weight);
                foreach (var (attribute, method) in group)
                {
                    float width = (EditorGUIUtility.currentViewWidth - 30) * (attribute.Weight / totalWeight) - group.Count;
                    if (method.GetParameters().Length > 0)
                        DrawParameterButton(target, method, attribute, width);
                    else DrawSimpleButton(target, method, attribute, width);
                }
            }
        }

        private static void DrawSimpleButton(MonoBehaviour target, MethodInfo method, ButtonAttribute attribute, float width)
        {
            var buttonPosition = EditorGUILayout.GetControlRect(GUILayout.Width(width), GUILayout.Height(attribute.Height));
            var buttonClicked = GUI.Button(buttonPosition, attribute.Label);
            var keyboardClicked = InspectorFocusedHelper.ProcessKeyboardClick(buttonPosition);
            if (buttonClicked || keyboardClicked)
                InvokeMethod(target, method);
        }

        private static void DrawParameterButton(MonoBehaviour target, MethodInfo method, ButtonAttribute attribute, float width)
        {
            var key = (target, method);
            if (!_parameterStates.TryGetValue(key, out var state))
                _parameterStates[key] = state = CreateParameterState(method);

            EditorGUILayout.BeginVertical();
            {
                float buttonWidth = width + (width / 200);
                if (RenderButtonHeader(attribute, buttonWidth, state, out var isExpanded))
                    InvokeMethod(target, method, state.ParameterValues);

                if (state.IsExpanded)
                {
                    using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(buttonWidth)))
                    {
                        for (int i = 0; i < method.GetParameters().Length; i++)
                        {
                            // Create a rect with consistent width
                            Rect fieldRect = EditorGUILayout.GetControlRect(GUILayout.Width(buttonWidth - 8));
                            state.ParameterValues[i] = RenderParameterField(fieldRect, method.GetParameters()[i], state.ParameterValues[i]);
                        }
                    }
                }
            }
            EditorGUILayout.EndVertical();
        }

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

        private static bool RenderButtonHeader(ButtonAttribute attribute, float width, ParameterState state, out bool isExpanded)
        {
            var position = EditorGUILayout.GetControlRect(GUILayout.Width(width), GUILayout.Height(attribute.Height));

            var foldoutPosition = new Rect(position.x + 13, position.y, 16, EditorGUIUtility.singleLineHeight);
            state.IsExpanded = EditorGUI.Foldout(foldoutPosition, state.IsExpanded, GUIContent.none);

            var buttonPosition = new Rect(position.x + 16, position.y, position.width - 16, position.height);
            var buttonClicked = GUI.Button(buttonPosition, attribute.Label);
            var keyboardClicked = InspectorFocusedHelper.ProcessKeyboardClick(buttonPosition);
            isExpanded = buttonClicked || keyboardClicked;

            return isExpanded;
        }

        private static object RenderParameterField(Rect position, ParameterInfo param, object value)
        {
            var none = GUIContent.none;
            var label = ObjectNames.NicifyVariableName(param.Name);

            position.x += EditorGUI.indentLevel * 16 + 16;
            position.width -= EditorGUI.indentLevel * 16 + 16;

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