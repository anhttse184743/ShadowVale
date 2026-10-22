using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Where Map 1's things come from, and the base's store (the approved design "Rớt đồ, hòm, tiếp tế căn cứ",
    /// 17/10/2026; numbers in Resources/Loot/Map01Loot.json, laid out like the content database):
    /// <list type="bullet">
    /// <item>A fallen guard leaves his pack, rolled from his kind's loot table (patrol, camp guard, operation guard,
    /// commander), and his rifle, worn to 15-35% (the commander also his K-54). The roll leans towards what Nam is
    /// short of - rounds when his reserve is under a magazine, dressings when he is hurt and has none. When medicine
    /// is scarce, the configured dry-pack limit adds a dressing. A roll is kept in the save, so a load finds the same pack.</item>
    /// <item>The camp chests hold their signature things (tier 5) and a few rolls of their camp's table, the guns in
    /// them in used but sound condition. Rolled from this run's seed, so each new game differs.</item>
    /// <item>The base store (KHO CĂN CỨ, a tab of the bench screen, and the supply point in base A): logistics points
    /// come from the command when an operation is done (Hùng home, supplies, radio, commander) and for every captured
    /// gun handed in (its base value, less as it is worn); they buy rounds, dressings, repair kits and parts at their
    /// base value. It replaces the base crates that refilled themselves every minute or so.</item>
    /// </list>
    /// </summary>
    public sealed class Map01Logistics : MonoBehaviour
    {
        [Serializable] public sealed class Rolled { public string point; public ForestIngredient[] items; }
        [Serializable] public sealed class State
        {
            public int seed, credit, medkitDry, handedIn;
            public bool[] granted;
            public Rolled[] rolled;
        }
        /// <summary>One thing the store issues: so many of an item for so many points.</summary>
        public struct Offer { public string item; public int count, price; }

        public const string GunPointPrefix = "gun_";
        private Map01Mission mission;
        private Map01Inventory inventory;
        private Map01Loot data;
        private readonly Dictionary<string, ForestIngredient[]> rolled = new Dictionary<string, ForestIngredient[]>();
        private bool[] granted = new bool[4];
        private int medkitDry;

        public int Seed { get; private set; }
        /// <summary>Logistics points to spend at the base store.</summary>
        public int Credit { get; private set; }
        /// <summary>Captured guns handed in this run.</summary>
        public int HandedIn { get; private set; }
        public event Action Changed;

        public static Map01Logistics For(Map01Mission mission)
        {
            if (mission == null) return null;
            var found = mission.GetComponent<Map01Logistics>();
            return found != null ? found : mission.gameObject.AddComponent<Map01Logistics>();
        }

        private void Awake()
        {
            mission = GetComponent<Map01Mission>();
            inventory = GetComponent<Map01Inventory>();
            data = Map01Loot.Load();
            Seed = UnityEngine.Random.Range(1, int.MaxValue);
        }

        private void Start()
        {
            // The base crates used to fill up again every minute and a half: free rounds without end, which left
            // nothing to decide about wear, repair or what to pick up. The starting crate now gives its kit once,
            // and the supply shelf opens the store once Hùng is home.
            foreach (var point in mission.Points)
            {
                if (point == null || (point.id != "tutorial_loot" && point.id != "supplies")) continue;
                point.restockSeconds = 0f;
                if (point.id == "tutorial_loot" && data.ContainerTable("base_start_crate") != null) point.items = ContainerLoot("base_start_crate");
            }
        }

        public int Operations => granted.Length;
        public string OperationName(int i) => OperationNames[i];
        public int OperationCredit(int i) => i < data.logistics.operationCredit.Length ? data.logistics.operationCredit[i] : 0;
        /// <summary>The command has sent what operation <paramref name="i"/> earns.</summary>
        public bool Granted(int i) => granted[i];

        // ---- what the command sends ------------------------------------------------------------------------

        private bool Done(int operation)
        {
            var story = GetComponent<Map01Story>();
            var quest = GetComponent<Map01Quest>();
            var p = story != null ? story.Progress : null;
            switch (operation)
            {
                case 0: return quest != null && quest.BaseResupplyOpen;      // Hùng home
                case 1: return p != null && p.suppliesDelivered;
                case 2: return p != null && p.radioDisabled;
                case 3: return p != null && p.commanderKilled;
                default: return false;
            }
        }
        private static readonly string[] OperationNames = { "Đưa Hùng về căn cứ", "Giao hàng tiếp tế", "Phá đài liên lạc", "Hạ chỉ huy địch" };

        private void Update()
        {
            if (mission == null || !mission.IsInitialized || Map01SaveSystem.IsRestoring) return;
            var credit = data.logistics.operationCredit;
            for (int i = 0; i < granted.Length && i < credit.Length; i++)
            {
                if (granted[i] || !Done(i)) continue;
                granted[i] = true;
                Credit += credit[i];
                mission.Say($"{OperationNames[i]}: cấp trên gửi tiếp tế +{credit[i]} điểm hậu cần (còn {Credit}). Lĩnh ở kho căn cứ A.", 6);
                Changed?.Invoke();
            }
        }

        // ---- the store -------------------------------------------------------------------------------------

        /// <summary>What the store issues (container base_requisition), at base value × count.</summary>
        public IEnumerable<Offer> Stock => (data.ContainerTable("base_requisition")?.entries ?? Array.Empty<Map01Loot.Entry>())
            .Select(e => new Offer { item = e.item, count = e.min, price = data.BaseValue(e.item) * e.min });

        /// <summary>True while Nam is in base A, by the bench or the supply shelf.</summary>
        public bool InBase()
        {
            if (mission == null || mission.player == null) return false;
            return mission.Interactables.Any(p => p != null && (p.kind == ForestPointKind.Workbench || p.kind == ForestPointKind.Supplies)
                && Vector3.Distance(mission.player.position, p.transform.position) < mission.Settings.interactRange + 1.5f);
        }
        public string IssueRefusal(Offer offer)
        {
            if (!InBase()) return "Kho chỉ cấp phát ở căn cứ A.";
            if (Credit < offer.price) return $"Thiếu {offer.price - Credit} điểm hậu cần.";
            return null;
        }
        public bool Issue(Offer offer)
        {
            string why = IssueRefusal(offer);
            if (why != null) { mission.Say(why, 3); return false; }
            Credit -= offer.price;
            inventory.Receive(offer.item, offer.count);
            mission.Say($"Kho cấp: {offer.count} {ForestInventory.Name(offer.item).ToLowerInvariant()} (−{offer.price} điểm, còn {Credit}).", 3);
            Changed?.Invoke();
            return true;
        }
        /// <summary>A captured gun into the store for its worth in points. The gun in hand is never handed in by mistake.</summary>
        public bool HandIn(Map01Inventory.Gun gun)
        {
            if (!InBase()) { mission.Say("Nộp súng thu được ở kho căn cứ A.", 3); return false; }
            string id = inventory.IdOf(gun);
            if (!Map01Inventory.HasCondition(id)) return false;
            if (inventory.IsEquipped(id) && gun.Slot != null) { mission.Say("Không nộp khẩu súng đang cầm trên tay.", 3); return false; }
            int worth = inventory.TurnInValue(gun);
            if (!inventory.RemoveGun(gun)) return false;
            Credit += worth; HandedIn++;
            mission.Say($"Đã nộp {ForestInventory.Name(id).ToLowerInvariant()}: +{worth} điểm hậu cần (còn {Credit}).", 3);
            Changed?.Invoke();
            return true;
        }

        // ---- rolls -----------------------------------------------------------------------------------------

        /// <summary>The loot kind of a guard (the database's enemy type).</summary>
        public static string KindOf(Map01EnemyController guard) =>
            guard.IsBoss ? "m1_commander" : guard.operationIndex > 0 ? "m1_operation_guard"
            : guard.name.StartsWith("Outpost guard ", StringComparison.Ordinal) ? "m1_camp_guard" : "m1_patrol";

        /// <summary>What a guard's pack is called (his loot table's name: "Túi lính gác trại").</summary>
        public string PackName(Map01EnemyController guard) => data.TableOf(data.EnemyOf(KindOf(guard))?.lootTable)?.name ?? "Túi lính tuần tra";

        /// <summary>What a fallen guard's pack holds: rolled once (leaning to what Nam is short of), then kept.</summary>
        public ForestIngredient[] GuardPack(Map01EnemyController guard, string pointId)
        {
            if (rolled.TryGetValue(pointId, out var kept))
            {
                // Older checkpoints may have rolled generic rifle or sidearm rounds.
                rolled[pointId] = MatchGuardAmmo(guard.RifleItemId, kept);
                return Copy(rolled[pointId]);
            }
            var kind = data.EnemyOf(KindOf(guard));
            var table = data.TableOf(kind?.lootTable);
            string ammo = guard.RifleItemId == Map01M16.Id ? Map01M16.Ammo : "ammo_rifle";
            var drops = Map01LootRoller.Roll(data, table, Map01LootRoller.Seed(Seed, pointId),
                item => Need(item.StartsWith("ammo_", StringComparison.Ordinal) && item != "ammo_pistol" ? ammo : item));
            bool dressing = drops.Any(d => d.item == "medkit_small");
            medkitDry = dressing ? 0 : medkitDry + 1;
            if (!dressing && medkitDry >= data.adaptive.medkitPity && inventory.Count("medkit_small") < 2)
            { drops.Add(new Map01LootRoller.Drop { item = "medkit_small", count = 1 }); medkitDry = 0; }
            var items = MatchGuardAmmo(guard.RifleItemId, drops.Select(d => new ForestIngredient { item_id = d.item, count = d.count }).ToArray());
            rolled[pointId] = Copy(items);
            return items;
        }

        /// <summary>Match rifle rounds to the held rifle; preserve K-54 rounds and all other supplies.</summary>
        public static ForestIngredient[] MatchGuardAmmo(string rifle, ForestIngredient[] items)
        {
            string ammo = rifle == Map01M16.Id ? Map01M16.Ammo : "ammo_rifle";
            var result = new List<ForestIngredient>();
            int rounds = 0;
            foreach (var item in items)
            {
                if (item.item_id.StartsWith("ammo_", StringComparison.Ordinal) && item.item_id != "ammo_pistol") rounds += item.count;
                else result.Add(new ForestIngredient { item_id = item.item_id, count = item.count, condition = item.condition });
            }
            if (rounds > 0) result.Add(new ForestIngredient { item_id = ammo, count = rounds });
            return result.ToArray();
        }

        /// <summary>How much likelier an item is for Nam now: rounds when his reserve is low, dressings when he is hurt with none.</summary>
        private float Need(string item)
        {
            var a = data.adaptive;
            if ((item == "ammo_rifle" || item == Map01M16.Ammo) && inventory.Count(item) < a.lowAmmo) return a.ammoBoost;
            if (item == "medkit_small" && inventory.Count("medkit_small") == 0
                && mission.PlayerHealth < a.lowHealth * mission.Settings.playerHP) return a.medkitBoost;
            return 1f;
        }

        /// <summary>The guns a guard drops (his rifle; the commander his pistol too), each worn within his kind's range.</summary>
        public IEnumerable<ForestIngredient> GuardGuns(Map01EnemyController guard, string pointId)
        {
            var kind = data.EnemyOf(KindOf(guard));
            if (kind == null) yield break;
            yield return Gun(guard.RifleItemId, kind.dropMin, kind.dropMax, pointId);
            if (!string.IsNullOrEmpty(kind.sidearm)) yield return Gun(kind.sidearm, kind.sidearmMin, kind.sidearmMax, pointId + "_sidearm");
        }
        private ForestIngredient Gun(string id, float min, float max, string salt) => new ForestIngredient
        {
            item_id = id, count = 1,
            condition = Map01LootRoller.Between(Map01LootRoller.Seed(Seed, salt + "_condition"), min, max) * inventory.MaxCondition(id),
        };

        /// <summary>What the container <paramref name="tag"/> holds this run: its signature things and its rolls, any gun in
        /// it found in used but sound condition.</summary>
        public ForestIngredient[] ContainerLoot(string tag)
        {
            var drops = Map01LootRoller.Roll(data, data.ContainerTable(tag), Map01LootRoller.Seed(Seed, tag));
            return drops.Select(d =>
            {
                var item = data.ItemOf(d.item);
                float condition = Map01Inventory.HasCondition(d.item) && item != null
                    ? Map01LootRoller.Between(Map01LootRoller.Seed(Seed, tag + "_" + d.item), item.foundMin, item.foundMax) * inventory.MaxCondition(d.item)
                    : Map01Inventory.NoCondition;
                return new ForestIngredient { item_id = d.item, count = d.count, condition = condition };
            }).ToArray();
        }

        private static ForestIngredient[] Copy(ForestIngredient[] items) =>
            items.Select(i => new ForestIngredient { item_id = i.item_id, count = i.count, condition = i.condition }).ToArray();

        // ---- the save --------------------------------------------------------------------------------------

        public State Capture() => new State
        {
            seed = Seed, credit = Credit, medkitDry = medkitDry, handedIn = HandedIn, granted = (bool[])granted.Clone(),
            rolled = rolled.Select(p => new Rolled { point = p.Key, items = Copy(p.Value) }).ToArray(),
        };

        /// <summary>Back to a saved state. A save from before the store keeps its seed but starts the store at 0 points,
        /// with what was already done counted as sent (it was paid in refills then).</summary>
        public void Restore(State saved)
        {
            rolled.Clear();
            if (saved == null || saved.seed == 0)
            {
                Credit = 0; HandedIn = 0; medkitDry = 0;
                for (int i = 0; i < granted.Length; i++) granted[i] = Done(i);
            }
            else
            {
                Seed = saved.seed; Credit = Mathf.Max(0, saved.credit); HandedIn = Mathf.Max(0, saved.handedIn); medkitDry = Mathf.Max(0, saved.medkitDry);
                granted = new bool[4];
                if (saved.granted != null) for (int i = 0; i < Mathf.Min(granted.Length, saved.granted.Length); i++) granted[i] = saved.granted[i];
                foreach (var r in saved.rolled ?? Array.Empty<Rolled>()) if (!string.IsNullOrEmpty(r.point) && r.items != null) rolled[r.point] = Copy(r.items);
            }
            Changed?.Invoke();
        }
    }
}
