using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChnnAsn.Montage
{
    public enum IKRetargetGoal
    {
        LeftHand,
        RightHand,
        LeftFoot,
        RightFoot
    }

    [Serializable]
    public sealed class IKRetargetGoalBinding
    {
        [SerializeField] private IKRetargetGoal goal;
        [SerializeField] private Transform target;
        [SerializeField] private Transform pole;
        [SerializeField, Range(0f, 1f)] private float positionWeight = 1f;
        [SerializeField, Range(0f, 1f)] private float rotationWeight = 1f;

        public IKRetargetGoal Goal => goal;
        public Transform Target => target;
        public Transform Pole => pole;
        public float PositionWeight => positionWeight;
        public float RotationWeight => rotationWeight;

        public IKRetargetGoalBinding()
        {
        }

        public IKRetargetGoalBinding(IKRetargetGoal goal)
        {
            this.goal = goal;
        }

        public void SetTarget(Transform value, Transform poleTarget = null)
        {
            target = value;
            pole = poleTarget;
        }
    }

    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class MontageIKRetargeter : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private IKRetargeterAsset retargeter;
        [SerializeField, Range(0f, 1f)] private float poseWeight = 1f;
        [SerializeField] private bool applyPoseCorrections = true;
        [SerializeField] private bool solveGoals = true;
        [SerializeField] private List<IKRetargetGoalBinding> goals = new List<IKRetargetGoalBinding>();

        public Animator Animator => animator;
        public IKRetargeterAsset Retargeter => retargeter;
        public IReadOnlyList<IKRetargetGoalBinding> Goals => goals;

        private void Reset()
        {
            animator = GetComponent<Animator>();
            goals = new List<IKRetargetGoalBinding>
            {
                new IKRetargetGoalBinding(IKRetargetGoal.LeftHand),
                new IKRetargetGoalBinding(IKRetargetGoal.RightHand),
                new IKRetargetGoalBinding(IKRetargetGoal.LeftFoot),
                new IKRetargetGoalBinding(IKRetargetGoal.RightFoot)
            };
        }

        private void LateUpdate()
        {
            SolveNow();
        }

        public void SetRetargeter(IKRetargeterAsset value)
        {
            retargeter = value;
        }

        public void SolveNow()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
                return;

            if (applyPoseCorrections && retargeter != null)
                retargeter.ApplyPoseCorrections(animator, poseWeight);
            if (!solveGoals)
                return;

            for (var i = 0; i < goals.Count; i++)
                SolveGoal(goals[i]);
        }

        private void SolveGoal(IKRetargetGoalBinding binding)
        {
            if (binding == null || binding.Target == null)
                return;
            if (!TryGetLimb(binding.Goal, out var upper, out var lower, out var tip))
                return;

            var polePosition = binding.Pole != null
                ? binding.Pole.position
                : DefaultPolePosition(binding.Goal, upper, lower, tip);
            TwoBoneIKSolver.Solve(
                upper,
                lower,
                tip,
                binding.Target.position,
                binding.Target.rotation,
                polePosition,
                binding.PositionWeight,
                binding.RotationWeight);
        }

        private bool TryGetLimb(IKRetargetGoal goal, out Transform upper, out Transform lower, out Transform tip)
        {
            HumanBodyBones upperBone;
            HumanBodyBones lowerBone;
            HumanBodyBones tipBone;
            switch (goal)
            {
                case IKRetargetGoal.LeftHand:
                    upperBone = HumanBodyBones.LeftUpperArm;
                    lowerBone = HumanBodyBones.LeftLowerArm;
                    tipBone = HumanBodyBones.LeftHand;
                    break;
                case IKRetargetGoal.RightHand:
                    upperBone = HumanBodyBones.RightUpperArm;
                    lowerBone = HumanBodyBones.RightLowerArm;
                    tipBone = HumanBodyBones.RightHand;
                    break;
                case IKRetargetGoal.LeftFoot:
                    upperBone = HumanBodyBones.LeftUpperLeg;
                    lowerBone = HumanBodyBones.LeftLowerLeg;
                    tipBone = HumanBodyBones.LeftFoot;
                    break;
                default:
                    upperBone = HumanBodyBones.RightUpperLeg;
                    lowerBone = HumanBodyBones.RightLowerLeg;
                    tipBone = HumanBodyBones.RightFoot;
                    break;
            }

            upper = animator.GetBoneTransform(upperBone);
            lower = animator.GetBoneTransform(lowerBone);
            tip = animator.GetBoneTransform(tipBone);
            return upper != null && lower != null && tip != null;
        }

        private static Vector3 DefaultPolePosition(
            IKRetargetGoal goal,
            Transform upper,
            Transform lower,
            Transform tip)
        {
            var chainDirection = tip.position - upper.position;
            var bend = Vector3.ProjectOnPlane(lower.position - upper.position, chainDirection);
            if (bend.sqrMagnitude <= 0.000001f)
            {
                var side = goal == IKRetargetGoal.LeftHand || goal == IKRetargetGoal.LeftFoot ? -1f : 1f;
                bend = upper.root.right * side;
            }

            return lower.position + bend.normalized * Mathf.Max(0.25f, chainDirection.magnitude);
        }

        private void OnDrawGizmosSelected()
        {
            for (var i = 0; i < goals.Count; i++)
            {
                var binding = goals[i];
                if (binding == null || binding.Target == null)
                    continue;
                Gizmos.color = binding.Goal == IKRetargetGoal.LeftHand || binding.Goal == IKRetargetGoal.RightHand
                    ? new Color(0.1f, 0.75f, 1f)
                    : new Color(1f, 0.65f, 0.1f);
                Gizmos.DrawWireSphere(binding.Target.position, 0.04f);
                if (binding.Pole != null)
                    Gizmos.DrawLine(binding.Target.position, binding.Pole.position);
            }
        }
    }
}
