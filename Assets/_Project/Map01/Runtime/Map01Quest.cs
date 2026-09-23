using System.Linq;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Map 1's quest line, on its own component. Rescue Hùng and bring him home to the base; from
    /// then on he stays there and hands out every order, and each finished task must be reported
    /// back to him in person before the next one: log the three enemy outposts through binoculars
    /// unseen (Map01Scouting), destroy them ("Outpost guard N", baked into the scene), then the
    /// commander who shows up once they fall. Reads Map01Mission for shared state and
    /// Map01Inventory for the rescue's herb.
    /// </summary>
    public sealed class Map01Quest : MonoBehaviour
    {
        // Stage numbers are the checkpoint format (v5) — append, never reorder. v4 saves predate
        // the report steps and go through FromV4Stage.
        public const int RescueStage = 0;
        public const int EscortStage = 1;
        public const int BriefingStage = 2;
        public const int ScoutStage = 3;
        public const int ReportScoutStage = 4;
        public const int CampsStage = 5;
        public const int ReportCampsStage = 6;
        public const int BossStage = 7;
        public const int ReportBossStage = 8;
        public const int CompleteStage = 9;
        public static readonly string[] Objectives = {
            "Tìm Hùng đang bị thương và dùng thảo dược chữa trị cho anh ấy [E]",
            "Đưa Hùng về căn cứ, nhận hàng tiếp tế [E]",
            "Gặp Hùng tại căn cứ để nhận nhiệm vụ [E]",
            "Trinh sát 3 doanh trại địch: giữ [F] dùng ống nhòm ghi vị trí — không để lính phát hiện, không tấn công",
            "Về căn cứ báo cáo kết quả trinh sát cho Hùng [E]",
            "Chiếm đóng 3 doanh trại của địch — tiêu diệt toàn bộ lính",
            "Về căn cứ báo cáo với Hùng: ba doanh trại đã bị hạ [E]",
            "Tiêu diệt chỉ huy địch",
            "Về căn cứ báo cáo chiến thắng với Hùng [E]",
            "Hoàn thành Map 1 — Những dấu chân trong rừng"
        };
        public static readonly string[] SaveLocations = {
            "Đang tìm Hùng", "Trên đường về căn cứ", "Căn cứ chỉ huy", "Trinh sát doanh trại", "Về căn cứ báo cáo",
            "Doanh trại địch", "Về căn cứ báo cáo", "Đối đầu chỉ huy", "Về căn cứ báo cáo", "Map 1 hoàn tất"
        };
        /// <summary>v4 stages: rescue, escort, scout, camps, boss, complete.</summary>
        public static int FromV4Stage(int stage) =>
            stage <= 1 ? Mathf.Max(0, stage) : stage == 2 ? ScoutStage : stage == 3 ? CampsStage : stage == 4 ? BossStage : CompleteStage;

        public int Stage { get; private set; }
        /// <summary>The objective line for the HUD, with the scouting tally while it runs.</summary>
        public string ObjectiveText => Stage == ScoutStage
            ? $"{Objectives[Stage]} ({scouting.FoundCount}/{scouting.Camps.Count})"
            : Objectives[Mathf.Clamp(Stage, 0, CompleteStage)];
        public bool AwaitingReport => Stage == BriefingStage || Stage == ReportScoutStage || Stage == ReportCampsStage || Stage == ReportBossStage;
        /// <summary>True while Nam is close enough to Hùng for [E] to mean something: treating him
        /// at the start, or reporting in afterwards.</summary>
        public bool HungInRange { get; private set; }
        /// <summary>What [E] does next to Hùng right now, for the HUD prompt.</summary>
        public string HungPrompt => Stage == RescueStage
            ? (inventory.Count("herb") > 0 ? "[E] Dùng thảo dược chữa trị cho Hùng" : "Cần thảo dược để chữa trị cho Hùng")
            : "[E] Báo cáo với Hùng";
        /// <summary>The base's supply point keeps resupplying once Hùng is home.</summary>
        public bool BaseResupplyOpen => Stage > EscortStage;
        /// <summary>Hùng walks with Nam only while being brought home; before that he is wounded,
        /// after it he runs the base.</summary>
        public bool ShouldFollowPlayer() => Stage == EscortStage;

        /// <summary>Direct-set for restoring a checkpoint; never call this mid-play otherwise.</summary>
        public void RestoreStage(int value)
        {
            Stage = Mathf.Clamp(value, 0, CompleteStage);
            // The commander is spawned at runtime, not baked into the scene — a checkpoint taken
            // mid-fight (the exit autosave allows that) must bring him back, or the objective can
            // never complete. The save system restores his health/position right after this.
            if (Stage == BossStage && boss == null) SpawnBoss();
        }

        private Map01Mission mission;
        private Map01Inventory inventory;
        private Map01Scouting scouting;
        private NavMeshAgent companion;
        private Map01EnemyController boss;
        public bool BossSpawned => boss != null;

        private void Awake()
        {
            mission = GetComponent<Map01Mission>();
            inventory = GetComponent<Map01Inventory>();
            // The scouting order's own component comes along at runtime rather than being baked
            // into Map 1.unity. Deliberately not "GetComponent() ?? AddComponent()" — see WeaponHotbar.
            scouting = GetComponent<Map01Scouting>();
            if (scouting == null) scouting = gameObject.AddComponent<Map01Scouting>();
        }

        private void Start()
        {
            if (!mission.IsInitialized) return;
            companion = mission.hung.GetComponent<NavMeshAgent>();
            PositionHungForRescue();
            mission.Say("Nam: Hùng đâu rồi? ... Kia! Bị thương rồi. Phải tìm thảo dược cứu anh ấy.", 9);
        }

        /// <summary>
        /// Moves Hùng away from the player's spawn point to a nearby, NavMesh-valid spot so the
        /// briefing's "go rescue Hùng" reads as a real short trek rather than him already standing
        /// next to Nam. Falls back to wherever the map data placed him if none of the offsets land
        /// on walkable ground — better an unmoved companion than one warped into a wall.
        /// </summary>
        private void PositionHungForRescue()
        {
            if (companion == null) return;
            Vector3[] offsets = {
                new Vector3(14, 0, 10), new Vector3(-14, 0, 10),
                new Vector3(10, 0, -14), new Vector3(-10, 0, -14),
                new Vector3(18, 0, 0), new Vector3(-18, 0, 0),
            };
            foreach (var offset in offsets)
                if (NavMesh.SamplePosition(mission.player.position + offset, out var hit, 6, NavMesh.AllAreas))
                { companion.Warp(hit.position); return; }
        }

        private bool NearHung(float distance) => mission.hung != null
            && Vector3.Distance(mission.player.position, mission.hung.position) <= distance;

        /// <summary>[E] next to Hùng: treat him at the start, otherwise report in for the next order.</summary>
        public void TalkToHung()
        {
            if (Stage == RescueStage) { TryRescueHung(); return; }
            if (!AwaitingReport || !NearHung(mission.Settings.interactRange)) return;
            switch (Stage)
            {
                case BriefingStage:
                    Stage = ScoutStage;
                    mission.Say("Hùng: Địch có ba doanh trại quanh đây, anh chỉ biết đại khái khu vực. Lén tới, giữ [F] dùng ống nhòm ghi lại vị trí cả ba. Tuyệt đối không để chúng phát hiện, không nổ súng. Xong thì về báo anh.", 12);
                    break;
                case ReportScoutStage:
                    Stage = CampsStage;
                    mission.Say("Hùng: Ba doanh trại, đúng vị trí cậu ghi. Giờ thì khác: tiêu diệt cả ba, hạ toàn bộ lính gác. Xong thì về đây.", 10);
                    break;
                case ReportCampsStage:
                    Stage = BossStage;
                    SpawnBoss();
                    mission.Say("Hùng: Làm tốt lắm, Nam. Nhưng chỉ huy của chúng vừa lộ diện ở doanh trại — hạ hắn đi.", 9);
                    break;
                case ReportBossStage:
                    Stage = CompleteStage;
                    mission.Say("Hùng: Chỉ huy địch đã gục, khu rừng này an toàn rồi. Map 1 hoàn tất, Nam!", 10);
                    break;
            }
        }

        /// <summary>
        /// [E] near wounded Hùng at the start. Needs one herb — the briefing's "kiếm thảo dược chữa
        /// trị cho Hùng" — and starts the walk home.
        /// </summary>
        public void TryRescueHung()
        {
            if (Stage != RescueStage || !NearHung(mission.Settings.interactRange)) return;
            if (inventory.Count("herb") <= 0) { mission.Say("Cần thảo dược để chữa trị cho Hùng.", 3); return; }
            inventory.Spend("herb", 1); Stage = EscortStage;
            mission.Say("Nam: Chịu khó chút, Hùng. Thảo dược này cầm máu được.\nHùng: ...Cảm ơn Nam. Về căn cứ lấy hàng tiếp tế rồi tính tiếp.", 9);
        }

        /// <summary>[E] on the base's supply point with Hùng alongside — he is home, and stays.</summary>
        public bool TryDeliverSupplies()
        {
            if (Stage != EscortStage) return false;
            if (!NearHung(10f)) { mission.Say("Hùng chưa theo kịp — đợi anh ấy về tới căn cứ đã.", 4); return false; }
            Stage = BriefingStage;
            if (companion != null && companion.isOnNavMesh) companion.ResetPath();
            mission.Say("Hùng: Về tới căn cứ rồi. Cảm ơn Nam — nhận hàng tiếp tế đi, rồi gặp anh nhận nhiệm vụ. [E]", 9);
            return true;
        }

        /// <summary>Map01Scouting calls this once all three camps are logged; Hùng wants the report in person.</summary>
        public void CompleteScouting()
        {
            if (Stage != ScoutStage) return;
            Stage = ReportScoutStage;
            mission.Say("Đã ghi chép vị trí cả 3 doanh trại mà không bị phát hiện. Về căn cứ báo cáo với Hùng.", 8);
        }

        private void Update()
        {
            if (mission.Stopped) return;
            HungInRange = (Stage == RescueStage || AwaitingReport) && NearHung(mission.Settings.interactRange);
            HandleCampObjective();
        }

        /// <summary>
        /// The outposts are done once every already-placed outpost guard is down. Requires at
        /// least one to exist so an empty/edited scene cannot skip the objective by vacuous truth.
        /// </summary>
        private void HandleCampObjective()
        {
            if (Stage != CampsStage) return;
            var outpostGuards = mission.Enemies.Where(e => e.name.StartsWith("Outpost guard ")).ToArray();
            if (outpostGuards.Length == 0 || outpostGuards.Any(e => e.Alive)) return;
            Stage = ReportCampsStage;
            mission.Say("Ba doanh trại đã im tiếng. Về căn cứ báo cáo với Hùng.", 8);
        }

        /// <summary>
        /// Clones an outpost guard as the commander — same rig and stats as a starting point,
        /// boosted, standing ground instead of patrolling. No dedicated boss model yet, so this
        /// is placeholder-look on purpose; swap in real art later without touching this logic.
        /// </summary>
        private void SpawnBoss()
        {
            // Deterministic pick: the commander inherits the template's parent, which is part of
            // his checkpoint SaveId, so it must be the same guard on every run.
            var template = mission.Enemies.Where(e => e.name.StartsWith("Outpost guard "))
                .OrderBy(e => e.SaveId, System.StringComparer.Ordinal).FirstOrDefault();
            if (template == null) { Debug.LogWarning("[Map01] No outpost guard to clone the commander from.", this); return; }
            var instance = Instantiate(template.gameObject, template.transform.position, template.transform.rotation, template.transform.parent);
            instance.name = "Chỉ huy địch";
            boss = instance.GetComponent<Map01EnemyController>();
            boss.Configure(System.Array.Empty<Vector3>()); // Holds this position rather than resuming a patrol loop.
            boss.BindMission(mission); // Before ConfigureAsBoss: binding resets damage to the balance baseline.
            boss.ConfigureAsBoss(3f, 1.6f);
            mission.AddEnemy(boss);
            var bossHealth = instance.GetComponent<Health>();
            if (bossHealth != null) bossHealth.onDied.AddListener(OnBossDown);
        }

        private void OnBossDown()
        {
            if (Stage != BossStage) return;
            Stage = ReportBossStage;
            mission.Say("Chỉ huy địch đã gục. Về căn cứ báo cáo chiến thắng với Hùng.", 8);
        }
    }
}
