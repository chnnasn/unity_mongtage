using UnityEngine;

namespace ChnnAsn.Montage
{
    public interface IMontageRootMotionReceiver
    {
        void ConsumeRootMotion(Vector3 deltaPosition, Quaternion deltaRotation);
    }

    public enum MontageRootMotionMode
    {
        ApplyToTransform,
        SendToReceiver
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class MontageRootMotionDriver : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private MontageRootMotionMode mode = MontageRootMotionMode.ApplyToTransform;
        [SerializeField] private MonoBehaviour receiver;

        private IMontageRootMotionReceiver typedReceiver;

        private void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
            typedReceiver = receiver as IMontageRootMotionReceiver;
            if (mode == MontageRootMotionMode.SendToReceiver && receiver != null && typedReceiver == null)
                Debug.LogError($"{receiver.GetType().Name} does not implement {nameof(IMontageRootMotionReceiver)}.", receiver);
        }

        private void OnAnimatorMove()
        {
            if (mode == MontageRootMotionMode.SendToReceiver)
                typedReceiver?.ConsumeRootMotion(animator.deltaPosition, animator.deltaRotation);
            else
            {
                transform.position += animator.deltaPosition;
                transform.rotation *= animator.deltaRotation;
            }
        }
    }
}
