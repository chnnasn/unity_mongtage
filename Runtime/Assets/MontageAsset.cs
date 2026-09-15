using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChnnAsn.Montage
{
    [CreateAssetMenu(fileName = "New Montage", menuName = "Montage/Montage Asset")]
    public sealed class MontageAsset : ScriptableObject
    {
        internal const int CurrentSerializedVersion = 1;

        [SerializeField, HideInInspector] private int serializedVersion = CurrentSerializedVersion;
        [SerializeField, Min(0f)] private float blendIn = 0.15f;
        [SerializeField, Min(0f)] private float blendOut = 0.15f;
        [SerializeField, Min(0.001f)] private float defaultPlayRate = 1f;
        [SerializeField] private List<MontageSection> sections = new List<MontageSection>();
        [SerializeField] private List<MontageSlotTrack> slotTracks = new List<MontageSlotTrack>();
        [SerializeField] private List<MontageNotify> notifies = new List<MontageNotify>();

        public int SerializedVersion => serializedVersion;
        public float BlendIn => blendIn;
        public float BlendOut => blendOut;
        public float DefaultPlayRate => defaultPlayRate;
        public IReadOnlyList<MontageSection> Sections => sections;
        public IReadOnlyList<MontageSlotTrack> SlotTracks => slotTracks;
        public IReadOnlyList<MontageNotify> Notifies => notifies;

        public float Duration
        {
            get
            {
                var result = 0f;
                for (var i = 0; i < slotTracks.Count; i++)
                {
                    var track = slotTracks[i];
                    if (track == null)
                        continue;
                    for (var j = 0; j < track.Segments.Count; j++)
                    {
                        var segment = track.Segments[j];
                        if (segment != null)
                            result = Mathf.Max(result, segment.EndTime);
                    }
                }

                return result;
            }
        }

        public MontageSection FindSection(string sectionName)
        {
            if (string.IsNullOrEmpty(sectionName))
                return null;
            for (var i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                if (section != null && string.Equals(section.Name, sectionName, StringComparison.Ordinal))
                    return section;
            }
            return null;
        }

        public void Validate(ICollection<string> errors)
        {
            if (errors == null)
                throw new ArgumentNullException(nameof(errors));
            if (slotTracks.Count == 0)
                errors.Add("Montage has no slot tracks.");
            if (Duration <= 0f)
                errors.Add("Montage duration must be greater than zero.");

            var sectionNames = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                if (section == null)
                {
                    errors.Add($"Section {i} is null.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(section.Name))
                    errors.Add($"Section {i} has no name.");
                else if (!sectionNames.Add(section.Name))
                    errors.Add($"Duplicate section name '{section.Name}'.");
                if (section.EndTime <= section.StartTime)
                    errors.Add($"Section '{section.Name}' must end after it starts.");
                if (section.EndTime > Duration)
                    errors.Add($"Section '{section.Name}' ends after the animation content.");
            }

            var slots = new Dictionary<string, MontageSlotTrack>(StringComparer.Ordinal);
            for (var i = 0; i < slotTracks.Count; i++)
            {
                var track = slotTracks[i];
                if (track == null)
                {
                    errors.Add($"Slot track {i} is null.");
                    continue;
                }
                if (slots.ContainsKey(track.Slot))
                    errors.Add($"Duplicate slot track '{track.Slot}'. A montage can contain one track per slot.");
                else
                    slots[track.Slot] = track;

                var previousEnd = -1f;
                for (var j = 0; j < track.Segments.Count; j++)
                {
                    var segment = track.Segments[j];
                    if (segment == null || segment.Clip == null)
                        errors.Add($"Slot '{track.Slot}' segment {j} has no animation clip.");
                    else if (segment.Duration <= 0f)
                        errors.Add($"Slot '{track.Slot}' segment {j} has no playable duration.");
                    else
                    {
                        if (segment.StartTime < previousEnd)
                            errors.Add($"Slot '{track.Slot}' segment {j} overlaps or is out of order.");
                        previousEnd = Mathf.Max(previousEnd, segment.EndTime);
                    }
                }
            }

            for (var i = 0; i < sections.Count; i++)
            {
                var section = sections[i];
                if (section != null && !string.IsNullOrEmpty(section.NextSection) && !sectionNames.Contains(section.NextSection))
                    errors.Add($"Section '{section.Name}' points to missing section '{section.NextSection}'.");
            }

            for (var i = 0; i < notifies.Count; i++)
            {
                var notify = notifies[i];
                if (notify != null && notify.EndTime > Duration)
                    errors.Add($"Notify {i} ends after the montage duration.");
            }
        }

        private void OnValidate()
        {
            blendIn = Mathf.Max(0f, blendIn);
            blendOut = Mathf.Max(0f, blendOut);
            defaultPlayRate = Mathf.Max(0.001f, defaultPlayRate);
            serializedVersion = CurrentSerializedVersion;
            for (var i = 0; i < sections.Count; i++)
                sections[i]?.EnsureId();
            for (var i = 0; i < notifies.Count; i++)
                notifies[i]?.EnsureId();
        }
    }
}
