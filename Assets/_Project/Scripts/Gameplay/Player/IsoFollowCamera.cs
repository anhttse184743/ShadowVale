using UnityEngine;

namespace ShadowVale.Gameplay.Player
{
    /// <summary>
    /// Keeps the camera at a fixed isometric offset above/behind a target, giving
    /// the 2.5D look. The fixed yaw here is what PlayerController uses as its
    /// movement basis, so keep them consistent.
    /// </summary>
    public sealed class IsoFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [Header("Iso rig (matches project 2.5D convention)")]
        [Tooltip("Offset from the target in world space, matching the (-12,14,-12) rig.")]
        [SerializeField] private Vector3 offset = new Vector3(-12f, 14f, -12f);

        [Tooltip("Camera rotation. Project convention is (30, 45, 0).")]
        [SerializeField] private Vector3 eulerAngles = new Vector3(30f, 45f, 0f);

        [Tooltip("How quickly the camera catches up to the target. 0 = snap.")]
        [SerializeField] private float followLerp = 10f;

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 desired = target.position + offset;
            transform.position = followLerp > 0f
                ? Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followLerp * Time.deltaTime))
                : desired;

            transform.rotation = Quaternion.Euler(eulerAngles);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }
}
