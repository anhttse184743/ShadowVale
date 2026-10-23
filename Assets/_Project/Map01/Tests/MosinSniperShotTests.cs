using System.Collections;
using NUnit.Framework;
using ShadowVale.AI.Fsm;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// The Mosin as a stealth rifle (2026-10-22). One round kills a guard it reaches, and rounds go through thatch and
    /// zinc roofs: C3's watchtower eave hangs at the sentry's head, so from the ground the round aimed at the head he
    /// shows under it used to stop in the roof. A shot is placed only by a guard within
    /// <see cref="Map01Mission.ShotPinpointMeters"/>; one further off, out to its 30 m, searches roughly that way and
    /// finds nobody out.
    /// </summary>
    public sealed class MosinSniperShotTests : ForestSceneTestBase
    {
        private static int detections;
        private static Map01DetectionCause lastCause;
        private static void CountDetection(Map01Detection d) { detections++; lastCause = d.cause; }

        [UnityTest]
        public IEnumerator OneRoundFromTheGroundKillsTheTowerSentryUnderHisRoof()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return WaitForMap01Ready();
            for (int i = 0; i < 20; i++) yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            mission.GetComponent<Map01Rescue>().MarkIntroSeen();
            var bag = mission.GetComponent<Map01Inventory>();
            bag.Add("rifle_mosin", 1); bag.Add("ammo_sniper", 5);
            int cell = -1;
            for (int i = 0; i < Map01Inventory.BagSize; i++) if (bag.Cell(i)?.id == "rifle_mosin") cell = i;
            Assert.IsTrue(bag.EquipFromCell(cell)); Assert.IsTrue(bag.Draw(Map01Inventory.Slot.Primary));
            for (int i = 0; i < 10; i++) yield return null;
            var mosin = mission.player.GetComponent<Map01Mosin>();
            Assert.AreEqual(WeaponKind.Sniper, mission.ModernCombat.EquippedKind);

            Map01EnemyController sentry = null;
            foreach (var g in mission.Enemies) if (g != null && g.name == "Operation guard 3_2") sentry = g;
            Assert.IsNotNull(sentry, "C3's watchtower sentry.");
            Assert.IsFalse(sentry.GetComponent<UnityEngine.AI.NavMeshAgent>().enabled, "Up on the deck, off the NavMesh.");
            var head = sentry.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.Head).position;
            var health = sentry.GetComponent<Health>();

            // A spot on the ground from which the line to his head runs through his roof and nothing else solid.
            int mask = (int)typeof(PlayerCombat).GetProperty("HitMask", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(mission.ModernCombat);
            Vector3 eye = default; bool found = false;
            foreach (float d in new[] { 25f, 40f })
                for (int k = 0; k < 16 && !found; k++)
                {
                    float a = k * 22.5f * Mathf.Deg2Rad;
                    var flat = sentry.transform.position + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * d;
                    if (!Physics.Raycast(flat + Vector3.up * 80f, Vector3.down, out var ground, 200f, mission.ObstructionMask, QueryTriggerInteraction.Ignore)) continue;
                    var at = ground.point + Vector3.up * 1.62f;
                    if (Physics.CheckSphere(at, .25f, mask, QueryTriggerInteraction.Ignore)) continue;
                    var dir = head - at;
                    if (!Physics.Raycast(at, dir.normalized, out var first, dir.magnitude + 1f, mask, QueryTriggerInteraction.Ignore)
                        || !Map01Mission.RoundPassesThrough(first.collider)) continue;
                    bool clear = true;
                    foreach (var h in Physics.RaycastAll(at, dir.normalized, dir.magnitude - .3f, mask, QueryTriggerInteraction.Ignore))
                        if (!Map01Mission.RoundPassesThrough(h.collider) && h.collider.GetComponentInParent<Health>() != health) clear = false;
                    if (clear) { eye = at; found = true; }
                }
            Assert.IsTrue(found, "From the ground, the roof is what stands between a rifle and his head.");

            // The PU shot exactly as the game fires it: from the camera along its forward, settled and aimed.
            var rig = mission.gameCamera.GetComponent<ThirdPersonCamera>(); rig.enabled = false;
            mission.gameCamera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(head - eye));
            typeof(PlayerCombat).GetMethod("FireShot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(mission.ModernCombat, new object[] { mosin.Weapon, ShooterStance.Standing, true });
            Assert.IsTrue(health.IsDead, $"One round from {eye}, {Vector3.Distance(eye, head):0} m off, through the eave: dead.");
            rig.enabled = true;
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AMosinShotFarOffIsSearchedForAndOnlyAClosePlacesNam()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return WaitForMap01Ready();
            for (int i = 0; i < 20; i++) yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            mission.GetComponent<Map01Rescue>().MarkIntroSeen();
            Map01EnemyController guard = null;
            foreach (var g in mission.Enemies) if (g != null && g.name == "Operation guard 3_3") guard = g;   // C3's rover
            Assert.IsNotNull(guard);
            var shot = typeof(Map01Mission).GetMethod("OnPlayerAttacked", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            detections = 0; guard.Detected += CountDetection;
            try
            {
                // 20 m off: heard, searched for, Nam not found out.
                Vector3 far = Muzzle(mission, guard.transform.position + guard.transform.right * 20f);
                shot.Invoke(mission, new object[] { WeaponKind.Sniper, far });
                for (int i = 0; i < 3; i++) yield return null;
                Assert.AreEqual(0, detections, "A Mosin shot 20 m off does not give Nam away.");
                Assert.AreEqual(EnemyStateId.Investigate, guard.State, "He goes to look.");
                Assert.IsFalse(guard.TryGetKnownThreat(out _), "And knows no position to pass to reinforcements.");

                // Beyond the rifle's 30 m: not heard at all.
                var calm = guard.State;
                // 8 m off: placed, as any gunshot.
                Vector3 near = Muzzle(mission, guard.transform.position + guard.transform.forward * 8f);
                shot.Invoke(mission, new object[] { WeaponKind.Sniper, near });
                yield return null;
                Assert.AreEqual(1, detections, "Within " + Map01Mission.ShotPinpointMeters + " m he knows where the shot came from.");
                Assert.AreEqual(Map01DetectionCause.Gunshot, lastCause);
                Assert.IsTrue(guard.TryGetKnownThreat(out _));
            }
            finally { guard.Detected -= CountDetection; }
            yield return new ExitPlayMode();
        }

        private static Vector3 Muzzle(Map01Mission mission, Vector3 flat)
            => Physics.Raycast(flat + Vector3.up * 40f, Vector3.down, out var ground, 80f, mission.ObstructionMask, QueryTriggerInteraction.Ignore)
                ? ground.point + Vector3.up * 1.4f : flat + Vector3.up * 1.4f;
    }
}
