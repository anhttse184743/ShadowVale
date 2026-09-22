using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    public sealed class ForestAutoSaveTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(ForestMission mission, string field, object value) => typeof(ForestMission).GetField(field, Private).SetValue(mission, value);

        [UnityTest]
        public IEnumerator ExitSavePreservesBackupsCombatCraftingAndBlocksFailedExit()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map01_ForestFootprints.unity");
            yield return new EnterPlayMode();
            yield return null;
            string root = Path.Combine(Application.temporaryCachePath, "exit-save-" + Guid.NewGuid().ToString("N"));
            var rootField = typeof(ForestSaveSlots).GetField("storageRoot", BindingFlags.Static | BindingFlags.NonPublic);
            rootField.SetValue(null, root);
            try {
                var mission = UnityEngine.Object.FindFirstObjectByType<ForestMission>();
                var menu = UnityEngine.Object.FindFirstObjectByType<ForestMenu>();
                mission.SetPaused(true);
                Assert.IsTrue(mission.SaveSlot(1, out var error), error);
                Assert.IsTrue(mission.SaveSlot(0, out error), error);
                string backup = File.ReadAllText(ForestSaveSlots.PathFor(1));
                string quick = File.ReadAllText(ForestSaveSlots.PathFor(0));
                var guard = UnityEngine.Object.FindFirstObjectByType<ForestGuard>();
                string guardId = guard.id;
                guard.hp = 7; guard.state = ForestGuardState.Engage; guard.suspicion = 1;
                Set(mission, "crafting", "craft_medkit_small"); Set(mission, "craftUntil", Time.time + 8);
                Assert.IsFalse(mission.SaveSlot(2, out _), "Manual combat saves must remain restricted.");
                // This is also the handler for native window close / Alt+F4 in the player.
                Assert.IsTrue((bool)typeof(ForestMenu).GetMethod("WantsToQuit", Private).Invoke(menu, null));
                Assert.IsTrue(ForestSaveSlots.Exists(ForestSaveSlots.AutoSlot));
                Assert.AreEqual(backup, File.ReadAllText(ForestSaveSlots.PathFor(1)));
                Assert.AreEqual(quick, File.ReadAllText(ForestSaveSlots.PathFor(0)));
                typeof(ForestMenu).GetMethod("GoToTitle", Private).Invoke(menu, null);
                yield return null;
                Assert.AreEqual("01_MainMenu", SceneManager.GetActiveScene().name);
                Assert.AreEqual(ForestSaveSlots.AutoSlot, ForestSaveSlots.Latest());
                ForestMission.BeginGame(ForestSaveSlots.AutoSlot);
                yield return null;
                var pending = typeof(ForestMission).GetField("pendingCheckpoint", BindingFlags.Static | BindingFlags.NonPublic);
                double deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (pending.GetValue(null) != null && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.IsNull(pending.GetValue(null));
                mission = UnityEngine.Object.FindFirstObjectByType<ForestMission>(); mission.SetPaused(true);
                guard = Array.Find(UnityEngine.Object.FindObjectsByType<ForestGuard>(FindObjectsSortMode.None), g => g.id == guardId);
                Assert.AreEqual(7, guard.hp);
                Assert.AreNotEqual(ForestGuardState.Patrol, guard.state);
                Assert.AreEqual("craft_medkit_small", typeof(ForestMission).GetField("crafting", Private).GetValue(mission));
                Assert.Greater((float)typeof(ForestMission).GetField("craftUntil", Private).GetValue(mission) - Time.time, 5);
                var autosave = File.ReadAllText(ForestSaveSlots.PathFor(ForestSaveSlots.AutoSlot));
                Set(mission, "hp", 0f);
                Assert.IsTrue(mission.AutoSaveOnExit(out error), error);
                Assert.AreEqual(autosave, File.ReadAllText(ForestSaveSlots.PathFor(ForestSaveSlots.AutoSlot)), "Death must not overwrite a usable save.");
                Set(mission, "hp", 10f);
                File.WriteAllText(Path.Combine(root, "blocked"), "not a directory");
                rootField.SetValue(null, Path.Combine(root, "blocked"));
                menu = UnityEngine.Object.FindFirstObjectByType<ForestMenu>();
                Assert.IsFalse((bool)typeof(ForestMenu).GetMethod("WantsToQuit", Private).Invoke(menu, null));
                typeof(ForestMenu).GetMethod("GoToTitle", Private).Invoke(menu, null);
                Assert.AreEqual("Map01_ForestFootprints", SceneManager.GetActiveScene().name);
                Assert.IsTrue(ForestMenu.Visible);
                Assert.IsTrue(mission.Paused);
            } finally {
                rootField.SetValue(null, null); Time.timeScale = 1;
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            yield return new ExitPlayMode();
        }
    }
}
