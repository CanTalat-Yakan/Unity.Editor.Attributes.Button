#if UNITY_EDITOR
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
        public static CoroutineHelper s_coroutineHelper;
        private static List<List<(ButtonAttribute attribute, MethodInfo method)>> _buttonGroups = new();

        [InitializeOnLoadMethod]
        public static void Initialization()
        {
            InspectorHook.AddInitialization(OnInitialize);
            InspectorHook.AddProcessMethod(OnProcessMethod);
        }

        public static void OnInitialize()
        {
            var target = InspectorHook.Target;
            if (target == null)
                return;

            BuildGroupHierarchy(target);
        }

        public static void OnProcessMethod(MethodInfo method)
        {
            var target = InspectorHook.Target;
            if (target == null)
                return;

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
                if (attribute == null)
                    continue;

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
            if (group.Count == 0)
                return;

            int totalWeight = group.Sum(button => button.attribute.Weight);
            float width = EditorGUIUtility.currentViewWidth - 30;

            EditorGUILayout.Space(8);
            if (group.Count > 1)
                EditorGUILayout.BeginHorizontal();

            foreach (var (attribute, method) in group)
            {
                var options = new List<GUILayoutOption> { GUILayout.Width(width * (attribute.Weight / (float)totalWeight)) };
                if (attribute.Height > 0)
                    options.Add(GUILayout.Height(attribute.Height));

                if (GUILayout.Button(attribute.Label, options.ToArray()))
                    Invoke(script, method);
            }

            if (group.Count > 1)
                EditorGUILayout.EndHorizontal();
        }

        public static void Invoke(MonoBehaviour script, MethodInfo method)
        {
            if (method.ReturnType == typeof(IEnumerator))
                GetCoroutineHelper().StartCoroutine((IEnumerator)method.Invoke(script, null));
            else method.Invoke(script, null);
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