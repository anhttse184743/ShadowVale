using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// Point 3's command post (Map01C3Mission) on the approved C3 art: the garrison on its posts and the commander in
    /// his headquarters from the start; nest A sees the tower's platform beyond the Mosin's 30 m report; the scouts' Mosin
    /// appears with the commander order; the kẻng wakes the sleepers; without its striker only one comes, and the
    /// telephone line brings runners; a silent patrol at the check-in puts the camp on guard; the knife from behind
    /// wounds the commander and never kills him; no pursuit past 40 m; and the save keeps it all.
    /// </summary>
    public sealed class C3MissionTests : ForestSceneTestBase
    {
        // Lambdas stay in static helpers or capture only static fields (the domain reload of EnterPlayMode).
        private static Map01Mission mission;
        private static Map01Quest quest;
        private static Map01C3Mission c3;
        private static Map01Inventory inventory;

        private static void OpenMap()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
        }

        private static void Settle(bool freezeGuards = true)
        {
            mission = Object.FindFirstObjectByType<Map01Mission>();
            quest = mission.GetComponent<Map01Quest>();
            c3 = mission.GetComponent<Map01C3Mission>();
            inventory = mission.GetComponent<Map01Inventory>();
            mission.GetComponent<Map01Rescue>().MarkIntroSeen();
            if (!freezeGuards) return;
            foreach (var enemy in mission.Enemies)
            {
                enemy.enabled = false;
                var agent = enemy.GetComponent<NavMeshAgent>();
                if (agent != null) agent.enabled = false;
            }
        }

        private static void StandAt(Vector3 at, Vector3 facing)
        {
            var controller = mission.player.GetComponent<CharacterController>();
            controller.enabled = false;
            mission.player.SetPositionAndRotation(at, Quaternion.LookRotation(Vector3.ProjectOnPlane(facing - at, Vector3.up)));
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static bool InHeadquarters(Vector3 p) => p.x > 63.5f && p.x < 72.5f && p.z > 61.5f && p.z < 69f;
        private static int ActiveSleepers() => c3.Sleepers.Count(s => s.gameObject.activeSelf);
        private static bool Listed(Map01EnemyController guard) => mission.Enemies.Contains(guard);

        [UnityTest]
        public IEnumerator TheGarrisonHoldsItsPostsAndTheCommanderIsHomeFromTheStart()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            Assert.IsNotNull(c3, "The mission sets up point 3.");
            Assert.IsNotNull(c3.Data, "Resources/Missions/Map01C3Layout.json loads.");
            foreach (var role in new[] { "tower", "gate", "rover", "porch", "patrolSouth", "patrolNorth" })
                Assert.IsNotNull(c3.Guard(role), role + " has a guard.");
            Assert.AreEqual(12.53f, c3.Guard("tower").transform.position.y, .05f, "The sentry stands on the tower's platform.");
            Assert.Less(Vector3.Distance(c3.Guard("gate").transform.position, new Vector3(68, c3.Guard("gate").transform.position.y, 44)), 1.5f);

            // The commander: one actor from the start, at home, never cloned at a stage change.
            Assert.Less(quest.Stage, Map01Quest.BossStage);
            Assert.IsTrue(quest.BossSpawned, "He is there before the order.");
            Assert.IsTrue(InHeadquarters(quest.CommanderEnemy.transform.position), "In his headquarters: " + quest.CommanderEnemy.transform.position);
            Assert.AreEqual(1, mission.Enemies.Count(e => e.IsBoss));

            // The reserve: off the field, out of the enemy list, until called.
            Assert.AreEqual(3, c3.Sleepers.Count);
            Assert.AreEqual(0, ActiveSleepers());
            Assert.IsFalse(c3.Sleepers.Any(Listed), "Sleepers are not counted as guards on the field.");
            Assert.IsTrue(mission.Points.Contains(c3.KiengPoint) && mission.Points.Contains(c3.PhonePoint));
            Assert.GreaterOrEqual(c3.CoverPoints.Count, 12, "Cover positions for the commander's fight.");

            // The scouts' Mosin: hidden until the order, then a pick-up of the rifle and five rounds.
            Assert.IsFalse(c3.CachePoint.isActiveAndEnabled, "No cache before the commander order.");
            quest.RestoreStage(Map01Quest.BossStage);
            yield return null; yield return null;
            Assert.IsTrue(c3.CachePoint.isActiveAndEnabled);
            CollectionAssert.AreEquivalent(new[] { "rifle_mosin1", "ammo_sniper5" }, c3.CachePoint.items.Select(i => i.item_id + i.count));
            int rifles = inventory.Count("rifle_mosin"), rounds = inventory.Count("ammo_sniper");
            mission.GetComponent<Map01PlayerInteraction>().Interact(c3.CachePoint);
            Assert.AreEqual(rifles + 1, inventory.Count("rifle_mosin"));
            Assert.AreEqual(rounds + 5, inventory.Count("ammo_sniper"));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator NestASeesTheTowerPlatformBeyondTheMosinsReport()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            var nest = c3.At(c3.Data.nestA[0], c3.Data.nestA[1]) + Vector3.up * 1.5f;
            var sentry = c3.Guard("tower").transform.position + Vector3.up * 1.3f;
            Assert.GreaterOrEqual(Vector3.Distance(nest, sentry), 31f, "Beyond the 30 m the Mosin is heard over.");
            Assert.GreaterOrEqual(Vector3.Distance(nest, c3.Guard("gate").transform.position), 31f);
            bool blocked = Physics.Linecast(nest, sentry, out var hit, ~0, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(c3.Guard("tower").transform);
            Assert.IsFalse(blocked, "Nest A sees the sentry; blocked by " + (blocked ? hit.collider.name : ""));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator TheKiengWakesTheSleepersWhenTheCampIsAlarmed()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            StandAt(mission.player.position, mission.player.position + Vector3.forward);
            var gate = c3.Guard("gate");
            gate.enabled = true;
            gate.Alert(gate.transform.position + Vector3.forward * 5);
            yield return null;
            Assert.IsTrue(c3.Alarmed);
            yield return WaitGameSeconds(c3.Data.kiengDelaySeconds + .5f);
            Assert.IsTrue(c3.Current.kiengRung, "The kẻng is struck.");
            Assert.AreEqual(3, ActiveSleepers(), "Every sleeper comes out.");
            Assert.IsTrue(c3.Sleepers.All(Listed), "…and counts as a guard now.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator WithoutTheStrikerOneSleeperAnswersAndTheLineBringsRunners()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            Assert.IsTrue(c3.Use(c3.KiengPoint));
            Assert.IsTrue(c3.KiengRemoved);
            Assert.IsTrue(c3.KiengPoint.used);
            Assert.IsFalse(c3.Use(c3.KiengPoint), "Taken once.");
            Assert.AreEqual(1 + 2, c3.CallReinforcements(), "One sleeper hears the shout; two runners come down the wire.");
            Assert.AreEqual(0, c3.CallReinforcements(), "He calls once.");
            Assert.IsFalse(c3.Current.kiengRung, "Without its striker the kẻng is not struck.");
            yield return WaitGameSeconds(c3.Data.sleeperDelayWithoutKieng + .4f);
            Assert.AreEqual(1, ActiveSleepers());
            yield return WaitGameSeconds(c3.Data.runners.delaySeconds - c3.Data.sleeperDelayWithoutKieng + .4f);
            Assert.AreEqual(2, c3.RunnerGuards.Count(r => r.gameObject.activeSelf && Listed(r)));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ACutLineMeansNoRunners()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            Assert.IsTrue(c3.Use(c3.PhonePoint));
            Assert.IsTrue(c3.PhoneCut);
            Assert.AreEqual(3, c3.CallReinforcements(), "The kẻng still wakes all three; nobody comes down the wire.");
            yield return WaitGameSeconds(c3.Data.runners.delaySeconds + .5f);
            Assert.IsFalse(c3.RunnerGuards.Any(r => r.gameObject.activeSelf));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ASilentPatrolAtTheCheckInPutsTheCampOnGuard()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            c3.Guard("patrolSouth").GetComponent<Health>().TakeDamage(99999, Vector3.zero, null);
            c3.Current.checkInRemaining = .2f;
            yield return WaitGameSeconds(.5f);
            Assert.IsTrue(c3.Wary, "Tổ một did not answer.");
            var north = c3.Guard("patrolNorth").transform.position;
            Assert.Less(new Vector2(north.x - 70.5f, north.z - 39.2f).magnitude, 1.5f, "The other patrol holds before the gate.");
            Assert.AreEqual(c3.Data.checkInSeconds, c3.CheckInRemaining, .5f);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator TheKnifeFromBehindWoundsTheCommanderAndNeverKillsHim()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            var boss = quest.CommanderEnemy;
            var health = boss.GetComponent<Health>();
            Assert.IsTrue(inventory.EquipItem("knife"));
            yield return null;
            Assert.AreEqual(WeaponKind.Knife, mission.ModernCombat.EquippedKind);
            StandAt(boss.transform.position - boss.transform.forward * 1.1f, boss.transform.position);
            yield return null;
            Assert.IsTrue(c3.CanAmbush(), "Behind him with the knife.");
            Assert.IsTrue(c3.TryAmbush());
            Assert.IsTrue(boss.Alive, "A commander does not die quietly.");
            Assert.AreEqual(health.Max * (1 - c3.Data.ambushDamage), health.Current, 1f);
            Assert.IsFalse(c3.CanAmbush(), "Once.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator NobodyChasesNamPastTheLeash()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            var rover = c3.Guard("rover");
            rover.Alert(rover.transform.position + Vector3.right * 3);
            Assert.IsTrue(rover.Engaged);
            StandAt(new Vector3(-30, 6, 10), new Vector3(0, 6, 10));
            yield return null; yield return null;
            Assert.IsFalse(rover.Engaged, "Nam is more than 40 m beyond the fence: he goes back to his post.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator TheSaveKeepsThePostAsItWas()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            c3.Use(c3.KiengPoint);
            c3.Use(c3.PhonePoint);
            c3.CallReinforcements();
            yield return WaitGameSeconds(c3.Data.sleeperDelayWithoutKieng + .4f);
            Assert.AreEqual(1, ActiveSleepers());
            var saved = c3.Capture();

            c3.Restore(null);
            Assert.IsFalse(c3.KiengRemoved); Assert.IsFalse(c3.PhoneCut);
            Assert.AreEqual(0, ActiveSleepers(), "A fresh post: the sleepers are back in bed.");

            c3.Restore(saved);
            Assert.IsTrue(c3.KiengRemoved); Assert.IsTrue(c3.PhoneCut); Assert.IsTrue(c3.Current.called);
            Assert.AreEqual(1, ActiveSleepers());
            Assert.IsTrue(c3.Sleepers.Where(s => s.gameObject.activeSelf).All(Listed));
            yield return new ExitPlayMode();
        }
    }
}
