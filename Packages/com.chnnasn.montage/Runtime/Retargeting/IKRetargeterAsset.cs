using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChnnAsn.Montage
{
    public enum RetargetChainMode
    {
        RotationOnly,
        RotationAndReach
    }

    [Serializable]
    public sealed class RetargetBoneChain
    {
        [SerializeField] private string name = "Chain";
        [SerializeField] private HumanBodyBones startBone = HumanBodyBones.Hips;
        [SerializeField] private HumanBodyBones endBone = HumanBodyBones.Hips;

        public string Name => name;
        public HumanBodyBones StartBone => startBone;
        public HumanBodyBones EndBone => endBone;

        public RetargetBoneChain()
        {
        }

        public RetargetBoneChain(string name, HumanBodyBones startBone, HumanBodyBones endBone)
        {
            this.name = name;
            this.startBone = startBone;
            this.endBone = endBone;
        }
    }

    [Serializable]
    public sealed class RetargetRigDefinition
    {
        [SerializeField] private Avatar avatar;
        [SerializeField] private List<RetargetBoneChain> chains = new List<RetargetBoneChain>();

        public Avatar Avatar => avatar;
        public IReadOnlyList<RetargetBoneChain> Chains => chains;

        public RetargetBoneChain FindChain(string chainName)
        {
            if (string.IsNullOrEmpty(chainName))
                return null;

            for (var i = 0; i < chains.Count; i++)
            {
                var chain = chains[i];
                if (chain != null && string.Equals(chain.Name, chainName, StringComparison.Ordinal))
                    return chain;
            }

            return null;
        }

        public void SetAvatar(Avatar value)
        {
            avatar = value;
        }

        internal void SetChains(IEnumerable<RetargetBoneChain> values)
        {
            chains.Clear();
            if (values == null)
                return;
            foreach (var value in values)
                if (value != null)
                    chains.Add(value);
        }
    }

    [Serializable]
    public sealed class RetargetChainMapping
    {
        [SerializeField] private string sourceChain;
        [SerializeField] private string targetChain;
        [SerializeField] private RetargetChainMode mode = RetargetChainMode.RotationAndReach;
        [SerializeField, Range(0f, 1f)] private float rotationWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float positionWeight = 1f;
        [SerializeField] private bool enableIK;

        public string SourceChain => sourceChain;
        public string TargetChain => targetChain;
        public RetargetChainMode Mode => mode;
        public float RotationWeight => rotationWeight;
        public float PositionWeight => positionWeight;
        public bool EnableIK => enableIK;

        public RetargetChainMapping()
        {
        }

        public RetargetChainMapping(string sourceChain, string targetChain, bool enableIK)
        {
            this.sourceChain = sourceChain;
            this.targetChain = targetChain;
            this.enableIK = enableIK;
        }
    }

    [CreateAssetMenu(fileName = "New IK Retargeter", menuName = "Montage/IK Retargeter")]
    public sealed class IKRetargeterAsset : ScriptableObject
    {
        [SerializeField] private RetargetRigDefinition sourceRig = new RetargetRigDefinition();
        [SerializeField] private RetargetRigDefinition targetRig = new RetargetRigDefinition();
        [SerializeField] private RetargetPoseAsset sourcePose;
        [SerializeField] private RetargetPoseAsset targetPose;
        [SerializeField] private List<RetargetChainMapping> chainMappings = new List<RetargetChainMapping>();
        [SerializeField, Min(0.01f)] private float rootTranslationScale = 1f;
        [SerializeField, Range(0f, 1f)] private float poseWeight = 1f;

        public RetargetRigDefinition SourceRig => sourceRig;
        public RetargetRigDefinition TargetRig => targetRig;
        public RetargetPoseAsset SourcePose => sourcePose;
        public RetargetPoseAsset TargetPose => targetPose;
        public IReadOnlyList<RetargetChainMapping> ChainMappings => chainMappings;
        public float RootTranslationScale => rootTranslationScale;
        public float PoseWeight => poseWeight;

        public void ResetToHumanoidDefaults()
        {
            var chains = CreateHumanoidChains();
            sourceRig.SetChains(chains);
            targetRig.SetChains(CreateHumanoidChains());
            chainMappings.Clear();
            for (var i = 0; i < chains.Count; i++)
            {
                var chain = chains[i];
                var useIK = chain.Name.EndsWith("Arm", StringComparison.Ordinal) ||
                            chain.Name.EndsWith("Leg", StringComparison.Ordinal);
                chainMappings.Add(new RetargetChainMapping(chain.Name, chain.Name, useIK));
            }
        }

        public void ApplyPoseCorrections(Animator animator, float weight = 1f)
        {
            if (animator == null || !animator.isHuman)
                return;

            var strength = Mathf.Clamp01(weight) * poseWeight;
            if (strength <= 0f)
                return;

            for (var index = 0; index < (int)HumanBodyBones.LastBone; index++)
            {
                var bone = (HumanBodyBones)index;
                var transform = animator.GetBoneTransform(bone);
                if (transform == null)
                    continue;

                var sourceRotation = sourcePose == null
                    ? Quaternion.identity
                    : sourcePose.GetRotationOffset(bone);
                var targetRotation = targetPose == null
                    ? Quaternion.identity
                    : targetPose.GetRotationOffset(bone);
                var correction = targetRotation * Quaternion.Inverse(sourceRotation);
                transform.localRotation *= Quaternion.Slerp(Quaternion.identity, correction, strength);

                var sourcePosition = sourcePose == null ? Vector3.zero : sourcePose.GetPositionOffset(bone);
                var targetPosition = targetPose == null ? Vector3.zero : targetPose.GetPositionOffset(bone);
                transform.localPosition += Vector3.Lerp(Vector3.zero, targetPosition - sourcePosition, strength);
            }
        }

        public void Validate(ICollection<string> errors)
        {
            if (errors == null)
                throw new ArgumentNullException(nameof(errors));

            ValidateRig("Source", sourceRig, errors);
            ValidateRig("Target", targetRig, errors);
            if (chainMappings.Count == 0)
                errors.Add("Retargeter has no chain mappings.");

            for (var i = 0; i < chainMappings.Count; i++)
            {
                var mapping = chainMappings[i];
                if (mapping == null)
                {
                    errors.Add($"Chain mapping {i} is null.");
                    continue;
                }

                if (sourceRig.FindChain(mapping.SourceChain) == null)
                    errors.Add($"Mapping {i} references missing source chain '{mapping.SourceChain}'.");
                if (targetRig.FindChain(mapping.TargetChain) == null)
                    errors.Add($"Mapping {i} references missing target chain '{mapping.TargetChain}'.");
            }
        }

        private void Reset()
        {
            ResetToHumanoidDefaults();
        }

        private void OnValidate()
        {
            rootTranslationScale = Mathf.Max(0.01f, rootTranslationScale);
            poseWeight = Mathf.Clamp01(poseWeight);
        }

        private static void ValidateRig(string label, RetargetRigDefinition rig, ICollection<string> errors)
        {
            if (rig == null)
            {
                errors.Add($"{label} rig is missing.");
                return;
            }

            if (rig.Avatar == null)
                errors.Add($"{label} rig has no Avatar.");
            else if (!rig.Avatar.isValid || !rig.Avatar.isHuman)
                errors.Add($"{label} Avatar must be a valid Humanoid Avatar.");

            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < rig.Chains.Count; i++)
            {
                var chain = rig.Chains[i];
                if (chain == null || string.IsNullOrWhiteSpace(chain.Name))
                    errors.Add($"{label} chain {i} has no name.");
                else if (!names.Add(chain.Name))
                    errors.Add($"{label} rig has duplicate chain '{chain.Name}'.");
            }
        }

        private static List<RetargetBoneChain> CreateHumanoidChains()
        {
            return new List<RetargetBoneChain>
            {
                new RetargetBoneChain("Root", HumanBodyBones.Hips, HumanBodyBones.Hips),
                new RetargetBoneChain("Spine", HumanBodyBones.Hips, HumanBodyBones.Head),
                new RetargetBoneChain("LeftArm", HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftHand),
                new RetargetBoneChain("RightArm", HumanBodyBones.RightUpperArm, HumanBodyBones.RightHand),
                new RetargetBoneChain("LeftLeg", HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftFoot),
                new RetargetBoneChain("RightLeg", HumanBodyBones.RightUpperLeg, HumanBodyBones.RightFoot),
                new RetargetBoneChain("LeftFingers", HumanBodyBones.LeftHand, HumanBodyBones.LeftMiddleDistal),
                new RetargetBoneChain("RightFingers", HumanBodyBones.RightHand, HumanBodyBones.RightMiddleDistal)
            };
        }
    }
}
