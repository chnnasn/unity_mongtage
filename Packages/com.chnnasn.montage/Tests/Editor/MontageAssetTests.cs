using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using ChnnAsn.Montage.Editor;

namespace ChnnAsn.Montage.Tests
{
    public sealed class MontageAssetTests
    {
        [Test]
        public void EmptyAssetReportsValidationErrors()
        {
            var asset = ScriptableObject.CreateInstance<MontageAsset>();
            var errors = new List<string>();

            asset.Validate(errors);

            Assert.That(errors, Is.Not.Empty);
            Object.DestroyImmediate(asset);
        }

        [Test]
        public void DefaultOptionsUseExpectedSentinels()
        {
            var options = MontagePlayOptions.Default;

            Assert.That(options.PlayRate, Is.EqualTo(1f));
            Assert.That(options.BlendIn, Is.EqualTo(-1f));
        }

        [Test]
        public void ValidMontageCreatesPlayableRuntimeAndReturnsHandle()
        {
            var clip = new AnimationClip();
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            var asset = CreateSingleClipMontage(clip);
            var character = new GameObject("Montage Test Character");

            try
            {
                character.AddComponent<Animator>();
                var player = character.AddComponent<MontagePlayer>();
                player.Initialize();

                var handle = player.Play(asset);

                Assert.That(handle.IsValid, Is.True);
                Assert.That(handle.IsPlaying, Is.True);
                Assert.That(player.IsPlaying(asset), Is.True);
                Assert.That(handle.Stop(0f), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(character);
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void PreviewGraphCanSampleAndDispose()
        {
            var clip = new AnimationClip();
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            var asset = CreateSingleClipMontage(clip);
            var character = new GameObject("Montage Preview Test Character");

            try
            {
                var animator = character.AddComponent<Animator>();
                using (var preview = new MontagePreviewGraph(animator, asset, false))
                {
                    Assert.That(preview.IsValid, Is.True);
                    Assert.DoesNotThrow(() => preview.Sample(0.5f, 0f));
                }
            }
            finally
            {
                Object.DestroyImmediate(character);
                Object.DestroyImmediate(asset);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void PreviewGridUsesLineTopology()
        {
            var mesh = MontageEditorWindow.CreateGridMesh(4f, 20);

            try
            {
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                Assert.That(mesh.GetTopology(0), Is.EqualTo(MeshTopology.Lines));
                Assert.That(mesh.bounds.size.x, Is.EqualTo(8f).Within(0.001f));
                Assert.That(mesh.bounds.size.z, Is.EqualTo(8f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void BuiltInNotifyAssetsExposeSerializedPayloads()
        {
            var clip = AudioClip.Create("Montage Audio", 4410, 1, 44100, false);
            var prefab = new GameObject("Montage VFX Prefab");
            var audio = ScriptableObject.CreateInstance<MontageAudioNotifyAsset>();
            var vfx = ScriptableObject.CreateInstance<MontageVfxNotifyAsset>();

            try
            {
                var audioSerialized = new SerializedObject(audio);
                audioSerialized.FindProperty("clip").objectReferenceValue = clip;
                audioSerialized.FindProperty("volume").floatValue = 0.65f;
                audioSerialized.FindProperty("pitch").floatValue = 1.25f;
                audioSerialized.ApplyModifiedPropertiesWithoutUndo();

                var vfxSerialized = new SerializedObject(vfx);
                vfxSerialized.FindProperty("prefab").objectReferenceValue = prefab;
                vfxSerialized.FindProperty("lifetime").floatValue = 2.5f;
                vfxSerialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(audio.Clip, Is.SameAs(clip));
                Assert.That(audio.Volume, Is.EqualTo(0.65f).Within(0.001f));
                Assert.That(audio.Pitch, Is.EqualTo(1.25f).Within(0.001f));
                Assert.That(vfx.Prefab, Is.SameAs(prefab));
                Assert.That(vfx.Lifetime, Is.EqualTo(2.5f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(audio);
                Object.DestroyImmediate(vfx);
                Object.DestroyImmediate(prefab);
                Object.DestroyImmediate(clip);
            }
        }

        [Test]
        public void EditorWindowCanAddMoveAndRemoveVfxEvent()
        {
            const string montagePath = "Assets/MontageEditorWindowTest.asset";
            const string prefabPath = "Assets/MontageEditorWindowVfx.prefab";
            const string clipPath = "Assets/MontageEditorWindowTest.anim";
            var clip = new AnimationClip();
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
            var asset = CreateSingleClipMontage(clip);
            var prefabSource = new GameObject("Window VFX");
            MontageEditorWindow window = null;

            try
            {
                AssetDatabase.CreateAsset(clip, clipPath);
                AssetDatabase.CreateAsset(asset, montagePath);
                var prefab = PrefabUtility.SaveAsPrefabAsset(prefabSource, prefabPath);
                window = ScriptableObject.CreateInstance<MontageEditorWindow>();
                window.SetMontage(asset);

                window.AddVfxEvent(prefab, 0.5f);

                Assert.That(asset.Notifies.Count, Is.EqualTo(1));
                Assert.That(asset.Notifies[0].Time, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(asset.Notifies[0].Asset, Is.TypeOf<MontageVfxNotifyAsset>());
                Assert.That(((MontageVfxNotifyAsset)asset.Notifies[0].Asset).Prefab, Is.SameAs(prefab));

                window.RemoveNotify(0);
                Assert.That(asset.Notifies.Count, Is.Zero);
            }
            finally
            {
                if (window != null)
                    Object.DestroyImmediate(window);
                Object.DestroyImmediate(prefabSource);
                AssetDatabase.DeleteAsset(prefabPath);
                AssetDatabase.DeleteAsset(montagePath);
                AssetDatabase.DeleteAsset(clipPath);
            }
        }

        [Test]
        public void RetargeterCreatesExpectedHumanoidChains()
        {
            var retargeter = ScriptableObject.CreateInstance<IKRetargeterAsset>();
            try
            {
                retargeter.ResetToHumanoidDefaults();

                Assert.That(retargeter.SourceRig.FindChain("Spine"), Is.Not.Null);
                Assert.That(retargeter.TargetRig.FindChain("LeftArm").EndBone, Is.EqualTo(HumanBodyBones.LeftHand));
                Assert.That(retargeter.ChainMappings.Count, Is.EqualTo(8));
                Assert.That(retargeter.ChainMappings[2].EnableIK, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(retargeter);
            }
        }

        [Test]
        public void RetargetPoseStoresSemanticBoneOffsets()
        {
            var pose = ScriptableObject.CreateInstance<RetargetPoseAsset>();
            try
            {
                pose.SetOffset(HumanBodyBones.RightUpperArm, new Vector3(0f, 15f, -5f), new Vector3(0.01f, 0f, 0f));

                Assert.That(Quaternion.Angle(
                    pose.GetRotationOffset(HumanBodyBones.RightUpperArm),
                    Quaternion.Euler(0f, 15f, -5f)), Is.LessThan(0.001f));
                Assert.That(pose.GetPositionOffset(HumanBodyBones.RightUpperArm).x, Is.EqualTo(0.01f).Within(0.0001f));
                Assert.That(pose.GetRotationOffset(HumanBodyBones.LeftUpperArm), Is.EqualTo(Quaternion.identity));
            }
            finally
            {
                Object.DestroyImmediate(pose);
            }
        }

        [Test]
        public void TwoBoneIkReachesTarget()
        {
            var root = new GameObject("IK Root").transform;
            var joint = new GameObject("IK Joint").transform;
            var tip = new GameObject("IK Tip").transform;
            joint.SetParent(root, false);
            tip.SetParent(joint, false);
            joint.localPosition = Vector3.right;
            tip.localPosition = Vector3.right;
            var target = new Vector3(1f, 1f, 0f);

            try
            {
                var solved = TwoBoneIKSolver.Solve(
                    root,
                    joint,
                    tip,
                    target,
                    Quaternion.identity,
                    new Vector3(0f, 0f, 1f));

                Assert.That(solved, Is.True);
                Assert.That(Vector3.Distance(tip.position, target), Is.LessThan(0.002f));
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        [Test]
        public void RetargetBakerCreatesTargetSkeletonCurves()
        {
            var character = new GameObject("Bake Target");
            var animatedBone = new GameObject("AnimatedBone");
            animatedBone.transform.SetParent(character.transform, false);
            var source = new AnimationClip { name = "Source", frameRate = 30f };
            source.SetCurve("AnimatedBone", typeof(Transform), "localPosition.y",
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            AnimationClip baked = null;

            try
            {
                baked = IKRetargetBaker.BakeToMemory(null, character, source, 30f);
                var binding = EditorCurveBinding.FloatCurve(
                    "AnimatedBone", typeof(Transform), "m_LocalPosition.y");
                var curve = AnimationUtility.GetEditorCurve(baked, binding);

                Assert.That(curve, Is.Not.Null);
                Assert.That(curve.length, Is.GreaterThan(1));
                Assert.That(curve.Evaluate(1f), Is.EqualTo(1f).Within(0.01f));
            }
            finally
            {
                if (baked != null)
                    Object.DestroyImmediate(baked);
                Object.DestroyImmediate(character);
                Object.DestroyImmediate(source);
            }
        }

        private static MontageAsset CreateSingleClipMontage(AnimationClip clip)
        {
            var asset = ScriptableObject.CreateInstance<MontageAsset>();
            var serialized = new SerializedObject(asset);
            var tracks = serialized.FindProperty("slotTracks");
            tracks.arraySize = 1;
            var track = tracks.GetArrayElementAtIndex(0);
            track.FindPropertyRelative("slot").stringValue = "FullBody";
            var segments = track.FindPropertyRelative("segments");
            segments.arraySize = 1;
            var segment = segments.GetArrayElementAtIndex(0);
            segment.FindPropertyRelative("clip").objectReferenceValue = clip;
            segment.FindPropertyRelative("duration").floatValue = 1f;
            segment.FindPropertyRelative("playRate").floatValue = 1f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }
    }
}
