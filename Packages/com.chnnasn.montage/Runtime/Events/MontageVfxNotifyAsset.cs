using UnityEngine;

namespace ChnnAsn.Montage
{
    [CreateAssetMenu(fileName = "VFX Montage Notify", menuName = "Montage/Notifies/VFX")]
    public sealed class MontageVfxNotifyAsset : MontageNotifyAsset
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private string attachPath;
        [SerializeField] private Vector3 localPosition;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField] private bool parentToOwner = true;
        [SerializeField, Min(0f)] private float lifetime = 3f;

        public GameObject Prefab => prefab;
        public string AttachPath => attachPath;
        public Vector3 LocalPosition => localPosition;
        public Vector3 LocalEulerAngles => localEulerAngles;
        public bool ParentToOwner => parentToOwner;
        public float Lifetime => lifetime;

        public override void OnNotify(in MontageNotifyContext context)
        {
            if (prefab == null || context.Owner == null)
                return;

            var target = MontageNotifyAttachment.Find(context.Owner.transform, attachPath);
            var instance = Object.Instantiate(prefab);
            instance.name = $"{prefab.name} (Montage VFX)";
            if (parentToOwner)
            {
                instance.transform.SetParent(target, false);
                instance.transform.localPosition = localPosition;
                instance.transform.localRotation = Quaternion.Euler(localEulerAngles);
            }
            else
            {
                instance.transform.SetPositionAndRotation(
                    target.TransformPoint(localPosition),
                    target.rotation * Quaternion.Euler(localEulerAngles));
            }

            if (lifetime > 0f)
                Object.Destroy(instance, lifetime);
        }
    }
}
