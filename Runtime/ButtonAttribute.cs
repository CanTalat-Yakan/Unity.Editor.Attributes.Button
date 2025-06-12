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

    /// <summary>
    /// Specifies that a method should be displayed as a button in a user interface, such as an editor or custom tool.
    /// </summary>
    /// <remarks>This attribute can be applied to methods to indicate that they should be represented as
    /// buttons in a UI. The appearance and behavior of the button can be customized using the provided properties, such
    /// as <see cref="Label"/>, <see cref="Layout"/>, <see cref="Weight"/>, and <see cref="Height"/>.</remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ButtonAttribute : Attribute
    {
        public string Label { get; set; }
        public ButtonLayout Layout { get; private set; }
        public int Weight { get; private set; }
        public int Height { get; private set; }

        public ButtonAttribute()
        {
            Label = null;
            Layout = ButtonLayout.None;
            Weight = 1;
            Height = 28;
        }

        public ButtonAttribute(string label = null, ButtonLayout layout = ButtonLayout.None, int weight = 1, int height = 18)
        {
            Label = label;
            Layout = layout;
            Weight = Mathf.Max(1, weight);
            Height = Mathf.Max(0, height);
        }

        public ButtonAttribute(ButtonLayout layout = ButtonLayout.None, int weight = 1, int height = 18)
        {
            Layout = layout;
            Weight = Mathf.Max(1, weight);
            Height = Mathf.Max(0, height);
        }

        public ButtonAttribute(int weight = 1, int height = 18)
        {
            Layout = ButtonLayout.None;
            Weight = Mathf.Max(1, weight);
            Height = Mathf.Max(0, height);
        }
    }
}