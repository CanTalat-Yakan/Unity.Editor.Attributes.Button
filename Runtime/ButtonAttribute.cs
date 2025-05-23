using System;
using UnityEngine;

namespace UnityEssentials
{
    /// <summary>
    /// Specifies the layout options for a button within a user interface.
    /// </summary>
    /// <remarks>This enumeration defines the possible positions or alignments for a button. Use <see
    /// cref="None"/> when no specific layout is required, <see cref="Begin"/>  to position the button at the start, and
    /// <see cref="End"/> to position it at the end.</remarks>
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
        public ButtonLayout Layout { get; }
        public int Weight { get; }
        public int Height { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ButtonAttribute"/> class,  allowing customization of a button's
        /// label, layout, weight, and height.
        /// </summary>
        /// <param name="label">The text to display on the button. If <see langword="null"/>, a default label may be used.</param>
        /// <param name="layout">The layout style of the button, specified as a <see cref="ButtonLayout"/> value.  Defaults to <see
        /// cref="ButtonLayout.None"/>.</param>
        /// <param name="weight">The relative weight of the button, used for layout purposes. Must be greater than or equal to 1.  Defaults
        /// to 1.</param>
        /// <param name="height">The height of the button in pixels. Must be greater than or equal to 0. Defaults to 18.</param>
        public ButtonAttribute(string label = null, ButtonLayout layout = ButtonLayout.None, int weight = 1, int height = 18)
        {
            Label = label;
            Layout = layout;
            Weight = Mathf.Max(1, weight);
            Height = Mathf.Max(0, height);
        }
    }
}