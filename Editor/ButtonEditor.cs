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
        private class ParameterState
        {
            public bool IsExpanded;
            public object[] ParameterValues;
        }

        public static CoroutineHelper s_coroutineHelper;
        private static List<List<(ButtonAttribute attribute, MethodInfo method)>> _buttonGroups = new();
        private static Dictionary<(MonoBehaviour, MethodInfo), ParameterState> _parameterStates = new();

        [InitializeOnLoadMethod]
        public static void Initialization()
        {
            InspectorHook.AddInitialization(OnInitialize);
            InspectorHook.AddProcessMethod(OnProcessMethod);
        }

        public static void OnInitialize()
        {
            var target = InspectorHook.Target;
            if (target == null) return;
            BuildGroupHierarchy(target);
        }

        public static void OnProcessMethod(MethodInfo method)
        {
            var target = InspectorHook.Target;
            if (target == null) return;

            foreach (var group in _buttonGroups)
                DrawGroup(group, target);

            _buttonGroups.Clear();
        }

        public static void BuildGroupHierarchy(MonoBehaviour target)
        {
            _buttonGroups.Clear();
            var currentGroup = new List<(ButtonAttribute attribute, MethodInfo method)>();
            var methods = target.GetType().GetMethods();

            foreach (var method in methods)
            {
                var attribute = method.GetCustomAttributes(typeof(ButtonAttribute), true)
                                     .FirstOrDefault() as ButtonAttribute;
                if (attribute == null) continue;

                attribute.Label ??= method.Name;

                if (attribute.Layout == ButtonLayout.Begin && currentGroup.Count > 0)
                {
                    _buttonGroups.Add(new List<(ButtonAttribute, MethodInfo)>(currentGroup));
                    currentGroup.Clear();
                }

                currentGroup.Add((attribute, method));

                if (attribute.Layout == ButtonLayout.End)
                {
                    _buttonGroups.Add(new List<(ButtonAttribute, MethodInfo)>(currentGroup));
                    currentGroup.Clear();
                }
            }

            if (currentGroup.Count > 0)
                _buttonGroups.Add(new List<(ButtonAttribute, MethodInfo)>(currentGroup));
        }

        public static void DrawGroup(List<(ButtonAttribute attribute, MethodInfo method)> group, MonoBehaviour script)
        {
            if (group.Count == 0) return;

            float width = EditorGUIUtility.currentViewWidth - 30;
            int totalWeight = group.Sum(button => button.attribute.Weight);

            EditorGUILayout.Space(8);
            if (group.Count > 1) EditorGUILayout.BeginHorizontal();

            foreach (var (attribute, method) in group)
            {
                var buttonWidth = width * (attribute.Weight / (float)totalWeight);
                var buttonHeight = attribute.Height;

                var parameters = method.GetParameters();
                if (parameters.Length > 0)
                    DrawParameterizedButton(script, method, attribute, parameters, buttonWidth, buttonHeight);
                else
                    DrawSimpleButton(script, method, attribute, buttonWidth, buttonHeight);
            }

            if (group.Count > 1) EditorGUILayout.EndHorizontal();
        }

        private static void DrawParameterizedButton(MonoBehaviour script, MethodInfo method, ButtonAttribute attribute, ParameterInfo[] parameters, float buttonWidth, float buttonHeight)
        {
            var key = (script, method);
            if (!_parameterStates.TryGetValue(key, out var state))
            {
                state = new ParameterState
                {
                    IsExpanded = false,
                    ParameterValues = parameters.Select(p => p.HasDefaultValue ? p.DefaultValue :
                        (p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType) : null)).ToArray()
                };
                _parameterStates[key] = state;
            }

            EditorGUILayout.BeginVertical();

            // Get control rect with specified width and height using GUILayout
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight));
            float foldoutWidth = 16;
            float foldoutCorrection = 13;
            Rect foldoutRect = new Rect(rect.x + foldoutCorrection, rect.y, foldoutWidth, EditorGUIUtility.singleLineHeight);
            state.IsExpanded = EditorGUI.Foldout(foldoutRect, state.IsExpanded, GUIContent.none, true, EditorStyles.foldout);

            float buttonCorrection = rect.width / 180;
            Rect buttonRect = new Rect(rect.x + foldoutWidth, rect.y, rect.width - foldoutWidth + buttonCorrection + 1, rect.height);
            if (GUI.Button(buttonRect, attribute.Label))
                InvokeWithParameters(script, method, state.ParameterValues);

            // Draw parameters in indented box
            if (state.IsExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical("Box");
                DrawParameterFields(parameters, state.ParameterValues);
                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndVertical();
        }

        private static void DrawParameterFields(ParameterInfo[] parameters, object[] values)
        {
            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                var type = param.ParameterType;
                var label = new GUIContent(ObjectNames.NicifyVariableName(param.Name));

                try
                {
                    if (type == typeof(int))
                        values[i] = EditorGUILayout.IntField(label, (int)values[i]);
                    else if (type == typeof(float))
                        values[i] = EditorGUILayout.FloatField(label, (float)values[i]);
                    else if (type == typeof(bool))
                        values[i] = EditorGUILayout.Toggle(label, (bool)values[i]);
                    else if (type == typeof(string))
                        values[i] = EditorGUILayout.TextField(label, (string)values[i]);
                    else if (type.IsEnum)
                        values[i] = EditorGUILayout.EnumPopup(label, (Enum)values[i]);
                    else if (typeof(UnityEngine.Object).IsAssignableFrom(type))
                        values[i] = EditorGUILayout.ObjectField(label, (UnityEngine.Object)values[i], type, true);
                    else
                        EditorGUILayout.LabelField(label, $"Unsupported type: {type.Name}");
                }
                catch (Exception)
                {
                    values[i] = type.IsValueType ? Activator.CreateInstance(type) : null;
                }
            }
        }

        private static void DrawSimpleButton(MonoBehaviour script, MethodInfo method, ButtonAttribute attribute, float buttonWidth, float buttonHeight)
        {
            // Get control rect with specified width and height using GUILayout
            Rect rect = EditorGUILayout.GetControlRect(GUILayout.Width(buttonWidth), GUILayout.Height(buttonHeight));
            rect.height = buttonHeight;
            if (GUI.Button(rect, attribute.Label))
                Invoke(script, method);
        }

        public static void Invoke(MonoBehaviour script, MethodInfo method)
        {
            try
            {
                if (method.ReturnType == typeof(IEnumerator))
                    GetCoroutineHelper().StartCoroutine((IEnumerator)method.Invoke(script, null));
                else
                    method.Invoke(script, null);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error invoking method {method.Name}: {e}");
            }
        }

        private static void InvokeWithParameters(MonoBehaviour script, MethodInfo method, object[] parameters)
        {
            try
            {
                if (method.ReturnType == typeof(IEnumerator))
                    GetCoroutineHelper().StartCoroutine((IEnumerator)method.Invoke(script, parameters));
                else
                    method.Invoke(script, parameters);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error invoking method {method.Name}: {e}");
            }
        }

        public static CoroutineHelper GetCoroutineHelper()
        {
            if (s_coroutineHelper == null)
                s_coroutineHelper = new GameObject("CoroutineHelper") { hideFlags = HideFlags.HideAndDontSave }
                    .AddComponent<CoroutineHelper>();
            return s_coroutineHelper;
        }
    }
}
#endif