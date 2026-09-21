using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Gameplay.Player
{
    /// <summary>
    /// Minimal greybox test player. Camera-relative WASD movement on the XZ plane
    /// for the 2.5D isometric view, driven by the new Input System.
    /// Uses a CharacterController so it collides with greybox walls/blocks and
    /// walks up ramps under manual gravity.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Movement (metric, 1 unit = 1 m)")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float rotationSpeed = 720f; // deg/sec toward travel direction
        [SerializeField] private float gravity = -20f;

        [Header("Camera basis")]
        [Tooltip("Transform whose yaw defines 'forward' for input. Defaults to Camera.main.")]
        [SerializeField] private Transform cameraTransform;

        private CharacterController _controller;
        private float _verticalVelocity;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            Vector2 input = ReadMoveInput();

            // Build a camera-relative basis flattened onto the XZ plane so "up" on
            // screen moves the player away from the iso camera.
            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
                if (forward.sqrMagnitude < 0.001f)
                {
                    // Camera looking near-straight-down: fall back to world axes.
                    forward = Vector3.forward;
                    right = Vector3.right;
                }
            }

            Vector3 move = right * input.x + forward * input.y;
            if (move.sqrMagnitude > 1f)
            {
                move.Normalize();
            }

            // Face travel direction.
            if (move.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(move, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, target, rotationSpeed * Time.deltaTime);
            }

            // Manual gravity so we stay grounded and can descend ramps.
            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -2f;
            }
            _verticalVelocity += gravity * Time.deltaTime;

            Vector3 velocity = move * moveSpeed;
            velocity.y = _verticalVelocity;
            _controller.Move(velocity * Time.deltaTime);
        }

        private static Vector2 ReadMoveInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return Vector2.zero; // No keyboard device present.
            }

            float x = 0f;
            float y = 0f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;
            return new Vector2(x, y);
        }
    }
}
