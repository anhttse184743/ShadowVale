using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Skills;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    public sealed class SkillIntegrationTests : ForestSceneTestBase
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [UnityTest] public IEnumerator MissionXpSurvivesResumeButRollsBackOnFailureWhileEngineeringPersists()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode(); yield return null; yield return null;
            var rootField = typeof(ForestSaveSlots).GetField("storageRoot", BindingFlags.Static | BindingFlags.NonPublic);
            object previousRoot = rootField.GetValue(null);
            string root = System.IO.Path.Combine(Application.temporaryCachePath, "skill-mission-flow-" + System.Guid.NewGuid().ToString("N"));
            rootField.SetValue(null, root);
            try
            {
                var mission = Object.FindFirstObjectByType<Map01Mission>();
                var save = mission.GetComponent<Map01SaveSystem>();
                mission.Cinematic = false;
                Assert.AreEqual("rescue", mission.Skills.MissionId);
                mission.Skills.Award(SkillKind.Shooting, 3); mission.Skills.Award(SkillKind.Stealth);
                mission.Skills.Award(SkillKind.Engineering, 2);
                string attempt = mission.Skills.CaptureMission().attemptId;
                Assert.IsTrue(save.SaveSlot(1, out var error, true), error);

                Map01SaveSystem.BeginGame(1); yield return WaitForSkillRestore();
                mission = Object.FindFirstObjectByType<Map01Mission>(); save = mission.GetComponent<Map01SaveSystem>();
                Assert.AreEqual(3, mission.Skills.Xp(SkillKind.Shooting));
                Assert.AreEqual(1, mission.Skills.Xp(SkillKind.Stealth));
                Assert.AreEqual(2, mission.Skills.Xp(SkillKind.Engineering));
                Assert.AreEqual(0, mission.Skills.CaptureMission().shooting, "A normal resume must keep the original baseline.");
                mission.Damage(10000); yield return null; yield return null;
                Assert.IsTrue(mission.Skills.MissionFailed);
                Assert.AreEqual(0, mission.Skills.Xp(SkillKind.Shooting));
                Assert.AreEqual(0, mission.Skills.Xp(SkillKind.Stealth));
                Assert.AreEqual(2, mission.Skills.Xp(SkillKind.Engineering));
                Assert.IsTrue(save.AutoSaveOnExit(out error), error);

                // Even the older manual save of the failed attempt must not restore provisional XP.
                Map01SaveSystem.BeginGame(1); yield return WaitForSkillRestore();
                mission = Object.FindFirstObjectByType<Map01Mission>(); save = mission.GetComponent<Map01SaveSystem>();
                Assert.Greater(mission.PlayerHealth, 0);
                Assert.AreEqual(0, mission.Skills.Xp(SkillKind.Shooting));
                Assert.AreEqual(0, mission.Skills.Xp(SkillKind.Stealth));
                Assert.AreEqual(2, mission.Skills.Xp(SkillKind.Engineering));
                Assert.AreNotEqual(attempt, mission.Skills.CaptureMission().attemptId);
                Assert.IsFalse(mission.Skills.MissionFailed);

                // Finishing rescue commits its XP; failing escort only removes escort gains.
                mission.Skills.Award(SkillKind.Shooting, 2); mission.Skills.Award(SkillKind.Stealth);
                mission.GetComponent<Map01Quest>().RestoreStage(Map01Quest.EscortStage); save.SyncSkillMission();
                Assert.AreEqual("escort", mission.Skills.MissionId);
                mission.Skills.Award(SkillKind.Shooting, 5); mission.Skills.Award(SkillKind.Stealth, 2);
                mission.Skills.Award(SkillKind.Engineering, 2);
                mission.Damage(10000); yield return null; yield return null;
                Assert.AreEqual(2, mission.Skills.Xp(SkillKind.Shooting));
                Assert.AreEqual(1, mission.Skills.Xp(SkillKind.Stealth));
                Assert.AreEqual(4, mission.Skills.Xp(SkillKind.Engineering));
                Assert.IsTrue(save.AutoSaveOnExit(out error), error);
                Map01SaveSystem.BeginGame(ForestSaveSlots.AutoSlot); yield return WaitForSkillRestore();
                mission = Object.FindFirstObjectByType<Map01Mission>();
                Assert.AreEqual(Map01Quest.EscortStage, mission.Stage);
                Assert.AreEqual(2, mission.Skills.Xp(SkillKind.Shooting));
                Assert.AreEqual(1, mission.Skills.Xp(SkillKind.Stealth));
                Assert.AreEqual(4, mission.Skills.Xp(SkillKind.Engineering));
                Assert.IsFalse(mission.Skills.MissionFailed);
            }
            finally
            {
                rootField.SetValue(null, previousRoot);
                string full = System.IO.Path.GetFullPath(root), boundary = System.IO.Path.GetFullPath(Application.temporaryCachePath) + System.IO.Path.DirectorySeparatorChar;
                if (!full.StartsWith(boundary, System.StringComparison.OrdinalIgnoreCase)) throw new System.InvalidOperationException("Invalid test path.");
                if (System.IO.Directory.Exists(full)) System.IO.Directory.Delete(full, true);
            }
            yield return new ExitPlayMode();
        }

        private static IEnumerator WaitForSkillRestore()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (Map01SaveSystem.IsRestoring && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsFalse(Map01SaveSystem.IsRestoring);
            yield return null;
        }

        [UnityTest] public IEnumerator CheckpointOwnsXpAndHudRendersHoverAtBothResolutions()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode(); yield return null; yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var skills = mission.Skills;
            Assert.IsNotNull(skills);
            skills.Award(SkillKind.Shooting, 3); skills.Award(SkillKind.Stealth);
            var save = mission.GetComponent<Map01SaveSystem>();
            var capture = typeof(Map01SaveSystem).GetMethod("Capture", Private);
            var checkpoint = capture.Invoke(save, new object[] { true });
            var json = JsonUtility.ToJson(checkpoint);
            Assert.That(json, Does.Contain("\"shooting\":3"));
            var snapshot = (SkillProgress)checkpoint.GetType().GetField("skills").GetValue(checkpoint);
            skills.Award(SkillKind.Shooting, 5);
            skills.Restore(snapshot);
            Assert.AreEqual(3, skills.Xp(SkillKind.Shooting), "Checkpoint must own a copy, including retry rollbacks.");
            Assert.AreEqual(1, skills.Level(SkillKind.Shooting));
            var oldSave = JsonUtility.FromJson(json.Replace("\"skills\":{\"shooting\":3,\"stealth\":1,\"engineering\":0},", ""), checkpoint.GetType());
            skills.Restore((SkillProgress)oldSave.GetType().GetField("skills").GetValue(oldSave));
            Assert.AreEqual(0, skills.Xp(SkillKind.Shooting), "Old saves must start at the base level.");
            skills.Restore(snapshot);

            // Scripted deaths are not player gun kills and must not grant progression.
            var guard = mission.Enemies.First(e => e.Alive);
            guard.GetComponent<Health>().TakeDamage(10000, guard.transform.position, mission.player.gameObject);
            Assert.AreEqual(3, skills.Xp(SkillKind.Shooting));
            Assert.AreEqual(1, skills.Xp(SkillKind.Stealth));

            // Exercise the actual gun trace/death event: only its lethal transition awards XP.
            foreach (var enemy in mission.Enemies) enemy.enabled = false;
            var gunGuard = mission.Enemies.First(e => e.Alive && !e.IsBoss);
            var agent = gunGuard.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            gunGuard.transform.position = new Vector3(0, 200, 0);
            var collider = gunGuard.GetComponent<Collider>();
            Assert.IsNotNull(collider);
            Physics.SyncTransforms();
            var cam = Camera.main;
            Assert.IsNotNull(cam);
            Vector3 centre = collider.bounds.center;
            cam.transform.SetPositionAndRotation(centre - Vector3.forward * 2, Quaternion.identity);
            mission.Cinematic = false;
            mission.ModernCombat.Equip(WeaponKind.Rifle);
            var originalReserve = mission.ModernCombat.ReserveRounds;
            mission.ModernCombat.ReserveRounds = () => 90;
            mission.ModernCombat.RestoreMagazine(10);
            Assert.IsTrue(mission.ModernCombat.TryReload());
            var magazine = (Magazine)typeof(PlayerCombat).GetField("_magazine", Private).GetValue(mission.ModernCombat);
            Assert.AreEqual(2.376f, magazine.ReloadRemaining, .00001f);
            mission.ModernCombat.RestoreMagazine(0);
            Assert.IsTrue(mission.ModernCombat.TryReload());
            Assert.AreEqual(2.376f, magazine.ReloadRemaining, .00001f);
            mission.ModernCombat.RestoreMagazine(30);
            mission.ModernCombat.ReserveRounds = originalReserve;
            var gun = (Weapon)typeof(PlayerCombat).GetField("_equipped", Private).GetValue(mission.ModernCombat);
            var fire = typeof(PlayerCombat).GetMethod("FireGun", Private);
            var spread = (WeaponSpreadState)typeof(PlayerCombat).GetField("_spread", Private).GetValue(mission.ModernCombat);
            for (int shot = 0; shot < 20 && gunGuard.Alive; shot++)
            { spread.Reset(); fire.Invoke(mission.ModernCombat, new object[] { gun }); }
            Assert.IsFalse(gunGuard.Alive, "The real gun trace must hit and kill its target.");
            Assert.AreEqual(4, skills.Xp(SkillKind.Shooting));
            fire.Invoke(mission.ModernCombat, new object[] { gun });
            Assert.AreEqual(4, skills.Xp(SkillKind.Shooting), "A corpse cannot pay XP twice.");
            // Run the production checkpoint restore, not merely the skill DTO round trip.
            typeof(Map01SaveSystem).GetField("pendingCheckpoint", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, json);
            typeof(Map01SaveSystem).GetMethod("Restore", Private).Invoke(save, null);
            Assert.AreEqual(3, skills.Xp(SkillKind.Shooting));
            Assert.AreEqual(1, skills.Xp(SkillKind.Stealth));
            Assert.IsFalse(Map01SaveSystem.IsRestoring);

            mission.Cinematic = false; mission.SetInventoryOpen(true); mission.SetPaused(true);
            var hud = mission.GetComponent<Map01Hud>();
            typeof(Map01Hud).GetField("skillsTab", Private).SetValue(hud, true);
            typeof(Map01Hud).GetField("pinnedSkill", Private).SetValue(hud, 0);
            typeof(Map01Hud).GetField("pinnedLevel", Private).SetValue(hud, 1);
            System.IO.Directory.CreateDirectory("Logs/SkillPreview");
            foreach (int width in new[] { 1600, 1280 })
            {
                SetPreviewResolution(width, width == 1600 ? 900 : 720);
                for (int skill = 0; skill < 3; skill++)
                {
                    typeof(Map01Hud).GetField("pinnedSkill", Private).SetValue(hud, skill);
                    yield return null; yield return null;
                    string name = skill == 0 ? "skills" : skill == 1 ? "stealth" : "engineering";
                    ScreenCapture.CaptureScreenshot($"Logs/SkillPreview/{name}-{width}.png");
                    yield return null; yield return null;
                }
            }
            yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator StealthExtendsEligibleRangeButKeepsObstructionsAndAwardsOnce()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode(); yield return null; yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var overseer = mission.GetComponent<Map01Rescue>().Squad[0];
            var guard = mission.GetComponent<Map01Rescue>().Squad[1];
            foreach (var enemy in mission.Enemies) enemy.enabled = false;
            var controller = mission.player.GetComponent<CharacterController>();
            controller.enabled = false;
            mission.player.SetPositionAndRotation(overseer.transform.position - overseer.transform.forward * 2.12f, overseer.transform.rotation);
            controller.enabled = true;
            mission.ModernPlayer.RestoreMotion(true); mission.Crouched = true;
            mission.ModernCombat.Equip(WeaponKind.Knife);
            Physics.SyncTransforms();
            Assert.IsFalse(overseer.CanSilentTakedown(), "Base overseer range is only 2 m.");
            mission.Skills.Award(SkillKind.Stealth, 3);
            Assert.IsFalse(overseer.CanSilentTakedown(), "Stealth must not upgrade the rescue overseer.");
            mission.Skills.Restore(null);
            controller.enabled = false;
            mission.player.SetPositionAndRotation(guard.transform.position - guard.transform.forward * 1.68f, guard.transform.rotation);
            controller.enabled = true;
            Physics.SyncTransforms();
            Assert.IsFalse(guard.CanSilentTakedown(), "Base ordinary guard range is only 1.6 m.");
            mission.Skills.Award(SkillKind.Stealth, 3);
            Assert.IsTrue(guard.CanSilentTakedown(), "Level 2 makes the same rear approach eligible.");
            Assert.IsTrue(mission.GetComponent<Map01Rescue>().GetTakedownHint(out var hint, out bool ready));
            Assert.AreSame(guard, hint);
            Assert.IsTrue(ready, "The ready signal must use the same upgraded range as the actual takedown.");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = (mission.player.position + guard.transform.position) / 2 + Vector3.up;
            wall.transform.localScale = new Vector3(.4f, 2, .4f);
            Physics.SyncTransforms();
            Assert.IsFalse(guard.TrySilentTakedown(), "Upgrades cannot bypass a wall.");
            Assert.AreEqual(3, mission.Skills.Xp(SkillKind.Stealth));
            Object.DestroyImmediate(wall); Physics.SyncTransforms();
            Assert.IsTrue(guard.TrySilentTakedown());
            Assert.IsFalse(guard.Alive);
            Assert.AreEqual(4, mission.Skills.Xp(SkillKind.Stealth));
            Assert.IsFalse(guard.TrySilentTakedown());
            Assert.AreEqual(4, mission.Skills.Xp(SkillKind.Stealth));
            Assert.AreEqual(0, mission.Skills.Xp(SkillKind.Shooting));
            yield return new ExitPlayMode();
        }
    }
}
