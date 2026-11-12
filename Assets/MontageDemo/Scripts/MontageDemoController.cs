using System;
using System.Collections;
using ChnnAsn.Montage;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

namespace ChnnAsn.Montage.Demo
{
    [DisallowMultipleComponent]
    public sealed class MontageDemoController : MonoBehaviour
    {
        [SerializeField] private MontagePlayer montagePlayer;
        [SerializeField] private MontageAsset montage;
        [SerializeField] private Transform impactFx;
        [SerializeField] private Renderer impactRenderer;
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private bool autoLoop = true;

        private MontageHandle activeHandle;
        private MaterialPropertyBlock impactProperties;
        private Texture2D panelTexture;
        private Texture2D accentTexture;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle headingStyle;
        private GUIStyle bodyStyle;
        private GUIStyle buttonStyle;
        private GUIStyle badgeStyle;
        private float playRate = 1f;
        private float impactPulse;
        private float nextAutoPlayTime;
        private string status = "READY";
        private string eventText = "Press Space or click Play Montage";
        private int completedPlays;

        public string RuntimeStatus => status;
        public string CurrentSection => activeHandle.IsValid ? activeHandle.CurrentSection : "None";
        public float CurrentMontageTime => activeHandle.IsValid ? activeHandle.Time : 0f;
        public float CurrentMontageWeight => activeHandle.IsValid ? activeHandle.Weight : 0f;
        public int CompletedPlays => completedPlays;

        private void Awake()
        {
            if (montagePlayer == null)
                montagePlayer = GetComponent<MontagePlayer>();

            montagePlayer.Started += OnMontageStarted;
            montagePlayer.SectionChanged += OnSectionChanged;
            montagePlayer.NotifyTriggered += OnNotifyTriggered;
            montagePlayer.Ended += OnMontageEnded;

            impactProperties = new MaterialPropertyBlock();
            if (impactFx != null)
                impactFx.gameObject.SetActive(false);
        }

        private IEnumerator Start()
        {
            yield return null;
            if (playOnStart)
                PlayMontage();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
                PlayMontage();
            if (Input.GetKeyDown(KeyCode.Alpha1))
                PlaySection("Windup");
            if (Input.GetKeyDown(KeyCode.Alpha2))
                PlaySection("Strike");
            if (Input.GetKeyDown(KeyCode.Alpha3))
                PlaySection("Recover");
            if (Input.GetKeyDown(KeyCode.S))
                StopMontage();
            if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
                ChangeRate(0.25f);
            if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
                ChangeRate(-0.25f);

            if (autoLoop && !activeHandle.IsValid && nextAutoPlayTime > 0f && Time.time >= nextAutoPlayTime)
            {
                nextAutoPlayTime = 0f;
                PlayMontage();
            }

            UpdateImpactFx();
        }

        public void PlayMontage()
        {
            if (montagePlayer == null || montage == null)
                return;

            activeHandle.Stop(0.05f);
            activeHandle = montagePlayer.Play(
                montage,
                MontagePlayOptions.Default
                    .WithPriority(100)
                    .WithPlayRate(playRate));

            if (!activeHandle.IsValid)
            {
                status = "PLAY REJECTED";
                eventText = "Check the Console for Montage validation errors";
            }
        }

        public void PlaySection(string sectionName)
        {
            if (montagePlayer == null || montage == null)
                return;

            activeHandle.Stop(0.05f);
            activeHandle = montagePlayer.Play(
                montage,
                MontagePlayOptions.Default
                    .WithSection(sectionName)
                    .WithPriority(110)
                    .WithPlayRate(playRate)
                    .WithBlendIn(0.08f));
        }

        public void StopMontage()
        {
            activeHandle.Stop(0.15f);
            nextAutoPlayTime = 0f;
            status = "STOPPING";
            eventText = "Manual blend-out requested";
        }

        public void TriggerImpact(float montageTime)
        {
            impactPulse = 1f;
            if (impactFx != null)
            {
                impactFx.gameObject.SetActive(true);
                impactFx.localScale = Vector3.one * 0.15f;
            }

            status = "IMPACT NOTIFY";
            eventText = $"Notify fired at {montageTime:0.00}s";
        }

