using UnityEngine;

namespace ChnnAsn.Montage
{
    internal static class MontageNotifyAttachment
    {
        public static Transform Find(Transform owner, string path)
        {
            if (owner == null || string.IsNullOrWhiteSpace(path))
                return owner;
            return owner.Find(path) ?? owner;
        }
    }
}
