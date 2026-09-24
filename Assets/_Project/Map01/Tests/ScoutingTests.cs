using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    public sealed class ScoutingTests : ForestSceneTestBase
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        // Lambdas stay in these static helpers: one capturing a test-iterator local breaks after the
        // domain reload that EnterPlayMode triggers (see ForestFlowTests.NearestOutpostGuard).

        private static void Teleport(Map01Mission mission, Vector3 position)
        {
            var controller = mission.player.GetComponent<CharacterController>();
            controller.enabled = false; mission.player.position = position; controller.enabled = true;
        }

        /// <summary>Stand the guard at his post facing his post direction, and keep him there.</summary>
        private static void PinAtPost(Map01EnemyController guard)
        {
            guard.ReturnToPost();
            guard.Configure(System.Array.Empty<Vector3>());
            var agent = guard.GetComponent<NavMeshAgent>();
            if (agent.isOnNavMesh) agent.ResetPath();
        }

        /// <summary>A NavMesh spot about <paramref name="distance"/> m in front of the post, inside
        /// his cone, that his eyes can actually see.</summary>
        private static bool FindSpotInView(Vector3 post, Quaternion facing, float distance, out Vector3 spot)
        {
            for (int a = 0; a <= 25; a += 5)
                foreach (int sign in new[] { 1, -1 })
                {
                    var dir = facing * Quaternion.Euler(0, a * sign, 0) * Vector3.forward;
                    if (!NavMesh.SamplePosition(post + dir * distance, out var hit, 2f, NavMesh.AllAreas)) continue;
                    if (Mathf.Abs(Vector3.Distance(hit.position, post) - distance) > 2.5f) continue;
                    if (Physics.Linecast(post + Vector3.up * 1.3f, hit.position + Vector3.up * 1.1f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    spot = hit.position; return true;
                }
            spot = default; return false;
        }

        /// <summary>Somewhere 30–42 m from the camp with a clear view of its centre or a guard.</summary>
        private static bool FindVantage(Map01Scouting.Camp camp, int mask, out Vector3 standAt, out Vector3 lookAt)
        {
            var spots = camp.Guards.Select(g => g.transform.position + Vector3.up * 1.2f).Prepend(camp.Center + Vector3.up * 1.5f).ToArray();
            for (float d = 30; d <= 42; d += 4)
                for (int a = 0; a < 360; a += 15)
                {
                    var p = camp.Center + Quaternion.Euler(0, a, 0) * Vector3.forward * d;
                    if (!NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas) || Vector3.Distance(hit.position, camp.Center) > 43) continue;
                    Vector3 eye = hit.position + Vector3.up * 1.6f;
                    foreach (var spot in spots)
                    {
                        if (Physics.Linecast(eye, spot, out var block, mask, QueryTriggerInteraction.Ignore) && Vector3.Distance(block.point, spot) > 1.5f) continue;
                        standAt = hit.position; lookAt = spot; return true;
                    }
                }
            standAt = lookAt = default; return false;
        }

        /// <summary>Somewhere a little beyond scan range of <paramref name="camp"/> and of every other camp.</summary>
        private static bool FindOutOfRange(Map01Scouting scouting, Map01Scouting.Camp camp, out Vector3 standAt)
        {
            for (int a = 0; a < 360; a += 15)
            {
                var p = camp.Center + Quaternion.Euler(0, a, 0) * Vector3.forward * (scouting.ScanRange + 10);
                if (!NavMesh.SamplePosition(p, out var hit, 4f, NavMesh.AllAreas)) continue;
                if (scouting.Camps.Min(c => Vector3.Distance(hit.position, c.Center)) < scouting.ScanRange + 3) continue;
                standAt = hit.position; return true;
            }
            standAt = default; return false;
        }

        /// <summary>Turn the orbit camera so it looks at <paramref name="point"/>.</summary>
        private static void AimCamera(Map01Mission mission, Vector3 point)
        {
            var rig = mission.gameCamera.GetComponent<ThirdPersonCamera>();
            var dir = point - mission.gameCamera.transform.position;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(dir.y, new Vector2(dir.x, dir.z).magnitude) * Mathf.Rad2Deg;
            typeof(ThirdPersonCamera).GetField("_yaw", Private).SetValue(rig, yaw);
            typeof(ThirdPersonCamera).GetField("_pitch", Private).SetValue(rig, pitch);
            mission.player.rotation = Quaternion.Euler(0, yaw, 0);
        }

        private static bool NamOutOfView(Map01Mission mission) =>
            mission.player.GetComponentsInChildren<Renderer>().All(r => r.shadowCastingMode == ShadowCastingMode.ShadowsOnly);

        [UnityTest]
        public IEnumerator GuardsSpotNamOnlyInTheirConeAndNeverInstantly()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var guard = mission.Enemies.First(e => e.name == "Outpost guard 0");
            foreach (var e in mission.Enemies) e.enabled = e == guard;
            // Sight only. Footsteps carry to the guards too (a teleport lands with a thud he
            // would hear and turn to), and hearing has tests of its own.
            if (mission.player.TryGetComponent(out PlayerFootsteps footsteps)) footsteps.enabled = false;
            PinAtPost(guard);
            yield return WaitGameSeconds(4.2f); // ReturnToPost leaves him calm for 4 s.
            Vector3 post = guard.transform.position;
            Quaternion facing = guard.transform.rotation;

            // Right behind him at 5 m: outside the cone, so nothing at all.
            Teleport(mission, post - facing * Vector3.forward * 5f);
            yield return WaitGameSeconds(1.5f);
            Assert.AreEqual(0f, guard.Suspicion, "Behind a guard, Nam is not seen.");
            Assert.IsFalse(guard.Engaged);

            // In the cone at 18 m, standing: noticed — but suspicion has to build first.
            Assert.IsTrue(FindSpotInView(post, facing, 18f, out var spot), "Need a spot in view 18 m out.");
            Teleport(mission, spot);
            yield return null; yield return null;
            Assert.IsFalse(guard.Engaged, "Being seen is not being spotted on the same frame.");
            AimCamera(mission, post + Vector3.up * 1.5f);
            for (float until = Time.time + 6; guard.Suspicion < .55f && !guard.Engaged && Time.time < until;) yield return null;
            Directory.CreateDirectory("Logs/GuidePreview");
            ScreenCapture.CaptureScreenshot("Logs/GuidePreview/guard-eye-suspicious.png");
            for (float until = Time.time + 6; !guard.Engaged && Time.time < until;) yield return null;
            Assert.IsTrue(guard.Engaged, "Standing in view long enough gets Nam spotted.");
            yield return null;
            ScreenCapture.CaptureScreenshot("Logs/GuidePreview/guard-eye-spotted.png");
            yield return null;

            // The same spot crouched: 18 m is beyond a crouching Nam's range (24 × 0.6 = 14.4 m).
            PinAtPost(guard);
            yield return WaitGameSeconds(4.2f);
            mission.Crouched = true;
            Teleport(mission, spot);
            yield return WaitGameSeconds(3f);
            Assert.IsFalse(guard.Engaged, "Crouching keeps Nam unseen beyond the shortened range.");
            Assert.AreEqual(0f, guard.Suspicion);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator BinocularsLogACampOnlyWhenAimedAtIt()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var scouting = mission.GetComponent<Map01Scouting>();
            Assert.IsNotNull(scouting, "Map01Quest must bring the scouting order along.");
            Assert.AreEqual(3, scouting.Camps.Count, "Map 1 has three enemy outposts.");
            Assert.IsTrue(scouting.Camps.All(c => c.Guards.Length > 0), "Every outpost has its guards.");
            // Logging is under test here, not stealth. The agent too: it keeps walking a guard down
            // his last path without the controller, out from under the spot being aimed at.
            foreach (var e in mission.Enemies) { e.enabled = false; e.GetComponent<NavMeshAgent>().enabled = false; }
            quest.RestoreStage(Map01Quest.ScoutStage);
            mission.Say(null, -1f);

            var camp = scouting.Camps[0];
            Assert.IsTrue(FindVantage(camp, mission.ObstructionMask, out var standAt, out var lookAt), "Need a vantage point over camp 1.");

            // Too far away: aimed straight at the camp, the binoculars still scan nothing.
            Assert.IsTrue(FindOutOfRange(scouting, camp, out var farAway), "Need a spot beyond scan range.");
            Teleport(mission, farAway);
            scouting.HoldBinoculars(true);
            for (int i = 0; i < 10; i++) { AimCamera(mission, camp.Center + Vector3.up * 1.5f); yield return WaitGameSeconds(.1f); }
            Assert.IsTrue(scouting.Binoculars);
            Assert.IsFalse(scouting.InScanRange, "No camp within scan range out here.");
            Assert.IsNull(scouting.Sighted);
            Assert.AreEqual(0f, scouting.RecordProgress, "Out of range nothing is logged.");
            Directory.CreateDirectory("Logs/GuidePreview");
            ScreenCapture.CaptureScreenshot("Logs/GuidePreview/scouting-out-of-range.png");
            yield return null; yield return null;

            Teleport(mission, standAt);
            yield return null; yield return null;
            Assert.IsTrue(scouting.InScanRange, "At the vantage point camp 1 is in range.");

            // Looking the other way: binoculars up, nothing to log.
            AimCamera(mission, standAt * 2 - lookAt);
            yield return WaitGameSeconds(1f);
            Assert.IsTrue(scouting.Binoculars);
            Assert.IsNull(scouting.Sighted);
            Assert.AreEqual(0f, scouting.RecordProgress);
            Assert.Less(Vector3.Distance(mission.gameCamera.transform.position, scouting.Eye), .3f, "Binoculars look from Nam's eyes, not over his shoulder.");
            Assert.Less(mission.gameCamera.fieldOfView, 20f, "Binoculars zoom well past the rifle's aim.");
            Assert.IsTrue(NamOutOfView(mission), "Nam's own model must not show in the binoculars.");

            // On target (re-aimed as the zoomed camera settles): logged after a couple of seconds.
            for (int i = 0; i < 8; i++) { AimCamera(mission, lookAt); yield return WaitGameSeconds(.1f); }
            Assert.AreSame(camp, scouting.Sighted, "Camp 1 must be in the binoculars.");
            yield return WaitGameSeconds(.5f);
            ScreenCapture.CaptureScreenshot("Logs/GuidePreview/scouting-binoculars.png");
            for (float until = Time.time + 3; !camp.Found && Time.time < until;) yield return null;
            Assert.IsTrue(camp.Found, "Holding the binoculars on the camp logs it.");
            Assert.AreEqual(1, scouting.FoundCount);
            Assert.AreEqual(Map01Quest.ScoutStage, quest.Stage, "Two camps still to go.");

            scouting.HoldBinoculars(false);
            yield return null; yield return null;
            Assert.IsFalse(scouting.Binoculars);
            Assert.IsFalse(NamOutOfView(mission), "Lowering the binoculars brings Nam back.");
            Assert.Greater(Vector3.Distance(mission.gameCamera.transform.position, scouting.Eye), 1f, "Back behind his shoulder.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator BeingSpottedOrAttackingACampFailsTheRun()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var scouting = mission.GetComponent<Map01Scouting>();
            quest.RestoreStage(Map01Quest.ScoutStage);
            var guard = scouting.Camps[0].Guards[0];
            var health = guard.GetComponent<Health>();
            Vector3 post = guard.transform.position;
            yield return null;

            // Shooting a camp guard is being noticed: all logged camps are lost.
            scouting.RestoreFound(0b011);
            health.TakeDamage(10, guard.transform.position, null);
            yield return null; yield return null;
            Assert.AreEqual(0, scouting.FoundCount, "Attacking a camp wipes the scouting run.");
            Assert.Less(Time.time - scouting.FailedAt, 1f);
            Assert.AreEqual(health.Max, health.Current, "The camp is back to strength.");
            Assert.AreEqual(Map01Quest.ScoutStage, quest.Stage, "Only the scouting restarts, not the map.");

            // A camp guard spotting Nam does the same.
            scouting.RestoreFound(0b001);
            typeof(Map01EnemyController).GetField("_engagedUntil", Private).SetValue(guard, Time.time + 5);
            yield return null; yield return null;
            Assert.AreEqual(0, scouting.FoundCount, "Being spotted wipes the scouting run.");
            Assert.IsFalse(guard.Engaged, "The camp calms down after a failed run.");

            // A guard Nam killed is replaced at his post, and his corpse loot goes with the corpse.
            yield return WaitGameSeconds(.2f);
            health.TakeDamage(9999, guard.transform.position, null);
            yield return WaitGameSeconds(.3f);
            Assert.IsTrue(guard.Alive, "A killed camp guard is replaced when the run fails.");
            Assert.IsNull(guard.GetComponent<ForestPoint>(), "No loot left on a guard who is back on duty.");
            Assert.Less(Vector3.Distance(guard.transform.position, post), 1.5f, "Back at his post.");
            yield return new ExitPlayMode();
        }
    }
}
