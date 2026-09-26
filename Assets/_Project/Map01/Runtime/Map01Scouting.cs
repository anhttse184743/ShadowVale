using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The reconnaissance order: find the three enemy outposts and log each through binoculars
    /// (hold F while looking at it from within range, with a clear line of sight) without being
    /// noticed. A camp guard spotting Nam, or Nam attacking one, fails the run: the camps are
    /// reinforced (every camp guard back at his post, calm) and all logged positions are lost.
    /// The one attack a camp does not notice is a silent knife takedown of a guard lured well
    /// away from it (quietKillDistance), e.g. by a thrown stone.
    /// Until a camp is logged, only a rough area around it is known — offset from the real spot.
    /// </summary>
    public sealed class Map01Scouting : MonoBehaviour
    {
        [SerializeField] private float binocularRange = 45f;
        [Tooltip("How far off the screen centre (degrees) the camp may be while logging it.")]
        [SerializeField] private float binocularAim = 15f;
        [SerializeField] private float recordSeconds = 2f;
        [Tooltip("Radius of the rough area shown for a camp not yet logged.")]
        [SerializeField] private float zoneRadius = 35f;
        [Tooltip("Eye height above Nam's feet, standing and crouched — the binocular view and its line of sight.")]
        [SerializeField] private float eyeHeight = 1.6f, crouchedEyeHeight = 1.15f;
        [Tooltip("A silent knife takedown at least this far from a camp's centre goes unnoticed by the camp.")]
        [SerializeField] private float quietKillDistance = 15f;
        public float QuietKillDistance => quietKillDistance;

        public sealed class Camp
        {
            public int Number;
            public Vector3 Center, ZoneCenter;
            public Map01EnemyController[] Guards;
            public bool Found;
        }

        public IReadOnlyList<Camp> Camps => camps;
        public float ZoneRadius => zoneRadius;
        public int FoundCount => camps.Count(c => c.Found);
        public int FoundMask => camps.Where(c => c.Found).Sum(c => 1 << (c.Number - 1));
        /// <summary>F held during the scouting order, with no panel open.</summary>
        public bool Binoculars { get; private set; }
        /// <summary>The camp in the binoculars right now, if it can be logged from here.</summary>
        public Camp Sighted { get; private set; }
        public float RecordProgress { get; private set; }
        /// <summary>How close a camp has to be for the binoculars to log it.</summary>
        public float ScanRange => binocularRange;
        /// <summary>Some camp not yet logged is within <see cref="ScanRange"/>. Out of range the
        /// binoculars log nothing, wherever they point.</summary>
        public bool InScanRange { get; private set; }
        public float FailedAt { get; private set; } = float.NegativeInfinity;
        /// <summary>The run was lost; the failure panel is up until <see cref="Restart"/>.</summary>
        public bool FailedRun { get; private set; }
        /// <summary>Why the run was lost, for the failure panel.</summary>
        public string FailReason { get; private set; }
        public Vector3 Eye => mission.player.position + Vector3.up * (mission.Crouched ? crouchedEyeHeight : eyeHeight);

        private readonly List<Camp> camps = new List<Camp>();
        private readonly Dictionary<Map01EnemyController, int> lastHurt = new Dictionary<Map01EnemyController, int>();
        private Map01Mission mission;
        private Map01Quest quest;
        private ThirdPersonCamera cameraRig;
        private bool held;

        private void Awake()
        {
            mission = GetComponent<Map01Mission>();
            quest = GetComponent<Map01Quest>();
        }

        private void Start()
        {
            if (!mission.IsInitialized) return;
            cameraRig = mission.gameCamera.GetComponent<ThirdPersonCamera>();
            // From the scene, not mission.Enemies: that list is filled in Map01Mission.Start, which
            // may run after this one.
            var outpostGuards = FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None)
                .Where(e => e.name.StartsWith("Outpost guard ")).ToArray();
            for (int number = 1; number <= 3; number++)
            {
                var root = GameObject.Find("Enemy outpost " + number);
                if (root == null) continue;
                Vector3 center = root.transform.position;
                // A fixed, per-camp offset: the rough area never gives away the exact spot.
                var offset = Quaternion.Euler(0, 40 + 120 * number, 0) * Vector3.forward * (zoneRadius * .5f);
                camps.Add(new Camp {
                    Number = number, Center = center, ZoneCenter = center + offset,
                    // By name: FindObjectsByType has no order, and a camp's guards should list the
                    // same way every time the map loads.
                    Guards = outpostGuards.Where(g => NearestCamp(g.transform.position) == root.transform)
                        .OrderBy(g => g.name, System.StringComparer.Ordinal).ToArray()
                });
            }
            TrackAttacks();
        }

        private static Transform NearestCamp(Vector3 position)
        {
            Transform best = null; float bestDistance = float.MaxValue;
            for (int number = 1; number <= 3; number++)
            {
                var root = GameObject.Find("Enemy outpost " + number);
                if (root == null) continue;
                float d = (root.transform.position - position).sqrMagnitude;
                if (d < bestDistance) { bestDistance = d; best = root.transform; }
            }
            return best;
        }

        /// <summary>Called every frame by Map01PlayerInteraction with the state of the F key.</summary>
        public void HoldBinoculars(bool pressed) => held = pressed;

        /// <summary>After a checkpoint load: the guards' state as restored is the new baseline, so
        /// nothing that happened before the save reads as a fresh attack on the camp.</summary>
        public void RestoreFound(int mask)
        {
            foreach (var camp in camps) camp.Found = (mask & (1 << (camp.Number - 1))) != 0;
            TrackAttacks();
        }

        private void Update()
        {
            if (!mission.IsInitialized) return;
            bool scouting = quest.Stage == Map01Quest.ScoutStage && !mission.Stopped;
            SetBinoculars(held && scouting && !mission.InventoryOpen && !mission.MapOpen && !ForestMenu.Visible);
            if (cameraRig != null) cameraRig.SetBinoculars(Binoculars, Eye.y - mission.player.position.y);
            if (!scouting) { TrackAttacks(); InScanRange = false; return; }
            if (CampSpottedOrAttacked(out string reason)) { Fail(reason); return; }
            InScanRange = false;
            foreach (var camp in camps)
                if (!camp.Found && Vector3.Distance(mission.player.position, camp.Center) <= binocularRange) InScanRange = true;
            RecordSighting();
        }

        private void SetBinoculars(bool value)
        {
            if (Binoculars == value) return;
            Binoculars = value;
            if (!value) { Sighted = null; RecordProgress = 0; }
        }

        private void TrackAttacks()
        {
            foreach (var camp in camps)
                foreach (var guard in camp.Guards) lastHurt[guard] = guard.HurtCount;
        }

        private bool CampSpottedOrAttacked(out string reason)
        {
            reason = null;
            foreach (var camp in camps)
                foreach (var guard in camp.Guards)
                {
                    if (guard.HurtCount != lastHurt[guard])
                    {
                        lastHurt[guard] = guard.HurtCount;
                        // A silent knife takedown of a guard drawn well away from his camp — by a
                        // thrown stone, say — goes unnoticed. Anything else is an attack on the camp.
                        if (!guard.TakenDownSilently) { reason = "Nam đã tấn công lính doanh trại — cả trại báo động."; return true; }
                        if (Vector3.Distance(guard.transform.position, camp.Center) < quietKillDistance)
                        { reason = $"Nam hạ lính ngay trong doanh trại {camp.Number} — đồng đội hắn đã phát hiện."; return true; }
                        mission.Say("Hạ gục lặng lẽ, xa doanh trại — không ai hay biết.", 4);
                    }
                    if (guard.Alive && guard.Engaged) { reason = $"Lính doanh trại {camp.Number} đã nhìn thấy Nam."; return true; }
                }
            return false;
        }

        /// <summary>Spotted, or caught attacking a camp: the run is over. Everything waits on the
        /// failure panel (Map01Mission.Stopped) until [Enter] starts it over — <see cref="Restart"/>.</summary>
        private void Fail(string reason)
        {
            FailedRun = true; FailReason = reason;
            FailedAt = Time.time;
            SetBinoculars(false);
            mission.Say(null, -1f);
        }

        /// <summary>
        /// [Enter] on the failure panel: back to the moment Hùng gave the order — Map 1 reloads
        /// there. Never a reset in place: that snapped the camp guards back to their posts (and
        /// Nam to Hùng) right in front of the player. Without a recorded moment (a save from
        /// before it was kept) one is made as if the order had just been given.
        /// </summary>
        public void Restart()
        {
            if (!FailedRun) return;
            var save = GetComponent<Map01SaveSystem>();
            if (!save.HasScoutStart) save.MarkFreshScoutingStart(camps.SelectMany(c => c.Guards));
            save.RestartScouting(Map01Quest.ScoutOrder);
        }

        private void RecordSighting()
        {
            var sighted = Binoculars ? SightedCamp() : null;
            if (sighted == null)
            {
                // A shaky hand or a leaf crossing the view should not throw away the log so far:
                // it fades rather than resets, and only a different camp starts over.
                RecordProgress = Mathf.Max(0, RecordProgress - Time.deltaTime / recordSeconds);
                if (RecordProgress <= 0) Sighted = null;
                return;
            }
            if (sighted != Sighted) { Sighted = sighted; RecordProgress = 0; }
            RecordProgress += Time.deltaTime / recordSeconds;
            if (RecordProgress < 1f) return;
            Sighted.Found = true;
            Sighted = null; RecordProgress = 0;
            if (FoundCount < camps.Count) mission.Say($"Đã ghi chép vị trí doanh trại ({FoundCount}/{camps.Count}). Tiếp tục tìm, giữ khoảng cách.", 6);
            else quest.CompleteScouting();
        }

        /// <summary>
        /// The unlogged camp closest to the screen centre that is within binocular range and
        /// actually visible from Nam's eyes — its centre or any of its guards, trees and huts in
        /// the way block the view like they would for the guards.
        /// </summary>
        private Camp SightedCamp()
        {
            var view = mission.gameCamera.transform;
            Vector3 eye = Eye;
            Camp best = null; float bestAngle = binocularAim;
            foreach (var camp in camps)
            {
                if (camp.Found || Vector3.Distance(mission.player.position, camp.Center) > binocularRange) continue;
                var spots = camp.Guards.Where(g => g.Alive).Select(g => g.transform.position + Vector3.up * 1.2f)
                    .Prepend(camp.Center + Vector3.up * 1.5f);
                foreach (var spot in spots)
                {
                    float angle = Vector3.Angle(view.forward, spot - view.position);
                    if (angle >= bestAngle) continue;
                    if (Physics.Linecast(eye, spot, out var hit, mission.ObstructionMask, QueryTriggerInteraction.Ignore)
                        && Vector3.Distance(hit.point, spot) > 1.5f) continue;
                    best = camp; bestAngle = angle;
                }
            }
            return best;
        }
    }
}
