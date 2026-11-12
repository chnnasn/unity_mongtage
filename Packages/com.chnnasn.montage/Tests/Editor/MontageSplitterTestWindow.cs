using System.Collections.Generic;
using System.Reflection;
using ChnnAsn.Montage.Editor;
using UnityEditor;
using UnityEngine;

namespace ChnnAsn.Montage.Tests
{
    internal sealed class MontageSplitterTestWindow : EditorWindow
    {
        internal MontageEditorWindow Target;
        internal bool Horizontal;
        internal readonly List<string> InputTrace = new List<string>();
        private static readonly MethodInfo Splitter = typeof(MontageEditorWindow).GetMethod("HandleSplitter", BindingFlags.Instance | BindingFlags.NonPublic);

        private void OnGUI()
        {
            if (Target == null)
                return;
            var input = Event.current.type;
            var before = GUIUtility.hotControl;
            var rect = Horizontal ? new Rect(0f, 200f, 500f, 6f) : new Rect(194f, 0f, 6f, 200f);
            Splitter.Invoke(Target, new object[] { rect, Horizontal, new Rect(0f, 0f, 500f, 200f) });
            if (input == EventType.MouseDown || input == EventType.MouseDrag || input == EventType.MouseUp)
                InputTrace.Add($"{input} {Event.current.mousePosition} hot {before} -> {GUIUtility.hotControl} result {Event.current.type}");
        }
    }
}
