using System;
using UnityEditor;
using UnityEngine;

namespace ChnnAsn.Montage.Editor
{
    internal sealed class MontageEditorWindow : EditorWindow
    {
        private const float HeaderWidth = 140f;
        private const float RowHeight = 28f;
        private static readonly float[] PlaybackSpeeds = { 0.25f, 0.5f, 1f, 1.5f, 2f };
        private static readonly string[] PlaybackSpeedLabels = { "0.25x", "0.5x", "1x", "1.5x", "2x" };

        private MontageAsset montage;
        private GameObject previewCharacter;
        private PreviewRenderUtility previewUtility;
        private GameObject previewInstance;
        private MontagePreviewGraph previewGraph;
        private Vector2 timelineScroll;
        private Vector2 orbit = new Vector2(180f, 12f);
        private Vector3 cameraFocus;
        private float cameraDistance = 3f;
        private float currentTime;
        private int sectionIndex;
        private int speedIndex = 2;
        private bool includeBaseController;
        private bool isPlaying;
        private bool loop = true;
        private bool pendingRebuild;
        private double previousEditorTime;
        private string previewError;

        [MenuItem("Window/Animation/Unity Montage")]
        public static void OpenWindow()
        {
            GetWindow<MontageEditorWindow>("Montage");
        }

        public static void Open(MontageAsset asset)
        {
            var window = GetWindow<MontageEditorWindow>("Montage");
            window.SetMontage(asset);
            window.Repaint();
        }

        private void OnEnable()
        {
            minSize = new Vector2(560f, 520f);
            previousEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
            EditorApplication.projectChanged += OnProjectChanged;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.projectChanged -= OnProjectChanged;
            Undo.undoRedoPerformed -= OnUndoRedo;
            CleanupPreview();
        }

        private void OnGUI()
        {
            if (pendingRebuild)
            {
                pendingRebuild = false;
                RebuildPreview();
            }

            DrawAssetToolbar();
            DrawPreviewArea();
            DrawPlaybackToolbar();
            DrawTimeControls();

            if (montage == null)
            {
                EditorGUILayout.HelpBox("Select a MontageAsset to inspect and preview.", MessageType.Info);
                return;
            }

            timelineScroll = EditorGUILayout.BeginScrollView(timelineScroll);
            DrawRuler(montage.Duration);
            DrawSections(montage.Duration);
            for (var i = 0; i < montage.SlotTracks.Count; i++)
                DrawTrack(montage.SlotTracks[i], montage.Duration, i);
            DrawNotifies(montage.Duration);
            EditorGUILayout.EndScrollView();
        }

        private void DrawAssetToolbar()
        {
            EditorGUI.BeginChangeCheck();
            var newMontage = (MontageAsset)EditorGUILayout.ObjectField("Montage", montage, typeof(MontageAsset), false);
            if (EditorGUI.EndChangeCheck())
                SetMontage(newMontage);

            EditorGUI.BeginChangeCheck();
            var newCharacter = (GameObject)EditorGUILayout.ObjectField(
                "Preview Character", previewCharacter, typeof(GameObject), false);
            if (EditorGUI.EndChangeCheck())
            {
                previewCharacter = newCharacter;
                RebuildPreview();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                includeBaseController = EditorGUILayout.ToggleLeft(
                    "Include Animator Controller base pose", includeBaseController, GUILayout.Width(230f));
                if (EditorGUI.EndChangeCheck())
                    RebuildPreview();

                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(montage == null || previewCharacter == null))
                {
                    if (GUILayout.Button("Rebuild Preview", GUILayout.Width(120f)))
                        RebuildPreview();
                }
            }
        }

        private void DrawPreviewArea()
        {
            var height = Mathf.Clamp(position.height * 0.44f, 220f, 460f);
            var rect = GUILayoutUtility.GetRect(100f, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.11f, 0.11f, 0.11f));

