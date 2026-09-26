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
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class RescueTests : ForestSceneTestBase
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        // Lambdas stay in static helpers: one capturing an iterator local breaks after the domain
        // reload EnterPlayMode triggers (see ForestFlowTests.NearestOutpostGuard).

        private static void Teleport(Map01Mission mission, Vector3 position)
        {
            var controller = mission.player.GetComponent<CharacterController>();
            controller.enabled = false; mission.player.position = position; controller.enabled = true;
        }

        private static Vector3 NavPoint(Vector3 near)
        {
            Assert.IsTrue(NavMesh.SamplePosition(near, out var hit, 4f, NavMesh.AllAreas), $"No walkable ground near {near}.");
            return hit.position;
        }

        private static void AimCamera(Map01Mission mission, Vector3 point)
        {
            var rig = mission.gameCamera.GetComponent<ThirdPersonCamera>();
            var dir = point - mission.gameCamera.transform.position;
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            typeof(ThirdPersonCamera).GetField("_yaw", Private).SetValue(rig, yaw);
            typeof(ThirdPersonCamera).GetField("_pitch", Private).SetValue(rig, 8f);
            mission.player.rotation = Quaternion.Euler(0, yaw, 0);
        }

        private static float DistanceToPath(Vector3 p, Vector3[] corners)
        {
            float best = float.MaxValue;
            for (int i = 0; i + 1 < corners.Length; i++)
            {
                Vector2 a = new Vector2(corners[i].x, corners[i].z), b = new Vector2(corners[i + 1].x, corners[i + 1].z), q = new Vector2(p.x, p.z);
                var ab = b - a; float t = ab.sqrMagnitude < 1e-4f ? 0 : Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(q, a + ab * t));
            }
            return best;
        }

        private static Vector3[] Route(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(NavPoint(from), NavPoint(to), NavMesh.AllAreas, path), $"No route {from} -> {to}.");
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, $"Route {from} -> {to} must be complete.");
            return path.corners;
        }

        private static Map01EnemyController Nearest(Map01Rescue rescue, Vector3 to) =>
            rescue.Squad.OrderBy(g => Vector3.Distance(g.transform.position, to)).First();
        private static bool AllCalmAtPost(Map01Rescue rescue) => rescue.Squad.All(g => g.Alive && !g.Engaged && !g.Alerted);

        [UnityTest]
        public IEnumerator HungIsHeldAtTheJettyByASquadOfFourWellAwayFromTheCamps()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var rescue = mission.GetComponent<Map01Rescue>();
            Assert.IsNotNull(rescue, "Map01Quest must bring the rescue along.");
            Assert.AreEqual(Map01Quest.RescueStage, quest.Stage);

            var jetty = GameObject.Find("B_HungCaptive").transform.position;
            Assert.Less(Vector3.Distance(mission.hung.position, jetty), 3f, "Hùng is held at the north jetty.");
            Assert.AreEqual(4, rescue.Squad.Count, "Four soldiers hold him.");
            foreach (var guard in rescue.Squad)
                Assert.Less(Vector3.Distance(guard.transform.position, mission.hung.position), 15f, $"{guard.name} surrounds the prisoner.");
            Vector3 held = mission.hung.position;
            yield return WaitGameSeconds(1.5f);
            Assert.Less(Vector3.Distance(mission.hung.position, held), .5f, "A prisoner does not follow Nam.");

            // No camp on the way there: base -> herb crate -> jetty — no camp guard stands or
            // patrols within sight of that walk — and none near the jetty.
            var herb = mission.Points.Single(p => p.id == "tutorial_loot").transform.position;
            var toHerb = Route(mission.player.position, herb);
            var toJetty = Route(herb, mission.hung.position);
            var scouting = mission.GetComponent<Map01Scouting>();
            Assert.AreEqual(3, scouting.Camps.Count);
            foreach (var camp in scouting.Camps)
            {
                Assert.Greater(Vector3.Distance(camp.Center, mission.hung.position), 45f, $"Camp {camp.Number} must not overlook the jetty.");
                foreach (var guard in camp.Guards)
                    foreach (var spot in guard.PatrolPoints.Prepend(guard.transform.position))
                    {
                        float off = Mathf.Min(DistanceToPath(spot, toHerb), DistanceToPath(spot, toJetty));
                        Assert.Greater(off, guard.VisionRange + 2f, $"{guard.name} of camp {camp.Number} must not see the rescue route from {spot}.");
                    }
            }
            Assert.Greater(Vector3.Distance(rescue.RetryPoint, rescue.CaptivePost), 40f, "A failed rescue restarts a way back down the road.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CreepingCloseOverhearsTheSquadAndThenHung()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var rescue = mission.GetComponent<Map01Rescue>();
            foreach (var guard in rescue.Squad) guard.enabled = false; // Listening is under test, not being seen.
            mission.Say(null, -1f);

            Vector3 away = (rescue.RetryPoint - rescue.CaptivePost).normalized;
            Teleport(mission, NavPoint(rescue.CaptivePost + away * 22f));
            for (float until = Time.time + 3; (mission.Dialogue == null || !mission.Dialogue.StartsWith("Lính địch")) && Time.time < until;) yield return null;
            StringAssert.StartsWith("Lính địch", mission.Dialogue, "Within earshot the squad is heard.");
            StringAssert.Contains("lính thông tin", mission.Dialogue, "They talk about the signals runner they caught.");
            AimCamera(mission, rescue.CaptivePost + Vector3.up);
            yield return WaitGameSeconds(.4f);
            Directory.CreateDirectory("Logs/GuidePreview");
            ScreenCapture.CaptureScreenshot("Logs/GuidePreview/rescue-jetty.png");
            yield return null; yield return null;

            Teleport(mission, NavPoint(rescue.CaptivePost + away * 8f));
            typeof(Map01Rescue).GetField("nextChatter", Private).SetValue(rescue, 0f);
            for (float until = Time.time + 2; !mission.Dialogue.StartsWith("Hùng") && Time.time < until;) yield return null;
            StringAssert.StartsWith("Hùng (thì thào)", mission.Dialogue, "Right up close, Hùng whispers a warning.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator EngagedSquadShootsHungAndHisFallRestartsTheRescue()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var inventory = mission.GetComponent<Map01Inventory>();
            var rescue = mission.GetComponent<Map01Rescue>();
            Vector3[] posts = rescue.Squad.Select(g => g.transform.position).ToArray();
            var shooter = Nearest(rescue, mission.hung.position);
            foreach (var guard in rescue.Squad) guard.enabled = guard == shooter;
            yield return null;

            // Alarmed, but Nam far out of sight: the guard turns on the prisoner instead.
            float full = rescue.HungHealth;
            Assert.AreEqual(rescue.HungMaxHealth, full);
            typeof(Map01EnemyController).GetField("_engagedUntil", Private).SetValue(shooter, Time.time + 30f);
            for (float until = Time.time + 6; rescue.HungHealth >= full && Time.time < until;) yield return null;
            Assert.Less(rescue.HungHealth, full, "An engaged guard with Nam out of sight shoots Hùng.");
            Assert.AreEqual(shooter.DamagePerShot, full - rescue.HungHealth, .01f, "One of his rounds.");

            // Hùng falls: everything stops on the failure panel.
            rescue.HitHung(9999f);
            Assert.IsTrue(rescue.HungDown);
            Assert.IsTrue(mission.Stopped, "Hùng's death stops the mission.");
            yield return WaitGameSeconds(.3f);
            Directory.CreateDirectory("Logs/GuidePreview");
            ScreenCapture.CaptureScreenshot("Logs/GuidePreview/rescue-failed.png");
            yield return null; yield return null;

            // [Enter]: the rescue starts over from a way back down the road.
            inventory.Spend("herb", inventory.Count("herb"));
            rescue.Retry();
            Assert.IsFalse(rescue.HungDown);
            Assert.IsFalse(mission.Stopped);
            Assert.AreEqual(Map01Quest.RescueStage, quest.Stage);
            Assert.AreEqual(rescue.HungMaxHealth, rescue.HungHealth, "Hùng is back on his feet.");
            Assert.Less(Vector3.Distance(mission.hung.position, rescue.CaptivePost), .6f, "And held at the jetty again.");
            Assert.Less(Vector3.Distance(mission.player.position, rescue.RetryPoint), 1f, "Nam restarts a way back down the road.");
            Assert.AreEqual(1, inventory.Count("herb"), "With a herb to treat Hùng again.");
            Assert.IsTrue(AllCalmAtPost(rescue), "The squad is calm again.");
            for (int i = 0; i < posts.Length; i++)
                Assert.Less(Vector3.Distance(rescue.Squad[i].transform.position, posts[i]), 1.5f, $"{rescue.Squad[i].name} back at his post.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator JettySquadIsBackOnceHungIsHome()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var inventory = mission.GetComponent<Map01Inventory>();
            var interaction = mission.GetComponent<Map01PlayerInteraction>();
            var rescue = mission.GetComponent<Map01Rescue>();
            var fallen = rescue.Squad[0];
            Vector3 post = fallen.transform.position;
            Vector3 home = mission.player.position;
            foreach (var guard in rescue.Squad) guard.enabled = false;

            fallen.GetComponent<Health>().TakeDamage(9999, fallen.transform.position, null);
            Assert.IsFalse(fallen.Alive);
            inventory.Add("herb", 1);
            Teleport(mission, mission.hung.position);
            yield return null;
            quest.TryRescueHung();
            Assert.AreEqual(Map01Quest.EscortStage, quest.Stage, "Freed and treated, Hùng heads home with Nam.");
            Assert.IsTrue(rescue.Exposed, "On the way home he can still be shot.");

            Teleport(mission, home);
            mission.hung.GetComponent<NavMeshAgent>().Warp(home + Vector3.right);
            interaction.Interact(mission.Points.Single(p => p.id == "supplies"));
            Assert.AreEqual(Map01Quest.BriefingStage, quest.Stage, "Home: the rescue is over.");
            Assert.IsFalse(rescue.Exposed);
            yield return WaitGameSeconds(.2f);
            Assert.IsTrue(fallen.Alive, "The jetty post is manned again once Hùng is home.");
            Assert.Less(Vector3.Distance(fallen.transform.position, post), 1.5f);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator HungsWoundsSurviveASaveAndLoad()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var rootField = typeof(ForestSaveSlots).GetField("storageRoot", BindingFlags.Static | BindingFlags.NonPublic);
            string root = Path.Combine(Application.temporaryCachePath, "rescue-test-" + Guid.NewGuid().ToString("N"));
            rootField.SetValue(null, root);
            try
            {
                var mission = Object.FindFirstObjectByType<Map01Mission>();
                var rescue = mission.GetComponent<Map01Rescue>();
                foreach (var guard in rescue.Squad) guard.enabled = false;
                rescue.HitHung(30f);
                float wounded = rescue.HungHealth;
                Assert.AreEqual(rescue.HungMaxHealth - 30f, wounded, .01f);
                Assert.IsTrue(mission.GetComponent<Map01SaveSystem>().SaveSlot(1, out var error, true), error);
                Map01SaveSystem.BeginGame(1);
                yield return null;
                var pending = typeof(Map01SaveSystem).GetField("pendingCheckpoint", BindingFlags.Static | BindingFlags.NonPublic);
                double deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (pending.GetValue(null) != null && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.IsNull(pending.GetValue(null));
                rescue = Object.FindFirstObjectByType<Map01Mission>().GetComponent<Map01Rescue>();
                Assert.AreEqual(wounded, rescue.HungHealth, .01f, "Hùng's wounds are part of the checkpoint.");
            }
            finally
            {
                rootField.SetValue(null, null); Time.timeScale = 1;
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            yield return new ExitPlayMode();
        }
    }
}
