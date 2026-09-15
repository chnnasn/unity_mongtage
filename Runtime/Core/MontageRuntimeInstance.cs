using System.Collections.Generic;
using UnityEngine.Animations;

namespace ChnnAsn.Montage
{
    internal sealed class MontageRuntimeInstance
    {
        public int Id;
        public MontageAsset Asset;
        public float Time;
        public float Weight;
        public float PlayRate;
        public int Priority;
        public string CurrentSection;
        public float FadeDuration;
        public float FadeElapsed;
        public float FadeStartWeight;
        public bool FadingIn;
        public bool FadingOut;
        public bool HoldTime;
        public MontageStopReason StopReason;
        public readonly List<MontageTrackRuntime> Tracks = new List<MontageTrackRuntime>();
        public readonly HashSet<MontageNotify> ActiveNotifies = new HashSet<MontageNotify>();
        public readonly Dictionary<string, string> NextSections = new Dictionary<string, string>();
    }

    internal sealed class MontageTrackRuntime
    {
        public MontageRuntimeInstance Owner;
        public MontageSlotTrack Track;
        public MontageSlotRuntime Slot;
        public int InputPort;
        public MontageSegment Segment;
        public AnimationClipPlayable ClipPlayable;

        public bool HasPlayable => ClipPlayable.IsValid();
    }
}
