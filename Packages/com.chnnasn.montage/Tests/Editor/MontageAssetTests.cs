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
