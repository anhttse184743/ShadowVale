using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    public sealed partial class ForestMission
    {
        public bool TryJump()
        {
            if (!IsInitialized || Stopped || inventoryOpen || mapOpen || crafting != null ||
                !controller.isGrounded || verticalVelocity > 0 || stamina < 8) return false;
            verticalVelocity = Mathf.Sqrt(2f * 22f * 1.15f);
            stamina -= 8;
            crouched = false;
            EmitNoise(player.position, 5);
            return true;
        }

        private void MovePlayer(Vector3 horizontal)
        {
            if (controller.isGrounded && verticalVelocity < 0) verticalVelocity = -2;
            verticalVelocity = Mathf.Max(-30, verticalVelocity - 22 * Time.deltaTime);
            var flags = controller.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
            if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0) verticalVelocity = 0;
        }

        private void UpdateCompanion()
        {
            if (companion == null || !companion.isOnNavMesh) return;
            companion.speed = Settings.sprintSpeed;
            companion.stoppingDistance = Settings.followDistance;
            if (NavMesh.SamplePosition(player.position, out var hit, 3, NavMesh.AllAreas))
                companion.SetDestination(hit.position);
        }

        private void LateUpdate()
        {
            if (gameCamera != null && player != null && gameCamera.GetComponent<ForestThirdPersonCamera>() == null)
            {
                var target = player.position - gameCamera.transform.forward * 30;
                gameCamera.transform.position = Vector3.Lerp(
                    gameCamera.transform.position, target, 1 - Mathf.Exp(-8 * Time.unscaledDeltaTime));
            }
        }
    }
}
