using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.Media;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// Guards finding a comrade's body (Map01BodyDiscovery), as the user locked it on 18/10/2026: seen by the guard's own
    /// sight rules from 0.6 of his range after a second; never through a wall, smoke or a bush pull; found once; jetty B
    /// searches and never executes Hùng; points 1–2 search, point 2 radios while the radio works; at C3 the finder runs to
    /// strike the kẻng (only the camp on guard without its striker, nothing if Nam drops him); the save keeps it.
    /// </summary>
    public sealed class BodyDiscoveryTests : ForestSceneTestBase
    {
        // Lambdas stay in static helpers or capture only static fields (the domain reload of EnterPlayMode).
        internal static Map01Mission mission;
        internal static Map01Quest quest;
        internal static Map01Rescue rescue;
        internal static Map01Story story;
        internal static Map01C3Mission c3;
        internal static Map01BodyDiscovery discovery;

        internal static void OpenMap()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
        }

        /// <summary>Every guard held still (controller and agent off) until a test wakes the ones it needs.</summary>
        internal static void Settle()
        {
            mission = Object.FindFirstObjectByType<Map01Mission>();
            quest = mission.GetComponent<Map01Quest>();
            rescue = mission.GetComponent<Map01Rescue>();
            story = mission.GetComponent<Map01Story>();
            c3 = mission.GetComponent<Map01C3Mission>();
            discovery = Map01BodyDiscovery.For(mission);
            rescue.MarkIntroSeen();
            foreach (var enemy in mission.Enemies) Hold(enemy, true);
        }

        internal static void Hold(Map01EnemyController guard, bool hold)
        {
            if (guard == null) return;
            guard.enabled = !hold;
            var agent = guard.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = !hold;
        }

        /// <summary>Killed while held, so no one hears or sees it (a jetty guard's hurt would raise the rescue's alarm).</summary>
        internal static void Kill(Map01EnemyController guard)
        {
            Hold(guard, true);
            guard.GetComponent<Health>().TakeDamage(99999, guard.transform.position + Vector3.up, null);
            Assert.IsFalse(guard.Alive);
        }

        /// <summary>
        /// <paramref name="guard"/> set down on the NavMesh about <paramref name="distance"/> m from the body, facing it, at
        /// the first spot round it from where he sees it and can walk to it. He holds there (no beat) until he notices.
        /// </summary>
        internal static bool Overlook(Map01EnemyController guard, Map01EnemyController body, float distance)
        {
            var at = Map01BodyDiscovery.BodySpot(body);
            for (int step = 0; step < 24; step++)
            {
                foreach (float d in new[] { distance, distance - 1.5f, distance + 1.5f })
                {
                    var dir = Quaternion.Euler(0, step * 15f, 0) * Vector3.forward;
                    if (!NavMesh.SamplePosition(at + dir * d, out var hit, 1f, NavMesh.AllAreas)) continue;
                    var path = new NavMeshPath();
                    if (!NavMesh.CalculatePath(hit.position, at, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) continue;
                    var toward = Vector3.ProjectOnPlane(Map01BodyDiscovery.SightPoint(body) - hit.position, Vector3.up);
                    guard.Repost(hit.position, Quaternion.LookRotation(toward).eulerAngles.y);
                    Physics.SyncTransforms();
                    if (Map01BodyDiscovery.CanSee(mission, guard, body, out _)) return true;
                }
            }
            return false;
        }

        internal static float Flat(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;
        // Sleeping reserves now have visible bed proxies; visibility is not proof that they woke.
        private static int ActiveSleepers() => c3.Current.sleepersAwake.Count(awake => awake);

        // Every guard of Map 1 at start serves jetty B, point 1, point 2 or C3 (the old scouting camps are off), so the
        // rules common to all places are tested on point 1's men.
        internal static Map01EnemyController[] Point(int point) =>
            story.Guards(point).Where(e => e != null && e.Alive).OrderBy(e => e.name, StringComparer.Ordinal).ToArray();

        [UnityTest]
        public IEnumerator ABodyInSightIsFoundAfterASecondOnceAndChecked()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            Assert.IsNotNull(discovery, "Map 1 sets up body discovery.");
            var guards = Point(1);
            Assert.GreaterOrEqual(guards.Length, 3);
            // The second stays held (out of the point's search) until he is the one to pass the body.
            var body = guards[0]; var finder = guards[1]; var second = guards[2];
            Kill(body);
            yield return WaitGameSeconds(2f);
            Assert.IsTrue(Overlook(finder, body, 5f), "A spot near the body from where it can be seen.");
            Hold(finder, false);
            yield return WaitGameSeconds(.5f);
            Assert.AreEqual(0, discovery.Found, "A glance is not enough.");
            yield return WaitGameSeconds(1f);
            Assert.AreEqual(1, discovery.Found, "Seen for a second: found.");
            Assert.IsTrue(body.BodyFound);
            Assert.IsTrue(finder.Wary, "He stays wary.");
            Assert.IsTrue(finder.Alerted, "He goes to look.");
            yield return WaitGameSeconds(5f);
            Assert.Less(Flat(finder.Position, Map01BodyDiscovery.BodySpot(body)), 3f, "He went over to the body.");

            Assert.IsTrue(Overlook(second, body, 5f));
            Hold(second, false);
            yield return WaitGameSeconds(1.6f);
            Assert.AreEqual(1, discovery.Found, "A body is found once.");
            Assert.IsFalse(second.Alerted, "…so the next man to pass it is not set off.");
            Assert.IsFalse(second.Wary);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AWallSmokeOrTheBushPullHidesTheBody()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            var guards = Point(1);
            var body = guards[0]; var finder = guards[1];
            Kill(body);
            yield return WaitGameSeconds(2f);
            Assert.IsTrue(Overlook(finder, body, 6f));
            Vector3 eye = finder.Position + Vector3.up * 1.3f, target = Map01BodyDiscovery.SightPoint(body);
            var mid = Vector3.Lerp(eye, target, .5f);

            // A wall between them.
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetPositionAndRotation(mid, Quaternion.LookRotation(Vector3.ProjectOnPlane(target - eye, Vector3.up)));
            wall.transform.localScale = new Vector3(4f, 4f, .3f);
            Physics.SyncTransforms();
            Assert.IsFalse(Map01BodyDiscovery.CanSee(mission, finder, body, out _), "The wall hides it.");
            Hold(finder, false);
            yield return WaitGameSeconds(1.6f);
            Assert.AreEqual(0, discovery.Found, "Not through a wall.");

            // A smoke cloud instead.
            Hold(finder, true);
            Object.Destroy(wall);
            yield return null;
            Map01Smoke.Spawn(new Vector3(mid.x, target.y, mid.z), 1.6f, 30f, false);
            Assert.IsFalse(Map01BodyDiscovery.CanSee(mission, finder, body, out _), "The smoke hides it.");
            Hold(finder, false);
            yield return WaitGameSeconds(1.6f);
            Assert.AreEqual(0, discovery.Found, "Not through smoke.");

            // Pulled into a bush (Map01BushTakedown's silentKind 1): never found, even in plain sight.
            Hold(finder, true);
            Map01Smoke.ClearAll();
            var bush = body.Capture(); bush.silent = true; bush.silentKind = 1;
            body.RestoreSnapshot(bush);
            Assert.IsTrue(body.BodyHidden);
            Assert.IsTrue(Overlook(finder, body, 5f), "In plain sight, but hidden by the rule.");
            Hold(finder, false);
            yield return WaitGameSeconds(1.6f);
            Assert.AreEqual(0, discovery.Found, "A bush-pulled body is never found.");

            // The same body in the open: found (the spots above were real).
            Hold(finder, true);
            var open = body.Capture(); open.silent = false; open.silentKind = 0; open.bodyState = 1;
            body.RestoreSnapshot(open);
            yield return null;
            Assert.IsTrue(Overlook(finder, body, 5f));
            Hold(finder, false);
            yield return WaitGameSeconds(1.6f);
            Assert.AreEqual(1, discovery.Found);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AtTheJettyTheSquadSearchesAndHungIsNotExecuted()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            quest.RestoreStoryStage(Map01Quest.RescueStage, RescueOrdered());
            Assert.IsTrue(quest.RescueOrderAccepted);
            var overseer = rescue.Squad[0]; var body = rescue.Squad[1]; var finder = rescue.Squad[2]; var helper = rescue.Squad[3];
            Kill(body);
            yield return WaitGameSeconds(2f);
            Assert.IsTrue(Overlook(finder, body, 5f));
            Hold(finder, false); Hold(helper, false); Hold(overseer, false);
            float helperBefore = Flat(helper.Position, Map01BodyDiscovery.BodySpot(body));
            yield return WaitGameSeconds(1.6f);
            Assert.AreEqual(1, discovery.Found);
            Assert.IsTrue(finder.Alerted && helper.Alerted, "The squad searches.");
            Assert.IsTrue(overseer.Wary && finder.Wary && helper.Wary, "…and stays wary.");
            yield return WaitGameSeconds(6f);
            Assert.AreEqual(Map01Rescue.Phase.Captive, rescue.CurrentPhase, "A body is not Nam seen: Hùng is not executed.");
            Assert.AreEqual(rescue.HungMaxHealth, rescue.HungHealth);
            Assert.IsTrue(overseer.AtHostagePost, "The overseer never leaves Hùng.");
            Assert.Less(Flat(helper.Position, Map01BodyDiscovery.BodySpot(body)), helperBefore, "The other patrol comes over.");
            Assert.IsTrue(rescue.CanExecuteCaptive, "The execution rule itself is untouched.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PointTwoRadiosForTheResponseWhileItsRadioWorks()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            var men = Point(2);
            Assert.GreaterOrEqual(men.Length, 3);
            var body = men[0]; var finder = men[1]; var helper = men[2];
            Assert.IsFalse(story.Progress.radioDisabled);
            Kill(body);
            yield return WaitGameSeconds(2f);
            Assert.IsTrue(Overlook(finder, body, 5f));
            Hold(finder, false); Hold(helper, false);
            yield return WaitGameSeconds(1.6f);
            Assert.AreEqual(1, discovery.Found);
            Assert.IsTrue(story.Progress.radioResponseCalled, "The radio still works: the response squad is called.");
            Assert.IsTrue(helper.Alerted && helper.Wary, "The point's men search.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator PointOneSearchesAndASilencedRadioCallsNoOne()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            story.Progress.radioDisabled = true;
            var two = Point(2);
            Kill(two[0]);
            yield return WaitGameSeconds(2f);
            Assert.IsTrue(Overlook(two[1], two[0], 5f));
            Hold(two[1], false);
            yield return WaitGameSeconds(1.6f);
            Assert.AreEqual(1, discovery.Found);
            Assert.IsFalse(story.Progress.radioResponseCalled, "Nam silenced the radio: nobody is called.");

            var one = Point(1);
            Assert.GreaterOrEqual(one.Length, 3);
            Kill(one[0]);
            yield return WaitGameSeconds(2f);
            Assert.IsTrue(Overlook(one[1], one[0], 5f));
            Hold(one[1], false); Hold(one[2], false);
            yield return WaitGameSeconds(1.6f);
            Assert.AreEqual(2, discovery.Found);
            Assert.IsTrue(one[1].Alerted && one[2].Alerted, "Point 1's men search.");
            Assert.IsFalse(story.Progress.radioResponseCalled, "Point 1 has no radio to call from.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AtC3TheFinderRunsToStrikeTheKieng()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            var finder = c3.Guard("rover"); var body = c3.Guard("porch");
            Kill(body);
            yield return WaitGameSeconds(2f);
            Assert.IsTrue(Overlook(finder, body, 5f));
            Hold(finder, false);
            yield return WaitGameSeconds(1.6f);
            Assert.AreEqual(1, discovery.Found);
            Assert.AreSame(finder, c3.BodyRunnerGuard, "He runs for the kẻng.");
            Assert.IsFalse(c3.Current.kiengRung, "Not struck yet: Nam has a moment to stop him.");
            for (int i = 0; i < 80 && !c3.Current.kiengRung; i++) yield return WaitGameSeconds(.5f);
            Assert.IsTrue(c3.Current.kiengRung, "He gets there and strikes it.");
            Assert.IsNull(c3.BodyRunnerGuard);
            Assert.AreEqual(3, ActiveSleepers(), "The sleepers come out…");
            Assert.IsFalse(c3.Sleepers.Any(s => s.Engaged), "…to search, not to fight: nobody knows where Nam is.");
            Assert.IsTrue(c3.Wary, "The camp stands on guard.");
            Assert.IsFalse(c3.Alarmed);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AtC3WithoutTheStrikerTheCampOnlyStandsOnGuard()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            Assert.IsTrue(c3.Use(c3.KiengPoint));
            var finder = c3.Guard("rover"); var body = c3.Guard("porch");
            Kill(body);
            yield return WaitGameSeconds(2f);
            Assert.IsTrue(Overlook(finder, body, 5f));
            Hold(finder, false);
            yield return WaitGameSeconds(1.6f);
            Assert.AreEqual(1, discovery.Found);
            Assert.IsTrue(c3.Wary, "He shouts: the camp stands on guard.");
            Assert.IsNull(c3.BodyRunnerGuard, "Nothing to strike.");
            yield return WaitGameSeconds(3f);
            Assert.IsFalse(c3.Current.kiengRung);
            Assert.AreEqual(0, ActiveSleepers());
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AtC3DroppingTheRunnerKeepsTheKiengSilent()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            var finder = c3.Guard("rover"); var body = c3.Guard("porch");
            Kill(body);
            yield return WaitGameSeconds(2f);
            Assert.IsTrue(Overlook(finder, body, 5f));
            Hold(finder, false);
            yield return WaitGameSeconds(1.6f);
            Assert.AreSame(finder, c3.BodyRunnerGuard);
            Kill(finder);
            yield return WaitGameSeconds(1f);
            Assert.IsNull(c3.BodyRunnerGuard);
            Assert.IsFalse(c3.Current.kiengRung, "Dropped on his way: no kẻng.");
            Assert.IsFalse(c3.Wary, "…and nobody else knows.");
            Assert.AreEqual(0, ActiveSleepers());
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator TheSaveKeepsFoundBodiesWaryGuardsAndTheKiengRunner()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            var guards = Point(1);
            var body = guards[0]; var finder = guards[1];
            Kill(body);
            yield return WaitGameSeconds(2f);
            Assert.IsTrue(Overlook(finder, body, 5f));
            Hold(finder, false);
            yield return WaitGameSeconds(1.6f);
            Assert.IsTrue(body.BodyFound);

            var bodySave = JsonUtility.FromJson<Map01EnemyController.Snapshot>(JsonUtility.ToJson(body.Capture()));
            var finderSave = JsonUtility.FromJson<Map01EnemyController.Snapshot>(JsonUtility.ToJson(finder.Capture()));
            Assert.AreEqual(2, bodySave.bodyState);
            Assert.IsTrue(finderSave.wary);

            body.ReturnToPost(); finder.ReturnToPost();
            Assert.IsTrue(body.Alive); Assert.IsFalse(finder.Wary, "A fresh post forgets.");
            body.RestoreSnapshot(bodySave); finder.RestoreSnapshot(finderSave);
            Assert.IsFalse(body.Alive);
            Assert.IsTrue(body.BodyFound, "Found stays found: no second discovery after a load.");
            Assert.IsTrue(finder.Wary);

            // A save from before bodies could be found: its dead count as found, so loading sets nobody off.
            bodySave.bodyState = 0;
            body.ReturnToPost(); body.RestoreSnapshot(bodySave);
            Assert.IsTrue(body.BodyFound);

            // The C3 runner on his way.
            var rover = c3.Guard("rover");
            c3.Current.bodyRunner = rover.SaveId; c3.Current.bodyAt = new[] { 1f, 2f, 3f };
            var post = c3.Capture();
            c3.Restore(null);
            Assert.IsNull(c3.BodyRunnerGuard);
            c3.Restore(post);
            Assert.AreSame(rover, c3.BodyRunnerGuard);
            yield return new ExitPlayMode();
        }
    }

    /// <summary>
    /// Review films of the body discovery at each place (Logs/BodyDiscovery/*.mp4, 15 fps, with a still every 3 s), for the
    /// user to watch before the behaviour is signed off. Explicit: run by name only.
    /// </summary>
    public sealed class BodyDiscoveryReview : ForestSceneTestBase
    {
        private static Map01Mission M => BodyDiscoveryTests.mission;

        /// <summary>
        /// Nam held 34 m straight above the finder, out of every guard's sight range but within earshot of their shouts
        /// (Map01BodyDiscovery.ShoutMeters), so the shouts reach the caption. Film only: he is frozen in the air.
        /// </summary>
        private static void ParkNam(Map01EnemyController finder)
        {
            M.player.GetComponent<CharacterController>().enabled = false;
            M.ModernPlayer.enabled = false;
            M.player.position = finder.Position + Vector3.up * 34f;
            Physics.SyncTransforms();
        }

        private static IEnumerator Roll(Film film, float seconds, Func<Map01EnemyController> follow, Vector3 body, Func<string> status)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                var guard = follow();
                var focus = Vector3.Lerp(guard != null ? guard.Position : body, body, .35f) + Vector3.up;
                film.Aim(focus, Vector3.ProjectOnPlane(body - (guard != null ? guard.Position : body), Vector3.up));
                string line = Time.time < M.DialogueUntil ? M.Dialogue : "";
                film.Shoot(status() + (string.IsNullOrEmpty(line) ? "" : "\n“" + line + "”"));
                yield return null;
            }
        }

        private static string Status(string place)
        {
            var d = BodyDiscoveryTests.discovery;
            string kieng = BodyDiscoveryTests.c3.Current.kiengRung ? " · kẻng đã gõ" : BodyDiscoveryTests.c3.BodyRunnerGuard != null ? " · đang chạy tới kẻng" : "";
            return $"{place} · t={Time.time:0.0}s · xác bị phát hiện: {d.Found}{kieng}";
        }

        [UnityTest, Explicit("Review film: run by name.")]
        public IEnumerator FilmJetty()
        {
            BodyDiscoveryTests.OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            BodyDiscoveryTests.Settle();
            var r = BodyDiscoveryTests.rescue;
            BodyDiscoveryTests.quest.RestoreStoryStage(Map01Quest.RescueStage, RescueOrdered());
            var body = r.Squad[1]; finderNow = r.Squad[2];
            BodyDiscoveryTests.Kill(body);
            yield return WaitGameSeconds(2f);
            Assert.IsTrue(BodyDiscoveryTests.Overlook(finderNow, body, 6f));
            var at = Map01BodyDiscovery.BodySpot(body);
            ParkNam(finderNow);
            using var film = new Film("ben-B-giu-Hung");
            foreach (var g in r.Squad) if (g != body) BodyDiscoveryTests.Hold(g, false);
            yield return Roll(film, 16f, StaticFinder, at, StaticStatusJetty);
            Assert.AreEqual(Map01Rescue.Phase.Captive, r.CurrentPhase);
            Assert.AreEqual(1, BodyDiscoveryTests.discovery.Found);
            yield return new ExitPlayMode();
        }

        // EnterPlayMode must be yielded by the [UnityTest] itself; the take after it is a plain helper.
        [UnityTest, Explicit("Review film: run by name.")]
        public IEnumerator FilmPointOne()
        {
            BodyDiscoveryTests.OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            BodyDiscoveryTests.Settle();
            yield return PointTake(1, "diem-1-kho");
            yield return new ExitPlayMode();
        }

        [UnityTest, Explicit("Review film: run by name.")]
        public IEnumerator FilmPointTwo()
        {
            BodyDiscoveryTests.OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            BodyDiscoveryTests.Settle();
            yield return PointTake(2, "diem-2-tram-dai");
            yield return new ExitPlayMode();
        }

        private static IEnumerator PointTake(int point, string take)
        {
            var men = BodyDiscoveryTests.Point(point);
            var body = men[0]; finderNow = men[1];
            BodyDiscoveryTests.Kill(body);
            yield return WaitGameSeconds(2f);
            Assert.IsTrue(BodyDiscoveryTests.Overlook(finderNow, body, 6f));
            var at = Map01BodyDiscovery.BodySpot(body);
            ParkNam(finderNow);
            placeNow = point == 1 ? "Điểm 1 — kho" : "Điểm 2 — trạm đài";
            using var film = new Film(take);
            foreach (var g in men) if (g != body) BodyDiscoveryTests.Hold(g, false);
            yield return Roll(film, 16f, StaticFinder, at, StaticStatusPlace);
            Assert.AreEqual(1, BodyDiscoveryTests.discovery.Found);
        }

        [UnityTest, Explicit("Review film: run by name.")]
        public IEnumerator FilmC3()
        {
            BodyDiscoveryTests.OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            BodyDiscoveryTests.Settle();
            var c3 = BodyDiscoveryTests.c3;
            var body = c3.Guard("porch"); finderNow = c3.Guard("rover");
            BodyDiscoveryTests.Kill(body);
            yield return WaitGameSeconds(2f);
            Assert.IsTrue(BodyDiscoveryTests.Overlook(finderNow, body, 6f));
            var at = Map01BodyDiscovery.BodySpot(body);
            ParkNam(finderNow);
            placeNow = "Trại C3";
            using var film = new Film("trai-C3-keng", 17f, 7f);
            // Not the tower's sentry: his agent stays off (he stands on the platform, off the NavMesh).
            foreach (var role in new[] { "rover", "gate", "patrolSouth", "patrolNorth" }) BodyDiscoveryTests.Hold(c3.Guard(role), false);
            yield return Roll(film, 30f, StaticFinder, at, StaticStatusPlace);
            Assert.IsTrue(c3.Current.kiengRung, "The kẻng was struck in the take.");
            yield return new ExitPlayMode();
        }

        // Static state for the helpers (no iterator-local captures across the domain reload).
        private static Map01EnemyController finderNow;
        private static string placeNow = "";
        private static Map01EnemyController StaticFinder() => finderNow;
        private static string StaticStatusJetty() => Status("Bến B — giữ Hùng") + (BodyDiscoveryTests.rescue.CurrentPhase == Map01Rescue.Phase.Captive ? " · Hùng an toàn" : " · Hùng gặp nguy");
        private static string StaticStatusPlace() => Status(placeNow);

        /// <summary>A review camera rendered into an mp4, one frame per game frame at 15 fps, with a caption strip.</summary>
        internal sealed class Film : IDisposable
        {
            private readonly int Width, Height, fps;
            private readonly GameObject root;
            private readonly Camera camera;
            private readonly RenderTexture target;
            private readonly Texture2D frame;
            private readonly TextMesh caption;
            private readonly MediaEncoder encoder;
            private readonly string path;
            private Vector3 smoothFocus, smoothSide;
            private int lastFrame = -1, frames;
            private readonly float height, side;

            /// <param name="height">Camera height over the action; high (C3) keeps the roofs out of the shot.</param>
            public Film(string take, float height = 5.5f, float side = 9f, int width = 1280, int heightPixels = 720, int framesPerSecond = 15)
            {
                Width = width; Height = heightPixels; fps = framesPerSecond;
                this.height = height; this.side = side;
                path = "Logs/BodyDiscovery/" + take + ".mp4";
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                Time.captureFramerate = fps;
                root = new GameObject("Body discovery review camera");
                camera = root.AddComponent<Camera>();
                camera.fieldOfView = 50f; camera.nearClipPlane = .1f; camera.farClipPlane = 600f; camera.enabled = false;
                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = target;
                frame = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
                var text = new GameObject("Caption");
                text.transform.SetParent(root.transform, false);
                text.transform.localPosition = new Vector3(0f, -.36f, 1f);
                caption = text.AddComponent<TextMesh>();
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                caption.font = font; text.GetComponent<MeshRenderer>().sharedMaterial = font.material;
                caption.fontSize = 48; caption.characterSize = .0062f; caption.anchor = TextAnchor.LowerCenter; caption.alignment = TextAlignment.Center;
                caption.color = new Color(1f, .96f, .82f);
                encoder = new MediaEncoder(path, new VideoTrackAttributes { frameRate = new MediaRational(fps), width = (uint)Width, height = (uint)Height, includeAlpha = false });
            }

            /// <summary>From the side of the line guard → body, a little above, easing after the guard.</summary>
            public void Aim(Vector3 focus, Vector3 line)
            {
                var across = Vector3.Cross(Vector3.up, line.sqrMagnitude > .01f ? line.normalized : Vector3.forward);
                if (frames == 0) { smoothFocus = focus; smoothSide = across; }
                smoothFocus = Vector3.Lerp(smoothFocus, focus, .15f);
                smoothSide = Vector3.Slerp(smoothSide, across, .05f);
                root.transform.position = smoothFocus + smoothSide * side + Vector3.up * height - line.normalized * 2f;
                root.transform.LookAt(smoothFocus);
            }

            public void Shoot(string text)
            {
                if (Time.frameCount == lastFrame) return;
                lastFrame = Time.frameCount;
                caption.text = text;
                camera.Render();
                var previous = RenderTexture.active;
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                frame.Apply();
                RenderTexture.active = previous;
                encoder.AddFrame(frame);
                if (frames % 45 == 0) File.WriteAllBytes(Path.ChangeExtension(path, null) + "-" + frames.ToString("D4") + ".png", frame.EncodeToPNG());
                frames++;
            }

            public void Dispose()
            {
                encoder.Dispose();
                Time.captureFramerate = 0;
                File.WriteAllText(path + ".txt", frames + " frames at " + fps + " fps; staged in a test on Map 1 (guards held still except the ones in the take), no audio.");
                // Leaving Play Mode may already have destroyed the camera and its textures.
                if (camera != null) camera.targetTexture = null;
                if (target != null) Object.Destroy(target);
                if (frame != null) Object.Destroy(frame);
                if (root != null) Object.Destroy(root);
            }
        }
    }
}
