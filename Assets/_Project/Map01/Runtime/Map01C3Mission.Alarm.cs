using ShadowVale.AI.Perception;
using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Boss;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// How the post raises the alarm: a body found, the half-health call, reinforcements, the sleepers
    /// waking, the runners, noises and the leash.
    /// </summary>
    public sealed partial class Map01C3Mission
    {
        // ---- a body found (Map01BodyDiscovery) -----------------------------------------------------------------------

        public bool IsGarrison(Map01EnemyController guard) => guard != null && Garrison().Contains(guard);
        private Map01EnemyController bodyRunner;
        private Vector3 KiengSpot => kiengPoint != null ? kiengPoint.transform.position : At(Data.kieng.use[0], Data.kieng.use[1]);
        /// <summary>The guard on his way to strike the kẻng over a body, or null.</summary>
        public Map01EnemyController BodyRunnerGuard =>
            string.IsNullOrEmpty(Current.bodyRunner) ? null
            : bodyRunner != null && bodyRunner.SaveId == Current.bodyRunner ? bodyRunner
            : bodyRunner = Garrison().FirstOrDefault(g => g != null && g.SaveId == Current.bodyRunner);

        /// <summary>
        /// A man of the post found a comrade's body: he (or, from the tower, the nearest of the gate, courtyard and porch
        /// guards) runs to strike the kẻng — Nam can stop him on the way. With the striker gone there is nothing to strike:
        /// he shouts and the camp stands on guard. A kẻng already struck: he searches.
        /// </summary>
        public void OnBodyFound(Map01EnemyController finder, Vector3 at)
        {
            if (!ready) return;
            if (Current.kiengRung || !string.IsNullOrEmpty(Current.bodyRunner))
            {
                Map01BodyDiscovery.Search(mission, finder, at, Array.Empty<Map01EnemyController>(), "Địch phát hiện thêm một xác — chúng lục soát quanh đó.");
                return;
            }
            if (Current.kiengRemoved)
            {
                Map01BodyDiscovery.Search(mission, finder, at, Array.Empty<Map01EnemyController>(), "Địch phát hiện xác, nhưng kẻng đã mất dùi — cả trại chuyển sang cảnh giác.");
                GoWaryOverBody();
                return;
            }
            var runner = finder.CanMove ? finder : new[] { "gate", "rover", "porch" }.Select(Guard)
                .Where(g => g != null && g.Alive && !g.Engaged && g.CanMove)
                .OrderBy(g => Vector3.Distance(g.Position, KiengSpot)).FirstOrDefault();
            if (runner == null)
            {
                Map01BodyDiscovery.Search(mission, finder, at, Array.Empty<Map01EnemyController>(), "Địch phát hiện xác — cả trại chuyển sang cảnh giác.");
                GoWaryOverBody();
                return;
            }
            if (runner != finder) finder.InspectBody(Map01BodyDiscovery.Beside(at, finder.Position));
            bodyRunner = runner; Current.bodyRunner = runner.SaveId; Current.bodyAt = new[] { at.x, at.y, at.z };
            runner.Dispatch(KiengSpot);
            Map01BodyDiscovery.Shout(mission, finder, "Địch phát hiện xác — một tên đang chạy đi gõ kẻng!");
        }

        /// <summary>The kẻng runner on his way: struck when he gets there; nothing if Nam dropped him first.</summary>
        private void BodyRunner()
        {
            if (string.IsNullOrEmpty(Current.bodyRunner)) return;
            var runner = BodyRunnerGuard;
            if (runner == null || !runner.Alive || runner.Engaged || Current.kiengRung) { Current.bodyRunner = ""; bodyRunner = null; return; }
            var body = Current.bodyAt != null && Current.bodyAt.Length >= 3 ? new Vector3(Current.bodyAt[0], Current.bodyAt[1], Current.bodyAt[2]) : runner.Position;
            if (Current.kiengRemoved)
            {
                // Nam took the striker while he ran.
                Current.bodyRunner = ""; bodyRunner = null;
                Map01BodyDiscovery.Shout(mission, runner, "Tên lính chạy tới kẻng nhưng dùi đã mất — cả trại chuyển sang cảnh giác.");
                GoWaryOverBody();
                return;
            }
            if (Vector3.ProjectOnPlane(runner.Position - KiengSpot, Vector3.up).magnitude <= 2.2f)
            {
                Current.bodyRunner = ""; bodyRunner = null;
                GoWaryOverBody();
                RingKieng(false, body);
                return;
            }
            if (!runner.Alerted) runner.Dispatch(KiengSpot); // the hurry ran out on a long way round
        }

        private void HalfHealthCall()
        {
            var boss = Commander;
            if (!AutoCallAtHalfHealth || Current.called || boss == null || !boss.Alive) return;
            var health = boss.GetComponent<Health>();
            if (health != null && health.Current <= health.Max * .5f) CallReinforcements();
        }

        /// <summary>
        /// The commander calls his men (once): the kẻng is struck if it still can be, and every sleeper comes out; if Nam
        /// took the striker, only the nearest sleeper hears the shout, a few seconds later. With the telephone line intact,
        /// runners from the jetty posts are on their way. Returns how many men answer now or soon.
        /// </summary>
        public int CallReinforcements()
        {
            if (Current.called) return 0;
            Current.called = true;
            var boss = Commander;
            if (boss != null && boss.Alive) mission.Say(Map01C3Dialogue.EnemyCall, 3);
            int answering = 0;
            if (!Current.kiengRemoved)
            {
                answering += sleepers.Count(s => s.Alive && !Current.sleepersAwake[sleepers.IndexOf(s)]);
                if (!Current.kiengRung) RingKieng();
            }
            else if (sleepers.Any(s => s.Alive && !Current.sleepersAwake[sleepers.IndexOf(s)])) { Current.sleeperIn = Data.sleeperDelayWithoutKieng; answering += Mathf.Min(Data.sleepersWithoutKieng, sleepers.Count(s => s.Alive && !Current.sleepersAwake[sleepers.IndexOf(s)])); }
            if (!Current.phoneCut && !Current.runnersSent) { Current.runnersIn = Data.runners.delaySeconds; answering += runners.Count; }
            return answering;
        }

        private void Countdowns()
        {
            if (Current.sleeperIn >= 0 && (Current.sleeperIn -= Time.deltaTime) < 0)
            {
                // The one nearest the commander hears him.
                var boss = Commander;
                int best = -1; float bestDistance = float.MaxValue;
                for (int i = 0; i < sleepers.Count; i++)
                {
                    if (Current.sleepersAwake[i] || !sleepers[i].Alive) continue;
                    float d = boss != null ? Vector3.Distance(boss.transform.position, sleepers[i].transform.position) : i;
                    if (d < bestDistance) { bestDistance = d; best = i; }
                }
                for (int n = 0; n < Data.sleepersWithoutKieng && best >= 0; n++) { Wake(best, true); best = Enumerable.Range(0,sleepers.Count).Where(i=>!Current.sleepersAwake[i]&&sleepers[i].Alive).DefaultIfEmpty(-1).First(); }
            }
            if (Current.runnersIn >= 0 && (Current.runnersIn -= Time.deltaTime) < 0) SendRunners();
        }

        public bool TryKnownThreat(out Vector3 at)
        {
            // Prefer a contact over a heard shot. Both are recorded positions, not a player lookup.
            foreach (var g in Garrison()) if (g != null && g.Engaged && g.TryGetKnownThreat(out at)) return true;
            foreach (var g in Garrison()) if (g != null && g.TryGetKnownThreat(out at)) return true;
            at = default; return false;
        }
        private void Wake(int index, bool fight, bool immediate = false, Vector3? threat = null)
        {
            if (index < 0 || index >= sleepers.Count || Current.sleepersAwake[index] || !sleepers[index].Alive) return;
            Current.sleepersAwake[index] = true;
            if (fight && !threat.HasValue && TryKnownThreat(out var known)) threat = known;
            var guard = sleepers[index];
            var bed = guard.GetComponent<Map01C3Sleeper>();
            if (bed != null) { bed.Wake(fight, immediate, threat); return; }
            var door = Data.sleepers[index].door;
            guard.gameObject.SetActive(true);
            guard.Repost(At(door[0], door[1]), 180f);
            mission.AddEnemy(guard);
            if (fight) { if (threat.HasValue) guard.Alert(threat.Value); else guard.InspectBody(guard.transform.position); }
        }

        public void RecordSleeperDeath(int index, bool execution)
        {
            if (index < 0 || index >= sleepers.Count) return;
            Current.sleepersKilled[index] = true; Current.sleepersExecuted[index] = execution;
            mission.AddEnemy(sleepers[index]);
        }
        public void WakeFromBedShot(Vector3 at, float radius)
        {
            for(int i=0;i<sleepers.Count;i++)
                if(Vector3.Distance(sleepers[i].GetComponent<Map01C3Sleeper>()?.BodyPoint ?? sleepers[i].transform.position,at)<=radius) Wake(i,true);
        }
        private void SendRunners()
        {
            if (Current.runnersSent) return;
            Current.runnersSent = true;
            foreach (var r in runners)
            {
                r.gameObject.SetActive(true);
                r.Repost(r.transform.position, 90f);
                mission.AddEnemy(r);
                if (TryKnownThreat(out var known)) r.Alert(known); else r.InspectBody(KiengSpot);
            }
            if (Near) mission.Say("Có thêm lính chạy tới từ đường mòn phía Bắc — dây điện thoại vẫn còn.", 4);
        }

        private void OnNoise(Vector3 position, float radius, NoiseKind kind)
        {
            if (!ready || Map01SaveSystem.IsRestoring || kind != NoiseKind.Gunshot && radius < 25f) return;
            for (int i = 0; i < sleepers.Count; i++)
            {
                if (Current.sleepersAwake[i] || !sleepers[i].Alive) continue;
                var inside = Data.sleepers[i].inside;
                if (Vector3.Distance(position, At(inside[0], inside[1])) <= Data.sleeperHearMeters)
                    Wake(i, true, threat: kind == NoiseKind.Gunshot ? (Vector3?)position : null);
            }
        }

        /// <summary>The story plan: no pursuit deep into the forest. Past the leash the garrison walks back to its posts.</summary>
        private void Leash()
        {
            if (mission.player == null || OutsideFence(mission.player.position) <= Data.leashMeters) return;
            foreach (var g in Garrison()) if (g != null && g.Alive && g.Engaged && !g.IsBoss) g.StandDown();
        }
    }
}
