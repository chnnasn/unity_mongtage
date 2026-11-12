using UnityEngine;

namespace ChnnAsn.Montage
{
    [CreateAssetMenu(fileName = "Audio Montage Notify", menuName = "Montage/Notifies/Audio")]
    public sealed class MontageAudioNotifyAsset : MontageNotifyAsset
    {
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField, Range(-3f, 3f)] private float pitch = 1f;
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [SerializeField] private string attachPath;
        [SerializeField] private Vector3 localOffset;

        public AudioClip Clip => clip;
        public float Volume => volume;
        public float Pitch => pitch;
        public float SpatialBlend => spatialBlend;
        public string AttachPath => attachPath;
        public Vector3 LocalOffset => localOffset;

        public override void OnNotify(in MontageNotifyContext context)
        {
            if (clip == null || context.Owner == null)
                return;

            var target = MontageNotifyAttachment.Find(context.Owner.transform, attachPath);
            var host = new GameObject($"Montage Audio - {clip.name}");
            host.transform.SetParent(target, false);
            host.transform.localPosition = localOffset;

            var source = host.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = Mathf.Approximately(pitch, 0f) ? 0.01f : pitch;
            source.spatialBlend = Mathf.Clamp01(spatialBlend);
            source.playOnAwake = false;
            source.Play();

            var lifetime = clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch)) + 0.1f;
            Object.Destroy(host, lifetime);
        }
    }
}
