using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace ChnnAsn.Montage.Editor
{
    internal sealed partial class MontageEditorWindow
    {
        private enum SelectionKind { Asset, Slot, Segment, Section, Notify, Curve }

        private SelectionKind selectionKind;
        private int selectedTrackIndex = -1;
        private int selectedElementIndex = -1;
        private Vector2 detailsScroll;
        private float timelineZoom = 1f;
        private bool snapToFrames = true;
        private int frameRate = 30;
        private bool followSectionLinks;
        private bool draggingElement;
        private int dragEdge;
        private float dragMouseX;
        private float dragStart;
        private float dragEnd;
        private float dragClipIn;
        private float dragClipOut;
        private float dragDuration;
        private int dragUndoGroup;
        private int dragControl;
        private readonly List<string> authoringErrors = new List<string>();

        private float TimelineDuration => draggingElement ? dragDuration : Mathf.Max(1f, montage.Duration + 0.5f);
        private float TimelineWidth => Mathf.Max(400f, position.width - 24f) * timelineZoom;
        private float SnapTime(float time) => snapToFrames ? Mathf.Round(time * frameRate) / frameRate : time;

        [OnOpenAsset]
        private static bool OnOpenMontage(int instanceId, int line)
        {
            if (!(EditorUtility.InstanceIDToObject(instanceId) is MontageAsset asset))
                return false;
            Open(asset);
            return true;
        }

        [MenuItem("Assets/Create/Montage/From Selected Animation Clip", true)]
        private static bool CanCreateFromClip() => Selection.activeObject is AnimationClip clip && !clip.legacy;

        [MenuItem("Assets/Create/Montage/From Selected Animation Clip")]
        private static void CreateFromClip()
        {
            var clip = Selection.activeObject as AnimationClip;
            if (clip == null)
                return;
            var asset = CreateInstance<MontageAsset>();
            asset.name = clip.name + " Montage";
            MontageTimelineEditing.AddSlot(asset);
            MontageTimelineEditing.AddSegment(asset, 0, clip, 0f);
            ProjectWindowUtil.CreateAsset(asset, asset.name + ".asset");
            Open(asset);
        }

        private void DrawAuthoringToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("+ Slot", EditorStyles.toolbarButton, GUILayout.Width(56f)))
                {
                    selectedTrackIndex = MontageTimelineEditing.AddSlot(montage);
                    selectionKind = SelectionKind.Slot;
                    MarkEdited();
                }
                using (new EditorGUI.DisabledScope(montage.Duration <= 0f))
                {
                    if (GUILayout.Button("+ Section", EditorStyles.toolbarButton, GUILayout.Width(75f)))
                    {
                        selectedElementIndex = MontageTimelineEditing.AddSection(montage, SnapTime(currentTime));
                        selectionKind = SelectionKind.Section;
                        MarkEdited();
                    }
                    if (GUILayout.Button("+ Notify", EditorStyles.toolbarButton, GUILayout.Width(66f)))
                        AddNamedNotify(currentTime, false, 0);
                    if (GUILayout.Button("+ State", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                        AddNamedNotify(currentTime, true, 0);
                }
                if (GUILayout.Button("+ Curve", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                    AddCurve();
                if (GUILayout.Button("Settings", EditorStyles.toolbarButton, GUILayout.Width(65f)))
                    selectionKind = SelectionKind.Asset;
            }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                showDetails = GUILayout.Toggle(showDetails, "Details", EditorStyles.toolbarButton, GUILayout.Width(58f));
                if (GUILayout.Button("Reset Layout", EditorStyles.toolbarButton, GUILayout.Width(86f)))
                {
                    previewPaneHeight = 250f;
                    detailsPanelWidth = 300f;
                }
                GUILayout.FlexibleSpace();
                snapToFrames = GUILayout.Toggle(snapToFrames, "Snap", EditorStyles.toolbarButton, GUILayout.Width(46f));
                frameRate = Mathf.Clamp(EditorGUILayout.IntField(frameRate, GUILayout.Width(35f)), 1, 240);
                GUILayout.Label("fps", EditorStyles.miniLabel, GUILayout.Width(22f));
                GUILayout.Label("Zoom", EditorStyles.miniLabel, GUILayout.Width(34f));
                timelineZoom = GUILayout.HorizontalSlider(timelineZoom, 1f, 8f, GUILayout.Width(80f));
            }
        }

        private Rect DrawDetailsPanel(float width, float height)
        {
            using (var panel = new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Width(width), GUILayout.Height(height)))
            {
                detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll);
                var serialized = new SerializedObject(montage);
                var property = GetSelectedProperty(serialized);
                EditorGUILayout.LabelField(selectionKind.ToString(), EditorStyles.boldLabel);
                var labelWidth = EditorGUIUtility.labelWidth;
                EditorGUIUtility.labelWidth = Mathf.Clamp(detailsPanelWidth * 0.44f, 90f, 175f);
                EditorGUI.BeginChangeCheck();
                if (property == null)
                {
                    DrawProperty(serialized, "slotGroup");
                    DrawProperty(serialized, "defaultPlayRate");
                    DrawProperty(serialized, "blendIn");
                    DrawProperty(serialized, "blendInCurve");
                    DrawProperty(serialized, "blendOut");
                    DrawProperty(serialized, "blendOutCurve");
                    DrawProperty(serialized, "autoBlendOut");
                    DrawProperty(serialized, "blendOutTriggerTime");
                }
                else if (selectionKind == SelectionKind.Section)
                {
                    var nameProperty = property.FindPropertyRelative("name");
                    var renamed = EditorGUILayout.DelayedTextField("Name", nameProperty.stringValue);
                    if (renamed != nameProperty.stringValue)
                    {
                        MontageTimelineEditing.RenameSection(montage, selectedElementIndex, renamed);
                        serialized.Update();
                    }
                    EditorGUILayout.PropertyField(property.FindPropertyRelative("startTime"));
                    EditorGUILayout.PropertyField(property.FindPropertyRelative("endTime"));
                    DrawNextSectionPopup(property.FindPropertyRelative("nextSection"));
                    if (GUILayout.Button("Preview This Section"))
                    {
                        sectionIndex = selectedElementIndex + 1;
                        SetTime(montage.Sections[selectedElementIndex].StartTime);
                    }
                }
                else
                {
                    var iterator = property.Copy();
                    var end = iterator.GetEndProperty();
                    var enter = true;
                    while (iterator.NextVisible(enter) && !SerializedProperty.EqualContents(iterator, end))
                    {
                        enter = false;
                        if (selectionKind == SelectionKind.Slot && iterator.name == "segments")
                            continue;
                        EditorGUILayout.PropertyField(iterator, true);
                    }
                }
                if (EditorGUI.EndChangeCheck())
                {
                    serialized.ApplyModifiedProperties();
                    MarkEdited();
                }
                EditorGUIUtility.labelWidth = labelWidth;
                if (selectionKind == SelectionKind.Notify && selectedNotifyIndex >= 0 && selectedNotifyIndex < montage.Notifies.Count)
                {
                    var payload = montage.Notifies[selectedNotifyIndex].Asset;
                    if (payload != null && GUILayout.Button("Edit Notify Payload"))
                        Selection.activeObject = payload;
                }
                DrawSectionLinks();
                authoringErrors.Clear();
                montage.Validate(authoringErrors);
                foreach (var error in authoringErrors)
                    EditorGUILayout.HelpBox(error, MessageType.Warning);
                EditorGUILayout.EndScrollView();
                return panel.rect;
            }
        }

        private static void DrawProperty(SerializedObject serialized, string name) =>
            EditorGUILayout.PropertyField(serialized.FindProperty(name), true);

        private SerializedProperty GetSelectedProperty(SerializedObject serialized)
        {
            switch (selectionKind)
            {
                case SelectionKind.Slot:
                case SelectionKind.Segment:
                    if (selectedTrackIndex < 0 || selectedTrackIndex >= montage.SlotTracks.Count)
                        return null;
                    var track = serialized.FindProperty("slotTracks").GetArrayElementAtIndex(selectedTrackIndex);
                    if (selectionKind == SelectionKind.Slot)
                        return track;
                    var segments = track.FindPropertyRelative("segments");
                    return selectedElementIndex >= 0 && selectedElementIndex < segments.arraySize
                        ? segments.GetArrayElementAtIndex(selectedElementIndex) : null;
                case SelectionKind.Section:
                    return ArrayElement(serialized, "sections", selectedElementIndex);
                case SelectionKind.Notify:
                    return ArrayElement(serialized, "notifies", selectedNotifyIndex);
                case SelectionKind.Curve:
                    return ArrayElement(serialized, "curves", selectedElementIndex);
                default:
                    return null;
            }
        }

        private static SerializedProperty ArrayElement(SerializedObject serialized, string array, int index)
        {
            var property = serialized.FindProperty(array);
            return index >= 0 && index < property.arraySize ? property.GetArrayElementAtIndex(index) : null;
        }

        private void DrawSectionLinks()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Section Links", EditorStyles.boldLabel);
            followSectionLinks = EditorGUILayout.ToggleLeft("Follow links during entire-montage preview", followSectionLinks);
            var serialized = new SerializedObject(montage);
            var sections = serialized.FindProperty("sections");
            EditorGUI.BeginChangeCheck();
            for (var i = 0; i < sections.arraySize; i++)
            {
                var section = sections.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(montage.Sections[i].Name, EditorStyles.miniButton, GUILayout.Width(105f)))
                    {
                        selectionKind = SelectionKind.Section;
                        selectedElementIndex = i;
                        sectionIndex = 0;
                        SetTime(montage.Sections[i].StartTime);
                    }
                    GUILayout.Label("→", GUILayout.Width(16f));
                    DrawNextSectionPopup(section.FindPropertyRelative("nextSection"), false);
                }
            }
            if (EditorGUI.EndChangeCheck())
            {
                serialized.ApplyModifiedProperties();
                MarkEdited();
            }
        }

        private void DrawNextSectionPopup(SerializedProperty property, bool label = true)
        {
            var names = GetSectionNames();
            names[0] = "End";
            var current = 0;
            for (var i = 1; i < names.Length; i++)
                if (names[i] == property.stringValue)
                    current = i;
            var selected = label ? EditorGUILayout.Popup("Next Section", current, names) : EditorGUILayout.Popup(current, names);
            if (selected != current)
                property.stringValue = selected == 0 ? string.Empty : names[selected];
        }

        private void DrawSections(float duration)
        {
            var row = GUILayoutUtility.GetRect(TimelineWidth, RowHeight, GUILayout.Width(TimelineWidth));
            DrawRowBackground(row, 0);
            GUI.Label(new Rect(row.x + 4f, row.y + 4f, HeaderWidth - 8f, 20f), "Sections");
            var content = ContentRect(row);
            DrawTimeGrid(content);
            for (var i = 0; i < montage.Sections.Count; i++)
            {
                var section = montage.Sections[i];
                var rect = TimeRect(content, section.StartTime, section.EndTime, duration);
                DrawEditableBlock(rect, section.Name + (section.NextSection == section.Name ? " ↻" : string.Empty),
                    new Color(0.42f, 0.29f, 0.62f), selectionKind == SelectionKind.Section && selectedElementIndex == i);
                HandleElementInteraction(rect, content, duration, SelectionKind.Section, -1, i);
            }
            if (Event.current.type == EventType.ContextClick && content.Contains(Event.current.mousePosition))
            {
                var time = SnapTime((Event.current.mousePosition.x - content.x) / content.width * duration);
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("New Section"), false, () =>
                {
                    selectedElementIndex = MontageTimelineEditing.AddSection(montage, time);
                    selectionKind = SelectionKind.Section;
                    MarkEdited();
                });
                menu.ShowAsContext();
                Event.current.Use();
            }
            DrawPlayhead(row, duration);
        }

        private void DrawTrack(MontageSlotTrack track, float duration, int index)
        {
            if (track == null)
                return;
            var row = GUILayoutUtility.GetRect(TimelineWidth, RowHeight, GUILayout.Width(TimelineWidth));
            DrawRowBackground(row, index + 1);
            if (GUI.Button(new Rect(row.x + 2f, row.y + 2f, HeaderWidth - 4f, RowHeight - 4f), track.Slot, EditorStyles.miniButton))
            {
                selectionKind = SelectionKind.Slot;
                selectedTrackIndex = index;
            }
            var content = ContentRect(row);
            DrawTimeGrid(content);
            for (var i = 0; i < track.Segments.Count; i++)
            {
                var segment = track.Segments[i];
                if (segment == null)
                    continue;
                var rect = TimeRect(content, segment.StartTime, segment.EndTime, duration);
                DrawEditableBlock(rect, segment.Clip != null ? segment.Clip.name : "Missing Clip",
                    track.Additive ? new Color(0.48f, 0.35f, 0.65f) : new Color(0.24f, 0.58f, 0.40f),
                    selectionKind == SelectionKind.Segment && selectedTrackIndex == index && selectedElementIndex == i);
                HandleElementInteraction(rect, content, duration, SelectionKind.Segment, index, i);
            }
            if (track.Segments.Count == 0)
                GUI.Label(content, "Drag AnimationClip here", CenteredMiniLabel());
            HandleAnimationDrop(content, duration, index);
            DrawPlayhead(row, duration);
        }

        private void DrawNotifies(float duration)
        {
            var lanes = 1;
            foreach (var notify in montage.Notifies)
                if (notify != null && !(notify.Asset is MontageAudioNotifyAsset) && !(notify.Asset is MontageVfxNotifyAsset))
                    lanes = Mathf.Max(lanes, notify.Track + 1);
            for (var lane = 0; lane < lanes; lane++)
            {
                var row = GUILayoutUtility.GetRect(TimelineWidth, RowHeight, GUILayout.Width(TimelineWidth));
                DrawRowBackground(row, lane);
                GUI.Label(new Rect(row.x + 4f, row.y + 4f, HeaderWidth - 8f, 20f), "Notifies " + (lane + 1));
                var content = ContentRect(row);
                DrawTimeGrid(content);
                for (var i = 0; i < montage.Notifies.Count; i++)
                {
                    var notify = montage.Notifies[i];
                    if (notify == null || notify.Track != lane || notify.Asset is MontageAudioNotifyAsset || notify.Asset is MontageVfxNotifyAsset)
                        continue;
                    var rect = TimeRect(content, notify.Time, notify.EndTime, duration);
                    if (!notify.IsState)
                        rect.width = Mathf.Max(14f, Mathf.Min(90f, content.xMax - rect.x));
                    DrawEventBlock(rect, i, notify.Name, new Color(0.83f, 0.53f, 0.18f));
                    HandleNotifyInteraction(rect, content, duration, i);
                }
                HandleNotifyLane(content, duration, lane);
                DrawPlayhead(row, duration);
            }
        }

        private void HandleNotifyLane(Rect content, float duration, int lane)
        {
            var current = Event.current;
            if (!content.Contains(current.mousePosition))
                return;
            var time = SnapTime((current.mousePosition.x - content.x) / content.width * duration);
            if (current.type == EventType.ContextClick)
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Add Notify"), false, () => AddNamedNotify(time, false, lane));
                menu.AddItem(new GUIContent("Add Notify State"), false, () => AddNamedNotify(time, true, lane));
                menu.AddItem(new GUIContent("Add Notify on New Track"), false, () => AddNamedNotify(time, false, lane + 1));
                menu.ShowAsContext();
                current.Use();
            }
            if (current.type != EventType.DragUpdated && current.type != EventType.DragPerform)
                return;
            foreach (var reference in DragAndDrop.objectReferences)
            {
                if (!(reference is MontageNotifyAsset payload))
                    continue;
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (current.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    selectedNotifyIndex = MontageTimelineEditing.AddNotify(montage, time, false, lane, payload);
                    selectionKind = SelectionKind.Notify;
                    MarkEdited();
                }
                current.Use();
                break;
            }
        }

        private void DrawCurves(float duration)
        {
            for (var i = 0; i < montage.Curves.Count; i++)
            {
                var curve = montage.Curves[i];
                if (curve == null)
                    continue;
                var row = GUILayoutUtility.GetRect(TimelineWidth, 48f, GUILayout.Width(TimelineWidth));
                DrawRowBackground(row, i);
                if (GUI.Button(new Rect(row.x + 2f, row.y + 2f, HeaderWidth - 4f, 24f), curve.Name, EditorStyles.miniButton))
                {
                    selectionKind = SelectionKind.Curve;
                    selectedElementIndex = i;
                }
                var content = ContentRect(row);
                var serialized = new SerializedObject(montage);
                var property = serialized.FindProperty("curves").GetArrayElementAtIndex(i).FindPropertyRelative("curve");
                EditorGUI.BeginChangeCheck();
                var value = EditorGUI.CurveField(content, property.animationCurveValue, Color.cyan, new Rect(0f, 0f, duration, 1f));
                if (EditorGUI.EndChangeCheck())
                {
                    property.animationCurveValue = value;
                    serialized.ApplyModifiedProperties();
                    MarkEdited();
                }
                DrawPlayhead(row, duration);
            }
        }

        private static void DrawEditableBlock(Rect rect, string label, Color color, bool selected)
        {
            EditorGUI.DrawRect(rect, selected ? Color.Lerp(color, Color.white, 0.3f) : color);
            GUI.Label(new Rect(rect.x + 6f, rect.y, rect.width - 12f, rect.height), label, EditorStyles.miniLabel);
            EditorGUI.DrawRect(new Rect(rect.x, rect.y, 3f, rect.height), new Color(1f, 1f, 1f, 0.35f));
            EditorGUI.DrawRect(new Rect(rect.xMax - 3f, rect.y, 3f, rect.height), new Color(1f, 1f, 1f, 0.35f));
        }

        private void HandleAnimationDrop(Rect content, float duration, int trackIndex)
        {
            var current = Event.current;
            if (!content.Contains(current.mousePosition) || (current.type != EventType.DragUpdated && current.type != EventType.DragPerform))
                return;
            var clips = new List<AnimationClip>();
            foreach (var reference in DragAndDrop.objectReferences)
                if (reference is AnimationClip clip && !clip.legacy)
                    clips.Add(clip);
            if (clips.Count == 0)
                return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (current.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                Undo.IncrementCurrentGroup();
                var group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Add Montage Animations");
                var time = SnapTime((current.mousePosition.x - content.x) / content.width * duration);
                foreach (var clip in clips)
                {
                    selectedElementIndex = MontageTimelineEditing.AddSegment(montage, trackIndex, clip, time);
                    if (selectedElementIndex >= 0)
                        time = montage.SlotTracks[trackIndex].Segments[selectedElementIndex].EndTime;
                }
                selectedTrackIndex = trackIndex;
                selectionKind = SelectionKind.Segment;
                Undo.CollapseUndoOperations(group);
                MarkEdited();
            }
            current.Use();
        }

        private void AddNamedNotify(float time, bool state, int lane)
        {
            selectedNotifyIndex = MontageTimelineEditing.AddNotify(montage, SnapTime(time), state, lane);
            selectionKind = SelectionKind.Notify;
            MarkEdited();
        }

        private void AddCurve()
        {
            var serialized = new SerializedObject(montage);
            var curves = serialized.FindProperty("curves");
            selectedElementIndex = curves.arraySize++;
            var property = curves.GetArrayElementAtIndex(selectedElementIndex);
            var curveName = "Curve" + selectedElementIndex;
            var names = new HashSet<string>();
            foreach (var curve in montage.Curves)
                names.Add(curve.Name);
            while (names.Contains(curveName))
                curveName += "_Copy";
            property.FindPropertyRelative("name").stringValue = curveName;
            property.FindPropertyRelative("curve").animationCurveValue = AnimationCurve.Linear(0f, 0f, Mathf.Max(1f, montage.Duration), 1f);
            serialized.ApplyModifiedProperties();
            selectionKind = SelectionKind.Curve;
            MarkEdited();
        }

        private void MarkEdited()
        {
            EditorUtility.SetDirty(montage);
            pendingRebuild = !draggingElement;
            Repaint();
        }

        private void AdvanceSectionPreview(float delta)
        {
            var section = montage.FindSectionAtTime(currentTime);
            if (section == null)
            {
                currentTime = Mathf.Min(montage.Duration, currentTime + delta);
                isPlaying = currentTime < montage.Duration;
                return;
            }
            for (var guard = 0; guard < 32; guard++)
            {
                var untilEnd = section.EndTime - currentTime;
                if (delta < untilEnd)
                {
                    currentTime += delta;
                    return;
                }
                delta -= untilEnd;
                var next = montage.FindSection(section.NextSection);
                if (next == null)
                {
                    if (!loop)
                    {
                        currentTime = section.EndTime;
                        isPlaying = false;
                        return;
                    }
                    next = montage.FindSectionAtTime(0f);
                }
                if (next == null || next.EndTime <= next.StartTime)
                {
                    isPlaying = false;
                    return;
                }
                currentTime = next.StartTime;
                section = next;
            }
            isPlaying = false;
        }

        private void HandleElementInteraction(Rect rect, Rect content, float duration, SelectionKind kind, int track, int index)
        {
            var current = Event.current;
            var control = GUIUtility.GetControlID(FocusType.Passive);
            EditorGUIUtility.AddCursorRect(new Rect(rect.x, rect.y, 5f, rect.height), MouseCursor.ResizeHorizontal);
            EditorGUIUtility.AddCursorRect(new Rect(rect.xMax - 5f, rect.y, 5f, rect.height), MouseCursor.ResizeHorizontal);
            if ((current.type == EventType.MouseDown && current.button == 0 || current.type == EventType.ContextClick) && rect.Contains(current.mousePosition))
            {
                selectionKind = kind;
                selectedTrackIndex = track;
                selectedElementIndex = index;
                selectedNotifyIndex = kind == SelectionKind.Notify ? index : -1;
                isPlaying = false;
                if (current.type == EventType.ContextClick)
                {
                    ShowElementMenu(SnapTime((current.mousePosition.x - content.x) / content.width * duration));
                    current.Use();
                    return;
                }
                dragEdge = current.mousePosition.x < rect.x + 6f ? -1 : (current.mousePosition.x > rect.xMax - 6f ? 1 : 0);
                dragMouseX = current.mousePosition.x;
                dragDuration = duration;
                if (kind == SelectionKind.Segment)
                {
                    var segment = montage.SlotTracks[track].Segments[index];
                    dragStart = segment.StartTime;
                    dragEnd = segment.EndTime;
                    dragClipIn = segment.ClipIn;
                    dragClipOut = segment.ClipOut;
                }
                else if (kind == SelectionKind.Section)
                {
                    dragStart = montage.Sections[index].StartTime;
                    dragEnd = montage.Sections[index].EndTime;
                    dragEdge = -1;
                }
                else
                {
                    var notify = montage.Notifies[index];
                    dragStart = notify.Time;
                    dragEnd = notify.EndTime;
                    if (!notify.IsState)
                        dragEdge = 0;
                }
                Undo.IncrementCurrentGroup();
                dragUndoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Edit Montage " + kind);
                draggingElement = true;
                dragControl = control;
                GUIUtility.hotControl = control;
                current.Use();
                Repaint();
            }

            var isSelected = selectionKind == kind && selectedTrackIndex == track && selectedElementIndex == index;
            if (!draggingElement || !isSelected)
                return;
            if (current.type == EventType.MouseDrag && GUIUtility.hotControl == dragControl)
            {
                var delta = (current.mousePosition.x - dragMouseX) / content.width * dragDuration;
                ApplyElementDrag(delta);
                current.Use();
            }
            if (current.type == EventType.MouseUp)
            {
                if (kind == SelectionKind.Segment && dragEdge == 0)
                    ReorderDraggedSegment(SnapTime((current.mousePosition.x - content.x) / content.width * duration));
                draggingElement = false;
                GUIUtility.hotControl = 0;
                Undo.CollapseUndoOperations(dragUndoGroup);
                MarkEdited();
                current.Use();
            }
        }

        private void ApplyElementDrag(float delta)
        {
            var serialized = new SerializedObject(montage);
            var property = GetSelectedProperty(serialized);
            if (property == null)
                return;
            var minimum = 1f / frameRate;
            if (selectionKind == SelectionKind.Segment)
            {
                var segments = montage.SlotTracks[selectedTrackIndex].Segments;
                var segment = segments[selectedElementIndex];
                var previousEnd = selectedElementIndex > 0 ? segments[selectedElementIndex - 1].EndTime : 0f;
                var nextStart = selectedElementIndex + 1 < segments.Count ? segments[selectedElementIndex + 1].StartTime : float.MaxValue;
                var start = dragStart;
                var end = dragEnd;
                if (dragEdge == 0)
                {
                    start = Mathf.Clamp(SnapTime(dragStart + delta), previousEnd, Mathf.Max(previousEnd, nextStart - (dragEnd - dragStart)));
                    end = start + dragEnd - dragStart;
                }
                else if (dragEdge > 0)
                {
                    var sourceEnd = segment.Loop ? float.MaxValue : dragStart + (dragClipOut - dragClipIn) * segment.LoopCount / Mathf.Abs(segment.PlayRate);
                    end = Mathf.Clamp(SnapTime(dragEnd + delta), start + minimum, Mathf.Min(nextStart, sourceEnd));
                }
                else
                {
                    // Left trim changes the source boundary as well as its montage position.
                    start = Mathf.Clamp(SnapTime(dragStart + delta), Mathf.Max(previousEnd, dragStart), dragEnd - minimum);
                    var sourceDelta = (start - dragStart) * Mathf.Abs(segment.PlayRate);
                    if (segment.PlayRate > 0f)
                        property.FindPropertyRelative("clipIn").floatValue = Mathf.Min(dragClipOut - 0.001f, dragClipIn + sourceDelta);
                    else
                        property.FindPropertyRelative("clipOut").floatValue = Mathf.Max(dragClipIn + 0.001f, dragClipOut - sourceDelta);
                }
                property.FindPropertyRelative("startTime").floatValue = start;
                property.FindPropertyRelative("duration").floatValue = Mathf.Max(minimum, end - start);
            }
            else if (selectionKind == SelectionKind.Section)
            {
                if (Mathf.Approximately(dragStart, 0f))
                    return;
                var start = Mathf.Clamp(SnapTime(dragStart + delta), 0f, dragEnd - minimum);
                var sections = serialized.FindProperty("sections");
                for (var i = 0; i < montage.Sections.Count; i++)
                {
                    if (i == selectedElementIndex)
                        continue;
                    var previous = montage.Sections[i];
                    if (Mathf.Approximately(previous.EndTime, montage.Sections[selectedElementIndex].StartTime))
                    {
                        start = Mathf.Max(previous.StartTime + minimum, start);
                        sections.GetArrayElementAtIndex(i).FindPropertyRelative("endTime").floatValue = start;
                    }
                }
                property.FindPropertyRelative("startTime").floatValue = start;
            }
            else if (selectionKind == SelectionKind.Notify)
            {
                var start = dragStart;
                var end = dragEnd;
                if (dragEdge == 0)
                {
                    start = Mathf.Clamp(SnapTime(dragStart + delta), 0f, Mathf.Max(0f, montage.Duration - (dragEnd - dragStart)));
                    end = start + dragEnd - dragStart;
                }
                else if (dragEdge < 0)
                    start = Mathf.Clamp(SnapTime(dragStart + delta), 0f, dragEnd - minimum);
                else
                    end = Mathf.Clamp(SnapTime(dragEnd + delta), dragStart + minimum, montage.Duration);
                property.FindPropertyRelative("time").floatValue = start;
                property.FindPropertyRelative("duration").floatValue = Mathf.Max(0f, end - start);
            }
            serialized.ApplyModifiedProperties();
            MarkEdited();
        }

        private void ReorderDraggedSegment(float dropTime)
        {
            var segments = montage.SlotTracks[selectedTrackIndex].Segments;
            var target = selectedElementIndex;
            for (var i = 0; i < segments.Count; i++)
            {
                if (i < selectedElementIndex && dropTime < (segments[i].StartTime + segments[i].EndTime) * 0.5f)
                {
                    target = i;
                    break;
                }
                if (i > selectedElementIndex && dropTime > (segments[i].StartTime + segments[i].EndTime) * 0.5f)
                    target = i;
            }
            if (target != selectedElementIndex)
            {
                MontageTimelineEditing.ReorderSegment(montage, selectedTrackIndex, selectedElementIndex, target);
                selectedElementIndex = target;
            }
        }

        private void ShowElementMenu(float time)
        {
            var menu = new GenericMenu();
            if (selectionKind == SelectionKind.Segment)
            {
                var clip = montage.SlotTracks[selectedTrackIndex].Segments[selectedElementIndex].Clip;
                menu.AddItem(new GUIContent("Open Animation Clip"), false, () => AssetDatabase.OpenAsset(clip));
                menu.AddItem(new GUIContent("Duplicate Segment"), false, DuplicateSelected);
            }
            if (selectionKind == SelectionKind.Notify)
                menu.AddItem(new GUIContent("Duplicate Notify"), false, DuplicateSelected);
            if (selectionKind == SelectionKind.Section || selectionKind == SelectionKind.Segment)
                menu.AddItem(new GUIContent("New Section Here"), false, () =>
                {
                    selectedElementIndex = MontageTimelineEditing.AddSection(montage, time);
                    selectionKind = SelectionKind.Section;
                    MarkEdited();
                });
            menu.AddItem(new GUIContent("Delete " + selectionKind), false, DeleteSelected);
            menu.ShowAsContext();
        }

        private void DuplicateSelected()
        {
            var serialized = new SerializedObject(montage);
            if (selectionKind == SelectionKind.Segment)
            {
                var segments = MontageTimelineEditing.GetSegments(serialized, selectedTrackIndex);
                var segment = montage.SlotTracks[selectedTrackIndex].Segments[selectedElementIndex];
                segments.InsertArrayElementAtIndex(selectedElementIndex);
                selectedElementIndex++;
                for (var i = selectedElementIndex; i < segments.arraySize; i++)
                    segments.GetArrayElementAtIndex(i).FindPropertyRelative("startTime").floatValue += segment.Duration;
            }
            else if (selectionKind == SelectionKind.Notify)
            {
                var notifies = serialized.FindProperty("notifies");
                notifies.InsertArrayElementAtIndex(selectedNotifyIndex);
                selectedNotifyIndex++;
                selectedElementIndex = selectedNotifyIndex;
                notifies.GetArrayElementAtIndex(selectedNotifyIndex).FindPropertyRelative("id").stringValue = Guid.NewGuid().ToString("N");
            }
            serialized.ApplyModifiedProperties();
            MarkEdited();
        }

        private void HandleDeleteKey()
        {
            if (Event.current.type != EventType.KeyDown || EditorGUIUtility.editingTextField || GUIUtility.hotControl != 0)
                return;
            if (Event.current.keyCode == KeyCode.Delete || Event.current.keyCode == KeyCode.Backspace)
            {
                DeleteSelected();
                Event.current.Use();
            }
        }

        private void DeleteSelected()
        {
            var serialized = new SerializedObject(montage);
            if (GetSelectedProperty(serialized) == null)
                return;
            switch (selectionKind)
            {
                case SelectionKind.Notify:
                    RemoveNotify(selectedNotifyIndex);
                    break;
                case SelectionKind.Section:
                    MontageTimelineEditing.RemoveSection(montage, selectedElementIndex);
                    break;
                case SelectionKind.Segment:
                    MontageTimelineEditing.GetSegments(serialized, selectedTrackIndex).DeleteArrayElementAtIndex(selectedElementIndex);
                    serialized.ApplyModifiedProperties();
                    break;
                case SelectionKind.Slot:
                    serialized.FindProperty("slotTracks").DeleteArrayElementAtIndex(selectedTrackIndex);
                    serialized.ApplyModifiedProperties();
                    break;
                case SelectionKind.Curve:
                    serialized.FindProperty("curves").DeleteArrayElementAtIndex(selectedElementIndex);
                    serialized.ApplyModifiedProperties();
                    break;
            }
            selectionKind = SelectionKind.Asset;
            selectedNotifyIndex = -1;
            MarkEdited();
        }
    }
}