        private void ChangeRate(float delta)
        {
            playRate = Mathf.Clamp(playRate + delta, 0.25f, 2f);
            activeHandle.SetPlayRate(playRate);
            eventText = $"Play rate changed to {playRate:0.00}x";
        }

        private void UpdateImpactFx()
        {
            if (impactPulse <= 0f || impactFx == null)
                return;

            impactPulse = Mathf.Max(0f, impactPulse - Time.deltaTime * 2.4f);
            var eased = 1f - impactPulse;
            impactFx.localScale = Vector3.one * Mathf.Lerp(0.15f, 2.5f, eased);
            impactFx.Rotate(Vector3.up, 180f * Time.deltaTime, Space.Self);

            if (impactRenderer != null)
            {
                impactRenderer.GetPropertyBlock(impactProperties);
                var color = new Color(1f, 0.72f, 0.12f, impactPulse);
                impactProperties.SetColor("_Color", color);
                impactProperties.SetColor("_EmissionColor", color * 2f);
                impactRenderer.SetPropertyBlock(impactProperties);
            }

            if (impactPulse <= 0f)
                impactFx.gameObject.SetActive(false);
        }

        private void OnMontageStarted(MontageHandle handle)
        {
            status = "PLAYING";
            eventText = "Montage started with a 0.15s blend-in";
        }

        private void OnSectionChanged(MontageHandle handle, string previous, string next)
        {
            status = string.IsNullOrEmpty(next) ? "PLAYING" : next.ToUpperInvariant();
            eventText = $"Section: {previous ?? "None"}  >  {next ?? "None"}";
        }

        private void OnNotifyTriggered(MontageNotifyContext context)
        {
            eventText = $"Notify dispatched: {context.Notify.Name}";
        }

        private void OnMontageEnded(MontageHandle handle, MontageStopReason reason)
        {
            if (handle != activeHandle)
                return;

            completedPlays++;
            status = reason.ToString().ToUpperInvariant();
            eventText = $"Montage ended: {reason}";
            if (autoLoop && reason == MontageStopReason.Completed)
                nextAutoPlayTime = Time.time + 0.8f;
        }

        private void OnDestroy()
        {
            if (montagePlayer != null)
            {
                montagePlayer.Started -= OnMontageStarted;
                montagePlayer.SectionChanged -= OnSectionChanged;
                montagePlayer.NotifyTriggered -= OnNotifyTriggered;
                montagePlayer.Ended -= OnMontageEnded;
            }

            if (panelTexture != null)
                Destroy(panelTexture);
            if (accentTexture != null)
                Destroy(accentTexture);
        }

        private void OnGUI()
        {
            EnsureGuiStyles();

            const float designWidth = 1280f;
            const float designHeight = 720f;
            var scale = Mathf.Min(Screen.width / designWidth, Screen.height / designHeight);
            scale = Mathf.Max(0.65f, scale);
            var previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            var panel = new Rect(32f, 32f, 430f, 286f);
            GUI.Box(panel, GUIContent.none, panelStyle);
            GUI.Label(new Rect(58f, 50f, 320f, 38f), "PLAYABLE MONTAGE", titleStyle);
            GUI.Label(new Rect(58f, 89f, 330f, 24f), "A UE-style montage workflow for Unity", bodyStyle);
            GUI.Label(new Rect(340f, 55f, 90f, 28f), status, badgeStyle);

            var section = activeHandle.IsValid ? activeHandle.CurrentSection : "None";
            var time = activeHandle.IsValid ? activeHandle.Time : 0f;
            var weight = activeHandle.IsValid ? activeHandle.Weight : 0f;
            GUI.Label(new Rect(58f, 126f, 360f, 24f),
                $"Section  {section,-8}    Time  {time:0.00}s    Weight  {weight:0.00}", headingStyle);

            DrawTimeline(new Rect(58f, 159f, 346f, 30f), time);
            GUI.Label(new Rect(58f, 194f, 350f, 22f), eventText, bodyStyle);

            if (GUI.Button(new Rect(58f, 230f, 112f, 42f), "PLAY", buttonStyle))
                PlayMontage();
            if (GUI.Button(new Rect(179f, 230f, 112f, 42f), "STRIKE", buttonStyle))
                PlaySection("Strike");
            if (GUI.Button(new Rect(300f, 230f, 104f, 42f), "STOP", buttonStyle))
                StopMontage();

            GUI.Label(new Rect(32f, 680f, 930f, 26f),
                "SPACE Play    1 Windup    2 Strike    3 Recover    S Stop    +/- Play Rate",
                headingStyle);
            GUI.Label(new Rect(1030f, 672f, 220f, 30f), $"{playRate:0.00}x  |  LOOP {completedPlays:00}", badgeStyle);

            GUI.matrix = previousMatrix;
        }

