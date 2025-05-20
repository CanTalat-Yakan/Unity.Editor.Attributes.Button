using System;
using UnityEngine;

namespace UnityEssentials
{
    public enum ButtonLayout
    {
        None,
        Begin,
        End
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ButtonAttribute : Attribute
    {

        public string Label { get; set; }
        public ButtonLayout Layout { get; }
        public int Weight { get; }
        public int Height { get; }

        public ButtonAttribute(string label = null, ButtonLayout layout = ButtonLayout.None, int weight = 1, int height = 18)
        {
            Label = label;
            Layout = layout;
            Weight = Mathf.Max(1, weight);
            Height = Mathf.Max(0, height);
        }
    }
}