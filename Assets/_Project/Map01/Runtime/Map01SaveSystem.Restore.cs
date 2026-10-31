using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Loading a save: reading the checkpoint, flushing the skill run, and putting the mission, the guards and Nam back.
    /// </summary>
    public sealed partial class Map01SaveSystem
    {
        /// <summary>[F9] — called by Map01PlayerInteraction, which owns the key bindings.</summary>
        public void Load()
        {
            try
            {
                FlushCurrentSkillRun();
                if (IsSandbox)
                {
                    if (!File.Exists(SandboxPath(0))) { mission.Say("Map thử chưa có bản lưu nhanh [F5]."); return; }
                    pendingCheckpoint = File.ReadAllText(SandboxPath(0)); pendingSeconds = 0; pendingMessage = null;
                    Time.timeScale = 1; Map01Mission.ReloadScene(gameObject.scene);
                    return;
                }
                if (ForestSaveSlots.Exists(0)) { BeginGame(0); return; }
                if (!File.Exists(SavePath)) { mission.Say("Chưa có bản lưu nhanh."); return; }
                string json = File.ReadAllText(SavePath);
                pendingCheckpoint = JsonUtility.ToJson(NormalizeFailedAttempt(JsonUtility.FromJson<CheckpointData>(json), json)); pendingSeconds = 0; pendingMessage = null;
                Time.timeScale = 1; SceneManager.LoadScene("Map 1");
            }
            catch (Exception e) { mission.Say("Không thể tải: " + e.Message); }
        }

        private static void FlushCurrentSkillRun()
        {
            var active = FindFirstObjectByType<Map01SaveSystem>();
            if (active == null || active.mission == null || !active.mission.IsInitialized || IsRestoring) return;
            active.SyncSkillMission();
            if (!active.skillCampaign.TryFlush(out var error)) throw new IOException(error);
        }

        private void Restore()
        {
            GetComponent<DialogueVoice>()?.Stop();
            GetComponent<Map01StoryDialogue>()?.Close(false);
            GetComponent<Map01RescueCinematic>()?.CancelForRestore();
            try
            {
                var data = JsonUtility.FromJson<CheckpointData>(pendingCheckpoint);
                mission.SetPlaySeconds(pendingSeconds);
                if (data == null || data.version < 4 || data.items == null || data.used == null) throw new IOException();
                if (data.version < LayoutVersion) { mission.Say(OldLayoutMessage, 8); return; }
                var controller = mission.player.GetComponent<CharacterController>();
                if (controller != null) { controller.enabled = false; mission.player.position = data.player; controller.enabled = true; }
                else mission.player.position = data.player;
                mission.hung.GetComponent<NavMeshAgent>()?.Warp(data.hung);
                quest.RestoreStoryStage(data.stage, data.story);
                // Before anything rolls loot: the run's seed and the packs already rolled come first (Map01Logistics).
                Map01Logistics.For(mission).Restore(data.logistics);
                GetComponent<Map01CampChests>()?.Reroll();
                var c3 = GetComponent<Map01C3Mission>();
                c3?.RerollCache();
                c3?.Restore(data.c3);   // before the guards' snapshots: a woken sleeper is back on the field to receive his
                inventory.RestoreFromSave(data.items, data.stones, data.crafting, data.craftRemaining);
                if (data.bag != null) inventory.RestoreLayout(data.bag);
                Map01Drops.For(mission).Restore(data.dropped);
                inventory.RestoreFieldToolkit(data.bag?.fieldKitVersion ?? 0);
                Map01Smoke.ClearAll();
                Map01Blast.ClearAll();
                EnsureSkillCampaign();
                skillCampaign.Initialize(RunId(data, pendingCheckpoint));
                skillCampaign.Restore(data.skills, data.skillMission);
                GetComponent<Map01Minimap>()?.RestoreExploration(data.exploration);
                missionStart = data.missionStart;
                GetComponent<Map01Story>().AfterInventoryRestore();
                GetComponent<Map01Story>().RestoreOperationActors(data.enemies);
                operationStart=data.operationStart;
                mission.Alarmed = data.alarmed;
                mission.RestoreHealthFromSave(Mathf.Clamp(data.hp, 1, mission.Settings.playerHP));
                mission.SetStamina(data.stamina);
                mission.Crouched = data.crouched;
                mission.player.rotation = Quaternion.Euler(0, data.playerYaw, 0);
                mission.hung.rotation = Quaternion.Euler(0, data.hungYaw, 0);
                GetComponent<Map01Rescue>().Restore(data.rescue, data.enemies);
                rescueStart=data.rescueStart;escortStart=data.escortStart;
                RestoreGameplay(data);
                GetComponent<Map01Rescue>().RestoreHealth(data.hungHealth);
                // Loading an escort resumes its pose; only the live rescue plays the rise from captivity.
                mission.hung.GetComponentInChildren<Map01HungVisual>()?.RestoreCheckpointPose();
                GetComponent<Map01Scouting>().RestoreFound(data.scoutedCamps);
                foreach (var point in mission.Points) point.used = data.used.Contains(point.id);
                // Starting a scouting run over keeps the moment it started from; any other load
                // takes whatever moment the save carries (none, outside scouting).
                if (pendingMessage == null) scoutStart = string.IsNullOrEmpty(data.scoutStart) ? null : data.scoutStart;
                if (pendingMessage != null) mission.Say(pendingMessage, 14);
                else mission.Say("Đã khôi phục tiến trình và trạng thái giao chiến.");
            }
            catch (Exception) { mission.Say("Checkpoint không hợp lệ. Bắt đầu lại Map 1."); }
            finally { pendingCheckpoint = null; pendingMessage = null; }
        }

        private void RestoreGameplay(CheckpointData data)
        {
            if (mission.ModernPlayer != null) mission.ModernPlayer.RestoreMotion(mission.Crouched);
            if (mission.ModernCombat != null)
            {
                if (Enum.IsDefined(typeof(ShadowVale.Gameplay.Combat.WeaponKind), data.equippedWeapon))
                {
                    var equipped=(ShadowVale.Gameplay.Combat.WeaponKind)data.equippedWeapon;
                    if(equipped==ShadowVale.Gameplay.Combat.WeaponKind.Rifle && inventory.SlotItem(Map01Inventory.Slot.Primary)=="rifle_mosin")
                        equipped=ShadowVale.Gameplay.Combat.WeaponKind.Sniper;
                    else if(equipped==ShadowVale.Gameplay.Combat.WeaponKind.Rifle && (inventory.SlotItem(Map01Inventory.Slot.Primary)!="rifle_standard" && inventory.SlotItem(Map01Inventory.Slot.Primary)!=Map01M16.Id))
                        equipped=ShadowVale.Gameplay.Combat.WeaponKind.Knife;
                    if(equipped==ShadowVale.Gameplay.Combat.WeaponKind.Pistol && inventory.SlotItem(Map01Inventory.Slot.Pistol)!="pistol_k54")
                        equipped=ShadowVale.Gameplay.Combat.WeaponKind.Knife;
                    if(equipped==ShadowVale.Gameplay.Combat.WeaponKind.Sniper && inventory.SlotItem(Map01Inventory.Slot.Primary)!="rifle_mosin")
                        equipped=ShadowVale.Gameplay.Combat.WeaponKind.Knife;
                    mission.ModernCombat.Equip(equipped);
                }
                inventory.ApplyPrimaryRifle(false);
                mission.ModernCombat.RestoreAttackCooldown(data.attackRemaining);
                // Saves written before magazines existed carry -1; leave the weapon loaded
                // rather than handing the player an empty rifle out of an old checkpoint.
                if (data.roundsInMagazine >= 0) mission.ModernCombat.RestoreMagazine(ShadowVale.Gameplay.Combat.WeaponKind.Rifle, data.roundsInMagazine);
                mission.ModernCombat.RestoreMagazine(ShadowVale.Gameplay.Combat.WeaponKind.Pistol, data.pistolRoundsInMagazine);
                mission.ModernCombat.RestoreMagazine(ShadowVale.Gameplay.Combat.WeaponKind.Sniper, data.mosinRoundsInMagazine);
            }
            foreach (var enemy in mission.Enemies)
            {
                if (enemy == null) continue;
                var saved = data.enemies?.FirstOrDefault(e => e.id == enemy.SaveId);
                // The previous runtime boss inherited an arbitrary camp parent. Import its
                // existing snapshot into the stable third-outpost actor, never duplicate him.
                if (saved == null && enemy.IsBoss && data.story == null)
                    saved = data.enemies?.FirstOrDefault(e => e.id != null && e.id.EndsWith("/Chỉ huy địch", StringComparison.Ordinal));
                if (saved != null) enemy.RestoreSnapshot(saved);
                else {
                    enemy.RefreshRifleLoadout();
                    if (enemy.IsBoss && GetComponent<Map01Story>().Progress.commanderKilled) {
                        var state = enemy.Capture(); state.hp = 0; enemy.RestoreSnapshot(state);
                    }
                }
            }
        }
    }
}
