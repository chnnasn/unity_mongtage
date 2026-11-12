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
        [SerializeField, Min(0f), Tooltip("Zero uses the end of the source clip.")] private float clipOut;
        [SerializeField, Min(0f), Tooltip("Zero derives length from the trimmed clip, play rate and loop count. Positive values explicitly set timeline length.")]
        private float duration;
        [SerializeField, Tooltip("Negative values play this source clip backwards.")] private float playRate = 1f;
        [SerializeField] private bool loop;
        [SerializeField, Min(1)] private int loopCount = 1;
        [SerializeField] private bool applyFootIK;
        [SerializeField] private bool applyPlayableIK;

        public AnimationClip Clip => clip;
        public float StartTime => startTime;
        public float ClipIn => clipIn;
        public float ClipOut => clip == null ? 0f : (clipOut > 0f ? Mathf.Min(clipOut, clip.length) : clip.length);
        public float PlayRate => Mathf.Abs(playRate) < 0.001f ? 1f : playRate;
        public bool Loop => loop;
        public int LoopCount => Mathf.Max(1, loopCount);
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
                return Mathf.Max(0f, ClipOut - clipIn) * LoopCount / Mathf.Abs(PlayRate);
            }
        }

        public float EndTime => StartTime + Duration;

        // Shared by runtime and preview so trim, reverse and loop sampling agree.
        public float GetClipTime(float montageTime)
        {
            var available = Mathf.Max(0f, ClipOut - ClipIn);
            var elapsed = Mathf.Clamp(montageTime - StartTime, 0f, Duration) * Mathf.Abs(PlayRate);
            if ((Loop || LoopCount > 1) && available > 0f)
            {
                var atEnd = montageTime >= EndTime && Mathf.Approximately(elapsed % available, 0f);
                elapsed = atEnd ? available : elapsed % available;
            }
            else
                elapsed = Mathf.Min(elapsed, available);
            return PlayRate < 0f ? ClipOut - elapsed : ClipIn + elapsed;
        }
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
        [SerializeField] private string eventName;
        [SerializeField, Min(0)] private int track;
        [SerializeField, Min(0f)] private float time;
        [SerializeField, Min(0f)] private float duration;
        [SerializeField] private MontageNotifyAsset notify;

        public string Id => id;
        public string Name => !string.IsNullOrEmpty(eventName) ? eventName : (notify != null ? notify.name : "Notify");
        public int Track => Mathf.Max(0, track);
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

    [Serializable]
    public sealed class MontageFloatCurve
    {
        [SerializeField] private string name = "Curve";
        [SerializeField] private AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        public string Name => name;
        public AnimationCurve Curve => curve;
        public float Evaluate(float time) => curve != null ? curve.Evaluate(time) : 0f;
    }
}
