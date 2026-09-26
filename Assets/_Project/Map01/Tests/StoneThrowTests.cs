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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    public sealed class StoneThrowTests : ForestSceneTestBase
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        // Lambdas stay in static helpers: one capturing an iterator local breaks after the domain
        // reload EnterPlayMode triggers (see ForestFlowTests.NearestOutpostGuard).

        private static Map01EnemyController OnlyGuard(Map01Mission mission, string name)
        {
            var guard = mission.Enemies.First(e => e.name == name);
            foreach (var e in mission.Enemies) e.enabled = e == guard;
            // Stand him at his post, facing out, and keep him there.
            guard.ReturnToPost();
            guard.Configure(System.Array.Empty<Vector3>());
            var agent = guard.GetComponent<NavMeshAgent>();
            if (agent.isOnNavMesh) agent.ResetPath();
            return guard;
        }

        private static void Teleport(Map01Mission mission, Vector3 position, float yaw)
        {
            var controller = mission.player.GetComponent<CharacterController>();
            controller.enabled = false; mission.player.SetPositionAndRotation(position, Quaternion.Euler(0, yaw, 0)); controller.enabled = true;
            typeof(ThirdPersonCamera).GetField("_yaw", Private).SetValue(mission.gameCamera.GetComponent<ThirdPersonCamera>(), yaw);
        }

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

        /// <summary>A walkable spot <paramref name="distance"/> m behind the guard that Nam can see him from.</summary>
        private static Vector3 BehindInSight(Map01Mission mission, Map01EnemyController guard, float distance)
        {
            Vector3 post = guard.transform.position;
            for (int a = 0; a <= 60; a += 10)
                foreach (int sign in new[] { 1, -1 })
                {
                    var dir = Quaternion.Euler(0, a * sign, 0) * -guard.transform.forward;
                    if (!NavMesh.SamplePosition(post + dir * distance, out var hit, 2.5f, NavMesh.AllAreas)) continue;
                    if (Physics.Linecast(hit.position + Vector3.up * 1.5f, post + Vector3.up * 1.2f, mission.ObstructionMask, QueryTriggerInteraction.Ignore)) continue;
                    return hit.position;
                }
            Assert.Fail("No clear spot behind the guard.");
            return default;
        }

        /// <summary>A walkable spot beside the guard, <paramref name="distance"/> m off to his right.</summary>
        private static Vector3 Beside(Map01EnemyController guard, float distance)
        {
            foreach (float d in new[] { distance, -distance, distance * .7f, -distance * .7f })
                if (NavMesh.SamplePosition(guard.transform.position + guard.transform.right * d, out var hit, 2f, NavMesh.AllAreas))
                    return hit.position;
            Assert.Fail("No walkable spot beside the guard.");
            return default;
        }

        private static float Yaw(Vector3 from, Vector3 to) { var d = to - from; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
        private static bool AnyAlertedBut(Map01Mission mission, Map01EnemyController except) => mission.Enemies.Any(e => e != except && e.Alerted);

        [UnityTest]
        public IEnumerator AimingShowsTheArcAndTheRingsOfTheGuardsWhoWouldHear()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var inventory = mission.GetComponent<Map01Inventory>();
            var stones = mission.GetComponent<Map01StoneThrow>();
            Assert.IsNotNull(stones, "Map01PlayerInteraction must bring the stone throw along.");
            SetPreviewResolution(1600, 900); // A Game View to take the screenshots from.
            mission.Say(null, -1f);
            // Out in the open at the foot of the jetty ramp: no tent or wall in the way of the arc.
            var guard = OnlyGuard(mission, "patrol_2");
            mission.Crouched = true;
            Vector3 stand = BehindInSight(mission, guard, 14f);
            Teleport(mission, stand, Yaw(stand, guard.transform.position));
            int carried = inventory.Count("stone");
            Assert.Greater(carried, 0, "Nam starts with stones.");

            // Aimed right beside him: the arc lands in his ring, which lights up.
            Vector3 near = Beside(guard, 5f);
            AimCamera(mission, near);
            yield return null; yield return null;
            stones.BeginAim();
            for (int i = 0; i < 4; i++) { AimCamera(mission, near); yield return null; }
            Assert.IsTrue(stones.Aiming);
            Assert.GreaterOrEqual(stones.Arc.Count, 2, "An arc is shown.");
            Assert.LessOrEqual(Vector3.ProjectOnPlane(stones.Landing - mission.player.position, Vector3.up).magnitude, stones.Range + .5f, "No further than a throw.");
            Assert.GreaterOrEqual(stones.RingsShown, 1, "The guard in sight shows his hearing ring.");
            Assert.Less(Vector3.Distance(stones.Landing, guard.transform.position), stones.HearingRadius, "The aim is inside his ring.");
            Assert.GreaterOrEqual(stones.WouldHear, 1);
            Directory.CreateDirectory("Logs/GuidePreview");
            ScreenCapture.CaptureScreenshot("Logs/GuidePreview/stone-aim.png");
            yield return null; yield return null;

            // Right mouse: put it away — nothing thrown.
            stones.Cancel();
            yield return null;
            Assert.IsFalse(stones.Aiming);
            Assert.AreEqual(carried, inventory.Count("stone"), "Cancelling keeps the stone.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AGuardWhoHearsAStoneGoesToLookThenBackToHisPost()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var inventory = mission.GetComponent<Map01Inventory>();
            var stones = mission.GetComponent<Map01StoneThrow>();
            var guard = OnlyGuard(mission, "Outpost guard 0");
            yield return WaitGameSeconds(.2f);
            Vector3 post = guard.transform.position;
            Quaternion facing = guard.transform.rotation;
            // Crouched 16 m behind him: beyond what he can see of a crouching Nam while he looks around.
            mission.Crouched = true;
            Vector3 stand = BehindInSight(mission, guard, 16f);
            Teleport(mission, stand, Yaw(stand, post));
            int carried = inventory.Count("stone");

            Vector3 target = Beside(guard, 6f);
            Assert.IsTrue(stones.ThrowAt(target), "A stone is thrown.");
            Assert.AreEqual(carried - 1, inventory.Count("stone"));
            for (float until = Time.time + 3; !guard.Alerted && Time.time < until;) yield return null;
            Assert.IsTrue(guard.Alerted, "Where it lands within his hearing, he hears it.");
            StringAssert.Contains("lính nghe thấy", mission.Dialogue);

            // Off to look...
            for (float until = Time.time + 8; !guard.Searching && Time.time < until;) yield return null;
            Assert.IsTrue(guard.Searching, "He goes over and starts looking around.");
            Assert.Less(Vector3.Distance(guard.transform.position, stones.Landing), 2.5f, "At the spot the stone fell.");
            Assert.Greater(Vector3.Distance(guard.transform.position, post), 3f, "Away from his post.");
            Assert.IsFalse(guard.Engaged, "Nam was never seen.");
            AimCamera(mission, guard.transform.position + Vector3.up);
            yield return WaitGameSeconds(.3f);
            ScreenCapture.CaptureScreenshot("Logs/GuidePreview/stone-investigate.png");

            // ...a while, then back where he was.
            for (float until = Time.time + 12; guard.Alerted && Time.time < until;) yield return null;
            Assert.IsFalse(guard.Alerted, "The look-around ends.");
            Assert.IsTrue(guard.Returning, "And he heads back.");
            for (float until = Time.time + 10; guard.Returning && Time.time < until;) yield return null;
            Assert.IsFalse(guard.Returning);
            Assert.Less(Vector3.Distance(guard.transform.position, post), 1.2f, "Back at his post.");
            Assert.Less(Quaternion.Angle(guard.transform.rotation, facing), 10f, "Facing out again.");
            Assert.IsFalse(guard.Engaged);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator KnifeFromBehindTakesDownAnUnawareGuardSilently()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var inventory = mission.GetComponent<Map01Inventory>();
            var routing = RouteInputToGame();
            var mouse = InputSystem.AddDevice<Mouse>();
            var keyboard = InputSystem.AddDevice<Keyboard>(); // Map01PlayerInteraction reads nothing without one.
            try
            {
                // What is under test is the knife making no sound — not the thud of a teleport landing.
                if (mission.player.TryGetComponent(out PlayerFootsteps steps)) steps.enabled = false;
                var guard = OnlyGuard(mission, "Outpost guard 0");
                yield return WaitGameSeconds(4.2f); // ReturnToPost leaves him calm for 4 s; no free pass.
                Assert.IsTrue(inventory.EquipItem("knife"));
                // Crept up crouched right behind him, facing his back.
                mission.ModernPlayer.RestoreMotion(true);
                Vector3 back = guard.transform.position - guard.transform.forward * 1.5f;
                Teleport(mission, back, guard.transform.eulerAngles.y);
                yield return WaitGameSeconds(.8f);
                Assert.IsTrue(mission.Crouched);
                Assert.AreEqual(0f, guard.Suspicion, "Crouched behind him, Nam goes unnoticed.");
                var centre = new Vector2(Screen.width / 2f, Screen.height / 2f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = centre }.WithButton(MouseButton.Left));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = centre });
                for (float until = Time.time + 1; guard.Alive && Time.time < until;) yield return null;
                Assert.IsFalse(guard.Alive, "One stab from behind takes an unaware guard down.");
                Assert.IsFalse(AnyAlertedBut(mission, guard), "Silently: no one else heard it.");

                // Face to face it is just a stab — and now he knows.
                var other = OnlyGuard(mission, "Outpost guard 1");
                yield return WaitGameSeconds(4.2f);
                Vector3 front = other.transform.position + other.transform.forward * 1.3f;
                Teleport(mission, front, Yaw(front, other.transform.position));
                yield return null; yield return null; // Before he has made Nam out.
                Assert.IsFalse(other.Engaged);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = centre }.WithButton(MouseButton.Left));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = centre });
                var health = other.GetComponent<Health>();
                for (float until = Time.time + 1; health.Current >= health.Max && Time.time < until;) yield return null;
                yield return null;
                Assert.Less(health.Current, health.Max, "The knife hit him.");
                Assert.IsTrue(other.Alive, "From the front it is not a takedown.");
                Assert.IsTrue(other.Engaged, "Hurt, he turns on Nam.");
            }
            finally { InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing); }
            yield return new ExitPlayMode();
        }
    }
}
