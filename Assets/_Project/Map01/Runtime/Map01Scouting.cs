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
        public Vector3 Eye => mission.player.position + Vector3.up * (mission.Crouched ? crouchedEyeHeight : eyeHeight);

        private readonly List<Camp> camps = new List<Camp>();
        private readonly Dictionary<Map01EnemyController, float> lastHealth = new Dictionary<Map01EnemyController, float>();
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
                    Guards = outpostGuards.Where(g => NearestCamp(g.transform.position) == root.transform).ToArray()
                });
            }
            TrackHealth();
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

        /// <summary>After a checkpoint load, which also restores guard health — take that as the
        /// new baseline, or a restored wound would read as Nam attacking the camp.</summary>
        public void RestoreFound(int mask)
        {
            foreach (var camp in camps) camp.Found = (mask & (1 << (camp.Number - 1))) != 0;
            TrackHealth();
        }

        private void Update()
        {
            if (!mission.IsInitialized) return;
            bool scouting = quest.Stage == Map01Quest.ScoutStage && !mission.Stopped;
            SetBinoculars(held && scouting && !mission.InventoryOpen && !mission.MapOpen && !ForestMenu.Visible);
            if (cameraRig != null) cameraRig.SetBinoculars(Binoculars, Eye.y - mission.player.position.y);
            if (!scouting) { TrackHealth(); InScanRange = false; return; }
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

        private void TrackHealth()
        {
            foreach (var camp in camps)
                foreach (var guard in camp.Guards) lastHealth[guard] = guard.GetComponent<Health>().Current;
        }

        private bool CampSpottedOrAttacked(out string reason)
        {
            reason = null;
            foreach (var camp in camps)
                foreach (var guard in camp.Guards)
                {
                    float health = guard.GetComponent<Health>().Current;
                    bool hurt = health < lastHealth[guard];
                    lastHealth[guard] = health;
                    if (hurt) { reason = "Không được tấn công lính doanh trại!"; return true; }
                    if (guard.Alive && guard.Engaged) { reason = "Lính doanh trại " + camp.Number + " đã phát hiện cậu!"; return true; }
                }
            return false;
        }

        private void Fail(string reason)
        {
            foreach (var camp in camps)
            {
                camp.Found = false;
                foreach (var guard in camp.Guards) guard.ReturnToPost();
            }
            TrackHealth();
            Sighted = null; RecordProgress = 0;
            FailedAt = Time.time;
            mission.Say("Hùng (bộ đàm): " + reason + " Rút ngay! Địch đã tăng cường canh gác — trinh sát lại cả ba doanh trại, lần này đừng để bị phát hiện.", 10);
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
