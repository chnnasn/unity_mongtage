using System;

namespace ChnnAsn.Montage
{
    public readonly struct MontageHandle : IEquatable<MontageHandle>
    {
        private readonly MontagePlayer player;
        private readonly int id;

        internal MontageHandle(MontagePlayer player, int id)
        {
            this.player = player;
            this.id = id;
        }

        public bool IsValid => player != null && player.Contains(id);
        public bool IsPlaying => IsValid && player.IsPlaying(id);
        public float Time => IsValid ? player.GetTime(id) : 0f;
        public float Weight => IsValid ? player.GetWeight(id) : 0f;
        public string CurrentSection => IsValid ? player.GetCurrentSection(id) : null;

        public bool Stop(float blendOut = -1f) => player != null && player.Stop(id, blendOut);
        public bool JumpToSection(string section) => player != null && player.JumpToSection(id, section);
        public bool SetNextSection(string from, string to) => player != null && player.SetNextSection(id, from, to);
        public bool SetPlayRate(float playRate) => player != null && player.SetPlayRate(id, playRate);

        public bool Equals(MontageHandle other) => player == other.player && id == other.id;
        public override bool Equals(object obj) => obj is MontageHandle other && Equals(other);
        public override int GetHashCode()
        {
            unchecked
            {
                return ((player != null ? player.GetHashCode() : 0) * 397) ^ id;
            }
        }
        public static bool operator ==(MontageHandle left, MontageHandle right) => left.Equals(right);
        public static bool operator !=(MontageHandle left, MontageHandle right) => !left.Equals(right);
    }
}
