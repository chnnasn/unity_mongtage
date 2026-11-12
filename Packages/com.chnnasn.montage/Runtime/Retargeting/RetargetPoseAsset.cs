using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChnnAsn.Montage
{
    [Serializable]
    public sealed class RetargetBonePoseOffset
    {
        [SerializeField] private HumanBodyBones bone = HumanBodyBones.Hips;
        [SerializeField] private Vector3 rotationOffset;
        [SerializeField] private Vector3 positionOffset;

        public HumanBodyBones Bone => bone;
        public Vector3 RotationOffset => rotationOffset;
        public Vector3 PositionOffset => positionOffset;

        public RetargetBonePoseOffset()
        {
        }

        public RetargetBonePoseOffset(HumanBodyBones bone, Vector3 rotationOffset, Vector3 positionOffset)
        {
            this.bone = bone;
            this.rotationOffset = rotationOffset;
            this.positionOffset = positionOffset;
        }

        internal void Set(Vector3 rotation, Vector3 position)
        {
            rotationOffset = rotation;
            positionOffset = position;
        }
    }

    [CreateAssetMenu(fileName = "New Retarget Pose", menuName = "Montage/IK Retarget Pose")]
    public sealed class RetargetPoseAsset : ScriptableObject
    {
        [SerializeField] private List<RetargetBonePoseOffset> boneOffsets =
            new List<RetargetBonePoseOffset>();

        public IReadOnlyList<RetargetBonePoseOffset> BoneOffsets => boneOffsets;

        public Quaternion GetRotationOffset(HumanBodyBones bone)
        {
            var entry = Find(bone);
            return entry == null ? Quaternion.identity : Quaternion.Euler(entry.RotationOffset);
        }

        public Vector3 GetPositionOffset(HumanBodyBones bone)
        {
            var entry = Find(bone);
            return entry == null ? Vector3.zero : entry.PositionOffset;
        }

        public void SetOffset(HumanBodyBones bone, Vector3 rotationOffset, Vector3 positionOffset)
        {
            if (!IsValidBone(bone))
                throw new ArgumentOutOfRangeException(nameof(bone), bone, "A valid humanoid bone is required.");

            var entry = Find(bone);
            if (entry == null)
            {
                boneOffsets.Add(new RetargetBonePoseOffset(bone, rotationOffset, positionOffset));
                return;
            }

            entry.Set(rotationOffset, positionOffset);
        }

        public bool RemoveOffset(HumanBodyBones bone)
        {
            for (var i = 0; i < boneOffsets.Count; i++)
            {
                var entry = boneOffsets[i];
                if (entry != null && entry.Bone == bone)
                {
                    boneOffsets.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        private RetargetBonePoseOffset Find(HumanBodyBones bone)
        {
            for (var i = 0; i < boneOffsets.Count; i++)
            {
                var entry = boneOffsets[i];
                if (entry != null && entry.Bone == bone)
                    return entry;
            }

            return null;
        }

        private void OnValidate()
        {
            var used = new HashSet<HumanBodyBones>();
            for (var i = boneOffsets.Count - 1; i >= 0; i--)
            {
                var entry = boneOffsets[i];
                if (entry == null || !IsValidBone(entry.Bone) || !used.Add(entry.Bone))
                    boneOffsets.RemoveAt(i);
            }
        }

        private static bool IsValidBone(HumanBodyBones bone)
        {
            return bone >= 0 && bone < HumanBodyBones.LastBone;
        }
    }
}
