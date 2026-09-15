using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChnnAsn.Montage
{
    [Serializable]
    public sealed class MontageSection
    {
        [SerializeField, HideInInspector] private string id;
        [SerializeField] private string name = "Section";
        [SerializeField, Min(0f)] private float startTime;
        [SerializeField, Min(0f)] private float endTime = 1f;
        [SerializeField] private string nextSection;

        public string Id => id;
        public string Name => name;
        public float StartTime => startTime;
        public float EndTime => endTime;
        public string NextSection => nextSection;

        internal void EnsureId()
        {
            if (string.IsNullOrEmpty(id))
                id = Guid.NewGuid().ToString("N");
        }
    }

    [Serializable]
    public sealed class MontageSegment
    {
        [SerializeField] private AnimationClip clip;
        [SerializeField, Min(0f)] private float startTime;
        [SerializeField, Min(0f)] private float clipIn;
        [SerializeField, Min(0f)] private float duration;
        [SerializeField, Min(0.001f)] private float playRate = 1f;
        [SerializeField] private bool loop;
        [SerializeField] private bool applyFootIK;
        [SerializeField] private bool applyPlayableIK;

        public AnimationClip Clip => clip;
        public float StartTime => startTime;
        public float ClipIn => clipIn;
        public float PlayRate => Mathf.Max(0.001f, playRate);
        public bool Loop => loop;
        public bool ApplyFootIK => applyFootIK;
        public bool ApplyPlayableIK => applyPlayableIK;

        public float Duration
        {
            get
            {
                if (duration > 0f)
                    return duration;
                if (clip == null)
                    return 0f;
                return Mathf.Max(0f, clip.length - clipIn) / PlayRate;
            }
        }

        public float EndTime => StartTime + Duration;
    }

    [Serializable]
    public sealed class MontageSlotTrack
    {
        [SerializeField] private string slot = "FullBody";
        [SerializeField] private AvatarMask avatarMask;
        [SerializeField] private bool additive;
        [SerializeField] private List<MontageSegment> segments = new List<MontageSegment>();

        public string Slot => string.IsNullOrWhiteSpace(slot) ? "FullBody" : slot;
        public AvatarMask AvatarMask => avatarMask;
        public bool Additive => additive;
        public IReadOnlyList<MontageSegment> Segments => segments;

        internal MontageSegment FindSegment(float montageTime)
        {
            MontageSegment last = null;
            for (var i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                if (segment == null || segment.Clip == null)
                    continue;

                if (montageTime >= segment.StartTime && montageTime < segment.EndTime)
                    return segment;

                if (montageTime >= segment.StartTime)
                    last = segment;
            }

            if (last != null && Mathf.Approximately(montageTime, last.EndTime))
                return last;
            return null;
        }
    }

    [Serializable]
    public sealed class MontageNotify
    {
        [SerializeField, HideInInspector] private string id;
        [SerializeField, Min(0f)] private float time;
        [SerializeField, Min(0f)] private float duration;
        [SerializeField] private MontageNotifyAsset notify;

        public string Id => id;
        public float Time => time;
        public float Duration => duration;
        public float EndTime => time + duration;
        public bool IsState => duration > 0f;
        public MontageNotifyAsset Asset => notify;

        internal void EnsureId()
        {
            if (string.IsNullOrEmpty(id))
                id = Guid.NewGuid().ToString("N");
        }
    }
}
