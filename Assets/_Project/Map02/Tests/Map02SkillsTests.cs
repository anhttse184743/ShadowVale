using System.Collections;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Skills;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class Map02SkillsTests : ForestSceneTestBase
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Place(Map02Mission mission, Vector3 position)
        {
            var body = mission.Player.GetComponent<CharacterController>();
            bool active = body.enabled; body.enabled = false; mission.Player.position = position; body.enabled = active;
            Physics.SyncTransforms();
        }

        [UnityTest, Timeout(240000)] public IEnumerator MapTwoAwardsCombatSkillsAndRestoresCareerAcrossFailure()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");
            yield return new EnterPlayMode();
            Map02Mission m = null; float deadline = Time.realtimeSinceStartup + 100;
            while (Time.realtimeSinceStartup < deadline)
            {
                m = Object.FindFirstObjectByType<Map02Mission>();
                if (m?.Ready == true && m.Bag != null) break;
                Object.FindFirstObjectByType<Map02Arrival>()?.Skip(); yield return null;
            }
            Assert.NotNull(m?.Skills); m.Chapter.enabled = false;
            m.Chapter.Cancel(); m.World.story.stage = Map02Stage.MissingScout;
            var skills = m.Skills; var combat = m.Player.GetComponent<PlayerCombat>();
            skills.Restore(new SkillProgress()); skills.CompleteMission(); m.SyncSkills();
            Assert.That(skills.MissionId, Does.StartWith("map2_"));
            Assert.IsNotEmpty(m.Player.GetComponent<SkillCampaignPersistence>().RunId);
            Assert.IsTrue(NavMesh.SamplePosition(m.Player.position, out var walkable, 20, NavMesh.AllAreas));

            // Use the real lethal gun trace, not an XP call or a scripted death.
            var go = new GameObject("Map 2 skill target");
            go.transform.position = walkable.position;
            var enemy = go.AddComponent<Map02ChapterEnemy>();
            enemy.Initialize(m.Chapter, "skill_target", "test", 100, false);
            enemy.enabled = false; enemy.GetComponent<NavMeshAgent>().enabled = false;
            go.transform.position = new Vector3(0, 200, 0);
            m.Chapter.Enemies.Add(enemy);
            Physics.SyncTransforms();
            var centre = go.GetComponent<Collider>().bounds.center;
            Camera.main.transform.SetPositionAndRotation(centre - Vector3.forward * 2, Quaternion.identity);
            combat.Equip(WeaponKind.Rifle);
            var gun = (Weapon)typeof(PlayerCombat).GetField("_equipped", Private).GetValue(combat);
            var fire = typeof(PlayerCombat).GetMethod("FireGun", Private);
            var spread = (WeaponSpreadState)typeof(PlayerCombat).GetField("_spread", Private).GetValue(combat);
            for (int i = 0; i < 30 && !enemy.Dead; i++) { spread.Reset(); fire.Invoke(combat, new object[] { gun }); }
            Assert.IsTrue(enemy.Dead); Assert.AreEqual(1, skills.Xp(SkillKind.Shooting));

            var stealthGo = new GameObject("Map 2 stealth target");
            stealthGo.transform.position = walkable.position;
            var stealth = stealthGo.AddComponent<Map02ChapterEnemy>();
            stealth.Initialize(m.Chapter, "stealth_target", "test", 100, false);
            stealth.enabled = false; stealth.GetComponent<NavMeshAgent>().enabled = false;
            stealthGo.transform.position = new Vector3(5, 200, 0);
            m.Chapter.Enemies.Add(stealth);
            combat.Equip(WeaponKind.Knife);
            Place(m, stealthGo.transform.position + Vector3.forward); Physics.SyncTransforms();
            Assert.IsFalse(stealth.CanSilentTakedown(m), "Front approach must fail.");
            Place(m, stealthGo.transform.position - Vector3.forward * 1.4f); Physics.SyncTransforms();
            Assert.IsTrue(m.TrySkillTakedown()); Assert.IsTrue(stealth.Dead);
            Assert.AreEqual(1, skills.Xp(SkillKind.Stealth));
            Assert.IsFalse(stealth.CanSilentTakedown(m), "Corpse cannot award again.");

            m.Chapter.Enemies.Remove(enemy); m.Chapter.Enemies.Remove(stealth);
            Object.DestroyImmediate(go); Object.DestroyImmediate(stealthGo);
            Place(m, m.CharacterWorkpoints[0].transform.position);
            var primary = Map01Inventory.Gun.InSlot(Map01Inventory.Slot.Primary);
            m.Bag.Wear(Map01Inventory.Slot.Primary, 30);
            m.Bag.Receive("weapon_parts", 10); m.Bag.Receive("gun_oil", 3);
            Assert.IsTrue(m.Bag.BenchRepair(primary), m.Bag.BenchRepairRefusal(primary));
            Assert.AreEqual(1, skills.Xp(SkillKind.Engineering));
            Assert.IsFalse(m.Bag.BenchRepair(primary), "Repairing a full weapon cannot award again.");
            Assert.AreEqual(1, skills.Xp(SkillKind.Engineering));
            skills.Award(SkillKind.Engineering, 2);
            Assert.AreEqual(skills.FieldRepairCap, m.Bag.FieldCap, .0001f);
            var saved = m.Capture(); m.Restore(saved);
            Assert.AreEqual(1, skills.Xp(SkillKind.Shooting)); Assert.AreEqual(1, skills.Xp(SkillKind.Stealth));
            Assert.AreEqual(3, skills.Xp(SkillKind.Engineering));
            m.Player.GetComponent<Health>().CinematicInvulnerable = false;
            m.Player.GetComponent<Health>().TakeDamage(10000, m.Player.position, null); m.SyncSkills();
            Assert.IsTrue(skills.MissionFailed);
            Assert.AreEqual(0, skills.Xp(SkillKind.Shooting)); Assert.AreEqual(0, skills.Xp(SkillKind.Stealth));
            m.Restore(saved);
            Assert.AreEqual(0, skills.Xp(SkillKind.Shooting), "An older save cannot recover failed-attempt XP.");
            Assert.AreEqual(0, skills.Xp(SkillKind.Stealth)); Assert.AreEqual(3, skills.Xp(SkillKind.Engineering));
            Assert.IsFalse(skills.MissionFailed);
            yield return new ExitPlayMode();
        }
    }
}
