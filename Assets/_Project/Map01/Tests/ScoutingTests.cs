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
        // domain reload that EnterPlayMode triggers (see the note at the top of ForestFlowTests).

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
        /// his cone, that his eyes can actually see — past no wall and no bush.</summary>
        private static bool FindSpotInView(Map01SightCover cover, Vector3 post, Quaternion facing, float distance, float slack, out Vector3 spot)
        {
            for (int a = 0; a <= 25; a += 5)
                foreach (int sign in new[] { 1, -1 })
                {
                    var dir = facing * Quaternion.Euler(0, a * sign, 0) * Vector3.forward;
                    if (!NavMesh.SamplePosition(post + dir * distance, out var hit, 2f, NavMesh.AllAreas)) continue;
                    if (Mathf.Abs(Vector3.Distance(hit.position, post) - distance) > slack) continue;
                    if (Physics.Linecast(post + Vector3.up * 1.3f, hit.position + Vector3.up * 1.1f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (cover.Blocks(post + Vector3.up * 1.3f, hit.position + Vector3.up * 1.1f)) continue;
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
        public IEnumerator NearingAMannedCampWarnsNam()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var scouting = mission.GetComponent<Map01Scouting>();
            foreach (var e in mission.Enemies) e.enabled = false;
            if (mission.player.TryGetComponent(out PlayerFootsteps steps)) steps.enabled = false;
            SetPreviewResolution(1600, 900);
            mission.Say(null, -1f);
            Assert.IsNull(scouting.NearCamp(out _), "At the base, far from every camp: no warning.");

            var camp = scouting.Camps[0];
            Assert.IsTrue(NavMesh.SamplePosition(camp.Center + Vector3.back * 20, out var near, 4, NavMesh.AllAreas));
            Teleport(mission, near.position);
            yield return null;
            Assert.AreSame(camp, scouting.NearCamp(out float distance), "About 20 m from a manned camp: warned.");
            Assert.Less(distance, scouting.WarnRadius);
            Directory.CreateDirectory("Logs/GuidePreview");
            ScreenCapture.CaptureScreenshot("Logs/GuidePreview/camp-warning.png");
            for (int i = 0; i < 4; i++) yield return null;

            foreach (var guard in camp.Guards) guard.GetComponent<Health>().TakeDamage(99999, guard.transform.position, null);
            yield return null;
            Assert.IsNull(scouting.NearCamp(out _), "Nobody left standing in the camp: nothing to warn of.");
            yield return new ExitPlayMode();
        }

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

            // In the cone, standing: noticed — but suspicion has to build first.
            // 0.8 of his sight, give or take 1.5 m: inside it standing, outside the 0.6 he sees a crouching Nam at.
            float reach = guard.VisionRange * .8f;
            Assert.IsTrue(FindSpotInView(mission.SightCover, post, facing, reach, 1.5f, out var spot), $"Need a spot in view {reach:0} m out.");
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

            // The same spot crouched: beyond a crouching Nam's range. He crouches before the guard's calm runs out —
            // left standing in view meanwhile, the guard grew suspicious and walked up to him. And he crouches for
            // real: Map01PlayerInteraction copies the player's own stance (IsSneaking) into mission.Crouched.
            PinAtPost(guard);
            mission.ModernPlayer.RestoreMotion(true);
            mission.Crouched = true;
            Teleport(mission, spot);
            yield return WaitGameSeconds(4.2f + 3f);
            Assert.IsFalse(guard.Engaged, "Crouching keeps Nam unseen beyond the shortened range. " +
                $"(crouched={mission.Crouched} sneaking={mission.ModernPlayer.IsSneaking} suspicion={guard.Suspicion:0.00} " +
                $"distance={Vector3.Distance(guard.transform.position, mission.player.position):0.0} sight={guard.VisionRange:0.0})");
            Assert.AreEqual(0f, guard.Suspicion);
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// F raises the binoculars from Nam's own eyes, zoomed and without his model in the view. They no longer log
        /// the three camps: in the operations they are for watching the officer at point 3 from afar (Map01Story,
        /// see Map01OperationsTests.OfficerNeedsDistantBinocularsAndRestoresCombatBeforeDiscovery).
        /// </summary>
        [UnityTest]
        public IEnumerator BinocularsLookFromNamsEyesButNoLongerLogCamps()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var scouting = mission.GetComponent<Map01Scouting>();
            Assert.IsNotNull(scouting, "Map01Quest must bring the binoculars along.");
            Assert.AreEqual(3, scouting.Camps.Count, "Map 1 has three enemy outposts.");
            Assert.IsTrue(scouting.Camps.All(c => c.Guards.Length > 0), "Every outpost has its guards.");
            // The view is under test here, not stealth. The agent too: it keeps walking a guard down
            // his last path without the controller, out from under the spot being aimed at.
            foreach (var e in mission.Enemies) { e.enabled = false; e.GetComponent<NavMeshAgent>().enabled = false; }
            quest.RestoreStage(Map01Quest.ScoutStage); // Point 1: the operations carry the binoculars.
            mission.Say(null, -1f);

            var camp = scouting.Camps[0];
            Assert.IsTrue(FindVantage(camp, mission.ObstructionMask, out var standAt, out var lookAt), "Need a vantage point over camp 1.");
            Teleport(mission, standAt);
            yield return null; yield return null;

            // Up and on the camp (re-aimed as the zoomed camera settles): the view is Nam's own.
            for (int i = 0; i < 8; i++) { scouting.HoldBinoculars(true); AimCamera(mission, lookAt); yield return WaitGameSeconds(.1f); }
            Assert.IsTrue(scouting.Binoculars, Diag(mission, scouting, camp, "Binoculars up."));
            Assert.Less(Vector3.Distance(mission.gameCamera.transform.position, scouting.Eye), .3f, "Binoculars look from Nam's eyes, not over his shoulder.");
            Assert.Less(mission.gameCamera.fieldOfView, 20f, "Binoculars zoom well past the rifle's aim.");
            Assert.IsTrue(NamOutOfView(mission), "Nam's own model must not show in the binoculars.");
            Directory.CreateDirectory("Logs/GuidePreview");
            ScreenCapture.CaptureScreenshot("Logs/GuidePreview/scouting-binoculars.png");

            // Held on the camp well past the two seconds a log once took: nothing is logged, nothing completes.
            yield return WaitGameSeconds(3f);
            Assert.IsFalse(camp.Found, "Camps are no longer logged through the binoculars.");
            Assert.AreEqual(0, scouting.FoundCount);
            Assert.IsNull(scouting.Sighted);
            Assert.AreEqual(0f, scouting.RecordProgress);
            Assert.IsFalse(scouting.InScanRange, "No scan to be in range of.");
            Assert.AreEqual(Map01Quest.ScoutStage, quest.Stage, "Looking at a camp completes no order.");

            scouting.HoldBinoculars(false);
            yield return null; yield return null;
            Assert.IsFalse(scouting.Binoculars, Diag(mission, scouting, camp, "Binoculars down."));
            Assert.IsFalse(NamOutOfView(mission), "Lowering the binoculars brings Nam back.");
            Assert.Greater(Vector3.Distance(mission.gameCamera.transform.position, scouting.Eye), 1f, "Back behind his shoulder.");
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// The camp guards are fair game in the operations: a knife from behind kills silently, the guard drawn away
        /// from his camp or standing in it, and either way no run is lost (the scouting run and its failure are gone).
        /// </summary>
        [UnityTest]
        public IEnumerator AKnifeTakedownOfACampGuardIsSilentAndFailsNothing()
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
            Assert.IsTrue(inventory.EquipItem("knife"));
            mission.ModernPlayer.RestoreMotion(true);
            mission.Crouched = true;

            // Drawn well away from the camp (as a thrown stone would), then knifed from behind.
            Vector3 spot = FarFromCamp(camp, scouting.QuietKillDistance + 5f, other.transform.position);
            lured.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(spot - camp.Center, Vector3.up));
            OnOpenGround(mission, lured, spot);
            StandBehind(mission, lured);
            // Drawing the knife is a move of Nam's own; a takedown starts once his hands are free.
            for (float until = Time.time + 6; Map01NamActions.For(mission).Busy && Time.time < until;) yield return null;
            Assert.IsTrue(lured.CanSilentTakedown(), "Close behind him, knife in hand.");
            bool took = Strike(mission);
            // A plain takedown kills at once; next to a bush Nam may pull him in instead, a kill once the take ends.
            for (float until = Time.time + 8; mission.Cinematic && Time.time < until;) yield return null;
            Assert.IsTrue(took, $"The knife goes for the takedown. (busy={Map01NamActions.For(mission).Busy})");
            Assert.IsFalse(lured.Alive, "A silent takedown from behind.");
            Assert.IsTrue(lured.TakenDownSilently);
            Assert.IsFalse(other.Engaged, "Away from the camp, nobody noticed.");
            Assert.IsFalse(scouting.FailedRun);

            // The same takedown on his comrade, at his post inside the camp: a kill, and still no run to lose.
            OnOpenGround(mission, other, other.transform.position);
            StandBehind(mission, other);
            for (float until = Time.time + 6; Map01NamActions.For(mission).Busy && Time.time < until;) yield return null;
            Assert.IsTrue(other.CanSilentTakedown(), "Behind the second guard at his post.");
            Assert.IsTrue(Strike(mission), "The knife goes for the takedown.");
            for (float until = Time.time + 8; mission.Cinematic && Time.time < until;) yield return null;
            Assert.IsFalse(other.Alive);
            Assert.IsTrue(other.TakenDownSilently);
            Assert.IsFalse(scouting.FailedRun, "Fighting at an outpost no longer fails anything.");
            Assert.IsFalse(mission.Stopped);
            Assert.AreEqual(Map01Quest.ScoutStage, quest.Stage);
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// <paramref name="guard"/> moved to open ground near <paramref name="around"/>: beside a bush a crouched knife
        /// pulls him into it instead (Map01BushTakedown), a struggle of its own that needs the player's keys.
        /// </summary>
        private static void OnOpenGround(Map01Mission mission, Map01EnemyController guard, Vector3 around)
        {
            for (int r = 0; r <= 8; r++)
                for (int a = 0; a < 360; a += 45)
                {
                    var p = around + Quaternion.Euler(0, a, 0) * Vector3.forward * r;
                    if (!NavMesh.SamplePosition(p, out var hit, 1.5f, NavMesh.AllAreas)) continue;
                    var behind = hit.position - guard.transform.forward * 1.25f;
                    if (mission.TryGetHideSpot(hit.position, out _) || mission.TryGetHideSpot(behind, out _)) continue;
                    if (!NavMesh.SamplePosition(behind, out _, .6f, NavMesh.AllAreas)) continue;
                    guard.GetComponent<NavMeshAgent>().Warp(hit.position);
                    return;
                }
            Assert.Fail("No open ground near " + around);
        }

        /// <summary>Crouched 1.25 m behind <paramref name="guard"/>, facing the way he faces.</summary>
        private static void StandBehind(Map01Mission mission, Map01EnemyController guard)
        {
            var at = guard.transform.position - guard.transform.forward * 1.25f;
            if (NavMesh.SamplePosition(at, out var hit, 1f, NavMesh.AllAreas)) at = hit.position;
            Teleport(mission, at);
            mission.player.rotation = guard.transform.rotation;
            Physics.SyncTransforms();
        }

        /// <summary>The attack button with the knife in hand: the takedown is tried first (Map01Rescue.TryPlayerTakedown).
        /// True when one started.</summary>
        private static bool Strike(Map01Mission mission)
        {
            typeof(PlayerCombat).GetMethod("Attack", Private).Invoke(mission.ModernCombat, null);
            return mission.Cinematic;
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
            $"toCamp={Vector3.Distance(mission.player.position, camp.Center):0.0} bino={scouting.Binoculars} found={camp.Found} fps={1f / Mathf.Max(.0001f, Time.unscaledDeltaTime):0} rate={Application.targetFrameRate})";

        private static void Spot(Map01Scouting scouting) =>
            typeof(Map01EnemyController).GetField("_engagedUntil", Private).SetValue(scouting.Camps[0].Guards[0], Time.time + 5);

        /// <summary>
        /// What replaced the scouting run's restart: an order taken from the commander keeps that moment (an operation
        /// checkpoint), and when Nam falls during the operation, [Enter] brings Map 1 back exactly there — every time.
        /// </summary>
        [UnityTest]
        public IEnumerator AFallenNamStartsOverFromTheCommandersLastOrder()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var storage = typeof(ForestSaveSlots).GetField("storageRoot", BindingFlags.Static | BindingFlags.NonPublic);
            storage.SetValue(null, Path.GetFullPath("Logs/Scouting/saves/" + System.Guid.NewGuid().ToString("N")));
            try
            {
                var mission = Object.FindFirstObjectByType<Map01Mission>();
                var quest = mission.GetComponent<Map01Quest>();
                var story = mission.GetComponent<Map01Story>();
                var inventory = mission.GetComponent<Map01Inventory>();
                var save = mission.GetComponent<Map01SaveSystem>();
                foreach (var e in mission.Enemies) e.enabled = false;

                // Home at the base, the commander gives point 1 in person.
                quest.RestoreStage(Map01Quest.BriefingStage);
                PlaceNam(mission, story.Commander.position + story.Commander.forward * 1.8f);
                Assert.IsTrue(story.TryInteract());
                yield return ConfirmConversation(mission);
                Assert.AreEqual(Map01Quest.ScoutStage, quest.Stage);
                for (float until = Time.time + 3; !save.HasOperationCheckpoint && Time.time < until;) yield return null;
                Assert.IsTrue(save.HasOperationCheckpoint, "Taking the order keeps the moment.");
                Vector3 orderedAt = mission.player.position;
                int stones = inventory.Count("stone");

                // Out in the field: stones thrown, then Nam falls.
                inventory.Spend("stone", 2);
                PlaceNam(mission, mission.Points.Single(p => p.id == "tutorial_loot").transform.position);
                mission.ModernHealth.TakeDamage(99999, mission.player.position, null);
                yield return null;
                Assert.LessOrEqual(mission.PlayerHealth, 0f, "Nam has fallen.");

                // [Enter] on "NAM ĐÃ GỤC NGÃ": Map 1 again, exactly as it was when the order was given.
                Assert.IsTrue(save.RestartCurrentMission());
                yield return WaitForRestore();
                mission = Object.FindFirstObjectByType<Map01Mission>();
                quest = mission.GetComponent<Map01Quest>();
                inventory = mission.GetComponent<Map01Inventory>();
                save = mission.GetComponent<Map01SaveSystem>();
                Assert.AreEqual(Map01Quest.ScoutStage, quest.Stage, "Back at point 1's order, not at the start of the map.");
                Assert.Greater(mission.PlayerHealth, 0f);
                Assert.IsFalse(mission.Stopped);
                Assert.AreEqual(stones, inventory.Count("stone"), "The stones thrown are back in the bag.");
                Assert.Less(Vector3.Distance(mission.player.position, orderedAt), 1f, "Nam is where he took the order.");

                // A second fall starts over from that same moment.
                inventory.Spend("stone", 1);
                mission.ModernHealth.TakeDamage(99999, mission.player.position, null);
                yield return null;
                Assert.IsTrue(save.RestartCurrentMission());
                yield return WaitForRestore();
                mission = Object.FindFirstObjectByType<Map01Mission>();
                Assert.AreEqual(Map01Quest.ScoutStage, mission.GetComponent<Map01Quest>().Stage);
                Assert.AreEqual(stones, mission.GetComponent<Map01Inventory>().Count("stone"));
                Assert.Less(Vector3.Distance(mission.player.position, orderedAt), 1f);
            }
            finally { storage.SetValue(null, null); }
            yield return new ExitPlayMode();
        }

        /// <summary>
        /// The scouting run's failure panel is gone: in the operations a camp that spots Nam, or a camp guard he shoots,
        /// is a fight to win — the game goes on and the order stands.
        /// </summary>
        [UnityTest]
        public IEnumerator SpottedOrAttackingACampIsAFightNotAFailure()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var scouting = mission.GetComponent<Map01Scouting>();
            quest.RestoreStage(Map01Quest.ScoutStage);
            var guard = scouting.Camps[0].Guards[0];
            foreach (var e in mission.Enemies) e.enabled = false;
            mission.ModernHealth.CinematicInvulnerable = true; // The fight itself is not under test.
            yield return null;

            // Shooting a camp guard: he is hurt, and that is all.
            var health = guard.GetComponent<Health>();
            health.TakeDamage(10, guard.transform.position, mission.player.gameObject);
            yield return WaitGameSeconds(.5f);
            Assert.Less(health.Current, health.Max, "The shot hurt him.");
            Assert.IsFalse(scouting.FailedRun, "Attacking a camp no longer loses a run.");
            Assert.IsFalse(mission.Stopped, "No failure panel holds the game.");

            // Spotted by a camp guard: the same.
            Spot(scouting);
            yield return null; yield return null;
            Assert.IsTrue(guard.Engaged);
            Assert.IsFalse(scouting.FailedRun, "Being spotted no longer loses a run either.");
            Assert.IsFalse(mission.Stopped);
            Assert.IsTrue(mission.ModernPlayer.enabled && mission.ModernCombat.enabled, "Nam fights on.");
            Assert.AreEqual(Map01Quest.ScoutStage, quest.Stage, "The order stands.");
            yield return new ExitPlayMode();
        }
    }
}
