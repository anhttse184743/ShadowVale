using System.Linq;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Map 1's briefing-driven quest line: rescue Hùng, report to base, scout the map, clear the
    /// three enemy outposts already built into the scene ("Enemy outpost 1/2/3", each with two
    /// "Outpost guard N" enemies — see Map01Expansion.cs), then the commander who calls them in.
    /// </summary>
    public sealed partial class ForestMission
    {
        /// <summary>True while the player is close enough to Hùng to treat him (stage 0 only).</summary>
        public bool HungInRange { get; private set; }
        private Map01EnemyController boss;

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
                if (NavMesh.SamplePosition(player.position + offset, out var hit, 6, NavMesh.AllAreas))
                { companion.Warp(hit.position); return; }
        }

        /// <summary>
        /// [E] near wounded Hùng at stage 0. Needs one herb — the briefing's "kiếm thảo dược chữa
        /// trị cho Hùng" — and hands stage 1 (report to base) to the caller by advancing it here.
        /// </summary>
        private void TryRescueHung()
        {
            if (stage != 0 || hung == null) return;
            if (Vector3.Distance(player.position, hung.position) > Settings.interactRange) return;
            if (Count("herb") <= 0) { Say("Cần thảo dược để chữa trị cho Hùng.", 3); return; }
            inventory["herb"]--; stage = 1;
            Say("Nam: Chịu khó chút, Hùng. Thảo dược này cầm máu được.\nHùng: ...Cảm ơn Nam. Về căn cứ lấy hàng tiếp tế rồi tính tiếp.", 9);
        }

        /// <summary>
        /// [E]: treating Hùng takes priority when he's the one in range, otherwise it's the usual
        /// point interaction. Rescuing him must never require walking away from every loot crate
        /// first — the herb he needs at stage 0 comes from one.
        /// </summary>
        private void HandleInteractKey()
        {
            if (HungInRange) TryRescueHung();
            else if (nearby != null) Interact(nearby);
        }

        /// <summary>[M] while stage 2 clears "Trinh sát địa hình" from the briefing.</summary>
        private void OnMapOpened()
        {
            if (stage != 2) return;
            stage = 3;
            Say("Đã xác định vị trí 3 doanh trại của địch trên bản đồ.", 6);
        }

        /// <summary>Refreshes <see cref="HungInRange"/> for the HUD prompt; called every frame.</summary>
        private void UpdateRescuePrompt()
        {
            HungInRange = stage == 0 && hung != null && Vector3.Distance(player.position, hung.position) <= Settings.interactRange;
        }

        /// <summary>
        /// Stage 3 clears once every already-placed outpost guard is down, then hands off to the
        /// commander fight. Requires at least one outpost guard to exist so an empty/edited scene
        /// cannot skip the objective by vacuous truth.
        /// </summary>
        private void HandleCampObjective()
        {
            if (stage != 3) return;
            var outpostGuards = modernEnemies.Where(e => e.name.StartsWith("Outpost guard ")).ToArray();
            if (outpostGuards.Length == 0 || outpostGuards.Any(e => e.Alive)) return;
            stage = BossStage;
            Say("Hùng: Ba doanh trại đã im tiếng. Cẩn thận — chỉ huy của chúng chắc đang ở gần.", 8);
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
            var template = modernEnemies.FirstOrDefault(e => e.name.StartsWith("Outpost guard "));
            if (template == null) { Debug.LogWarning("[Map01] No outpost guard to clone the commander from.", this); return; }
            var instance = Instantiate(template.gameObject, template.transform.position, template.transform.rotation, template.transform.parent);
            instance.name = "Chỉ huy địch";
            boss = instance.GetComponent<Map01EnemyController>();
            boss.Configure(System.Array.Empty<Vector3>()); // Holds this position rather than resuming a patrol loop.
            boss.ConfigureAsBoss(3f, 1.6f);
            boss.BindMission(this);
            modernEnemies = modernEnemies.Append(boss).ToArray();
            var bossHealth = instance.GetComponent<Health>();
            if (bossHealth != null) bossHealth.onDied.AddListener(OnBossDown);
        }

        private void OnBossDown()
        {
            if (stage != BossStage) return;
            stage = CompleteStage;
            Say("Hùng: Chỉ huy của chúng đã gục. Map 1 hoàn tất, Nam.", 10);
        }
    }
}
