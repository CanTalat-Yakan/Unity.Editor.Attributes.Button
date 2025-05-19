#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityEssentials
{
    public class CoroutineHelper : MonoBehaviour { }

    public static class ButtonEditor
    {
        public static CoroutineHelper s_coroutineHelper;

        [InitializeOnLoadMethod]
        public static void Initialization()
        {
            InspectorHook.AddInitialization(OnInspectorGUI);
        }

        public static void OnInspectorGUI()
        {
            var target = InspectorHook.Target;
            if (target == null)
                return;

            var buttons = new List<(ButtonAttribute attribute, System.Reflection.MethodInfo method)>();
            var methods = target.GetType().GetMethods();

            foreach (var method in methods)
            {
                var attribute = method.GetCustomAttributes(typeof(ButtonAttribute), true)
                    .FirstOrDefault() as ButtonAttribute;

                if (attribute == null)
                    continue;

                if (string.IsNullOrEmpty(attribute.Label))
                    attribute.Label = method.Name;

                if (attribute.Layout == ButtonLayout.Begin && buttons.Count > 0)
                {
                    DrawGroup(buttons, target);
                    buttons.Clear();
                }

                buttons.Add((attribute, method));

                if (attribute.Layout == ButtonLayout.End)
                {
                    DrawGroup(buttons, target);
                    buttons.Clear();
                }
            }

            if (buttons.Count > 0)
                DrawGroup(buttons, target);
        }

        public static void DrawGroup(List<(ButtonAttribute attribute, System.Reflection.MethodInfo method)> group, MonoBehaviour script)
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

        public static void Invoke(MonoBehaviour script, System.Reflection.MethodInfo method)
        {
            if (method.ReturnType == typeof(IEnumerator))
            {
                if (s_coroutineHelper == null)
                {
                    var go = new GameObject("CoroutineHelper") { hideFlags = HideFlags.HideAndDontSave };
                    s_coroutineHelper = go.AddComponent<CoroutineHelper>();
                }
                s_coroutineHelper.StartCoroutine((IEnumerator)method.Invoke(script, null));
            }
            else method.Invoke(script, null);
        }
    }
}
#endif
