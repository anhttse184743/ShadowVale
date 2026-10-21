using System.Collections;
using System.Linq;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class BodyDiscoveryFollowupTests : ForestSceneTestBase
    {
        [Test]
        public void OlderBalanceWithoutWaryScaleKeepsTheOriginalDefault()
        {
            var old = JsonUtility.FromJson<ForestSettings>("{\"detectionSeconds\":1.5}");
            Assert.AreEqual(Map01EnemyController.WaryDetectionScale, old.waryDetectionScale);
        }

        [UnityTest]
        public IEnumerator UnrecordedCharacterCallsRemainReadableAndRecordedCallsStayAudioOnly()
        {
            BodyDiscoveryTests.OpenMap();
            yield return new EnterPlayMode();
            yield return WaitForMap01Ready();
            BodyDiscoveryTests.Settle();
            var m = BodyDiscoveryTests.mission;
            DialogueVoice.StopAll();
            const string unrecorded = "Lính địch: Có người nằm ở đây!";
            m.Say(unrecorded, 4);
            Assert.AreEqual(unrecorded, m.Dialogue, "Without a recording the player's only cue is the full caption.");
            Assert.Greater(m.DialogueUntil, Time.time + 3.9f);
            var voice = m.GetComponent<DialogueVoice>();
            Assert.IsFalse(voice.HasSpeech);

            m.Say(Map01C3Dialogue.NoAnswer, 4);
            Assert.IsTrue(voice.HasSpeech, "The production C3 radio recording must still play.");
            Assert.IsNull(m.Dialogue, "Recorded ambient speech keeps the approved audio-only presentation.");
            m.Say(null);
            Assert.IsFalse(voice.HasSpeech);
            Assert.IsNull(m.Dialogue);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator WaryDetectionUsesConfiguredScaleAndFallsBackForOldBalanceFiles()
        {
            BodyDiscoveryTests.OpenMap();
            yield return new EnterPlayMode();
            yield return WaitForMap01Ready();
            BodyDiscoveryTests.Settle();
            var m = BodyDiscoveryTests.mission;
            var guard = BodyDiscoveryTests.Point(1)[0];
            m.ModernPlayer.enabled = false;
            guard.enabled = true;
            yield return WaitGameSeconds(.1f);
            float calm = guard.DetectionSeconds;
            guard.MakeWary();
            // FromJsonOverwrite exercises the authored field, without a test-only tuning setter.
            JsonUtility.FromJsonOverwrite("{\"waryDetectionScale\":0.4}", m.Settings);
            yield return WaitGameSeconds(.1f);
            Assert.AreEqual(calm * .4f, guard.DetectionSeconds, .0001f);
            foreach (float invalid in new[] { 0f, -1f })
            {
                JsonUtility.FromJsonOverwrite("{\"waryDetectionScale\":" + invalid + "}", m.Settings);
                yield return WaitGameSeconds(.1f);
                Assert.AreEqual(calm * Map01EnemyController.WaryDetectionScale, guard.DetectionSeconds, .0001f);
            }
            guard.enabled = false;
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator C3WaryOrdersDoNotWarpMenOrMoveTheTowerSentry()
        {
            BodyDiscoveryTests.OpenMap();
            yield return new EnterPlayMode();
            yield return WaitForMap01Ready();
            BodyDiscoveryTests.Settle();
            var c3 = BodyDiscoveryTests.c3;
            var men = new[] { "patrolNorth", "rover", "porch" }.Select(c3.Guard).Append(c3.Commander).ToArray();
            var before = men.Select(g => g.Position).ToArray();
            var tower = c3.Guard("tower");
            Vector3 towerBefore = tower.Position;
            c3.GoWary();
            for (int i = 0; i < men.Length; i++)
                Assert.Less(Vector3.Distance(before[i], men[i].Position), .001f, men[i].name + " must walk, never warp at the order.");
            Assert.AreEqual(towerBefore, tower.Position);
            Assert.IsFalse(tower.CanMove);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator AnOrdinaryGuardCallsOnlyNearbySquadMatesToSearchWithoutKnowingNam()
        {
            BodyDiscoveryTests.OpenMap();
            yield return new EnterPlayMode();
            yield return WaitForMap01Ready();
            BodyDiscoveryTests.Settle();
            var m = BodyDiscoveryTests.mission;
            m.ModernPlayer.enabled = false;
            m.player.GetComponent<CharacterController>().enabled = false;
            m.player.position += Vector3.up * 34f;
            var template = BodyDiscoveryTests.Point(1)[0];
            var body = CopyOrdinary(template, "Followup body", "followup", template.Position);
            var finder = CopyOrdinary(template, "Followup finder", "followup", template.Position);
            BodyDiscoveryTests.Kill(body);
            yield return WaitGameSeconds(2f);
            Assert.IsTrue(BodyDiscoveryTests.Overlook(finder, body, 5f));
            var near = CopyOrdinary(template, "Followup nearby mate", "followup", Sample(finder.Position + Vector3.right * 6f));
            var other = CopyOrdinary(template, "Followup unrelated guard", "other", Sample(finder.Position + Vector3.left * 6f));
            var far = CopyOrdinary(template, "Followup distant mate", "followup", Sample(finder.Position + Vector3.back * 22f));
            Assert.Less(Vector3.Distance(finder.Position, near.Position), Map01BodyDiscovery.SquadCallMeters);
            Assert.Greater(Vector3.Distance(finder.Position, far.Position), Map01BodyDiscovery.SquadCallMeters);
            foreach (var g in new[] { near, other, far })
            {
                // They can hear the finder, but cannot independently notice this body first.
                g.Repost(g.Position, Quaternion.LookRotation(Vector3.ProjectOnPlane(g.Position - body.Position, Vector3.up)).eulerAngles.y);
                BodyDiscoveryTests.Hold(g, false);
            }
            Assert.AreEqual(0, BodyDiscoveryTests.story.PointOf(finder));
            Assert.IsFalse(BodyDiscoveryTests.c3.IsGarrison(finder) || BodyDiscoveryTests.rescue.IsRescueGuard(finder));
            BodyDiscoveryTests.Hold(finder, false);
            yield return WaitGameSeconds(1.6f);
            Assert.AreEqual(1, BodyDiscoveryTests.discovery.Found);
            Assert.IsTrue(body.BodyFound && finder.Wary && finder.Alerted);
            Assert.IsTrue(near.Wary && near.Alerted, "A squad mate within 15 m searches with him.");
            Assert.IsFalse(other.Wary || far.Wary, "A different squad and a mate outside call range are not summoned.");
            Assert.IsFalse(new[] { finder, near, other, far }.Any(g => g.Engaged), "A body gives no unseen player position.");
            float until = Time.time + 8f;
            while (BodyDiscoveryTests.Flat(finder.Position, Map01BodyDiscovery.BodySpot(body)) >= 3f && Time.time < until)
                yield return null;
            Assert.Less(BodyDiscoveryTests.Flat(finder.Position, Map01BodyDiscovery.BodySpot(body)), 3f);
            yield return new ExitPlayMode();
        }

        private static Vector3 Sample(Vector3 at)
        {
            Assert.IsTrue(NavMesh.SamplePosition(at, out var hit, 3f, NavMesh.AllAreas), "A real walkable helper position.");
            return hit.position;
        }

        [UnityTest]
        public IEnumerator ANewWalkingPostCancelsThePreviousPatrolPause()
        {
            BodyDiscoveryTests.OpenMap();yield return new EnterPlayMode();yield return WaitForMap01Ready();
            BodyDiscoveryTests.Settle();
            var guard=BodyDiscoveryTests.c3.Guard("porch");
            var snapshot=guard.Capture();snapshot.pauseRemaining=60;
            guard.RestoreSnapshot(snapshot);
            Assert.Greater(guard.Capture().pauseRemaining,59);
            guard.Repost(guard.Position+Vector3.forward,0,walk:true);
            Assert.AreEqual(0,guard.Capture().pauseRemaining,"A new post must supersede the old 45-second porch stop.");
            yield return new ExitPlayMode();
        }

        [UnityTest, Timeout(600000), Explicit("1080p60 functional review film: run by name; this is not a performance benchmark.")]
        public IEnumerator C3CheckInWalkingReview()
        {
            BodyDiscoveryTests.OpenMap();
            yield return new EnterPlayMode();
            yield return WaitForMap01Ready();
            yield return RecordCheckInWalking();
            yield return new ExitPlayMode();
        }

        // Create closures only after EnterPlayMode's domain reload.
        private static IEnumerator RecordCheckInWalking()
        {
            BodyDiscoveryTests.Settle();
            var m = BodyDiscoveryTests.mission;
            var c3 = BodyDiscoveryTests.c3;
            var roles = new[] { "patrolNorth", "rover", "porch" };
            var men = roles.Select(c3.Guard).Append(c3.Commander).ToArray();
            var targets = roles.Select(role => c3.Data.waryPosts.Single(p => p.role == role))
                .Select(p => c3.At(p.post[0], p.post[1]))
                .Append(c3.At(c3.Data.commander.points[2], c3.Data.commander.points[3])).ToArray();
            var before = men.Select(g => g.Position).ToArray();
            var last = before.ToArray();
            var walked = new float[men.Length];
            var largestStep = new float[men.Length];
            var verticalStep = new float[men.Length];
            var faults = new System.Collections.Generic.List<string>();
            var tower = c3.Guard("tower");
            Vector3 towerBefore = tower.Position;
            m.player.GetComponent<CharacterController>().enabled = false;
            m.ModernPlayer.enabled = false;
            m.player.position = men[0].Position + Vector3.up * 34f;
            foreach (var g in men)
            {
                BodyDiscoveryTests.Hold(g, false);
                Assert.IsTrue(g.CanMove, g.name + " must be on the existing NavMesh.");
            }
            var missing = c3.Guard("patrolSouth");
            BodyDiscoveryTests.Kill(missing);
            missing.MarkBodyFound(); // Isolate missed check-in from the separately tested body alarm.
            c3.Current.checkInRemaining = .2f;
            Directory.CreateDirectory("Logs/BodyDiscovery");
            using (var film = new BodyDiscoveryReview.Film("c3-check-in-walk", height: 5f, side: 10f,
                       width: 1920, heightPixels: 1080, framesPerSecond: 60))
            {
                float began = Time.time;
                float until = Time.time + 45f;
                while (Time.time < until)
                {
                    for (int i = 0; i < men.Length; i++)
                    {
                        float step = BodyDiscoveryTests.Flat(last[i], men[i].Position);
                        float rise = Mathf.Abs(last[i].y-men[i].Position.y);
                        var agent=men[i].GetComponent<NavMeshAgent>();
                        walked[i] += step;
                        largestStep[i] = Mathf.Max(largestStep[i], step);
                        verticalStep[i]=Mathf.Max(verticalStep[i],rise);
                        float bound=agent.speed*Mathf.Max(Time.deltaTime,.02f)*2f+.05f;
                        if(step>=bound||rise>=agent.height*.5f)
                            faults.Add($"{men[i].name}: horizontal={step:F4}, vertical={rise:F4}, speed={agent.speed:F3}, dt={Time.deltaTime:F4}, from={last[i]}, to={men[i].Position}");
                        last[i] = men[i].Position;
                    }
                    Assert.Less(Vector3.Distance(towerBefore, tower.Position), .001f);
                    var north = men[0];
                    film.Aim(north.Position + Vector3.up, Vector3.forward);
                    film.Shoot($"C3 · điểm danh thiếu người · t={Time.time - began:0.0}s\nTuần tra đi về cổng · không dịch chuyển tức thời");
                    bool arrived = men.Select((g, i) => BodyDiscoveryTests.Flat(g.Position, targets[i]) < 1.5f).All(v => v);
                    if (c3.Wary && arrived && Time.time - began > 4f) break;
                    yield return null;
                }
            }
            var metrics = new System.Text.StringBuilder();
            for (int i = 0; i < men.Length; i++)
            {
                float remaining = BodyDiscoveryTests.Flat(men[i].Position, targets[i]);
                metrics.AppendLine($"{men[i].name}: path={walked[i]:F4}m; maxHorizontalStep={largestStep[i]:F4}m; maxVerticalStep={verticalStep[i]:F4}m; remaining={remaining:F4}m");
            }
            File.WriteAllText("Logs/BodyDiscovery/c3-check-in-walk-metrics.txt", metrics.ToString());
            Assert.IsEmpty(faults,string.Join("\n",faults.Take(8)));
            for(int i=0;i<men.Length;i++)
                Assert.Less(BodyDiscoveryTests.Flat(men[i].Position,targets[i]),1.5f,men[i].name+" must reach the wary post without getting stuck.");
            Assert.IsTrue(c3.Wary);
            Assert.IsFalse(c3.Alarmed || c3.Current.kiengRung);
        }

        private static Map01EnemyController CopyOrdinary(Map01EnemyController template, string name, string squad, Vector3 at)
        {
            var instance = Object.Instantiate(template.gameObject);
            instance.name = name;
            var guard = instance.GetComponent<Map01EnemyController>();
            guard.operationIndex = 0;
            typeof(Map01EnemyController).GetProperty(nameof(Map01EnemyController.SquadId)).SetValue(guard, squad);
            guard.BindMission(BodyDiscoveryTests.mission);
            guard.Repost(at, 0);
            BodyDiscoveryTests.mission.AddEnemy(guard);
            BodyDiscoveryTests.Hold(guard, true);
            return guard;
        }
    }
}
