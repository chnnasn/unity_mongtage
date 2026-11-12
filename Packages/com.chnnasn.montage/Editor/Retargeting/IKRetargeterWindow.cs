using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ChnnAsn.Montage.Editor
{
    internal sealed class IKRetargeterWindow : EditorWindow
    {
        private static readonly Color SourceChainColor = new Color(0.2f, 0.7f, 1f, 0.95f);
        private static readonly Color TargetChainColor = new Color(1f, 0.56f, 0.16f, 0.95f);

        [SerializeField] private IKRetargeterAsset retargeter;
        [SerializeField] private GameObject sourceCharacter;
        [SerializeField] private GameObject targetCharacter;
        [SerializeField] private AnimationClip sourceClip;
        [SerializeField] private float bakeFrameRate = 60f;

        private PreviewRenderUtility previewUtility;
        private GameObject sourceInstance;
        private GameObject targetInstance;
        private Animator sourceAnimator;
        private Animator targetAnimator;
        private Mesh gridMesh;
        private Material gridMaterial;
        private Vector3 gridCenter;
        private Vector2 orbit = new Vector2(180f, 12f);
        private Vector3 cameraFocus;
        private float cameraDistance = 4f;
        private float currentTime;
        private float speed = 1f;
        private bool isPlaying;
        private bool loop = true;
        private double previousEditorTime;
        private string previewError;

        [MenuItem("Window/Animation/Unity Montage/IK Retargeter")]
        public static void OpenWindow()
        {
            GetWindow<IKRetargeterWindow>("IK Retargeter");
        }

        public static void Open(IKRetargeterAsset asset)
        {
            var window = GetWindow<IKRetargeterWindow>("IK Retargeter");
            window.SetRetargeter(asset);
            window.Repaint();
        }

        private void OnEnable()
        {
            minSize = new Vector2(700f, 560f);
            previousEditorTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            Undo.undoRedoPerformed -= OnUndoRedo;
            CleanupPreview();
        }

        private void OnGUI()
        {
            DrawAssetFields();
            DrawPlaybackToolbar();
            DrawPreview();
            DrawTimeline();
            DrawChainMappings();
            DrawActions();
        }

        internal void SetRetargeter(IKRetargeterAsset asset)
        {
            retargeter = asset;
            RebuildPreview();
        }

        private void DrawAssetFields()
        {
            EditorGUI.BeginChangeCheck();
            retargeter = (IKRetargeterAsset)EditorGUILayout.ObjectField(
                "IK Retargeter", retargeter, typeof(IKRetargeterAsset), false);
            sourceCharacter = (GameObject)EditorGUILayout.ObjectField(
                "Source Character", sourceCharacter, typeof(GameObject), false);
            targetCharacter = (GameObject)EditorGUILayout.ObjectField(
                "Target Character", targetCharacter, typeof(GameObject), false);
            sourceClip = (AnimationClip)EditorGUILayout.ObjectField(
                "Source Animation", sourceClip, typeof(AnimationClip), false);
            if (EditorGUI.EndChangeCheck())
            {
                currentTime = 0f;
                isPlaying = false;
                RebuildPreview();
            }
        }

        private void DrawPlaybackToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(sourceClip == null || previewUtility == null))
                {
                    if (GUILayout.Button(isPlaying ? "Pause" : "Play", EditorStyles.toolbarButton, GUILayout.Width(55f)))
                    {
                        isPlaying = !isPlaying;
                        previousEditorTime = EditorApplication.timeSinceStartup;
                    }

                    if (GUILayout.Button("Stop", EditorStyles.toolbarButton, GUILayout.Width(45f)))
                    {
                        isPlaying = false;
                        SetTime(0f);
                    }

                    if (GUILayout.Button("-1f", EditorStyles.toolbarButton, GUILayout.Width(38f)))
                    {
                        isPlaying = false;
                        SetTime(currentTime - 1f / Mathf.Max(1f, sourceClip.frameRate));
                    }

                    if (GUILayout.Button("+1f", EditorStyles.toolbarButton, GUILayout.Width(38f)))
                    {
                        isPlaying = false;
                        SetTime(currentTime + 1f / Mathf.Max(1f, sourceClip.frameRate));
                    }
                }

                loop = GUILayout.Toggle(loop, "Loop", EditorStyles.toolbarButton, GUILayout.Width(45f));
                GUILayout.FlexibleSpace();
                GUILayout.Label("Speed", EditorStyles.miniLabel);
                speed = EditorGUILayout.Slider(speed, 0.1f, 2f, GUILayout.Width(150f));
            }
        }

        private void DrawPreview()
        {
            var height = Mathf.Clamp(position.height * 0.5f, 260f, 520f);
            var rect = GUILayoutUtility.GetRect(100f, height, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(rect, new Color(0.105f, 0.115f, 0.13f));
            if (previewUtility == null || sourceInstance == null || targetInstance == null)
            {
                var message = string.IsNullOrEmpty(previewError)
                    ? "Assign source and target character Prefabs/models for dual preview."
                    : previewError;
                GUI.Label(rect, message, CenteredWrappedLabel());
                return;
            }

            HandlePreviewInput(rect);
            UpdatePreviewCamera(rect);
            previewUtility.BeginPreview(rect, GUIStyle.none);
            DrawGrid();
            previewUtility.Render(true);
            var texture = previewUtility.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
            GUI.Box(rect, GUIContent.none);
            DrawChainOverlay(rect);

            GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width * 0.5f - 16f, 20f),
                "SOURCE", SourceLabelStyle());
            GUI.Label(new Rect(rect.center.x + 4f, rect.y + 8f, rect.width * 0.5f - 16f, 20f),
                "TARGET", TargetLabelStyle());
            GUI.Label(new Rect(rect.x + 8f, rect.yMax - 24f, rect.width - 16f, 18f),
                "RMB Orbit   MMB Pan   Wheel Zoom   F Frame", EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(previewError))
                GUI.Label(new Rect(rect.x + 8f, rect.y + 30f, rect.width - 16f, 38f), previewError, EditorStyles.helpBox);
        }

        private void DrawTimeline()
        {
            using (new EditorGUI.DisabledScope(sourceClip == null))
            {
                var duration = sourceClip == null ? 0f : sourceClip.length;
                EditorGUI.BeginChangeCheck();
                var value = EditorGUILayout.Slider($"Time  {currentTime:0.000}s", currentTime, 0f, duration);
                if (EditorGUI.EndChangeCheck())
                {
                    isPlaying = false;
                    SetTime(value);
                }
            }
        }

        private void DrawChainMappings()
        {
            if (retargeter == null)
                return;

            EditorGUILayout.LabelField("Chain Mapping", EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (retargeter.ChainMappings.Count == 0)
                    EditorGUILayout.LabelField("No mappings. Use Auto Configure Humanoid.", EditorStyles.miniLabel);
                for (var i = 0; i < retargeter.ChainMappings.Count; i++)
                {
                    var mapping = retargeter.ChainMappings[i];
                    if (mapping == null)
                        continue;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Label(mapping.SourceChain, GUILayout.Width(140f));
                        GUILayout.Label("→", GUILayout.Width(22f));
                        GUILayout.Label(mapping.TargetChain, GUILayout.Width(140f));
                        GUILayout.Label(mapping.Mode.ToString(), EditorStyles.miniLabel, GUILayout.Width(120f));
                        GUILayout.FlexibleSpace();
                        GUILayout.Label(mapping.EnableIK ? "IK" : "FK", EditorStyles.miniBoldLabel, GUILayout.Width(24f));
                    }
                }
            }
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(retargeter == null))
                {
                    if (GUILayout.Button("Auto Configure Humanoid"))
                        AutoConfigure();
                    if (GUILayout.Button("Create Source Pose"))
                        CreatePose(true);
                    if (GUILayout.Button("Create Target Pose"))
                        CreatePose(false);
                }

                bakeFrameRate = EditorGUILayout.FloatField("Bake FPS", bakeFrameRate, GUILayout.Width(150f));
                using (new EditorGUI.DisabledScope(targetCharacter == null || sourceClip == null))
                {
                    if (GUILayout.Button("Bake Retargeted Clip", GUILayout.Width(160f)))
                        BakeClip();
                }
            }
        }

        private void AutoConfigure()
        {
            Undo.RecordObject(retargeter, "Auto Configure Humanoid Retargeter");
            retargeter.ResetToHumanoidDefaults();
            retargeter.SourceRig.SetAvatar(FindAvatar(sourceCharacter));
            retargeter.TargetRig.SetAvatar(FindAvatar(targetCharacter));
            EditorUtility.SetDirty(retargeter);
            AssetDatabase.SaveAssets();
            SamplePreview();
        }

        private void CreatePose(bool source)
        {
            var defaultName = source ? "SourceRetargetPose.asset" : "TargetRetargetPose.asset";
            var path = EditorUtility.SaveFilePanelInProject(
                source ? "Create Source Retarget Pose" : "Create Target Retarget Pose",
                defaultName,
                "asset",
                "Choose where to save the Retarget Pose asset.");
            if (string.IsNullOrEmpty(path))
                return;

            var pose = CreateInstance<RetargetPoseAsset>();
            AssetDatabase.CreateAsset(pose, AssetDatabase.GenerateUniqueAssetPath(path));
            var serialized = new SerializedObject(retargeter);
            serialized.FindProperty(source ? "sourcePose" : "targetPose").objectReferenceValue = pose;
            serialized.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Selection.activeObject = pose;
        }

        private void BakeClip()
        {
            var path = EditorUtility.SaveFilePanelInProject(
                "Bake Retargeted Animation",
                sourceClip.name + "_Retargeted.anim",
                "anim",
                "Choose where to save the target-skeleton animation.");
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                IKRetargetBaker.BakeToAsset(retargeter, targetCharacter, sourceClip, bakeFrameRate, path);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("IK Retargeter", exception.Message, "OK");
            }
        }

        private void RebuildPreview()
        {
            CleanupPreview();
            previewError = null;
            if (sourceCharacter == null || targetCharacter == null)
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

                sourceInstance = previewUtility.InstantiatePrefabInScene(sourceCharacter);
                targetInstance = previewUtility.InstantiatePrefabInScene(targetCharacter);
                sourceInstance.name = sourceCharacter.name + " (Source Preview)";
                targetInstance.name = targetCharacter.name + " (Target Preview)";
                DisablePreviewBehaviours(sourceInstance);
                DisablePreviewBehaviours(targetInstance);
                sourceAnimator = sourceInstance.GetComponentInChildren<Animator>(true);
                targetAnimator = targetInstance.GetComponentInChildren<Animator>(true);
                if (sourceAnimator == null || targetAnimator == null)
                    previewError = "Both characters must contain an Animator.";
                else if (!sourceAnimator.isHuman || !targetAnimator.isHuman)
                    previewError = "Humanoid preview requires valid Humanoid Avatars. Generic clips can still be baked by transform path.";

                SamplePreview();
                FrameCharacters();
            }
            catch (Exception exception)
            {
                var message = "Could not create retarget preview: " + exception.Message;
                CleanupPreview();
                previewError = message;
                Debug.LogException(exception);
            }
        }

        private void CleanupPreview()
        {
            isPlaying = false;
            sourceInstance = null;
            targetInstance = null;
            sourceAnimator = null;
            targetAnimator = null;
            if (gridMesh != null)
                DestroyImmediate(gridMesh);
            gridMesh = null;
            if (gridMaterial != null)
                DestroyImmediate(gridMaterial);
            gridMaterial = null;
            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }
        }

        private void SamplePreview()
        {
            if (sourceInstance == null || targetInstance == null)
                return;

            if (sourceClip != null)
            {
                sourceClip.SampleAnimation(sourceInstance, currentTime);
                sourceClip.SampleAnimation(targetInstance, currentTime);
                if (retargeter != null && targetAnimator != null)
                    retargeter.ApplyPoseCorrections(targetAnimator);
            }

            sourceInstance.transform.position = new Vector3(-0.75f, 0f, 0f);
            targetInstance.transform.position = new Vector3(0.75f, 0f, 0f);
            sourceInstance.transform.rotation = Quaternion.identity;
            targetInstance.transform.rotation = Quaternion.identity;
            Repaint();
        }

        private void FrameCharacters()
        {
            var renderers = previewUtility == null
                ? Array.Empty<Renderer>()
                : GetCombinedRenderers(sourceInstance, targetInstance);
            if (renderers.Length == 0)
            {
                cameraFocus = Vector3.up;
                cameraDistance = 4f;
                BuildGrid(Vector3.zero, 4f);
                return;
            }

            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            cameraFocus = bounds.center;
            cameraDistance = Mathf.Max(1f, bounds.extents.magnitude * 2.6f);
            BuildGrid(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z),
                Mathf.Max(2f, bounds.extents.magnitude * 2.4f));
        }

        private void BuildGrid(Vector3 center, float halfExtent)
        {
            gridCenter = center;
            gridMesh = MontageEditorWindow.CreateGridMesh(halfExtent, 20);
            var shader = Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                return;
            gridMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            gridMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            gridMaterial.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            gridMaterial.SetInt("_Cull", (int)CullMode.Off);
            gridMaterial.SetInt("_ZWrite", 0);
            gridMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
        }

        private void DrawGrid()
        {
            if (gridMesh == null || gridMaterial == null)
                return;
            previewUtility.DrawMesh(gridMesh, Matrix4x4.TRS(gridCenter, Quaternion.identity, Vector3.one), gridMaterial, 0);
        }

        private void DrawChainOverlay(Rect rect)
        {
            if (retargeter == null || sourceAnimator == null || targetAnimator == null ||
                !sourceAnimator.isHuman || !targetAnimator.isHuman)
                return;

            Handles.BeginGUI();
            for (var i = 0; i < retargeter.ChainMappings.Count; i++)
            {
                var mapping = retargeter.ChainMappings[i];
                if (mapping == null)
                    continue;
                DrawChainLine(rect, sourceAnimator, retargeter.SourceRig.FindChain(mapping.SourceChain), SourceChainColor);
                DrawChainLine(rect, targetAnimator, retargeter.TargetRig.FindChain(mapping.TargetChain), TargetChainColor);
            }
            Handles.EndGUI();
        }

        private void DrawChainLine(Rect rect, Animator animator, RetargetBoneChain chain, Color color)
        {
            if (chain == null)
                return;
            var start = animator.GetBoneTransform(chain.StartBone);
            var end = animator.GetBoneTransform(chain.EndBone);
            if (start == null || end == null)
                return;

            if (!TryProjectToGui(rect, start.position, out var startPoint) ||
                !TryProjectToGui(rect, end.position, out var endPoint))
                return;
            Handles.color = color;
            Handles.DrawAAPolyLine(2.5f, startPoint, endPoint);
            Handles.DrawSolidDisc(startPoint, Vector3.forward, 2.5f);
            Handles.DrawSolidDisc(endPoint, Vector3.forward, 2.5f);
        }

        private bool TryProjectToGui(Rect rect, Vector3 world, out Vector3 point)
        {
            var viewport = previewUtility.camera.WorldToViewportPoint(world);
            point = new Vector3(rect.x + viewport.x * rect.width, rect.y + (1f - viewport.y) * rect.height, 0f);
            return viewport.z > 0f;
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
            if (current.type == EventType.MouseDown && current.button == 1)
            {
                Focus();
                current.Use();
            }
            else if (current.type == EventType.MouseDrag && current.button == 1)
            {
                orbit.x -= current.delta.x * 0.6f;
                orbit.y = Mathf.Clamp(orbit.y + current.delta.y * 0.6f, -80f, 80f);
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
                FrameCharacters();
                current.Use();
                Repaint();
            }
        }

        private void OnEditorUpdate()
        {
            var now = EditorApplication.timeSinceStartup;
            var delta = Math.Min(0.1d, Math.Max(0d, now - previousEditorTime));
            previousEditorTime = now;
            if (!isPlaying || sourceClip == null)
                return;

            currentTime += (float)delta * speed;
            if (currentTime >= sourceClip.length)
            {
                if (loop && sourceClip.length > 0f)
                    currentTime %= sourceClip.length;
                else
                {
                    currentTime = sourceClip.length;
                    isPlaying = false;
                }
            }
            SamplePreview();
        }

        private void SetTime(float value)
        {
            currentTime = sourceClip == null ? 0f : Mathf.Clamp(value, 0f, sourceClip.length);
            SamplePreview();
        }

        private void OnUndoRedo()
        {
            SamplePreview();
            Repaint();
        }

        private static Avatar FindAvatar(GameObject character)
        {
            var animator = character == null ? null : character.GetComponentInChildren<Animator>(true);
            return animator == null ? null : animator.avatar;
        }

        private static void DisablePreviewBehaviours(GameObject root)
        {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;
            var animator = root.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.enabled = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
            }
        }

        private static Renderer[] GetCombinedRenderers(GameObject first, GameObject second)
        {
            var left = first == null ? Array.Empty<Renderer>() : first.GetComponentsInChildren<Renderer>(true);
            var right = second == null ? Array.Empty<Renderer>() : second.GetComponentsInChildren<Renderer>(true);
            var result = new Renderer[left.Length + right.Length];
            Array.Copy(left, result, left.Length);
            Array.Copy(right, 0, result, left.Length, right.Length);
            return result;
        }

        private static GUIStyle CenteredWrappedLabel()
        {
            return new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = 12
            };
        }

        private static GUIStyle SourceLabelStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = SourceChainColor } };
        }

        private static GUIStyle TargetLabelStyle()
        {
            return new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = TargetChainColor } };
        }
    }
}
