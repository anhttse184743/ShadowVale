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

        /// <summary>A walkable spot at least <paramref name="distance"/> m from the camp's centre, on the far side from <paramref name="avoid"/>.</summary>
        private static Vector3 FarFromCamp(Map01Scouting.Camp camp, float distance, Vector3 avoid)
        {
            var away = Vector3.ProjectOnPlane(camp.Center - avoid, Vector3.up).normalized;
            for (int a = 0; a <= 90; a += 15)
                foreach (int sign in new[] { 1, -1 })
                {
                    var p = camp.Center + Quaternion.Euler(0, a * sign, 0) * away * distance;
                    if (NavMesh.SamplePosition(p, out var hit, 3f, NavMesh.AllAreas) && Vector3.Distance(hit.position, camp.Center) >= distance - 1f)
                        return hit.position;
                }
            Assert.Fail("No walkable spot away from the camp.");
            return default;
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
            Assert.IsTrue(scouting.InScanRange, Diag(mission, scouting, camp, "At the vantage point camp 1 is in range."));

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
            Assert.IsFalse(scouting.Binoculars, Diag(mission, scouting, camp, "Binoculars down."));
            Assert.IsFalse(NamOutOfView(mission), "Lowering the binoculars brings Nam back.");
            Assert.Greater(Vector3.Distance(mission.gameCamera.transform.position, scouting.Eye), 1f, "Back behind his shoulder.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AQuietKnifeTakedownAwayFromTheCampGoesUnnoticed()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var scouting = mission.GetComponent<Map01Scouting>();
            var inventory = mission.GetComponent<Map01Inventory>();
            if (mission.player.TryGetComponent(out PlayerFootsteps steps)) steps.enabled = false; // No teleport thuds.
            quest.RestoreStage(Map01Quest.ScoutStage);
            var camp = scouting.Camps[0];
            var lured = camp.Guards[0];
            var other = camp.Guards[1];
            foreach (var e in mission.Enemies) e.enabled = e == lured || e == other;
            PinAtPost(lured); PinAtPost(other);
            yield return WaitGameSeconds(4.2f); // ReturnToPost leaves them calm for 4 s.
            scouting.RestoreFound(0b010); // Another camp already logged: it must stay logged.
            Assert.IsTrue(inventory.EquipItem("knife"));
            mission.Crouched = true;

            // Drawn well away from the camp (as a thrown stone would), then knifed from behind.
            Vector3 spot = FarFromCamp(camp, scouting.QuietKillDistance + 5f, other.transform.position);
            lured.GetComponent<NavMeshAgent>().Warp(spot);
            lured.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(spot - camp.Center, Vector3.up));
            Teleport(mission, spot - lured.transform.forward * 1.5f);
            yield return null;
            lured.GetComponent<Health>().TakeDamage(45f, lured.transform.position, mission.player.gameObject); // The knife's own blow.
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsFalse(lured.Alive, "A silent takedown from behind.");
            Assert.IsTrue(float.IsNegativeInfinity(scouting.FailedAt), "Away from the camp, nobody noticed.");
            Assert.AreEqual(0b010, scouting.FoundMask, "The logged camp stays logged.");
            Assert.AreEqual(Map01Quest.ScoutStage, quest.Stage);

            // The same takedown at a post inside the camp: his comrades notice.
            Teleport(mission, other.transform.position - other.transform.forward * 1.5f);
            yield return null;
            other.GetComponent<Health>().TakeDamage(45f, other.transform.position, mission.player.gameObject);
            for (float until = Time.time + 1; float.IsNegativeInfinity(scouting.FailedAt) && Time.time < until;) yield return null;
            Assert.Less(Time.time - scouting.FailedAt, 1f, "Inside the camp, it is noticed.");
            Assert.IsTrue(scouting.FailedRun, "And the run is lost.");
            StringAssert.Contains("ngay trong doanh trại", scouting.FailReason);
            yield return new ExitPlayMode();
        }

        private static IEnumerator WaitForRestore()
        {
            var pending = typeof(Map01SaveSystem).GetField("pendingCheckpoint", BindingFlags.Static | BindingFlags.NonPublic);
            yield return null;
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (pending.GetValue(null) != null && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsNull(pending.GetValue(null), "The restart checkpoint must be applied.");
        }

        private static string Diag(Map01Mission mission, Map01Scouting scouting, Map01Scouting.Camp camp, string what) =>
            $"{what} (stopped={mission.Stopped} restoring={Map01SaveSystem.IsRestoring} stage={mission.GetComponent<Map01Quest>().Stage} scoutingEnabled={scouting.isActiveAndEnabled} " +
            $"sameScouting={ReferenceEquals(scouting, Object.FindFirstObjectByType<Map01Scouting>())} kb={UnityEngine.InputSystem.Keyboard.current != null} f={(UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.fKey.isPressed)} " +
            $"toCamp={Vector3.Distance(mission.player.position, camp.Center):0.0} bino={scouting.Binoculars} found={camp.Found} fps={1f / Mathf.Max(.0001f, Time.unscaledDeltaTime):0} rate={Application.targetFrameRate})";

        private static Map01EnemyController CampGuard(Map01Scouting scouting, string name) =>
            scouting.Camps.SelectMany(c => c.Guards).First(g => g.name == name);

        private static void Spot(Map01Scouting scouting) =>
            typeof(Map01EnemyController).GetField("_engagedUntil", Private).SetValue(scouting.Camps[0].Guards[0], Time.time + 5);

        [UnityTest]
        public IEnumerator ScoutingFailureStartsOverFromHungsOrder()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            SetPreviewResolution(1600, 900); // A Game View for the screenshot.
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var inventory = mission.GetComponent<Map01Inventory>();
            var scouting = mission.GetComponent<Map01Scouting>();

            // Home at the base, Hùng gives the order in person.
            quest.RestoreStage(Map01Quest.BriefingStage);
            Vector3 home = mission.player.position;
            mission.hung.GetComponent<NavMeshAgent>().Warp(home + Vector3.right * 2f);
            yield return null;
            quest.TalkToHung();
            Assert.AreEqual(Map01Quest.ScoutStage, quest.Stage);
            Vector3 orderedAt = mission.player.position;
            int stones = inventory.Count("stone");

            // Out in the field: stones thrown, a camp logged — then spotted.
            inventory.Spend("stone", 2);
            scouting.RestoreFound(0b001);
            Teleport(mission, mission.Points.Single(p => p.id == "tutorial_loot").transform.position);
            Spot(scouting);
            yield return null; yield return null;
            Assert.IsTrue(scouting.FailedRun, "Spotted: the run is lost.");
            Assert.IsTrue(mission.Stopped, "The failure panel holds the game.");
            yield return WaitGameSeconds(.3f);
            Directory.CreateDirectory("Logs/GuidePreview");
            ScreenCapture.CaptureScreenshot("Logs/GuidePreview/scouting-failed.png");
            yield return null; yield return null;

            // [Enter]: Map 1 again, exactly as it was when Hùng gave the order.
            scouting.Restart();
            yield return WaitForRestore();
            mission = Object.FindFirstObjectByType<Map01Mission>();
            quest = mission.GetComponent<Map01Quest>();
            inventory = mission.GetComponent<Map01Inventory>();
            scouting = mission.GetComponent<Map01Scouting>();
            Assert.AreEqual(Map01Quest.ScoutStage, quest.Stage);
            Assert.IsFalse(scouting.FailedRun);
            Assert.IsFalse(mission.Stopped);
            Assert.AreEqual(0, scouting.FoundCount, "Nothing logged yet.");
            Assert.AreEqual(stones, inventory.Count("stone"), "The stones thrown are back in the bag.");
            Assert.Less(Vector3.Distance(mission.player.position, orderedAt), 1f, "Nam is where he took the order.");
            StringAssert.StartsWith("Hùng: Địch có ba doanh trại", mission.Dialogue, "Hùng gives the order again.");

            // A second lost run starts over from that same moment.
            scouting.RestoreFound(0b010);
            Spot(scouting);
            yield return null; yield return null;
            Assert.IsTrue(scouting.FailedRun);
            scouting.Restart();
            yield return WaitForRestore();
            mission = Object.FindFirstObjectByType<Map01Mission>();
            scouting = mission.GetComponent<Map01Scouting>();
            Assert.AreEqual(Map01Quest.ScoutStage, mission.GetComponent<Map01Quest>().Stage);
            Assert.AreEqual(0, scouting.FoundCount);
            Assert.Less(Vector3.Distance(mission.player.position, orderedAt), 1f);
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
            // Straight into the order, as a save from before its start was kept would be.
            quest.RestoreStage(Map01Quest.ScoutStage);
            var guard = scouting.Camps[0].Guards[0];
            string guardName = guard.name; // Found by name after each reload.
            var health = guard.GetComponent<Health>();
            Vector3 post = guard.transform.position;
            yield return null;

            // Shooting a camp guard is being noticed: the run is lost and the game holds on the
            // panel — with nobody snapping back anywhere while it is up.
            scouting.RestoreFound(0b011);
            health.TakeDamage(10, guard.transform.position, null);
            yield return null; yield return null;
            Assert.IsTrue(scouting.FailedRun, $"Attacking a camp loses the scouting run. (stopped={mission.Stopped} paused={mission.Paused} restoring={Map01SaveSystem.IsRestoring} menu={ForestMenu.Visible} hurtCount={guard.HurtCount} alive={guard.Alive} enabled={guard.enabled})");
            Assert.IsTrue(mission.Stopped, "The failure panel holds the game.");
            Assert.Less(Time.time - scouting.FailedAt, 1f);
            StringAssert.Contains("tấn công", scouting.FailReason);
            Vector3 hit = guard.transform.position;
            Vector3 nam = mission.player.position;
            yield return WaitGameSeconds(.5f);
            Assert.Less(Vector3.Distance(guard.transform.position, hit), .2f, "The attacked guard stays put while the panel is up.");
            Assert.Less(Vector3.Distance(mission.player.position, nam), .2f);

            // [Enter]: Map 1 reloads as if the order had just been given (no moment was kept).
            scouting.Restart();
            yield return WaitForRestore();
            Assert.AreNotSame(mission, Object.FindFirstObjectByType<Map01Mission>(), "A reload, not a reset in place.");
            mission = Object.FindFirstObjectByType<Map01Mission>();
            scouting = mission.GetComponent<Map01Scouting>();
            guard = CampGuard(scouting, guardName);
            health = guard.GetComponent<Health>();
            Assert.AreEqual(Map01Quest.ScoutStage, mission.GetComponent<Map01Quest>().Stage, "Only the scouting restarts, not the map.");
            Assert.IsFalse(scouting.FailedRun);
            Assert.IsFalse(mission.Stopped);
            Assert.AreEqual(0, scouting.FoundCount, "Every log is lost.");
            Assert.AreEqual(health.Max, health.Current, "The camp is back to strength.");
            Assert.Less(Vector3.Distance(guard.transform.position, post), 1.5f, "At his post.");
            Assert.Less(Vector3.Distance(mission.player.position, mission.hung.position), 4f, "Nam starts beside Hùng.");
            StringAssert.StartsWith("Hùng: Địch có ba doanh trại", mission.Dialogue);

            // A camp guard spotting Nam does the same — and from now on the moment is kept.
            Assert.IsTrue(mission.GetComponent<Map01SaveSystem>().HasScoutStart);
            scouting.RestoreFound(0b001);
            Spot(scouting);
            yield return null; yield return null;
            Assert.IsTrue(scouting.FailedRun, "Being spotted loses the run.");
            StringAssert.Contains("nhìn thấy Nam", scouting.FailReason);
            scouting.Restart();
            yield return WaitForRestore();
            mission = Object.FindFirstObjectByType<Map01Mission>();
            scouting = mission.GetComponent<Map01Scouting>();
            Assert.AreEqual(0, scouting.FoundCount);
            Assert.IsFalse(CampGuard(scouting, guardName).Engaged, "The camp is calm again.");

            // A guard Nam killed is back at his post — his corpse loot gone with the corpse.
            guard = CampGuard(scouting, guardName);
            yield return WaitGameSeconds(.2f);
            guard.GetComponent<Health>().TakeDamage(9999, guard.transform.position, null);
            yield return WaitGameSeconds(.3f);
            Assert.IsTrue(scouting.FailedRun);
            scouting.Restart();
            yield return WaitForRestore();
            mission = Object.FindFirstObjectByType<Map01Mission>();
            guard = CampGuard(mission.GetComponent<Map01Scouting>(), guardName);
            Assert.IsTrue(guard.Alive, "A killed camp guard is back when the run starts over.");
            Assert.IsNull(guard.GetComponent<ForestPoint>(), "No loot left on a guard who is back on duty.");
            Assert.Less(Vector3.Distance(guard.transform.position, post), 1.5f, "Back at his post.");
            yield return new ExitPlayMode();
        }
    }
}
