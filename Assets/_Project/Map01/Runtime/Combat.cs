using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    public sealed partial class ForestMission
    {
        private void Aim()
        {
            if (gameCamera.TryGetComponent<ForestThirdPersonCamera>(out _))
            {
                var centerRay = gameCamera.ViewportPointToRay(new Vector3(.5f, .5f));
                aim = Physics.Raycast(centerRay, out var targetHit, Weapon.range,
                    ObstructionMask | LayerMask.GetMask("Enemy"), QueryTriggerInteraction.Ignore)
                    ? targetHit.point : centerRay.GetPoint(Weapon.range);
                var look = aim - player.position;
                look.y = 0;
                if (look.sqrMagnitude > .01f) player.rotation = Quaternion.LookRotation(look);
                return;
            }
            if (Mouse.current == null) return;
            var ray = gameCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (new Plane(Vector3.up, player.position).Raycast(ray, out float distance)) aim = ray.GetPoint(distance);
            var facing = aim - player.position;
            facing.y = 0;
            if (facing.sqrMagnitude > .01f) player.rotation = Quaternion.LookRotation(facing);
        }

        private void Fire()
        {
            nextShot = Time.time + 1 / Weapon.fire_rate;
            if (Count("ammo_rifle") <= 0)
            {
                Say("Hết đạn — vẫn có thể lén đi hoặc đánh lạc hướng.", 2);
                return;
            }
            inventory["ammo_rifle"]--;
            var origin = player.position + Vector3.up * 1.1f;
            var shotDirection = gameCamera.GetComponent<ForestThirdPersonCamera>() != null
                ? (aim - origin).normalized : player.forward;
            var target = origin + shotDirection * Weapon.range;
            if (ForestBallistics.MuzzleBlocked(origin, player))
            {
                EmitNoise(player.position, Weapon.noise_radius);
                return;
            }
            if (ForestBallistics.Cast(origin, shotDirection, Weapon.range, player, out var hit))
            {
                target = hit.point;
                var enemy = hit.collider.GetComponentInParent<ForestGuard>();
                if (enemy != null) enemy.Hit(Weapon.damage);
            }
            Trace(origin, target, new Color(1, .84f, .45f));
            EmitNoise(player.position, Weapon.noise_radius);
        }

        public void Trace(Vector3 start, Vector3 end, Color color)
        {
            var trail = new GameObject("Transient trail");
            var line = trail.AddComponent<LineRenderer>();
            line.sharedMaterial = trailMaterial;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = .055f;
            line.endWidth = .015f;
            line.startColor = color;
            line.endColor = color;
            Destroy(trail, .12f);
        }
    }
}
