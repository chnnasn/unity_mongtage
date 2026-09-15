using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace ChnnAsn.Montage.Editor
{
    internal sealed class MontagePreviewGraph : IDisposable
    {
        private sealed class SegmentNode
        {
            public MontageSegment Segment;
            public AnimationClipPlayable Playable;
            public int InputPort;
        }

        private sealed class TrackNode
        {
            public MontageSlotTrack Track;
            public AnimationMixerPlayable Mixer;
            public int LayerPort;
            public readonly List<SegmentNode> Segments = new List<SegmentNode>();
        }

        private readonly List<TrackNode> tracks = new List<TrackNode>();
        private PlayableGraph graph;
        private AnimationLayerMixerPlayable layerMixer;

        public bool IsValid => graph.IsValid();

        public MontagePreviewGraph(Animator animator, MontageAsset montage, bool includeBaseController)
        {
            if (animator == null)
                throw new ArgumentNullException(nameof(animator));
            if (montage == null)
                throw new ArgumentNullException(nameof(montage));

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            graph = PlayableGraph.Create("Unity Montage Editor Preview");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            layerMixer = AnimationLayerMixerPlayable.Create(graph, 1);

            Playable basePlayable;
            if (includeBaseController && animator.runtimeAnimatorController != null)
                basePlayable = AnimatorControllerPlayable.Create(graph, animator.runtimeAnimatorController);
            else
                basePlayable = AnimationMixerPlayable.Create(graph, 0);
            graph.Connect(basePlayable, 0, layerMixer, 0);
            layerMixer.SetInputWeight(0, 1f);

            for (var trackIndex = 0; trackIndex < montage.SlotTracks.Count; trackIndex++)
            {
                var track = montage.SlotTracks[trackIndex];
                if (track == null)
                    continue;

                var node = new TrackNode
                {
                    Track = track,
                    Mixer = AnimationMixerPlayable.Create(graph, track.Segments.Count),
                    LayerPort = layerMixer.GetInputCount()
                };
                layerMixer.SetInputCount(node.LayerPort + 1);
                graph.Connect(node.Mixer, 0, layerMixer, node.LayerPort);
                layerMixer.SetInputWeight(node.LayerPort, 0f);
                layerMixer.SetLayerAdditive((uint)node.LayerPort, track.Additive);
                if (track.AvatarMask != null)
                    layerMixer.SetLayerMaskFromAvatarMask((uint)node.LayerPort, track.AvatarMask);

                for (var segmentIndex = 0; segmentIndex < track.Segments.Count; segmentIndex++)
                {
                    var segment = track.Segments[segmentIndex];
                    if (segment == null || segment.Clip == null)
                        continue;
                    var playable = AnimationClipPlayable.Create(graph, segment.Clip);
                    playable.SetApplyFootIK(segment.ApplyFootIK);
                    playable.SetApplyPlayableIK(false);
                    playable.SetSpeed(0d);
                    graph.Connect(playable, 0, node.Mixer, segmentIndex);
                    node.Mixer.SetInputWeight(segmentIndex, 0f);
                    node.Segments.Add(new SegmentNode
                    {
                        Segment = segment,
                        Playable = playable,
                        InputPort = segmentIndex
                    });
                }
                tracks.Add(node);
            }

            var output = AnimationPlayableOutput.Create(graph, "Montage Preview Output", animator);
            output.SetSourcePlayable(layerMixer);
            graph.Play();
        }

        public void Sample(float montageTime, float deltaTime)
        {
            if (!graph.IsValid())
                return;

            for (var trackIndex = 0; trackIndex < tracks.Count; trackIndex++)
            {
                var track = tracks[trackIndex];
                SegmentNode active = null;
                for (var segmentIndex = 0; segmentIndex < track.Segments.Count; segmentIndex++)
                {
                    var segment = track.Segments[segmentIndex];
                    var isActive = montageTime >= segment.Segment.StartTime &&
                                   montageTime < segment.Segment.EndTime;
                    if (isActive && active == null)
                        active = segment;
                }

                if (active == null)
                {
                    for (var segmentIndex = track.Segments.Count - 1; segmentIndex >= 0; segmentIndex--)
                    {
                        var segment = track.Segments[segmentIndex];
                        if (Mathf.Approximately(montageTime, segment.Segment.EndTime))
                        {
                            active = segment;
                            break;
                        }
                    }
                }

                for (var segmentIndex = 0; segmentIndex < track.Segments.Count; segmentIndex++)
                {
                    var segment = track.Segments[segmentIndex];
                    track.Mixer.SetInputWeight(segment.InputPort, ReferenceEquals(segment, active) ? 1f : 0f);
                }

                layerMixer.SetInputWeight(track.LayerPort, active != null ? 1f : 0f);
                if (active == null)
                    continue;

                var source = active.Segment;
                var localTime = Mathf.Max(0f, montageTime - source.StartTime) * source.PlayRate;
                var available = Mathf.Max(0f, source.Clip.length - source.ClipIn);
                if (source.Loop && available > 0f)
                    localTime %= available;
                else
                    localTime = Mathf.Min(localTime, available);
                active.Playable.SetTime(source.ClipIn + localTime);
            }

            graph.Evaluate(Mathf.Max(0f, deltaTime));
        }

        public void Dispose()
        {
            if (graph.IsValid())
                graph.Destroy();
            tracks.Clear();
        }
    }
}
