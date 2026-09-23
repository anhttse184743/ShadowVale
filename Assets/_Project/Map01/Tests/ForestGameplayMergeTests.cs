using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    public sealed class ForestGameplayMergeTests : ForestSceneTestBase
    {
        [UnityTest]
        public IEnumerator MigratedPlayerAndEnemiesShareInventoryPauseAndCheckpoints()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var rootField = typeof(ForestSaveSlots).GetField("storageRoot", BindingFlags.NonPublic | BindingFlags.Static);
            string root = Path.Combine(Application.temporaryCachePath, "merge-test-" + Guid.NewGuid().ToString("N"));
            rootField.SetValue(null, root);
            try {
                var mission = UnityEngine.Object.FindFirstObjectByType<Map01Mission>();
                var inventory = mission.GetComponent<Map01Inventory>();
                var saveSystem = mission.GetComponent<Map01SaveSystem>();
                Assert.IsNotNull(mission);
                Assert.IsTrue(mission.IsInitialized);
                Assert.IsFalse(ForestMenu.Visible);
                var player = mission.player.GetComponent<PlayerController>();
                var combat = player.GetComponent<PlayerCombat>();
                var health = player.GetComponent<Health>();
                Assert.IsTrue(combat.UsesInventoryHotkeys);
                Assert.IsTrue(player.InputAllowed());
                mission.SetInventoryOpen(true);
                Assert.IsFalse(player.InputAllowed());
                Assert.IsFalse(combat.InputAllowed());
                mission.SetInventoryOpen(false);
                mission.SetPaused(true);
                Assert.IsFalse(player.InputAllowed());
                Assert.IsFalse(combat.InputAllowed());
                mission.SetPaused(false);
                mission.Damage(30);
                Assert.AreEqual(health.Current, mission.PlayerHealth);
                combat.Equip(WeaponKind.Knife);
                int ammo = inventory.Count("ammo_rifle");
                Assert.IsTrue(combat.TryConsumeRound());
                Assert.AreEqual(ammo - 1, inventory.Count("ammo_rifle"));
                var enemies = UnityEngine.Object.FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None);
                Assert.Greater(enemies.Length, 1);
                var wounded = enemies[0]; var dead = enemies[1];
                string woundedId = wounded.SaveId, deadId = dead.SaveId;
                wounded.GetComponent<Health>().RestoreHealth(23);
                wounded.Hear(wounded.transform.position, 10);
                dead.GetComponent<Health>().RestoreHealth(0);
                Assert.IsFalse(saveSystem.CanSave(), "Alerted enemies must block manual backups.");
                Assert.IsTrue(saveSystem.AutoSaveOnExit(out var error), error);
                Map01SaveSystem.BeginGame(ForestSaveSlots.AutoSlot);
                yield return null;
                var pending = typeof(Map01SaveSystem).GetField("pendingCheckpoint", BindingFlags.NonPublic | BindingFlags.Static);
                double deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (pending.GetValue(null) != null && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.IsNull(pending.GetValue(null));
                mission = UnityEngine.Object.FindFirstObjectByType<Map01Mission>();
                inventory = mission.GetComponent<Map01Inventory>();
                mission.SetPaused(true);
                Assert.AreEqual(70, mission.player.GetComponent<Health>().Current);
                Assert.AreEqual(WeaponKind.Knife, mission.player.GetComponent<PlayerCombat>().EquippedKind);
                Assert.AreEqual(ammo - 1, inventory.Count("ammo_rifle"));
                enemies = UnityEngine.Object.FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None);
                wounded = enemies.Single(e => e.SaveId == woundedId);
                Assert.AreEqual(23, wounded.GetComponent<Health>().Current);
                Assert.IsTrue(wounded.Alerted);
                Assert.IsFalse(enemies.Single(e => e.SaveId == deadId).Alive);
                Assert.IsNotNull(enemies.Single(e => e.SaveId == deadId).GetComponent<ForestPoint>(), "Checkpoint corpses retain their loot.");
            } finally {
                rootField.SetValue(null, null);
                Time.timeScale = 1;
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            yield return new ExitPlayMode();
        }
    }
}