        private void EnsureGuiStyles()
        {
            if (panelStyle != null)
                return;

            panelTexture = MakeTexture(new Color(0.035f, 0.055f, 0.09f, 0.94f));
            accentTexture = MakeTexture(new Color(0.05f, 0.8f, 0.96f, 1f));
            panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture } };
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 25,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.12f, 0.87f, 1f) }
            };
            headingStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = new Color(0.68f, 0.76f, 0.86f) }
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.03f, 0.08f, 0.12f), background = accentTexture },
                hover = { textColor = Color.white }
            };
            badgeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.08f, 0.9f, 1f) }
            };
        }

        private void DrawTimeline(Rect rect, float time)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width * 0.333f, rect.height), new Color(0.12f, 0.38f, 0.55f, 1f));
            DrawRect(new Rect(rect.x + rect.width * 0.333f, rect.y, rect.width * 0.217f, rect.height), new Color(0.95f, 0.42f, 0.16f, 1f));
            DrawRect(new Rect(rect.x + rect.width * 0.55f, rect.y, rect.width * 0.45f, rect.height), new Color(0.26f, 0.23f, 0.5f, 1f));

            var labelStyle = new GUIStyle(headingStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 11 };
            GUI.Label(new Rect(rect.x, rect.y, rect.width * 0.333f, rect.height), "WINDUP", labelStyle);
            GUI.Label(new Rect(rect.x + rect.width * 0.333f, rect.y, rect.width * 0.217f, rect.height), "STRIKE", labelStyle);
            GUI.Label(new Rect(rect.x + rect.width * 0.55f, rect.y, rect.width * 0.45f, rect.height), "RECOVER", labelStyle);

            var playhead = rect.x + rect.width * Mathf.Clamp01(time / 3f);
            DrawRect(new Rect(playhead - 1.5f, rect.y - 4f, 3f, rect.height + 8f), Color.white);
        }

        private static void DrawRect(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }

    public sealed class MontageDemoImpactNotify : MontageNotifyAsset
    {
        public override void OnNotify(in MontageNotifyContext context)
        {
            var controller = context.Owner != null
                ? context.Owner.GetComponent<MontageDemoController>()
                : null;
            controller?.TriggerImpact(context.Time);
            Debug.Log($"[Montage Demo] Impact Notify fired at {context.Time:0.00}s", context.Owner);
        }
    }

