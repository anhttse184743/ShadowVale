using System.Linq;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Map 1's briefing-driven quest line, on its own component: rescue Hùng, report to base,
    /// scout the map, clear the three enemy outposts already built into the scene ("Enemy
    /// outpost 1/2/3", each with two "Outpost guard N" enemies baked into the scene), then
    /// the commander who calls them in. Reads Map01Mission for shared state and Map01Inventory
    /// for the herb the rescue needs; nothing else needs to know the stage numbers.
    /// </summary>
    public sealed class Map01Quest : MonoBehaviour
    {
        // Stage numbers are the save format — see Map01SaveSystem — so append, never reorder.
        public const int BossStage = 4;
        public const int CompleteStage = 5;
        public static readonly string[] Objectives = {
            "Tìm Hùng đang bị thương và dùng thảo dược chữa trị cho anh ấy [E]",
            "Đưa Hùng về căn cứ, nhận hàng tiếp tế [E]",
            "Trinh sát địa hình — mở bản đồ [M]",
            "Chiếm đóng 3 doanh trại của địch — tiêu diệt toàn bộ lính",
            "Tiêu diệt chỉ huy địch",
            "Hoàn thành Map 1 — Những dấu chân trong rừng"
        };

        public int Stage { get; private set; }
        /// <summary>True while the player is close enough to Hùng to treat him (stage 0 only).</summary>
        public bool HungInRange { get; private set; }
        /// <summary>Direct-set for restoring a checkpoint; never call this mid-play otherwise.</summary>
        public void RestoreStage(int value) => Stage = Mathf.Clamp(value, 0, CompleteStage);

        private Map01Mission mission;
        private Map01Inventory inventory;
        private NavMeshAgent companion;
        private Map01EnemyController boss;
        public bool BossSpawned => boss != null;

        private void Awake()
        {
            mission = GetComponent<Map01Mission>();
            inventory = GetComponent<Map01Inventory>();
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

        /// <summary>
        /// [E] near wounded Hùng at stage 0. Needs one herb — the briefing's "kiếm thảo dược chữa
        /// trị cho Hùng" — and advances to stage 1 (report to base).
        /// </summary>
        public void TryRescueHung()
        {
            if (Stage != 0 || mission.hung == null) return;
            if (Vector3.Distance(mission.player.position, mission.hung.position) > mission.Settings.interactRange) return;
            if (inventory.Count("herb") <= 0) { mission.Say("Cần thảo dược để chữa trị cho Hùng.", 3); return; }
            inventory.Spend("herb", 1); Stage = 1;
            mission.Say("Nam: Chịu khó chút, Hùng. Thảo dược này cầm máu được.\nHùng: ...Cảm ơn Nam. Về căn cứ lấy hàng tiếp tế rồi tính tiếp.", 9);
        }

        /// <summary>[E] on the Supplies point once Hùng is treated — advances to stage 2.</summary>
        public bool TryDeliverSupplies()
        {
            if (Stage != 1) return false;
            Stage = 2;
            mission.Say("Hùng: Cảm ơn Nam. Anh sẽ ở lại căn cứ chỉ huy — cứ quay lại đây khi cần giao nhiệm vụ mới.");
            return true;
        }

        /// <summary>[M] while stage 2 clears "Trinh sát địa hình" from the briefing.</summary>
        public void OnMapOpened()
        {
            if (Stage != 2) return;
            Stage = 3;
            mission.Say("Đã xác định vị trí 3 doanh trại của địch trên bản đồ.", 6);
        }

        public bool ShouldFollowPlayer() => Stage >= 1;

        private void Update()
        {
            if (mission.Stopped) return;
            HungInRange = Stage == 0 && mission.hung != null
                && Vector3.Distance(mission.player.position, mission.hung.position) <= mission.Settings.interactRange;
            HandleCampObjective();
        }

        /// <summary>
        /// Stage 3 clears once every already-placed outpost guard is down, then hands off to the
        /// commander fight. Requires at least one outpost guard to exist so an empty/edited scene
        /// cannot skip the objective by vacuous truth.
        /// </summary>
        private void HandleCampObjective()
        {
            if (Stage != 3) return;
            var outpostGuards = mission.Enemies.Where(e => e.name.StartsWith("Outpost guard ")).ToArray();
            if (outpostGuards.Length == 0 || outpostGuards.Any(e => e.Alive)) return;
            Stage = BossStage;
            mission.Say("Hùng: Ba doanh trại đã im tiếng. Cẩn thận — chỉ huy của chúng chắc đang ở gần.", 8);
            SpawnBoss();
        }

        /// <summary>
        /// Clones a defeated outpost guard as the commander — same rig and stats as a starting
        /// point, boosted, standing ground instead of patrolling. No dedicated boss model/visual
        /// yet, so this is placeholder-look on purpose; swap it for real art later without
        /// touching this logic.
        /// </summary>
        private void SpawnBoss()
        {
            var template = mission.Enemies.FirstOrDefault(e => e.name.StartsWith("Outpost guard "));
            if (template == null) { Debug.LogWarning("[Map01] No outpost guard to clone the commander from.", this); return; }
            var instance = Instantiate(template.gameObject, template.transform.position, template.transform.rotation, template.transform.parent);
            instance.name = "Chỉ huy địch";
            boss = instance.GetComponent<Map01EnemyController>();
            boss.Configure(System.Array.Empty<Vector3>()); // Holds this position rather than resuming a patrol loop.
            boss.ConfigureAsBoss(3f, 1.6f);
            boss.BindMission(mission);
            mission.AddEnemy(boss);
            var bossHealth = instance.GetComponent<Health>();
            if (bossHealth != null) bossHealth.onDied.AddListener(OnBossDown);
        }

        private void OnBossDown()
        {
            if (Stage != BossStage) return;
            Stage = CompleteStage;
            mission.Say("Hùng: Chỉ huy của chúng đã gục. Map 1 hoàn tất, Nam.", 10);
        }
    }
}
