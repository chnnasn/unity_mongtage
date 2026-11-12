using UnityEditor;
using UnityEngine;

namespace ChnnAsn.Montage.Editor
{
    internal sealed partial class MontageEditorWindow
    {
        private const float SplitterSize = 6f;
        [SerializeField] private float previewPaneHeight = 250f;
        [SerializeField] private float detailsPanelWidth = 300f;
        [SerializeField] private bool showDetails = true;
        private int resizingSplitter;
        private Vector2 resizeOrigin;
        private float resizeStartValue;
        internal Rect PreviewPanelRect { get; private set; }
        internal Rect DetailsPanelRect { get; private set; }

        private void DrawResizableWorkspace()
        {
            var maximumHeight = Mathf.Max(90f, position.height - 310f);
            var height = Mathf.Clamp(previewPaneHeight, 70f, maximumHeight);
            var width = Mathf.Clamp(detailsPanelWidth, 190f, Mathf.Max(190f, position.width - 160f));
            Rect workspace;
            using (new EditorGUILayout.HorizontalScope(GUILayout.Height(height)))
            {
                PreviewPanelRect = GUILayoutUtility.GetRect(100f, height, GUILayout.ExpandWidth(true));
                DrawPreviewArea(PreviewPanelRect);
                workspace = PreviewPanelRect;
                if (montage != null && showDetails)
                {
                    var splitter = GUILayoutUtility.GetRect(SplitterSize, height, GUILayout.Width(SplitterSize));
                    DetailsPanelRect = DrawDetailsPanel(width, height);
                    workspace.xMax = DetailsPanelRect.xMax;
                    HandleSplitter(splitter, false, workspace);
                }
                else
                    DetailsPanelRect = default;
            }
            var horizontalSplitter = GUILayoutUtility.GetRect(100f, SplitterSize, GUILayout.ExpandWidth(true));
            HandleSplitter(horizontalSplitter, true, workspace);
        }

        private void HandleSplitter(Rect rect, bool horizontal, Rect workspace)
        {
            var control = GUIUtility.GetControlID(FocusType.Passive);
            var current = Event.current;
            EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));
            var grip = horizontal
                ? new Rect(rect.center.x - 18f, rect.y + 2f, 36f, 2f)
                : new Rect(rect.x + 2f, rect.center.y - 18f, 2f, 36f);
            EditorGUI.DrawRect(grip, new Color(0.5f, 0.5f, 0.5f));
            EditorGUIUtility.AddCursorRect(rect, horizontal ? MouseCursor.ResizeVertical : MouseCursor.ResizeHorizontal);
            if (current.type == EventType.MouseDown && current.button == 0 && rect.Contains(current.mousePosition))
            {
                GUIUtility.hotControl = control;
                resizingSplitter = control;
                resizeOrigin = current.mousePosition;
                resizeStartValue = horizontal ? workspace.height : workspace.xMax - rect.xMax;
                current.Use();
            }
            if (resizingSplitter != control || GUIUtility.hotControl != control)
                return;
            if (current.type == EventType.MouseDrag)
            {
                if (horizontal)
                    previewPaneHeight = Mathf.Clamp(resizeStartValue + current.mousePosition.y - resizeOrigin.y, 70f, Mathf.Max(90f, position.height - 310f));
                else
                    detailsPanelWidth = Mathf.Clamp(resizeStartValue - current.mousePosition.x + resizeOrigin.x, 190f, Mathf.Max(190f, workspace.width - 150f));
                current.Use();
                Repaint();
            }
            if (current.type == EventType.MouseUp)
            {
                resizingSplitter = 0;
                GUIUtility.hotControl = 0;
                current.Use();
                Repaint();
            }
        }
    }
}
