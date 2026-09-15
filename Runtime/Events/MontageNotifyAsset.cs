using UnityEngine;

namespace ChnnAsn.Montage
{
    public abstract class MontageNotifyAsset : ScriptableObject
    {
        public virtual void OnNotify(in MontageNotifyContext context) { }
        public virtual void OnBegin(in MontageNotifyContext context) { }
        public virtual void OnTick(in MontageNotifyContext context) { }
        public virtual void OnEnd(in MontageNotifyContext context) { }
    }

    public readonly struct MontageNotifyContext
    {
        public MontagePlayer Player { get; }
        public MontageAsset Montage { get; }
        public MontageHandle Handle { get; }
        public MontageNotify Notify { get; }
        public float Time { get; }
        public float DeltaTime { get; }
        public GameObject Owner => Player != null ? Player.gameObject : null;

        internal MontageNotifyContext(
            MontagePlayer player,
            MontageAsset montage,
            MontageHandle handle,
            MontageNotify notify,
            float time,
            float deltaTime)
        {
            Player = player;
            Montage = montage;
            Handle = handle;
            Notify = notify;
            Time = time;
            DeltaTime = deltaTime;
        }
    }
}
