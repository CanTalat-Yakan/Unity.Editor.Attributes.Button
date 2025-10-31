# Unity Essentials

This module is part of the Unity Essentials ecosystem and follows the same lightweight, editor-first approach.
Unity Essentials is a lightweight, modular set of editor utilities and helpers that streamline Unity development. It focuses on clean, dependency-free tools that work well together.

All utilities are under the `UnityEssentials` namespace.

```csharp
using UnityEssentials;
```

## Installation

Install the Unity Essentials entry package via Unity's Package Manager, then install modules from the Tools menu.

- Add the entry package (via Git URL)
  - Window → Package Manager
  - "+" → "Add package from git URL…"
  - Paste: `https://github.com/CanTalat-Yakan/UnityEssentials.git`

- Install or update Unity Essentials packages
  - Tools → Install & Update UnityEssentials
  - Install all or select individual modules; run again anytime to update

---

# Button Attribute

> Quick overview: Expose methods as clickable buttons in the Inspector, with optional labels, grouping, weight-based layout, custom height, parameters UI, and coroutine support (in Play Mode).

Add `[Button]` to public instance methods on a MonoBehaviour to render buttons directly in the Inspector. Parameterized methods get a small, inline parameters UI. You can group buttons horizontally and control their relative width and height.

![screenshot](Documentation/Screenshot.png)

## Features
- Inspector buttons for public instance methods on MonoBehaviours
- Optional label override: `[Button("Run Setup")]`
- Grouping and layout with weights:
  - `ButtonLayout.Begin` / `ButtonLayout.End` to start/end a horizontal group
  - `weight` to control relative width in a group
  - `height` to control button height
- Parameters UI for supported types: int, float, bool, string, enums, and UnityEngine.Object
- Default parameter values are respected when present
- Coroutine support: if the method returns `IEnumerator`, it’s started automatically (Play Mode)
- Keyboard activation when the control is focused

## Requirements
- Unity Editor 6000.0+ (Editor-only rendering; attribute lives in Runtime for convenience)
- Depends on the Unity Essentials Inspector Hooks module (provides the custom inspector integration)

Tip: Buttons appear only on MonoBehaviours using the built-in Inspector; no menu commands are added.

## Usage
Basic

```csharp
using UnityEngine;
using UnityEssentials;

public class Example : MonoBehaviour
{
    [Button]
    public void Ping()
    {
        Debug.Log("Ping");
    }
}
```

Custom label and height

```csharp
public class Example : MonoBehaviour
{
    [Button("Run Setup", height: 28)]
    public void Setup() { /* ... */ }
}
```

Grouping and weights

```csharp
public class Example : MonoBehaviour
{
    [Button(ButtonLayout.Begin, weight: 2)]
    public void Build() { /* ... */ }

    [Button(weight: 1)]
    public void Test() { /* ... */ }

    [Button(ButtonLayout.End, weight: 1)]
    public void Deploy() { /* ... */ }
}
```

Parameters and default values

```csharp
public class Example : MonoBehaviour
{
    [Button]
    public void Spawn(int count = 1, float radius = 5f, bool randomize = true) { /* ... */ }

    [Button]
    public void LoadAsset(Object asset) { /* ... */ }
}
```

Coroutines (Play Mode)

```csharp
using System.Collections;

public class Example : MonoBehaviour
{
    [Button]
    public IEnumerator FadeOut(float seconds = 1f)
    {
        // Runs as a coroutine while in Play Mode
        yield return new WaitForSeconds(seconds);
        // ...
    }
}
```

## How It Works
- A custom inspector (Inspector Hooks module) scans public instance methods declared on the component’s type
- Methods with `[Button]` are collected and rendered as buttons
- Grouping: methods marked with `ButtonLayout.Begin` start a row; a subsequent `ButtonLayout.End` ends it
- Weights: `weight` controls each button’s width share within the row; `height` sets pixel height
- Parameterized methods show an inline foldout with fields for supported parameter types
- When you click a parameterized button, the method is invoked using the current input values
- If a method returns `IEnumerator`, it’s started on a hidden helper MonoBehaviour so it runs in Play Mode

## Notes and Limitations
- Scope
  - Only public instance methods on MonoBehaviours are considered
  - Static, private/protected, or inherited (base class) methods are not shown
- Inheritance: only methods declared on the component’s concrete type are scanned
- Parameters UI supports: int, float, bool, string, enums, UnityEngine.Object
  - Other types default to null or a default value
- Coroutines run in Play Mode; in Edit Mode they won’t progress
- Multi-object editing: buttons execute against the currently inspected target object
- No runtime cost when not inspecting; this is an editor-only feature

## Files in This Package
- `Runtime/ButtonAttribute.cs` – The `[Button]` attribute and layout options
- `Editor/ButtonEditor.cs` – Inspector integration and rendering, invocation, parameters UI, coroutine helper
- `Runtime/UnityEssentials.ButtonAttribute.asmdef` – Runtime assembly definition
- `Editor/UnityEssentials.ButtonAttribute.Editor.asmdef` – Editor assembly definition (references Inspector Hooks)

## Tags
unity, unity-editor, attribute, inspector, button, ui, coroutine, parameters, layout, tools, workflow
