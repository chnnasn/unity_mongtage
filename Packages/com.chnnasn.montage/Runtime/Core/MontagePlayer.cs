using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ChnnAsn.Montage
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class MontagePlayer : MonoBehaviour
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

        public event Action<MontageHandle> Started;
        public event Action<MontageHandle, string, string> SectionChanged;
        public event Action<MontageNotifyContext> NotifyTriggered;
        public event Action<MontageHandle, MontageStopReason> Ended;

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

            var deltaTime = Time.deltaTime;
            for (var i = instances.Count - 1; i >= 0; i--)
            {
                var instance = instances[i];
                UpdateInstance(instance, deltaTime);
                if (instance.FadingOut && instance.Weight <= 0f)
                    RemoveInstanceAt(i);
            }

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
            return Play(montage, MontagePlayOptions.Default);
        }

        public MontageHandle Play(MontageAsset montage, MontagePlayOptions options)
        {
            if (montage == null)
                throw new ArgumentNullException(nameof(montage));
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
                if (!SharesSlot(current.Asset, montage) || current.FadingOut)
                    continue;
                if (current.Priority > priority)
                    return default;
            }

            for (var i = 0; i < instances.Count; i++)
            {
                var current = instances[i];
                if (SharesSlot(current.Asset, montage) && !current.FadingOut)
                    BeginFadeOut(current, current.Asset.BlendOut, MontageStopReason.Interrupted);
            }

            var blendIn = options.BlendIn >= 0f ? options.BlendIn : montage.BlendIn;
            var instance = new MontageRuntimeInstance
            {
                Id = nextInstanceId++,
                Asset = montage,
                Time = startSection != null ? startSection.StartTime : 0f,
                Weight = blendIn <= 0f ? 1f : 0f,
                PlayRate = options.PlayRate > 0f ? options.PlayRate : montage.DefaultPlayRate,
                Priority = priority,
                CurrentSection = startSection != null ? startSection.Name : FindInitialSection(montage),
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
            DispatchAtCurrentTime(instance, 0f);
            UpdateSlotWeights();
            var handle = new MontageHandle(this, instance.Id);
            Started?.Invoke(handle);
            return handle;
        }

        public void StopAll(float blendOut = 0.2f)
        {
            for (var i = 0; i < instances.Count; i++)
                BeginFadeOut(instances[i], blendOut, MontageStopReason.Stopped);
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
            if (instance == null || playRate <= 0f)
                return false;
            instance.PlayRate = playRate;
            return true;
        }

        internal bool JumpToSection(int id, string sectionName)
        {
            var instance = FindInstance(id);
            var section = instance?.Asset.FindSection(sectionName);
            if (instance == null || section == null)
                return false;

            CloseActiveNotifies(instance, 0f);
            var previous = instance.CurrentSection;
            instance.CurrentSection = section.Name;
            instance.Time = section.StartTime;
            instance.HoldTime = false;
            UpdateTrackPlayables(instance);
            DispatchAtCurrentTime(instance, 0f);
            SectionChanged?.Invoke(new MontageHandle(this, id), previous, section.Name);
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

        private void UpdateInstance(MontageRuntimeInstance instance, float deltaTime)
        {
            if (!instance.HoldTime)
            {
                if (instance.FadingOut)
                    instance.Time = Mathf.Min(instance.Asset.Duration, instance.Time + deltaTime * instance.PlayRate);
                else
                    AdvanceTime(instance, deltaTime * instance.PlayRate, deltaTime);
            }

            if (instance.FadingIn)
            {
                instance.FadeElapsed += deltaTime;
                instance.Weight = instance.FadeDuration <= 0f
                    ? 1f
                    : Mathf.Clamp01(instance.FadeElapsed / instance.FadeDuration);
                if (instance.Weight >= 1f)
                    instance.FadingIn = false;
            }
            else if (instance.FadingOut)
            {
                instance.FadeElapsed += deltaTime;
                var t = instance.FadeDuration <= 0f ? 1f : Mathf.Clamp01(instance.FadeElapsed / instance.FadeDuration);
                instance.Weight = Mathf.Lerp(instance.FadeStartWeight, 0f, t);
            }

            TickActiveNotifies(instance, deltaTime);
            UpdateTrackPlayables(instance);
        }

        private void AdvanceTime(MontageRuntimeInstance instance, float amount, float frameDelta)
        {
            var remaining = amount;
            for (var guard = 0; guard < 32 && remaining > 0f; guard++)
            {
                var section = instance.Asset.FindSection(instance.CurrentSection);
                var limit = section != null ? section.EndTime : instance.Asset.Duration;
                var target = instance.Time + remaining;
                if (target < limit)
                {
                    DispatchRange(instance, instance.Time, target, frameDelta);
                    instance.Time = target;
                    return;
                }

                DispatchRange(instance, instance.Time, limit, frameDelta);
                remaining = target - limit;
                instance.Time = limit;

                if (section == null)
                {
                    BeginFadeOut(instance, instance.Asset.BlendOut, MontageStopReason.Completed);
                    instance.HoldTime = true;
                    return;
                }

                var nextName = instance.NextSections.TryGetValue(section.Name, out var overridden)
                    ? overridden
                    : section.NextSection;
                var next = instance.Asset.FindSection(nextName);
                if (next == null)
                {
                    BeginFadeOut(instance, instance.Asset.BlendOut, MontageStopReason.Completed);
                    instance.HoldTime = true;
                    return;
                }

                var previousName = instance.CurrentSection;
                instance.CurrentSection = next.Name;
                instance.Time = next.StartTime;
                DispatchAtCurrentTime(instance, frameDelta);
                SectionChanged?.Invoke(new MontageHandle(this, instance.Id), previousName, next.Name);
            }

            if (remaining > 0f)
            {
                Debug.LogError($"Montage '{instance.Asset.name}' exceeded 32 section transitions in one frame.", instance.Asset);
                BeginFadeOut(instance, 0f, MontageStopReason.Stopped);
            }
        }

        private void DispatchRange(MontageRuntimeInstance instance, float from, float to, float deltaTime)
        {
            if (to < from)
                return;
            var handle = new MontageHandle(this, instance.Id);
            for (var i = 0; i < instance.Asset.Notifies.Count; i++)
            {
                var notify = instance.Asset.Notifies[i];
                if (notify == null || notify.Asset == null)
                    continue;

                if (!notify.IsState)
                {
                    if (notify.Time > from && notify.Time <= to)
                    {
                        var context = new MontageNotifyContext(this, instance.Asset, handle, notify, notify.Time, deltaTime);
                        notify.Asset.OnNotify(context);
                        NotifyTriggered?.Invoke(context);
                    }
                    continue;
                }

                if (notify.Time > from && notify.Time <= to && instance.ActiveNotifies.Add(notify))
                    notify.Asset.OnBegin(new MontageNotifyContext(this, instance.Asset, handle, notify, notify.Time, deltaTime));
                if (notify.EndTime > from && notify.EndTime <= to && instance.ActiveNotifies.Remove(notify))
                    notify.Asset.OnEnd(new MontageNotifyContext(this, instance.Asset, handle, notify, notify.EndTime, deltaTime));
            }
        }

        private void DispatchAtCurrentTime(MontageRuntimeInstance instance, float deltaTime)
        {
            var handle = new MontageHandle(this, instance.Id);
            for (var i = 0; i < instance.Asset.Notifies.Count; i++)
            {
                var notify = instance.Asset.Notifies[i];
                if (notify == null || notify.Asset == null || !Mathf.Approximately(notify.Time, instance.Time))
                    continue;
                var context = new MontageNotifyContext(this, instance.Asset, handle, notify, instance.Time, deltaTime);
                if (notify.IsState)
                {
                    if (instance.ActiveNotifies.Add(notify))
                        notify.Asset.OnBegin(context);
                }
                else
                {
                    notify.Asset.OnNotify(context);
                    NotifyTriggered?.Invoke(context);
                }
            }
        }

        private void TickActiveNotifies(MontageRuntimeInstance instance, float deltaTime)
        {
            if (instance.ActiveNotifies.Count == 0)
                return;
            var handle = new MontageHandle(this, instance.Id);
            foreach (var notify in instance.ActiveNotifies)
                notify.Asset?.OnTick(new MontageNotifyContext(this, instance.Asset, handle, notify, instance.Time, deltaTime));
        }

        private void CloseActiveNotifies(MontageRuntimeInstance instance, float deltaTime)
        {
            if (instance.ActiveNotifies.Count == 0)
                return;
            var handle = new MontageHandle(this, instance.Id);
            foreach (var notify in instance.ActiveNotifies)
                notify.Asset?.OnEnd(new MontageNotifyContext(this, instance.Asset, handle, notify, instance.Time, deltaTime));
            instance.ActiveNotifies.Clear();
        }

        private void BeginFadeOut(MontageRuntimeInstance instance, float duration, MontageStopReason reason)
        {
            if (instance.FadingOut)
                return;
            instance.FadingIn = false;
            instance.FadingOut = true;
            instance.FadeElapsed = 0f;
            instance.FadeDuration = Mathf.Max(0f, duration);
            instance.FadeStartWeight = instance.Weight;
            instance.StopReason = reason;
            CloseActiveNotifies(instance, 0f);
            if (instance.FadeDuration <= 0f)
                instance.Weight = 0f;
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

                var localTime = Mathf.Max(0f, instance.Time - segment.StartTime) * segment.PlayRate;
                var available = Mathf.Max(0f, segment.Clip.length - segment.ClipIn);
                if (segment.Loop && available > 0f)
                    localTime %= available;
                else
                    localTime = Mathf.Min(localTime, available);
                runtime.ClipPlayable.SetTime(segment.ClipIn + localTime);
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

        private void RemoveInstanceAt(int index)
        {
            var instance = instances[index];
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
            instances.RemoveAt(index);
            Ended?.Invoke(new MontageHandle(this, instance.Id), instance.StopReason);
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
            if (!initialized)
                return;
            for (var i = instances.Count - 1; i >= 0; i--)
            {
                instances[i].StopReason = reason;
                instances[i].Weight = 0f;
                RemoveInstanceAt(i);
            }
            slots.Clear();
            if (graph.IsValid())
                graph.Destroy();
            initialized = false;
        }

        private static bool SharesSlot(MontageAsset left, MontageAsset right)
        {
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

        private static string FindInitialSection(MontageAsset montage)
        {
            for (var i = 0; i < montage.Sections.Count; i++)
            {
                var section = montage.Sections[i];
                if (section != null && section.StartTime <= 0f && section.EndTime > 0f)
                    return section.Name;
            }
            return null;
        }
    }
}
