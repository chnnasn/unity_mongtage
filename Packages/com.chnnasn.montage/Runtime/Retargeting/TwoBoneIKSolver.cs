using UnityEngine;

namespace ChnnAsn.Montage
{
    public static class TwoBoneIKSolver
    {
        public static bool Solve(
            Transform root,
            Transform joint,
            Transform tip,
            Vector3 targetPosition,
            Quaternion targetRotation,
            Vector3 polePosition,
            float positionWeight = 1f,
            float rotationWeight = 1f)
        {
            if (root == null || joint == null || tip == null)
                return false;

            positionWeight = Mathf.Clamp01(positionWeight);
            rotationWeight = Mathf.Clamp01(rotationWeight);
            if (positionWeight <= 0f && rotationWeight <= 0f)
                return true;

            var rootPosition = root.position;
            var jointPosition = joint.position;
            var tipPosition = tip.position;
            var upperLength = Vector3.Distance(rootPosition, jointPosition);
            var lowerLength = Vector3.Distance(jointPosition, tipPosition);
            if (upperLength <= 0.00001f || lowerLength <= 0.00001f)
                return false;

            var blendedTarget = Vector3.Lerp(tipPosition, targetPosition, positionWeight);
            var toTarget = blendedTarget - rootPosition;
            var rawDistance = toTarget.magnitude;
            if (rawDistance <= 0.00001f)
                return false;

            var direction = toTarget / rawDistance;
            var minimumReach = Mathf.Abs(upperLength - lowerLength) + 0.0001f;
            var maximumReach = upperLength + lowerLength - 0.0001f;
            var distance = Mathf.Clamp(rawDistance, minimumReach, maximumReach);
            var solvedTarget = rootPosition + direction * distance;

            var poleDirection = Vector3.ProjectOnPlane(polePosition - rootPosition, direction);
            if (poleDirection.sqrMagnitude <= 0.000001f)
                poleDirection = Vector3.ProjectOnPlane(jointPosition - rootPosition, direction);
            if (poleDirection.sqrMagnitude <= 0.000001f)
                poleDirection = Vector3.Cross(direction, Vector3.up);
            if (poleDirection.sqrMagnitude <= 0.000001f)
                poleDirection = Vector3.Cross(direction, Vector3.right);
            poleDirection.Normalize();

            var along = (upperLength * upperLength + distance * distance - lowerLength * lowerLength) /
                        (2f * distance);
            var perpendicular = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            var desiredJoint = rootPosition + direction * along + poleDirection * perpendicular;

            var currentUpper = joint.position - root.position;
            var desiredUpper = desiredJoint - root.position;
            if (currentUpper.sqrMagnitude > 0.000001f && desiredUpper.sqrMagnitude > 0.000001f)
            {
                var delta = Quaternion.FromToRotation(currentUpper, desiredUpper);
                root.rotation = delta * root.rotation;
            }

            var currentLower = tip.position - joint.position;
            var desiredLower = solvedTarget - joint.position;
            if (currentLower.sqrMagnitude > 0.000001f && desiredLower.sqrMagnitude > 0.000001f)
            {
                var delta = Quaternion.FromToRotation(currentLower, desiredLower);
                joint.rotation = delta * joint.rotation;
            }

            if (rotationWeight > 0f)
                tip.rotation = Quaternion.Slerp(tip.rotation, targetRotation, rotationWeight);
            return true;
        }
    }
}
