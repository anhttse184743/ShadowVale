using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Camera))]
    public sealed class ForestThirdPersonCamera : MonoBehaviour
    {
        public Transform target;
        public ForestMission mission;
        public float distance = 4.5f, sensitivity = .12f;
        float yaw, pitch = 16;
        void OnEnable()
        {
            yaw = target != null ? target.eulerAngles.y : transform.eulerAngles.y;
            var camera = GetComponent<Camera>();
            camera.orthographic = false; camera.fieldOfView = 62; camera.nearClipPlane = .08f;
        }
        void LateUpdate()
        {
            if (target == null) return;
            bool active = mission != null && mission.CameraInputEnabled && Application.isFocused;
            Cursor.lockState = active ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !active;
            if (active && Mouse.current != null)
            {
                var delta = Mouse.current.delta.ReadValue();
                yaw += delta.x * sensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -25, 65);
            }
            var rotation = Quaternion.Euler(pitch, yaw, 0);
            var pivot = target.position + Vector3.up * 1.55f;
            var offset = rotation * new Vector3(.55f, .15f, -distance);
            float length = offset.magnitude;
            if (Physics.SphereCast(pivot, .22f, offset.normalized, out var hit, length,
                mission != null ? mission.ObstructionMask : Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                length = Mathf.Max(.05f, hit.distance - .08f);
            transform.SetPositionAndRotation(pivot + offset.normalized * length, rotation);
        }
        void OnGUI()
        {
            if (mission != null && mission.CameraInputEnabled)
                GUI.Label(new Rect(Screen.width / 2f - 5, Screen.height / 2f - 10, 20, 20), "+");
        }
        void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