            if (previewUtility == null || previewInstance == null)
            {
                var message = !string.IsNullOrEmpty(previewError)
                    ? previewError
                    : "Assign a character Prefab or model to start previewing.";
                GUI.Label(rect, message, CenteredWrappedLabel());
                return;
            }

            HandlePreviewInput(rect);
            UpdatePreviewCamera(rect);
            previewUtility.BeginPreview(rect, GUIStyle.none);
            previewUtility.Render(true);
            var texture = previewUtility.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
            GUI.Box(rect, GUIContent.none);

            var hintRect = new Rect(rect.x + 8f, rect.yMax - 24f, rect.width - 16f, 18f);
            GUI.Label(hintRect, "LMB Orbit   MMB Pan   Wheel Zoom   F Frame", EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(previewError))
                GUI.Label(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 36f), previewError, EditorStyles.helpBox);
        }

        private void DrawPlaybackToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            using (new EditorGUI.DisabledScope(previewGraph == null || montage == null || montage.Duration <= 0f))
            {
                if (GUILayout.Button(isPlaying ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(55f)))
                {
                    isPlaying = !isPlaying;
                    if (isPlaying)
                    {
                        GetPlaybackRange(out var start, out var end);
                        if (currentTime < start || currentTime >= end)
                            SetTime(start);
                        previousEditorTime = EditorApplication.timeSinceStartup;
                    }
                }
                if (GUILayout.Button("Stop", EditorStyles.toolbarButton, GUILayout.Width(48f)))
                {
                    isPlaying = false;
                    GetPlaybackRange(out var start, out _);
                    SetTime(start);
                }
                if (GUILayout.Button("-1f", EditorStyles.toolbarButton, GUILayout.Width(38f)))
                {
                    isPlaying = false;
                    SetTime(currentTime - 1f / 30f);
                }
                if (GUILayout.Button("+1f", EditorStyles.toolbarButton, GUILayout.Width(38f)))
                {
                    isPlaying = false;
                    SetTime(currentTime + 1f / 30f);
                }

                GUILayout.Space(8f);
                loop = GUILayout.Toggle(loop, "Loop", EditorStyles.toolbarButton, GUILayout.Width(45f));
                GUILayout.FlexibleSpace();
                GUILayout.Label("Speed", EditorStyles.miniLabel);
                speedIndex = EditorGUILayout.Popup(speedIndex, PlaybackSpeedLabels, EditorStyles.toolbarPopup, GUILayout.Width(64f));
            }
        }

        private void DrawTimeControls()
        {
            if (montage == null)
                return;

            var sectionNames = GetSectionNames();
            EditorGUI.BeginChangeCheck();
            sectionIndex = EditorGUILayout.Popup("Preview Range", Mathf.Clamp(sectionIndex, 0, sectionNames.Length - 1), sectionNames);
            if (EditorGUI.EndChangeCheck())
            {
                isPlaying = false;
                GetPlaybackRange(out var start, out _);
                SetTime(start);
            }

            GetPlaybackRange(out var rangeStart, out var rangeEnd);
            EditorGUI.BeginChangeCheck();
            var sampledTime = EditorGUILayout.Slider(
                $"Time  {currentTime:0.000}s", currentTime, rangeStart, Mathf.Max(rangeStart, rangeEnd));
            if (EditorGUI.EndChangeCheck())
            {
                isPlaying = false;
                SetTime(sampledTime);
            }
        }

        private void OnEditorUpdate()
        {
            var now = EditorApplication.timeSinceStartup;
            var delta = Math.Min(0.1d, Math.Max(0d, now - previousEditorTime));
            previousEditorTime = now;
            if (!isPlaying || previewGraph == null || montage == null)
                return;

            GetPlaybackRange(out var start, out var end);
            if (end <= start)
            {
                isPlaying = false;
                return;
            }

            var scaledDelta = (float)delta * PlaybackSpeeds[Mathf.Clamp(speedIndex, 0, PlaybackSpeeds.Length - 1)];
            var nextTime = currentTime + scaledDelta;
            if (nextTime >= end)
            {
                if (loop)
                    nextTime = start + (nextTime - start) % (end - start);
                else
                {
                    nextTime = end;
                    isPlaying = false;
                }
            }

            currentTime = nextTime;
            previewGraph.Sample(currentTime, scaledDelta);
            Repaint();
        }

        private void SetMontage(MontageAsset asset)
        {
            montage = asset;
            sectionIndex = 0;
            currentTime = 0f;
            isPlaying = false;
            RebuildPreview();
        }

        private void SetTime(float time)
        {
            GetPlaybackRange(out var start, out var end);
            currentTime = Mathf.Clamp(time, start, Mathf.Max(start, end));
            previewGraph?.Sample(currentTime, 0f);
            Repaint();
        }

        private void RebuildPreview()
        {
            CleanupPreview();
            previewError = null;
            if (montage == null || previewCharacter == null)
                return;

            try
            {
                previewUtility = new PreviewRenderUtility();
                previewUtility.cameraFieldOfView = 30f;
                previewUtility.ambientColor = new Color(0.32f, 0.32f, 0.32f);
                previewUtility.lights[0].intensity = 1.15f;
                previewUtility.lights[0].transform.rotation = Quaternion.Euler(35f, 35f, 0f);
                previewUtility.lights[1].intensity = 0.65f;
                previewUtility.lights[1].transform.rotation = Quaternion.Euler(320f, 210f, 0f);

                previewInstance = previewUtility.InstantiatePrefabInScene(previewCharacter);
                previewInstance.name = $"{previewCharacter.name} (Montage Preview)";
                previewInstance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                DisablePreviewBehaviours(previewInstance);

                var animator = previewInstance.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    previewError = "The preview character does not contain an Animator.";
                    return;
                }

                animator.enabled = true;
                previewGraph = new MontagePreviewGraph(animator, montage, includeBaseController);
                GetPlaybackRange(out var start, out _);
                currentTime = start;
                previewGraph.Sample(currentTime, 0f);
                FrameCharacter();
            }
            catch (Exception exception)
            {
                var message = $"Could not create preview: {exception.Message}";
                CleanupPreview();
                previewError = message;
                Debug.LogException(exception);
            }
        }

        private void CleanupPreview()
        {
            isPlaying = false;
            previewGraph?.Dispose();
            previewGraph = null;
            previewInstance = null;
            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }
        }

        private void FrameCharacter()
        {
            if (previewInstance == null)
                return;
            var renderers = previewInstance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                cameraFocus = previewInstance.transform.position + Vector3.up;
                cameraDistance = 3f;
                return;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            cameraFocus = bounds.center;
            cameraDistance = Mathf.Max(0.5f, bounds.extents.magnitude * 2.6f);
        }

        private void UpdatePreviewCamera(Rect rect)
        {
            var camera = previewUtility.camera;
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = new Color(0.105f, 0.115f, 0.13f);
            camera.nearClipPlane = Mathf.Max(0.01f, cameraDistance * 0.01f);
            camera.farClipPlane = Mathf.Max(100f, cameraDistance * 20f);
            camera.aspect = Mathf.Max(0.01f, rect.width / Mathf.Max(1f, rect.height));
            var rotation = Quaternion.Euler(orbit.y, orbit.x, 0f);
            camera.transform.position = cameraFocus + rotation * new Vector3(0f, 0f, -cameraDistance);
            camera.transform.rotation = Quaternion.LookRotation(cameraFocus - camera.transform.position, Vector3.up);
        }

        private void HandlePreviewInput(Rect rect)
        {
            var current = Event.current;
            if (!rect.Contains(current.mousePosition))
                return;

            if (current.type == EventType.MouseDrag && current.button == 0)
            {
                orbit.x += current.delta.x * 0.6f;
                orbit.y = Mathf.Clamp(orbit.y - current.delta.y * 0.6f, -80f, 80f);
                current.Use();
                Repaint();
            }
            else if (current.type == EventType.MouseDrag && current.button == 2)
            {
                var camera = previewUtility.camera;
                cameraFocus += (-camera.transform.right * current.delta.x + camera.transform.up * current.delta.y) *
                               (cameraDistance * 0.0025f);
                current.Use();
                Repaint();
            }
            else if (current.type == EventType.ScrollWheel)
            {
                cameraDistance = Mathf.Clamp(cameraDistance * (1f + current.delta.y * 0.08f), 0.1f, 100f);
                current.Use();
                Repaint();
            }
            else if (current.type == EventType.KeyDown && current.keyCode == KeyCode.F)
            {
                FrameCharacter();
                current.Use();
                Repaint();
            }
        }

        private void DrawRuler(float duration)
        {
            duration = Mathf.Max(0.001f, duration);
            var rect = GUILayoutUtility.GetRect(position.width - 20f, 24f);
            EditorGUI.DrawRect(rect, new Color(0.14f, 0.14f, 0.14f));
            var content = ContentRect(rect);
            for (var i = 0; i <= 10; i++)
            {
                var x = Mathf.Lerp(content.x, content.xMax, i / 10f);
                EditorGUI.DrawRect(new Rect(x, rect.yMax - 6f, 1f, 6f), Color.gray);
                GUI.Label(new Rect(x + 2f, rect.y, 48f, 18f), (duration * i / 10f).ToString("0.##"), EditorStyles.miniLabel);
            }
            HandleTimelineScrub(content, duration);
            DrawPlayhead(rect, duration);
        }

        private void DrawSections(float duration)
        {
            duration = Mathf.Max(0.001f, duration);
            var rect = GUILayoutUtility.GetRect(position.width - 20f, RowHeight);
            DrawRowBackground(rect, 0);
            GUI.Label(new Rect(rect.x + 4f, rect.y + 5f, HeaderWidth - 8f, 20f), "Sections");
            var content = ContentRect(rect);
            for (var i = 0; i < montage.Sections.Count; i++)
            {
                var section = montage.Sections[i];
                if (section == null)
                    continue;
                var sectionRect = TimeRect(content, section.StartTime, section.EndTime, duration);
                EditorGUI.DrawRect(sectionRect, new Color(0.25f, 0.48f, 0.72f));
                GUI.Label(sectionRect, section.Name, EditorStyles.miniLabel);
            }
            DrawPlayhead(rect, duration);
        }

        private void DrawTrack(MontageSlotTrack track, float duration, int row)
        {
            if (track == null)
                return;
            duration = Mathf.Max(0.001f, duration);
            var rect = GUILayoutUtility.GetRect(position.width - 20f, RowHeight);
            DrawRowBackground(rect, row + 1);
            GUI.Label(new Rect(rect.x + 4f, rect.y + 5f, HeaderWidth - 8f, 20f), track.Slot);
            var content = ContentRect(rect);
            for (var i = 0; i < track.Segments.Count; i++)
            {
                var segment = track.Segments[i];
                if (segment == null || segment.Clip == null)
                    continue;
                var segmentRect = TimeRect(content, segment.StartTime, segment.EndTime, duration);
                EditorGUI.DrawRect(segmentRect, track.Additive
                    ? new Color(0.48f, 0.35f, 0.65f)
                    : new Color(0.24f, 0.58f, 0.40f));
                GUI.Label(segmentRect, segment.Clip.name, EditorStyles.miniLabel);
            }
            DrawPlayhead(rect, duration);
        }

        private void DrawNotifies(float duration)
        {
            duration = Mathf.Max(0.001f, duration);
            var rect = GUILayoutUtility.GetRect(position.width - 20f, RowHeight);
            DrawRowBackground(rect, montage.SlotTracks.Count + 1);
            GUI.Label(new Rect(rect.x + 4f, rect.y + 5f, HeaderWidth - 8f, 20f), "Notifies");
            var content = ContentRect(rect);
            for (var i = 0; i < montage.Notifies.Count; i++)
            {
                var notify = montage.Notifies[i];
                if (notify == null)
                    continue;
                var active = notify.IsState
                    ? currentTime >= notify.Time && currentTime <= notify.EndTime
                    : Mathf.Abs(currentTime - notify.Time) <= 1f / 30f;
                var width = notify.IsState
                    ? Mathf.Max(3f, content.width * notify.Duration / duration)
                    : 3f;
                var x = content.x + content.width * notify.Time / duration;
                var notifyRect = new Rect(x, rect.y + 3f, width, rect.height - 6f);
                EditorGUI.DrawRect(notifyRect, active
                    ? new Color(1f, 0.82f, 0.22f)
                    : new Color(0.9f, 0.58f, 0.18f));
            }
            DrawPlayhead(rect, duration);
        }

        private void HandleTimelineScrub(Rect content, float duration)
        {
            var current = Event.current;
            if ((current.type != EventType.MouseDown && current.type != EventType.MouseDrag) ||
                current.button != 0 || !content.Contains(current.mousePosition))
                return;
            isPlaying = false;
            var normalized = Mathf.InverseLerp(content.x, content.xMax, current.mousePosition.x);
            SetTime(normalized * duration);
            current.Use();
        }

        private void DrawPlayhead(Rect row, float duration)
        {
            if (montage == null || duration <= 0f)
                return;
            var content = ContentRect(row);
            var x = content.x + content.width * Mathf.Clamp01(currentTime / duration);
            EditorGUI.DrawRect(new Rect(x, row.y, 1f, row.height), new Color(1f, 0.35f, 0.25f));
        }

        private string[] GetSectionNames()
        {
            var names = new string[montage.Sections.Count + 1];
            names[0] = "Entire Montage";
            for (var i = 0; i < montage.Sections.Count; i++)
                names[i + 1] = montage.Sections[i] != null ? montage.Sections[i].Name : $"Missing Section {i}";
            return names;
        }

        private void GetPlaybackRange(out float start, out float end)
        {
            start = 0f;
            end = montage != null ? montage.Duration : 0f;
            if (montage == null || sectionIndex <= 0 || sectionIndex > montage.Sections.Count)
                return;
            var section = montage.Sections[sectionIndex - 1];
            if (section == null)
                return;
            start = section.StartTime;
            end = Mathf.Min(section.EndTime, montage.Duration);
        }

        private void OnProjectChanged()
        {
            if (montage != null && previewCharacter != null)
                pendingRebuild = true;
        }

        private void OnUndoRedo()
        {
            pendingRebuild = true;
            Repaint();
        }

        private static void DisablePreviewBehaviours(GameObject root)
        {
            var behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
            for (var i = 0; i < behaviours.Length; i++)
                behaviours[i].enabled = false;
        }

        private static GUIStyle CenteredWrappedLabel()
        {
            return new GUIStyle(EditorStyles.wordWrappedLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.72f, 0.72f, 0.72f) }
            };
        }

        private static Rect ContentRect(Rect row) =>
            new Rect(row.x + HeaderWidth, row.y + 2f, Mathf.Max(1f, row.width - HeaderWidth), row.height - 4f);

        private static Rect TimeRect(Rect content, float start, float end, float duration)
        {
            var x = content.x + content.width * Mathf.Clamp01(start / duration);
            var xMax = content.x + content.width * Mathf.Clamp01(end / duration);
            return new Rect(x, content.y, Mathf.Max(2f, xMax - x), content.height);
        }

        private static void DrawRowBackground(Rect rect, int row)
        {
            EditorGUI.DrawRect(rect, row % 2 == 0
                ? new Color(0.19f, 0.19f, 0.19f)
                : new Color(0.16f, 0.16f, 0.16f));
        }
    }
}
