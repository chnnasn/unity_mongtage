using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ChnnAsn.Montage
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed partial class MontagePlayer : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private bool logValidationErrors = true;

        private readonly List<MontageRuntimeInstance> instances = new List<MontageRuntimeInstance>();
        private readonly Dictionary<string, MontageSlotRuntime> slots =
            new Dictionary<string, MontageSlotRuntime>(StringComparer.Ordinal);
        private readonly List<string> validationErrors = new List<string>();

        private PlayableGraph graph;
        private AnimationLayerMixerPlayable layerMixer;
        private int nextInstanceId = 1;
        private bool initialized;
        private bool shuttingDown;
        private readonly List<MontageRuntimeInstance> updateSnapshot = new List<MontageRuntimeInstance>();

        public event Action<MontageHandle> Started;
        public event Action<MontageHandle, string, string> SectionChanged;
        public event Action<MontageNotifyContext> NotifyTriggered;
        public event Action<MontageHandle, MontageStopReason> Ended;
        public event Action<MontageHandle, MontageStopReason> BlendingOut;
        public event Action<MontageHandle> Completed;
        public event Action<MontageHandle> Interrupted;
        public event Action<MontageNotifyContext> NotifyStateBegin;
        public event Action<MontageNotifyContext> NotifyStateTick;
        public event Action<MontageNotifyContext> NotifyStateEnd;

        public Animator Animator => animator;

        private void Reset()
        {
            animator = GetComponent<Animator>();
        }

        private void OnEnable()
        {
            Initialize();
        }

        private void Update()
        {
            if (!initialized)
                return;

            Tick(Time.deltaTime);
        }

        // Kept separate from Update for deterministic editor regression tests.
        private void Tick(float deltaTime)
        {
            if (!initialized || deltaTime < 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                return;
            updateSnapshot.Clear();
            updateSnapshot.AddRange(instances);
            for (var i = 0; i < updateSnapshot.Count; i++)
            {
                var instance = updateSnapshot[i];
                if (instance.Removed)
                    continue;
                UpdateInstance(instance, deltaTime);
                if (!instance.Removed && instance.FadingOut && instance.Weight <= 0f)
                    RemoveInstance(instance);
            }
            if (initialized)
                UpdateSlotWeights();
        }

        private void OnDisable()
        {
            Shutdown(MontageStopReason.PlayerDisabled);
        }

        private void OnDestroy()
        {
            Shutdown(MontageStopReason.PlayerDisabled);
        }

        public void Initialize()
        {
            if (initialized)
                return;
            if (animator == null)
                animator = GetComponent<Animator>();
            if (animator == null)
                throw new InvalidOperationException("MontagePlayer requires an Animator.");

            graph = PlayableGraph.Create($"Montage: {name}");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            layerMixer = AnimationLayerMixerPlayable.Create(graph, 1);

            Playable basePlayable;
            if (animator.runtimeAnimatorController != null)
                basePlayable = AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController);
            else
                basePlayable = AnimationMixerPlayable.Create(graph, 0);

            graph.Connect(basePlayable, 0, layerMixer, 0);
            layerMixer.SetInputWeight(0, 1f);
            var output = AnimationPlayableOutput.Create(graph, "Montage Output", animator);
            output.SetSourcePlayable(layerMixer);
            graph.Play();
            initialized = true;
        }

        public MontageHandle Play(MontageAsset montage)
        {
            var options = MontagePlayOptions.Default;
            if (montage != null)
                options.PlayRate = montage.DefaultPlayRate;
            return Play(montage, options);
        }

        public MontageHandle Play(MontageAsset montage, MontagePlayOptions options)
        {
            if (montage == null)
                throw new ArgumentNullException(nameof(montage));
            if (shuttingDown || !isActiveAndEnabled)
                return default;
            Initialize();

            validationErrors.Clear();
            montage.Validate(validationErrors);
            if (validationErrors.Count > 0)
            {
                if (logValidationErrors)
                    Debug.LogError($"Cannot play montage '{montage.name}':\n- {string.Join("\n- ", validationErrors)}", montage);
                return default;
            }

            var startSection = string.IsNullOrEmpty(options.StartSection)
                ? null
                : montage.FindSection(options.StartSection);
            if (!string.IsNullOrEmpty(options.StartSection) && startSection == null)
            {
                Debug.LogError($"Montage '{montage.name}' has no section named '{options.StartSection}'.", montage);
                return default;
            }

            var priority = options.Priority;
            for (var i = 0; i < instances.Count; i++)
            {
                var current = instances[i];
                if (!ConflictsWith(current.Asset, montage) || current.FadingOut)
                    continue;
                if (current.Priority > priority)
                    return default;
            }

            var previousInstances = instances.ToArray();
            var startTime = startSection != null ? startSection.StartTime : Mathf.Clamp(options.StartTime, 0f, montage.Duration);
            if (float.IsNaN(startTime) || float.IsInfinity(startTime) ||
                float.IsNaN(options.BlendIn) || float.IsInfinity(options.BlendIn) ||
                float.IsNaN(options.PlayRate) || float.IsInfinity(options.PlayRate))
                return default;
            var blendIn = options.BlendIn >= 0f ? options.BlendIn : montage.BlendIn;
            var instance = new MontageRuntimeInstance
            {
                Id = nextInstanceId++,
                Asset = montage,
                Time = startTime,
                Weight = blendIn <= 0f ? 1f : 0f,
                PlayRate = options.PlayRate > 0f ? options.PlayRate : montage.DefaultPlayRate,
                Priority = priority,
                CurrentSection = startSection != null ? startSection.Name : montage.FindSectionAtTime(startTime)?.Name,
                FadeDuration = blendIn,
                FadingIn = blendIn > 0f
            };

            for (var i = 0; i < montage.SlotTracks.Count; i++)
            {
                var track = montage.SlotTracks[i];
                if (track == null)
                    continue;
                var runtime = new MontageTrackRuntime { Owner = instance, Track = track };
                runtime.Slot = GetOrCreateSlot(track);
                runtime.InputPort = runtime.Slot.Allocate(runtime);
                instance.Tracks.Add(runtime);
            }

            instances.Add(instance);
            UpdateTrackPlayables(instance);
            var handle = new MontageHandle(this, instance.Id);
            for (var i = 0; i < previousInstances.Length; i++)
            {
                var current = previousInstances[i];
                if (!current.Removed && ConflictsWith(current.Asset, montage))
                    BeginFadeOut(current, current.Asset.BlendOut, MontageStopReason.Interrupted);
            }
            if (instance.Removed)
                return default;
            var startRevision = instance.PositionRevision;
            Started?.Invoke(handle);
            if (CanDispatch(instance) && instance.PositionRevision == startRevision)
                DispatchAtCurrentTime(instance, 0f);
            if (initialized)
                UpdateSlotWeights();
            return handle;
        }

        public void StopAll(float blendOut = 0.2f)
        {
            foreach (var instance in instances.ToArray())
                BeginFadeOut(instance, blendOut, MontageStopReason.Stopped);
        }

        public void StopGroup(string group, float blendOut = -1f)
        {
            foreach (var instance in instances.ToArray())
                if (string.Equals(instance.Asset.SlotGroup, group, StringComparison.Ordinal))
                    BeginFadeOut(instance, blendOut >= 0f ? blendOut : instance.Asset.BlendOut, MontageStopReason.Stopped);
        }

        public bool IsPlaying(MontageAsset montage)
        {
            for (var i = 0; i < instances.Count; i++)
                if (instances[i].Asset == montage)
                    return true;
            return false;
        }

        internal bool Contains(int id) => FindInstance(id) != null;
        internal bool IsPlaying(int id) => FindInstance(id) != null;
        internal float GetTime(int id) => FindInstance(id)?.Time ?? 0f;
        internal float GetWeight(int id) => FindInstance(id)?.Weight ?? 0f;
        internal string GetCurrentSection(int id) => FindInstance(id)?.CurrentSection;
        internal bool GetPaused(int id) => FindInstance(id)?.Paused ?? false;
        internal bool GetBlendingOut(int id) => FindInstance(id)?.FadingOut ?? false;
        internal float GetPlayRate(int id) => FindInstance(id)?.PlayRate ?? 0f;
        internal float GetCurveValue(int id, string name)
        {
            var instance = FindInstance(id);
            return instance != null ? instance.Asset.EvaluateCurve(name, instance.Time) : 0f;
        }

        internal bool SetPaused(int id, bool paused)
        {
            var instance = FindInstance(id);
            if (instance == null)
                return false;
            instance.Paused = paused;
            return true;
        }

        internal bool SetPosition(int id, float time)
        {
            var instance = FindInstance(id);
            if (instance == null || instance.FadingOut || float.IsNaN(time) || float.IsInfinity(time))
                return false;
            time = Mathf.Clamp(time, 0f, instance.Asset.Duration);
            MoveTo(instance, time, instance.Asset.FindSectionAtTime(time)?.Name);
            return true;
        }

        internal bool ClearNextSection(int id, string from)
        {
            var instance = FindInstance(id);
            return instance != null && from != null && instance.NextSections.Remove(from);
        }

        internal bool Stop(int id, float blendOut)
        {
            var instance = FindInstance(id);
            if (instance == null)
                return false;
            BeginFadeOut(instance, blendOut >= 0f ? blendOut : instance.Asset.BlendOut, MontageStopReason.Stopped);
            return true;
        }

        internal bool SetPlayRate(int id, float playRate)
        {
            var instance = FindInstance(id);
            if (instance == null || playRate <= 0f || float.IsNaN(playRate) || float.IsInfinity(playRate))
                return false;
            instance.PlayRate = playRate;
            return true;
        }

        internal bool JumpToSection(int id, string sectionName)
        {
            var instance = FindInstance(id);
            var section = instance?.Asset.FindSection(sectionName);
            if (instance == null || section == null || instance.FadingOut)
                return false;
            MoveTo(instance, section.StartTime, section.Name);
            return true;
        }

        internal bool SetNextSection(int id, string from, string to)
        {
            var instance = FindInstance(id);
            if (instance == null || instance.Asset.FindSection(from) == null)
                return false;
            if (!string.IsNullOrEmpty(to) && instance.Asset.FindSection(to) == null)
                return false;
            instance.NextSections[from] = to;
            return true;
        }

        private void UpdateTrackPlayables(MontageRuntimeInstance instance)
        {
            for (var i = 0; i < instance.Tracks.Count; i++)
            {
                var runtime = instance.Tracks[i];
                var segment = runtime.Track.FindSegment(instance.Time);
                if (!ReferenceEquals(runtime.Segment, segment))
                    ReplaceClipPlayable(runtime, segment);
                if (!runtime.HasPlayable || segment == null)
                    continue;

                runtime.ClipPlayable.SetTime(segment.GetClipTime(instance.Time));
            }
        }

        private void ReplaceClipPlayable(MontageTrackRuntime runtime, MontageSegment segment)
        {
            if (runtime.HasPlayable)
            {
                graph.Disconnect(runtime.Slot.Mixer, runtime.InputPort);
                graph.DestroyPlayable(runtime.ClipPlayable);
                runtime.ClipPlayable = default;
            }
            runtime.Segment = segment;
            if (segment == null || segment.Clip == null)
                return;

            var playable = AnimationClipPlayable.Create(graph, segment.Clip);
            playable.SetApplyFootIK(segment.ApplyFootIK);
            playable.SetApplyPlayableIK(segment.ApplyPlayableIK);
            playable.SetSpeed(0d);
            graph.Connect(playable, 0, runtime.Slot.Mixer, runtime.InputPort);
            runtime.ClipPlayable = playable;
        }

        private MontageSlotRuntime GetOrCreateSlot(MontageSlotTrack track)
        {
            if (slots.TryGetValue(track.Slot, out var slot))
            {
                if (slot.AvatarMask != track.AvatarMask || slot.Additive != track.Additive)
                    Debug.LogWarning($"Slot '{track.Slot}' was already created with different mask or additive settings. The first definition wins.", this);
                return slot;
            }

            var layerPort = layerMixer.GetInputCount();
            layerMixer.SetInputCount(layerPort + 1);
            var mixer = AnimationMixerPlayable.Create(graph, 0);
            graph.Connect(mixer, 0, layerMixer, layerPort);
            layerMixer.SetInputWeight(layerPort, 0f);
            layerMixer.SetLayerAdditive((uint)layerPort, track.Additive);
            if (track.AvatarMask != null)
                layerMixer.SetLayerMaskFromAvatarMask((uint)layerPort, track.AvatarMask);

            slot = new MontageSlotRuntime(track.Slot, track.AvatarMask, track.Additive, layerPort, mixer);
            slots.Add(track.Slot, slot);
            return slot;
        }

        private void UpdateSlotWeights()
        {
            foreach (var pair in slots)
            {
                var slot = pair.Value;
                var total = 0f;
                foreach (var trackPair in slot.Tracks)
                {
                    var track = trackPair.Value;
                    if (track.HasPlayable)
                        total += Mathf.Max(0f, track.Owner.Weight);
                }

                layerMixer.SetInputWeight(slot.LayerPort, Mathf.Clamp01(total));
                foreach (var trackPair in slot.Tracks)
                {
                    var track = trackPair.Value;
                    var weight = total > 0f && track.HasPlayable
                        ? Mathf.Max(0f, track.Owner.Weight) / total
                        : 0f;
                    slot.Mixer.SetInputWeight(trackPair.Key, weight);
                }
            }
        }

        private void RemoveInstance(MontageRuntimeInstance instance)
        {
            if (instance.Removed)
                return;
            instance.Removed = true;
            instances.Remove(instance);
            CloseActiveNotifies(instance, 0f);
            for (var i = 0; i < instance.Tracks.Count; i++)
            {
                var track = instance.Tracks[i];
                if (track.HasPlayable)
                {
                    graph.Disconnect(track.Slot.Mixer, track.InputPort);
                    graph.DestroyPlayable(track.ClipPlayable);
                }
                track.Slot.Release(track.InputPort);
            }
            var handle = new MontageHandle(this, instance.Id);
            Ended?.Invoke(handle, instance.StopReason);
            if (instance.StopReason == MontageStopReason.Completed)
                Completed?.Invoke(handle);
        }

        private MontageRuntimeInstance FindInstance(int id)
        {
            for (var i = 0; i < instances.Count; i++)
                if (instances[i].Id == id)
                    return instances[i];
            return null;
        }

        private void Shutdown(MontageStopReason reason)
        {
            if (!initialized || shuttingDown)
                return;
            shuttingDown = true;
            foreach (var instance in instances.ToArray())
            {
                instance.StopReason = reason;
                instance.Weight = 0f;
                RemoveInstance(instance);
            }
            slots.Clear();
            if (graph.IsValid())
                graph.Destroy();
            initialized = false;
            shuttingDown = false;
        }

        private static bool ConflictsWith(MontageAsset left, MontageAsset right)
        {
            if (!string.IsNullOrEmpty(left.SlotGroup) && string.Equals(left.SlotGroup, right.SlotGroup, StringComparison.Ordinal))
                return true;
            for (var i = 0; i < left.SlotTracks.Count; i++)
            {
                var leftTrack = left.SlotTracks[i];
                if (leftTrack == null)
                    continue;
                for (var j = 0; j < right.SlotTracks.Count; j++)
                {
                    var rightTrack = right.SlotTracks[j];
                    if (rightTrack != null && string.Equals(leftTrack.Slot, rightTrack.Slot, StringComparison.Ordinal))
                        return true;
                }
            }
            return false;
        }

    }
}
