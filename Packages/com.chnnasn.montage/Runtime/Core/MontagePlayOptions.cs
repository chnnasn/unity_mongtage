namespace ChnnAsn.Montage
{
    public struct MontagePlayOptions
    {
        public string StartSection;
        public float PlayRate;
        public int Priority;
        public float BlendIn;
        public float StartTime;

        public static MontagePlayOptions Default => new MontagePlayOptions
        {
            PlayRate = 1f,
            BlendIn = -1f
        };

        public MontagePlayOptions WithSection(string section)
        {
            StartSection = section;
            return this;
        }

        public MontagePlayOptions WithStartTime(float seconds)
        {
            StartTime = seconds;
            StartSection = null;
            return this;
        }

        public MontagePlayOptions WithPlayRate(float playRate)
        {
            PlayRate = playRate;
            return this;
        }

        public MontagePlayOptions WithPriority(int priority)
        {
            Priority = priority;
            return this;
        }

        public MontagePlayOptions WithBlendIn(float seconds)
        {
            BlendIn = seconds;
            return this;
        }
    }
}
