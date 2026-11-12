using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using ChnnAsn.Montage.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ChnnAsn.Montage.Tests
{
    public sealed class MontagePlaybackTests
    {
        private GameObject character;
        private MontagePlayer player;
        private AnimationClip clip;
        private MontageAsset asset;
        private readonly List<Object> temporaryAssets = new List<Object>();
        private static readonly MethodInfo TickMethod = typeof(MontagePlayer).GetMethod("Tick", BindingFlags.Instance | BindingFlags.NonPublic);

        [SetUp]
        public void SetUp()
        {
            clip = new AnimationClip();
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 2f, 2f));
            asset = CreateAsset("FullBody", "");
            character = new GameObject("Montage Playback Regression");
            character.AddComponent<Animator>();
            player = character.AddComponent<MontagePlayer>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(character);
            foreach (var temporary in temporaryAssets)
                Object.DestroyImmediate(temporary);
            temporaryAssets.Clear();
            Object.DestroyImmediate(clip);
        }

        private MontageAsset CreateAsset(string slot, string group)
        {
            var result = ScriptableObject.CreateInstance<MontageAsset>();
            temporaryAssets.Add(result);
            MontageTimelineEditing.AddSlot(result);
            MontageTimelineEditing.AddSegment(result, 0, clip, 0f);
            var serialized = new SerializedObject(result);
            serialized.FindProperty("slotTracks").GetArrayElementAtIndex(0).FindPropertyRelative("slot").stringValue = slot;
            serialized.FindProperty("slotGroup").stringValue = group;
            serialized.FindProperty("blendIn").floatValue = 0f;
            serialized.FindProperty("blendOut").floatValue = 0.2f;
            serialized.FindProperty("autoBlendOut").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return result;
        }

        private void Tick(float delta)
        {
            try { TickMethod.Invoke(player, new object[] { delta }); }
            catch (TargetInvocationException exception) { throw exception.InnerException ?? exception; }
        }

        private void SetAsset(Action<SerializedObject> edit)
        {
            var serialized = new SerializedObject(asset);
            edit(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private int Notify(float time, float duration = 0f, string name = "Event")
        {
            var index = MontageTimelineEditing.AddNotify(asset, time, duration > 0f, 0);
            SetAsset(serialized =>
            {
                var notify = serialized.FindProperty("notifies").GetArrayElementAtIndex(index);
                notify.FindPropertyRelative("duration").floatValue = duration;
                notify.FindPropertyRelative("eventName").stringValue = name;
            });
            return index;
        }

        [Test]
        public void PauseFreezesPositionAndBlendAndResumeContinues()
        {
            SetAsset(s => s.FindProperty("blendIn").floatValue = 1f);
            var handle = player.Play(asset);
            Tick(0.25f);
            handle.Pause();
            Tick(0.5f);
            Assert.That(handle.IsPaused, Is.True);
            Assert.That(handle.Time, Is.EqualTo(0.25f).Within(0.0001f));
            Assert.That(handle.Weight, Is.EqualTo(0.25f).Within(0.0001f));
            handle.Resume();
            Tick(0.25f);
            Assert.That(handle.Time, Is.EqualTo(0.5f).Within(0.0001f));
        }

        [Test]
        public void StartPositionEntersOverlappingStateAndSkipsEarlierPoints()
        {
            Notify(0.2f);
            Notify(0.3f, 0.8f);
            var points = 0;
            var begins = 0;
            player.NotifyTriggered += _ => points++;
            player.NotifyStateBegin += _ => begins++;
            var handle = player.Play(asset, MontagePlayOptions.Default.WithStartTime(0.5f));
            Assert.That(handle.Time, Is.EqualTo(0.5f));
            Assert.That(points, Is.Zero);
            Assert.That(begins, Is.EqualTo(1));
        }

        [Test]
        public void SeekClosesOldStateAndEntersNewStateWithoutFiringSkippedPoints()
        {
            Notify(0f, 0.5f, "Old");
            Notify(0.8f, 0f, "Skipped");
            Notify(1f, 0.5f, "New");
            var events = new List<string>();
            player.NotifyStateBegin += context => events.Add("Begin " + context.Notify.Name);
            player.NotifyStateEnd += context => events.Add("End " + context.Notify.Name);
            player.NotifyTriggered += context => events.Add(context.Notify.Name);
            var handle = player.Play(asset);
            handle.SetPosition(1.2f);
            CollectionAssert.AreEqual(new[] { "Begin Old", "End Old", "Begin New" }, events);
        }

        [Test]
        public void UnsortedNotifiesDispatchInTimeOrderWithAccurateHandlePosition()
        {
            Notify(0.7f);
            Notify(0.2f);
            Notify(0.4f);
            var times = new List<float>();
            player.NotifyTriggered += context =>
            {
                Assert.That(context.Handle.Time, Is.EqualTo(context.Time));
                times.Add(context.Time);
            };
            player.Play(asset);
            Tick(1f);
            CollectionAssert.AreEqual(new[] { 0.2f, 0.4f, 0.7f }, times);
        }

        [Test]
        public void BranchingFromNotifySkipsOldRangeAndConsumesRemainingFrame()
        {
            var section = MontageTimelineEditing.AddSection(asset, 1f);
            var targetName = asset.Sections[section].Name;
            Notify(0.2f, 0f, "Branch");
            Notify(0.4f, 0f, "MustSkip");
            Notify(1.1f, 0f, "Destination");
            var events = new List<string>();
            player.NotifyTriggered += context =>
            {
                events.Add(context.Notify.Name);
                if (context.Notify.Name == "Branch")
                    context.Handle.JumpToSection(targetName);
            };
            var handle = player.Play(asset);
            Tick(0.5f);
            CollectionAssert.AreEqual(new[] { "Branch", "Destination" }, events);
            Assert.That(handle.Time, Is.EqualTo(1.3f).Within(0.0001f));
        }

        [Test]
        public void StopFromNotifyPreventsLaterEventsInSameFrame()
        {
            Notify(0.2f);
            Notify(0.4f);
            var points = 0;
            player.NotifyTriggered += context => { points++; context.Handle.Stop(0f); };
            var handle = player.Play(asset);
            Tick(1f);
            Assert.That(points, Is.EqualTo(1));
            Assert.That(handle.IsValid, Is.False);
        }

        [Test]
        public void NotifyStateTicksOnlyItsActualActiveInterval()
        {
            Notify(0.2f, 0.3f);
            var elapsed = 0f;
            var ends = 0;
            player.NotifyStateTick += context => elapsed += context.DeltaTime;
            player.NotifyStateEnd += _ => ends++;
            player.Play(asset);
            Tick(1f);
            Assert.That(elapsed, Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(ends, Is.EqualTo(1));
        }

        [Test]
        public void SectionLoopClosesAndReopensStatesEachIteration()
        {
            var endSection = MontageTimelineEditing.AddSection(asset, 0.5f);
            SetAsset(s => s.FindProperty("sections").GetArrayElementAtIndex(0).FindPropertyRelative("nextSection").stringValue = "Default");
            Notify(0f, 1.5f);
            var begins = 0;
            var ends = 0;
            player.NotifyStateBegin += _ => begins++;
            player.NotifyStateEnd += _ => ends++;
            var handle = player.Play(asset);
            Tick(1.1f);
            Assert.That(begins, Is.EqualTo(3));
            Assert.That(ends, Is.EqualTo(2));
            handle.SetNextSection("Default", asset.Sections[endSection].Name);
            Tick(0.5f);
            Assert.That(handle.CurrentSection, Is.EqualTo(asset.Sections[endSection].Name));
            Assert.That(handle.ClearNextSection("Default"), Is.True);
        }

        [Test]
        public void EndBoundaryNotifyCanSelectNextSection()
        {
            var nextIndex = MontageTimelineEditing.AddSection(asset, 0.5f);
            SetAsset(s => s.FindProperty("sections").GetArrayElementAtIndex(0).FindPropertyRelative("nextSection").stringValue = "");
            Notify(0.5f);
            player.NotifyTriggered += context => context.Handle.SetNextSection("Default", asset.Sections[nextIndex].Name);
            var handle = player.Play(asset);
            Tick(0.6f);
            Assert.That(handle.CurrentSection, Is.EqualTo(asset.Sections[nextIndex].Name));
            Assert.That(handle.Time, Is.EqualTo(0.6f).Within(0.0001f));
        }

        [Test]
        public void AutomaticBlendOutUsesFinalSectionAndCompletesOnce()
        {
            SetAsset(s => s.FindProperty("autoBlendOut").boolValue = true);
            var callbacks = new List<string>();
            player.BlendingOut += (_, reason) => callbacks.Add("Blend " + reason);
            player.Ended += (_, reason) => callbacks.Add("End " + reason);
            player.Completed += _ => callbacks.Add("Complete");
            var handle = player.Play(asset);
            Tick(1.9f);
            Assert.That(handle.IsBlendingOut, Is.True);
            Assert.That(handle.Weight, Is.EqualTo(0.5f).Within(0.001f));
            Tick(0.2f);
            Assert.That(handle.IsValid, Is.False);
            CollectionAssert.AreEqual(new[] { "Blend Completed", "End Completed", "Complete" }, callbacks);
        }

        [Test]
        public void DisabledAutoBlendOutHoldsFinalPoseUntilExplicitStop()
        {
            var handle = player.Play(asset);
            Tick(3f);
            Assert.That(handle.IsValid, Is.True);
            Assert.That(handle.Time, Is.EqualTo(2f));
            Assert.That(handle.Weight, Is.EqualTo(1f));
            handle.Stop(0f);
            Tick(0f);
            Assert.That(handle.IsValid, Is.False);
        }

        [Test]
        public void SameGroupInterruptsDifferentSlotsButOtherGroupsRunConcurrently()
        {
            var first = CreateAsset("UpperBody", "Action");
            var second = CreateAsset("Hands", "Action");
            var independent = CreateAsset("Face", "Facial");
            var a = player.Play(first);
            var b = player.Play(independent);
            var interrupted = 0;
            player.Interrupted += _ => interrupted++;
            var c = player.Play(second);
            Assert.That(a.IsBlendingOut, Is.True);
            Assert.That(b.IsBlendingOut, Is.False);
            Assert.That(c.IsValid, Is.True);
            Assert.That(interrupted, Is.EqualTo(1));
            player.StopGroup("Facial", 0f);
            Tick(0f);
            Assert.That(b.IsValid, Is.False);
            Assert.That(c.IsValid, Is.True);
        }

        [Test]
        public void GroupPriorityRejectsLowerPriorityBeforeInterruptingAnything()
        {
            var a = CreateAsset("A", "Action");
            var b = CreateAsset("B", "Action");
            var active = player.Play(a, MontagePlayOptions.Default.WithPriority(10));
            var rejected = player.Play(b, MontagePlayOptions.Default.WithPriority(1));
            Assert.That(rejected.IsValid, Is.False);
            Assert.That(active.IsBlendingOut, Is.False);
        }

        [Test]
        public void LegacyEmptyGroupsStillAllowDifferentSlotsConcurrently()
        {
            var other = CreateAsset("UpperBody", "");
            var a = player.Play(asset);
            var b = player.Play(other);
            Assert.That(a.IsBlendingOut, Is.False);
            Assert.That(b.IsBlendingOut, Is.False);
        }

        [Test]
        public void DisableInsideStateEndSafelyDestroysGraph()
        {
            Notify(0f, 0.5f);
            player.NotifyStateEnd += _ =>
            {
                player.enabled = false;
                // Non-ExecuteAlways behaviours receive this message from Unity in Play Mode.
                typeof(MontagePlayer).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
            };
            var handle = player.Play(asset);
            Assert.DoesNotThrow(() => Tick(1f));
            Assert.That(handle.IsValid, Is.False);
        }

        [Test]
        public void StateTickCanSeekWithoutInvalidatingNotifyEnumeration()
        {
            Notify(0f, 1f);
            var once = false;
            player.NotifyStateTick += context =>
            {
                if (once)
                    return;
                once = true;
                context.Handle.SetPosition(1.5f);
            };
            var handle = player.Play(asset);
            Assert.DoesNotThrow(() => Tick(0.2f));
            Assert.That(handle.Time, Is.EqualTo(1.5f));
        }

        [Test]
        public void NonLinearBlendAndNamedFloatCurveAreSampled()
        {
            SetAsset(s =>
            {
                s.FindProperty("blendIn").floatValue = 1f;
                s.FindProperty("blendInCurve").animationCurveValue = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.5f, 0.25f), new Keyframe(1f, 1f));
                var curves = s.FindProperty("curves");
                curves.arraySize = 1;
                curves.GetArrayElementAtIndex(0).FindPropertyRelative("name").stringValue = "Damage";
                curves.GetArrayElementAtIndex(0).FindPropertyRelative("curve").animationCurveValue = AnimationCurve.Linear(0f, 0f, 2f, 10f);
            });
            var handle = player.Play(asset);
            Tick(0.5f);
            Assert.That(handle.Weight, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(handle.GetCurveValue("Damage"), Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(handle.GetCurveValue("Missing"), Is.Zero);
        }

        [Test]
        public void TrimReverseAndLoopCountShareOneSamplingFunction()
        {
            SetAsset(s =>
            {
                var segment = MontageTimelineEditing.GetSegments(s, 0).GetArrayElementAtIndex(0);
                segment.FindPropertyRelative("clipIn").floatValue = 0.5f;
                segment.FindPropertyRelative("clipOut").floatValue = 1.5f;
                segment.FindPropertyRelative("playRate").floatValue = -2f;
                segment.FindPropertyRelative("loopCount").intValue = 3;
            });
            var segment = asset.SlotTracks[0].Segments[0];
            Assert.That(segment.Duration, Is.EqualTo(1.5f));
            Assert.That(segment.GetClipTime(0f), Is.EqualTo(1.5f));
            Assert.That(segment.GetClipTime(0.25f), Is.EqualTo(1f));
            Assert.That(segment.GetClipTime(0.5f), Is.EqualTo(1.5f));
            Assert.That(segment.GetClipTime(1.5f), Is.EqualTo(0.5f));
        }

        [Test]
        public void OldSegmentWithoutNewFieldsPreservesTiming()
        {
            SetAsset(s =>
            {
                var segment = MontageTimelineEditing.GetSegments(s, 0).GetArrayElementAtIndex(0);
                segment.FindPropertyRelative("clipOut").floatValue = 0f;
                segment.FindPropertyRelative("loopCount").intValue = 0;
                segment.FindPropertyRelative("duration").floatValue = 3f;
                segment.FindPropertyRelative("loop").boolValue = true;
            });
            var segment = asset.SlotTracks[0].Segments[0];
            Assert.That(segment.Duration, Is.EqualTo(3f));
            Assert.That(segment.GetClipTime(2.5f), Is.EqualTo(0.5f));
        }

        [Test]
        public void AuthoringAddsSequentialClipsAndExtendsDefaultSection()
        {
            MontageTimelineEditing.AddSegment(asset, 0, clip, 0.5f);
            Assert.That(asset.SlotTracks[0].Segments[1].StartTime, Is.EqualTo(2f));
            Assert.That(asset.Duration, Is.EqualTo(4f));
            Assert.That(asset.Sections[0].EndTime, Is.EqualTo(4f));
            var errors = new List<string>();
            asset.Validate(errors);
            Assert.That(errors, Is.Empty);
        }

        [Test]
        public void SectionSplitRenameAndDeletePreserveLinksAndCoverage()
        {
            var second = MontageTimelineEditing.AddSection(asset, 1f);
            MontageTimelineEditing.RenameSection(asset, second, "Finish");
            Assert.That(asset.Sections[0].NextSection, Is.EqualTo("Finish"));
            Assert.That(asset.Sections[0].EndTime, Is.EqualTo(1f));
            MontageTimelineEditing.RemoveSection(asset, second);
            Assert.That(asset.Sections[0].EndTime, Is.EqualTo(2f));
            Assert.That(asset.Sections[0].NextSection, Is.Empty);
        }

        [Test]
        public void AppendedNotifyDoesNotInheritPreviousPayloadOrDuration()
        {
            Notify(0.2f, 0.4f, "First");
            var index = MontageTimelineEditing.AddNotify(asset, 1f, false, 2);
            Assert.That(asset.Notifies[index].IsState, Is.False);
            Assert.That(asset.Notifies[index].Asset, Is.Null);
            Assert.That(asset.Notifies[index].Track, Is.EqualTo(2));
            Assert.That(asset.Notifies[index].Id, Is.Not.EqualTo(asset.Notifies[0].Id));
        }

        [Test]
        public void InvalidPlaybackNumbersAreRejected()
        {
            var handle = player.Play(asset);
            Assert.That(handle.SetPlayRate(float.NaN), Is.False);
            Assert.That(handle.SetPlayRate(float.PositiveInfinity), Is.False);
            Assert.That(handle.SetPosition(float.NaN), Is.False);
            Assert.That(handle.SetPosition(float.NegativeInfinity), Is.False);
        }

        [TestCase("Impact", false, "Impact")]
        [TestCase("", false, "Notify")]
        [TestCase("", true, "Payload Event")]
        public void DemoCallbackHandlesNotifyNamesWithOptionalPayload(string eventName, bool withPayload, string expectedName)
        {
            var demoType = Type.GetType("ChnnAsn.Montage.Demo.MontageDemoController, Assembly-CSharp");
            if (demoType == null)
                Assert.Ignore("The demo integration test requires the development project's MontageDemo scripts.");

            var demo = character.AddComponent(demoType);
            var index = Notify(0.2f, 0f, eventName);
            if (withPayload)
            {
                var payload = ScriptableObject.CreateInstance<MontageAudioNotifyAsset>();
                payload.name = "Payload Event";
                temporaryAssets.Add(payload);
                SetAsset(s => s.FindProperty("notifies").GetArrayElementAtIndex(index)
                    .FindPropertyRelative("notify").objectReferenceValue = payload);
            }

            var callback = (Action<MontageNotifyContext>)Delegate.CreateDelegate(
                typeof(Action<MontageNotifyContext>), demo, "OnNotifyTriggered");
            player.NotifyTriggered += callback;
            try
            {
                var handle = player.Play(asset);
                Assert.DoesNotThrow(() => Tick(0.3f));
                Assert.That(handle.Time, Is.EqualTo(0.3f).Within(0.0001f));
                Assert.That(demoType.GetField("eventText", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(demo),
                    Is.EqualTo("Notify dispatched: " + expectedName));
            }
            finally
            {
                player.NotifyTriggered -= callback;
            }
        }

        [Test]
        public void DemoCallbackHandlesSeekingOutsideNamedSections()
        {
            var demoType = Type.GetType("ChnnAsn.Montage.Demo.MontageDemoController, Assembly-CSharp");
            if (demoType == null)
                Assert.Ignore("The demo integration test requires the development project's MontageDemo scripts.");
            var demo = character.AddComponent(demoType);
            var callback = (Action<MontageHandle, string, string>)Delegate.CreateDelegate(
                typeof(Action<MontageHandle, string, string>), demo, "OnSectionChanged");
            player.SectionChanged += callback;
            try
            {
                var handle = player.Play(asset);
                Assert.DoesNotThrow(() => handle.SetPosition(asset.Duration));
                Assert.That(demoType.GetField("status", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(demo),
                    Is.EqualTo("PLAYING"));
            }
            finally
            {
                player.SectionChanged -= callback;
            }
        }

        [Test]
        public void AdjacentSectionsDispatchSharedBoundaryOnceAndKeepSpanningState()
        {
            MontageTimelineEditing.AddSection(asset, 1f);
            Notify(0.5f, 1f);
            Notify(1f);
            var points = 0;
            var begins = 0;
            var ends = 0;
            player.NotifyTriggered += _ => points++;
            player.NotifyStateBegin += _ => begins++;
            player.NotifyStateEnd += _ => ends++;
            player.Play(asset);
            Tick(1.2f);
            Assert.That(points, Is.EqualTo(1));
            Assert.That(begins, Is.EqualTo(1));
            Assert.That(ends, Is.Zero);
        }

        [Test]
        public void StartedCallbackSeekingDoesNotDispatchDestinationTwice()
        {
            Notify(1f);
            var points = 0;
            player.NotifyTriggered += _ => points++;
            player.Started += handle => handle.SetPosition(1f);
            player.Play(asset);
            Assert.That(points, Is.EqualTo(1));
        }

        [Test]
        public void ImmediateStopDuringAutomaticFadeCannotRestoreWeight()
        {
            SetAsset(s => s.FindProperty("autoBlendOut").boolValue = true);
            var handle = player.Play(asset);
            Tick(1.85f);
            handle.Stop(0f);
            Tick(0.01f);
            Assert.That(handle.IsValid, Is.False);
        }

        [Test]
        public void StoppingPausedFadeResumesCleanup()
        {
            var handle = player.Play(asset);
            handle.Stop(0.2f);
            handle.Pause();
            handle.Stop(0.2f);
            Tick(0.3f);
            Assert.That(handle.IsValid, Is.False);
        }

        [Test]
        public void PlayWithoutOptionsUsesAssetDefaultRate()
        {
            SetAsset(s => s.FindProperty("defaultPlayRate").floatValue = 2f);
            var handle = player.Play(asset);
            Tick(0.2f);
            Assert.That(handle.Time, Is.EqualTo(0.4f).Within(0.0001f));
        }

        [Test]
        public void EditorLayoutSizesSurviveWindowSerialization()
        {
            var window = ScriptableObject.CreateInstance<MontageEditorWindow>();
            var restored = ScriptableObject.CreateInstance<MontageEditorWindow>();
            try
            {
                var serialized = new SerializedObject(window);
                serialized.FindProperty("previewPaneHeight").floatValue = 175f;
                serialized.FindProperty("detailsPanelWidth").floatValue = 420f;
                serialized.FindProperty("showDetails").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(window), restored);
                var readBack = new SerializedObject(restored);
                Assert.That(readBack.FindProperty("previewPaneHeight").floatValue, Is.EqualTo(175f));
                Assert.That(readBack.FindProperty("detailsPanelWidth").floatValue, Is.EqualTo(420f));
                Assert.That(readBack.FindProperty("showDetails").boolValue, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(window);
                Object.DestroyImmediate(restored);
            }
        }

        [Test]
        public void StartingAtEndDoesNotRepeatTerminalNotifyOnFirstTick()
        {
            Notify(2f);
            var points = 0;
            player.NotifyTriggered += _ => points++;
            player.Play(asset, MontagePlayOptions.Default.WithStartTime(2f));
            Tick(0.1f);
            Assert.That(points, Is.EqualTo(1));
        }

        [Test]
        public void EarlyFadeCompletionSkipsEventsBeyondItsLifetime()
        {
            SetAsset(s =>
            {
                s.FindProperty("autoBlendOut").boolValue = true;
                s.FindProperty("blendOutTriggerTime").floatValue = 1f;
            });
            Notify(1.5f);
            var points = 0;
            player.NotifyTriggered += _ => points++;
            var handle = player.Play(asset);
            Tick(1.8f);
            Assert.That(points, Is.Zero);
            Assert.That(handle.IsValid, Is.False);
        }

        [UnityTest]
        public IEnumerator WorkspaceSplittersChangeDimensionsDuringMouseDrag()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("The splitter interaction test requires a graphics-enabled Unity Editor.");
            var window = ScriptableObject.CreateInstance<MontageEditorWindow>();
            var driver = ScriptableObject.CreateInstance<MontageSplitterTestWindow>();
            try
            {
                window.position = new Rect(0f, 0f, 720f, 700f);
                driver.Target = window;
                driver.position = new Rect(50f, 50f, 500f, 300f);
                driver.Show();
                driver.Focus();
                yield return null;
                driver.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(197f, 100f) });
                yield return null;
                driver.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = new Vector2(237f, 100f) });
                yield return null;
                driver.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = new Vector2(237f, 100f) });
                yield return null;
                if (driver.InputTrace.Count == 0)
                    Assert.Ignore("This editor host did not deliver native mouse events to the test window.");
                Assert.That(new SerializedObject(window).FindProperty("detailsPanelWidth").floatValue, Is.EqualTo(260f).Within(0.01f), string.Join("\n", driver.InputTrace));

                driver.Horizontal = true;
                driver.Repaint();
                yield return null;
                driver.SendEvent(new Event { type = EventType.MouseDown, button = 0, mousePosition = new Vector2(250f, 203f) });
                yield return null;
                driver.SendEvent(new Event { type = EventType.MouseDrag, button = 0, mousePosition = new Vector2(250f, 243f) });
                yield return null;
                driver.SendEvent(new Event { type = EventType.MouseUp, button = 0, mousePosition = new Vector2(250f, 243f) });
                yield return null;
                Assert.That(new SerializedObject(window).FindProperty("previewPaneHeight").floatValue, Is.EqualTo(240f).Within(0.01f));
            }
            finally
            {
                driver.Close();
                Object.DestroyImmediate(window);
            }
        }

        [UnityTest]
        public IEnumerator NarrowEditorWindowRendersWithoutLayoutErrors()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("The window layout test requires a graphics-enabled Unity Editor.");
            var window = ScriptableObject.CreateInstance<MontageEditorWindow>();
            try
            {
                window.SetMontage(asset);
                window.position = new Rect(0f, 0f, 580f, 440f);
                window.Show();
                window.Repaint();
                yield return null;
                window.SendEvent(new Event { type = EventType.MouseMove, mousePosition = new Vector2(200f, 200f) });
                yield return null;
                if (window.PreviewPanelRect.width <= 0f || window.DetailsPanelRect.width <= 0f)
                    Assert.Ignore("This editor host has not painted the test window.");
                Assert.That(window.DetailsPanelRect.xMin, Is.GreaterThanOrEqualTo(window.PreviewPanelRect.xMax));
                Assert.That(window.DetailsPanelRect.yMin, Is.EqualTo(window.PreviewPanelRect.yMin).Within(5f));
                Assert.That(window.DetailsPanelRect.yMin, Is.GreaterThan(40f));
                Assert.That(window.DetailsPanelRect.xMax, Is.LessThanOrEqualTo(window.position.width + 1f));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                window.Close();
            }
        }
    }
}
