using System;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ShadowVale.Map01
{
    public sealed partial class ForestMission
    {
        private PlayerController modernPlayer;
        private PlayerCombat modernCombat;
        private ShadowVale.Gameplay.Combat.Health modernHealth;
        private Map01EnemyController[] modernEnemies = Array.Empty<Map01EnemyController>();

        // Legacy scenes keep their original controller; migrated scenes have one movement/combat owner.
        private void ConnectGameplay()
        {
            modernPlayer = player.GetComponent<PlayerController>();
            if (modernPlayer == null) return;
            modernCombat = player.GetComponent<PlayerCombat>();
            modernHealth = player.GetComponent<ShadowVale.Gameplay.Combat.Health>();
            modernEnemies = FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None);
            modernPlayer.InputAllowed = () => CameraInputEnabled && !ForestMenu.Visible && crafting == null;
            modernPlayer.SprintAllowed = () => stamina > 2;
            if (modernHealth != null) modernHealth.KeepCheckpointCorpse();
            if (modernCombat != null) {
                modernCombat.UsesInventoryHotkeys = true;
                modernCombat.InputAllowed = () => {
                    if (Mouse.current == null || !Mouse.current.leftButton.isPressed) suppressFireUntilRelease = false;
                    return CameraInputEnabled && !ForestMenu.Visible && crafting == null && !suppressFireUntilRelease && !HudPointerBlocked();
                };
                modernCombat.TryConsumeRound = () => {
                    if (Count("ammo_rifle") <= 0) { Say("Hết đạn — nhặt thêm đạn hoặc đổi vũ khí bằng 6 / 7 / 8.", 2); return false; }
                    inventory["ammo_rifle"]--;
                    EmitNoise(player.position, Weapon.noise_radius);
                    return true;
                };
            }
            if (gameCamera.TryGetComponent<ThirdPersonCamera>(out var cameraRig))
                cameraRig.InputAllowed = () => CameraInputEnabled && !ForestMenu.Visible;
            foreach (var enemy in modernEnemies) enemy.BindMission(this);
        }

        private void RestoreGameplay(Checkpoint data)
        {
            if (modernPlayer == null) return;
            modernHealth?.RestoreHealth(hp);
            modernPlayer.RestoreMotion(crouched);
            if (modernCombat != null && data.modernGameplay) {
                if (Enum.IsDefined(typeof(WeaponKind), data.equippedWeapon)) modernCombat.Equip((WeaponKind)data.equippedWeapon);
                modernCombat.RestoreAttackCooldown(data.attackRemaining);
            }
            foreach (var enemy in modernEnemies) {
                var saved = data.enemies?.FirstOrDefault(e => e.id == enemy.SaveId);
                if (saved != null) enemy.RestoreSnapshot(saved);
                else {
                    // Saves from before the character migration still retain defeated enemies.
                    var old = data.guards?.FirstOrDefault(g => g.id == enemy.LegacyId);
                    if (old != null) enemy.RestoreLegacy(old);
                    else if (data.down.Contains(enemy.LegacyId)) enemy.RestoreLegacy(new ForestGuard.Snapshot {
                        position = enemy.transform.position, rotation = enemy.transform.rotation,
                        state = ForestGuardState.Down, destination = enemy.transform.position
                    });
                }
            }
        }
    }
}
