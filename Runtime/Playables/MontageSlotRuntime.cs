using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;

namespace ChnnAsn.Montage
{
    internal sealed class MontageSlotRuntime
    {
        private readonly Stack<int> freePorts = new Stack<int>();

        public string Name { get; }
        public AvatarMask AvatarMask { get; }
        public bool Additive { get; }
        public int LayerPort { get; }
        public AnimationMixerPlayable Mixer { get; }
        public readonly Dictionary<int, MontageTrackRuntime> Tracks =
            new Dictionary<int, MontageTrackRuntime>();

        public MontageSlotRuntime(
            string name,
            AvatarMask avatarMask,
            bool additive,
            int layerPort,
            AnimationMixerPlayable mixer)
        {
            Name = name;
            AvatarMask = avatarMask;
            Additive = additive;
            LayerPort = layerPort;
            Mixer = mixer;
        }

        public int Allocate(MontageTrackRuntime track)
        {
            int port;
            if (freePorts.Count > 0)
                port = freePorts.Pop();
            else
            {
                port = Mixer.GetInputCount();
                Mixer.SetInputCount(port + 1);
            }

            Tracks.Add(port, track);
            return port;
        }

        public void Release(int port)
        {
            if (!Tracks.Remove(port))
                return;
            freePorts.Push(port);
            Mixer.SetInputWeight(port, 0f);
        }
    }
}
