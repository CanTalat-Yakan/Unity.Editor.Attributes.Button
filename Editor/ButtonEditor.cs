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
            InspectorHook.AddProcessMethod(OnMethodProcessed);
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

        private static void OnMethodProcessed(MethodInfo method)
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
                    float width = (EditorGUIUtility.currentViewWidth - 30) * (attribute.Weight / totalWeight) - group.Count * 1;
                    if (method.GetParameters().Length > 0)
                        DrawParameterButton(target, method, attribute, width);
                    else
                        DrawSimpleButton(target, method, attribute, width);
                }
            }
        }

        private static void DrawSimpleButton(MonoBehaviour target, MethodInfo method, ButtonAttribute attribute, float width)
        {
            var rect = EditorGUILayout.GetControlRect(GUILayout.Width(width + (width / 200)), GUILayout.Height(attribute.Height));
            if (GUI.Button(rect, attribute.Label))
                InvokeMethod(target, method);
        }

        private static void DrawParameterButton(MonoBehaviour target, MethodInfo method, ButtonAttribute attribute, float width)
        {
            var key = (target, method);
            if (!_parameterStates.TryGetValue(key, out var state))
                _parameterStates[key] = state = CreateParameterState(method);

            EditorGUILayout.BeginVertical();
            {
                if (RenderButtonHeader(attribute, width, state, out var isExpanded))
                    InvokeMethod(target, method, state.ParameterValues);

                if (state.IsExpanded)
                    RenderParameterFields(method.GetParameters(), state.ParameterValues);
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

        private static bool RenderButtonHeader(ButtonAttribute attribute, float width,
            ParameterState state, out bool isExpaned)
        {
            var rect = EditorGUILayout.GetControlRect(GUILayout.Width(width + (width / 200)), GUILayout.Height(attribute.Height));
            var foldoutRect = new Rect(rect.x + 13, rect.y, 16, EditorGUIUtility.singleLineHeight);
            state.IsExpanded = EditorGUI.Foldout(foldoutRect, state.IsExpanded, GUIContent.none);

            var buttonRect = new Rect(rect.x + 16, rect.y, rect.width - 16, rect.height);
            isExpaned = GUI.Button(buttonRect, attribute.Label);

            return isExpaned;
        }

        private static void RenderParameterFields(ParameterInfo[] parameters, object[] values)
        {
            EditorGUI.indentLevel++;
            using (new EditorGUILayout.VerticalScope("box"))
            {
                for (int i = 0; i < parameters.Length; i++)
                    values[i] = RenderParameterField(parameters[i], values[i]);
            }
            EditorGUI.indentLevel--;
        }

        private static object RenderParameterField(ParameterInfo param, object value)
        {
            var fieldRect = EditorGUILayout.GetControlRect();
            var label = ObjectNames.NicifyVariableName(param.Name);

            return param.ParameterType switch
            {
                Type t when t == typeof(int) => EditorGUI.IntField(fieldRect, label, value as int? ?? default),
                Type t when t == typeof(float) => EditorGUI.FloatField(fieldRect, label, value as float? ?? default),
                Type t when t == typeof(bool) => EditorGUI.Toggle(fieldRect, label, value as bool? ?? false),
                Type t when t == typeof(string) => EditorGUI.TextField(fieldRect, label, value as string ?? string.Empty),
                Type t when t.IsEnum => EditorGUI.EnumPopup(fieldRect, label, value as Enum ?? (Enum)Activator.CreateInstance(t)),
                Type t when typeof(UnityEngine.Object).IsAssignableFrom(t) => EditorGUI.ObjectField(fieldRect, label, value as UnityEngine.Object, t, true),
                _ => default
                //_ => EditorGUI.LabelField(fieldRect, label, $"Unsupported type: {param.ParameterType.Name}")
            };
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