#if UNITY_EDITOR
    public static class MontageDemoBuilder
    {
        private const string RootFolder = "Assets/MontageDemo";
        private const string ScenePath = RootFolder + "/Scenes/MontageDemo.unity";
        private const string ClipPath = RootFolder + "/Animations/DemoAttack.anim";
        private const string MontagePath = RootFolder + "/Montages/DemoAttackMontage.asset";
        private const string NotifyPath = RootFolder + "/Montages/DemoImpactNotify.asset";
        private const string PrefabPath = RootFolder + "/Prefabs/DemoCharacter.prefab";
        private const string MaterialFolder = RootFolder + "/Materials";

        [MenuItem("Tools/Unity Montage/Create Demo Scene")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Exit Play Mode before rebuilding the Montage demo.");
                return;
            }

            EnsureFolders();
            var clip = CreateAnimationClip();
            var notify = GetOrCreateAsset<MontageDemoImpactNotify>(NotifyPath);
            var montage = CreateMontage(clip, notify);
            CreateScene(montage);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Montage Demo] Created scene: {ScenePath}");
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "MontageDemo");
            EnsureFolder(RootFolder, "Animations");
            EnsureFolder(RootFolder, "Materials");
            EnsureFolder(RootFolder, "Montages");
            EnsureFolder(RootFolder, "Prefabs");
            EnsureFolder(RootFolder, "Scenes");
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;

            asset = ScriptableObject.CreateInstance<T>();
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static AnimationClip CreateAnimationClip()
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if (clip == null)
            {
                clip = new AnimationClip { name = "Demo Attack Montage", frameRate = 60f };
                AssetDatabase.CreateAsset(clip, ClipPath);
            }
            else
            {
                clip.ClearCurves();
                clip.frameRate = 60f;
            }

            SetCurve(clip, "Body", "localPosition.y", 1.8f,
                new Keyframe(0.65f, 1.62f), new Keyframe(1f, 1.62f), new Keyframe(1.18f, 2.03f),
                new Keyframe(1.58f, 1.78f), new Keyframe(3f, 1.8f));
            SetCurve(clip, "Body", "localEulerAnglesRaw.y", 0f,
                new Keyframe(0.65f, -24f), new Keyframe(1f, -38f), new Keyframe(1.2f, 32f),
                new Keyframe(1.65f, 8f), new Keyframe(3f, 0f));
            SetCurve(clip, "Head", "localEulerAnglesRaw.y", 0f,
                new Keyframe(0.9f, 18f), new Keyframe(1.22f, -22f), new Keyframe(2.1f, 6f), new Keyframe(3f, 0f));
            SetCurve(clip, "Arm_R", "localEulerAnglesRaw.z", -18f,
                new Keyframe(0.7f, -72f), new Keyframe(1f, -105f), new Keyframe(1.18f, 88f),
                new Keyframe(1.65f, 34f), new Keyframe(3f, -18f));
            SetCurve(clip, "Arm_L", "localEulerAnglesRaw.z", 18f,
                new Keyframe(0.7f, 58f), new Keyframe(1f, 76f), new Keyframe(1.18f, -38f),
                new Keyframe(1.65f, -12f), new Keyframe(3f, 18f));
            SetCurve(clip, "Leg_R", "localEulerAnglesRaw.z", 0f,
                new Keyframe(0.8f, -18f), new Keyframe(1.2f, 15f), new Keyframe(2f, 4f), new Keyframe(3f, 0f));
            SetCurve(clip, "Leg_L", "localEulerAnglesRaw.z", 0f,
                new Keyframe(0.8f, 14f), new Keyframe(1.2f, -10f), new Keyframe(2f, -3f), new Keyframe(3f, 0f));

            clip.EnsureQuaternionContinuity();
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static void SetCurve(AnimationClip clip, string path, string property, float initial, params Keyframe[] keys)
        {
            var allKeys = new Keyframe[keys.Length + 1];
            allKeys[0] = new Keyframe(0f, initial);
            Array.Copy(keys, 0, allKeys, 1, keys.Length);
            var curve = new AnimationCurve(allKeys);
            for (var i = 0; i < curve.length; i++)
                AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }

        private static MontageAsset CreateMontage(AnimationClip clip, MontageDemoImpactNotify notify)
        {
            var montage = GetOrCreateAsset<MontageAsset>(MontagePath);
            var serialized = new SerializedObject(montage);
            serialized.FindProperty("blendIn").floatValue = 0.15f;
            serialized.FindProperty("blendOut").floatValue = 0.2f;
            serialized.FindProperty("defaultPlayRate").floatValue = 1f;

            var sections = serialized.FindProperty("sections");
            sections.arraySize = 3;
            SetSection(sections.GetArrayElementAtIndex(0), "Windup", 0f, 1f, "Strike");
            SetSection(sections.GetArrayElementAtIndex(1), "Strike", 1f, 1.65f, "Recover");
            SetSection(sections.GetArrayElementAtIndex(2), "Recover", 1.65f, 3f, string.Empty);

            var tracks = serialized.FindProperty("slotTracks");
            tracks.arraySize = 1;
            var track = tracks.GetArrayElementAtIndex(0);
            track.FindPropertyRelative("slot").stringValue = "FullBody";
            track.FindPropertyRelative("avatarMask").objectReferenceValue = null;
            track.FindPropertyRelative("additive").boolValue = false;
            var segments = track.FindPropertyRelative("segments");
            segments.arraySize = 1;
            var segment = segments.GetArrayElementAtIndex(0);
            segment.FindPropertyRelative("clip").objectReferenceValue = clip;
            segment.FindPropertyRelative("startTime").floatValue = 0f;
            segment.FindPropertyRelative("clipIn").floatValue = 0f;
            segment.FindPropertyRelative("duration").floatValue = 3f;
            segment.FindPropertyRelative("playRate").floatValue = 1f;
            segment.FindPropertyRelative("loop").boolValue = false;
            segment.FindPropertyRelative("applyFootIK").boolValue = false;
            segment.FindPropertyRelative("applyPlayableIK").boolValue = false;

            var notifies = serialized.FindProperty("notifies");
            notifies.arraySize = 1;
            var notifyProperty = notifies.GetArrayElementAtIndex(0);
            notifyProperty.FindPropertyRelative("id").stringValue = Guid.NewGuid().ToString("N");
            notifyProperty.FindPropertyRelative("time").floatValue = 1.18f;
            notifyProperty.FindPropertyRelative("duration").floatValue = 0f;
            notifyProperty.FindPropertyRelative("notify").objectReferenceValue = notify;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(montage);
            return montage;
        }

        private static void SetSection(SerializedProperty property, string name, float start, float end, string next)
        {
            property.FindPropertyRelative("id").stringValue = Guid.NewGuid().ToString("N");
            property.FindPropertyRelative("name").stringValue = name;
            property.FindPropertyRelative("startTime").floatValue = start;
            property.FindPropertyRelative("endTime").floatValue = end;
            property.FindPropertyRelative("nextSection").stringValue = next;
        }

        private static void CreateScene(MontageAsset montage)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var navy = CreateMaterial("Navy", new Color(0.035f, 0.08f, 0.14f));
            var cyan = CreateMaterial("Cyan", new Color(0.02f, 0.68f, 0.9f), true);
            var orange = CreateMaterial("Orange", new Color(1f, 0.27f, 0.05f), true);
            var purple = CreateMaterial("Purple", new Color(0.3f, 0.16f, 0.58f), true);
            var white = CreateMaterial("White", new Color(0.78f, 0.86f, 0.95f));
            var dark = CreateMaterial("Dark", new Color(0.015f, 0.025f, 0.045f));

            var environment = new GameObject("Demo Environment");
            CreatePart(environment.transform, "Ground", PrimitiveType.Plane, Vector3.zero, new Vector3(2.3f, 1f, 2.3f), navy);
            CreatePart(environment.transform, "Stage", PrimitiveType.Cylinder, new Vector3(0f, 0.18f, 0f), new Vector3(2.6f, 0.18f, 2.6f), dark);
            CreatePart(environment.transform, "Windup_Block", PrimitiveType.Cube, new Vector3(-4.3f, 1.1f, 3.5f), new Vector3(2.2f, 2.2f, 0.35f), cyan);
            CreatePart(environment.transform, "Strike_Block", PrimitiveType.Cube, new Vector3(0f, 1.1f, 3.7f), new Vector3(2.2f, 2.2f, 0.35f), orange);
            CreatePart(environment.transform, "Recover_Block", PrimitiveType.Cube, new Vector3(4.3f, 1.1f, 3.5f), new Vector3(2.2f, 2.2f, 0.35f), purple);

            var character = new GameObject("DemoCharacter");
            character.transform.position = new Vector3(0f, 0.35f, 0f);
            var animator = character.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            CreatePart(character.transform, "Body", PrimitiveType.Cube, new Vector3(0f, 1.8f, 0f), new Vector3(1.15f, 1.45f, 0.65f), white);
            CreatePart(character.transform, "Core", PrimitiveType.Sphere, new Vector3(0f, 1.9f, -0.36f), new Vector3(0.36f, 0.36f, 0.18f), cyan);
            CreatePart(character.transform, "Head", PrimitiveType.Sphere, new Vector3(0f, 3f, 0f), new Vector3(0.78f, 0.68f, 0.7f), white);
            CreatePart(character.transform, "Visor", PrimitiveType.Cube, new Vector3(0f, 3.03f, -0.38f), new Vector3(0.56f, 0.18f, 0.08f), cyan);
            var leftArm = CreatePart(character.transform, "Arm_L", PrimitiveType.Capsule, new Vector3(-0.94f, 2.05f, 0f), new Vector3(0.34f, 0.72f, 0.34f), cyan);
            leftArm.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
            var rightArm = CreatePart(character.transform, "Arm_R", PrimitiveType.Capsule, new Vector3(0.94f, 2.05f, 0f), new Vector3(0.34f, 0.72f, 0.34f), orange);
            rightArm.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
            CreatePart(character.transform, "Leg_L", PrimitiveType.Capsule, new Vector3(-0.38f, 0.65f, 0f), new Vector3(0.4f, 0.65f, 0.4f), purple);
            CreatePart(character.transform, "Leg_R", PrimitiveType.Capsule, new Vector3(0.38f, 0.65f, 0f), new Vector3(0.4f, 0.65f, 0.4f), purple);
            CreatePart(rightArm.transform, "Blade", PrimitiveType.Cube, new Vector3(0f, 1.1f, 0f), new Vector3(0.16f, 1.15f, 0.1f), orange);
            PrefabUtility.SaveAsPrefabAsset(character, PrefabPath);

            var impact = CreatePart(environment.transform, "ImpactFX", PrimitiveType.Sphere, new Vector3(1.9f, 2.15f, -0.2f), Vector3.one * 0.2f, orange);
            impact.SetActive(false);

            var player = character.AddComponent<MontagePlayer>();
            var controller = character.AddComponent<MontageDemoController>();
            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("montagePlayer").objectReferenceValue = player;
            controllerSerialized.FindProperty("montage").objectReferenceValue = montage;
            controllerSerialized.FindProperty("impactFx").objectReferenceValue = impact.transform;
            controllerSerialized.FindProperty("impactRenderer").objectReferenceValue = impact.GetComponent<Renderer>();
            controllerSerialized.FindProperty("playOnStart").boolValue = true;
            controllerSerialized.FindProperty("autoLoop").boolValue = true;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();

            CreateWorldText(environment.transform, "Title", "UNITY  MONTAGE", new Vector3(0f, 5.2f, 3.3f), 0.075f, Color.white);
            CreateWorldText(environment.transform, "Subtitle", "PLAYABLES  /  SECTIONS  /  NOTIFIES", new Vector3(0f, 4.55f, 3.3f), 0.035f, new Color(0.15f, 0.85f, 1f));

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.008f, 0.015f, 0.03f);
            camera.fieldOfView = 48f;
            cameraObject.transform.position = new Vector3(7.2f, 4.2f, -8.7f);
            cameraObject.transform.LookAt(new Vector3(0f, 2f, 0.55f));

            var keyLight = new GameObject("Key Light").AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.15f;
            keyLight.color = new Color(0.72f, 0.88f, 1f);
            keyLight.transform.rotation = Quaternion.Euler(42f, -38f, 0f);

            var rimLight = new GameObject("Rim Light").AddComponent<Light>();
            rimLight.type = LightType.Point;
            rimLight.range = 12f;
            rimLight.intensity = 3.2f;
            rimLight.color = new Color(0.2f, 0.72f, 1f);
            rimLight.transform.position = new Vector3(-4f, 3.5f, -1.5f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.16f, 0.24f);
            Selection.activeGameObject = character;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static Material CreateMaterial(string name, Color color, bool emissive = false)
        {
            var path = MaterialFolder + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Standard");
                if (shader == null)
                    shader = Shader.Find("Unlit/Color");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (emissive && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 1.35f);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePart(Transform parent, string name, PrimitiveType primitive, Vector3 localPosition, Vector3 localScale, Material material)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            var collider = part.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.DestroyImmediate(collider);
            var renderer = part.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = material;
            return part;
        }

        private static void CreateWorldText(Transform parent, string name, string text, Vector3 position, float size, Color color)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = position;
            textObject.transform.localRotation = Quaternion.identity;
            var textMesh = textObject.AddComponent<TextMesh>();
            textMesh.text = text;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = size;
            textMesh.fontSize = 64;
            textMesh.color = color;
        }
    }
#endif
}
