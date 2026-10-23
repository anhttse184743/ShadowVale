using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ShadowVale.Gameplay.Combat;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Everything the player carries and the bench's crafting queue. What is carried lives in two places:
    /// the four equipment slots (1 long gun, 2 pistol, 3 knife, plus the throwable selected for left mouse) and the
    /// bag, a 6 x 4 grid where each cell holds one stack. Cells keep their place (the player arranges them,
    /// and a save keeps the arrangement); counts are read off the cells. Presentation (names, icons,
    /// descriptions) stays in the static <see cref="ForestInventory"/> helper; the grid's moves are in
    /// Map01Inventory.Bag.cs.
    /// </summary>
    public sealed partial class Map01Inventory : MonoBehaviour
    {
        private string selectedItem;
        private float nextQuickUse;

        /// <summary>One thing being made at the bench. Its materials were taken when it was queued; only the
        /// first job runs, and its clock starts when it reaches the front.</summary>
        public sealed class CraftJob
        {
            public ForestRecipe recipe;
            public float until;
            public float Seconds => Mathf.Max(.01f, recipe.craft_seconds);
        }
        private readonly List<CraftJob> queue = new List<CraftJob>();
        private readonly Dictionary<string, int> batchMade = new Dictionary<string, int>();

        /// <summary>Set when a panel toggles or a weapon is equipped, so the click that did it
        /// cannot also fire. Map01PlayerInteraction clears it once the panels are shut and the
        /// left button is up — it must never outlive that click, or combat stays blocked for good.</summary>
        public bool SuppressFire { get; set; }
        public bool IsCrafting => queue.Count > 0;
        public string SelectedItem { get => selectedItem; set => selectedItem = value; }
        public float CraftRemaining => queue.Count > 0 ? Mathf.Max(0, queue[0].until - Time.time) : 0;
        public string CraftingId => queue.Count > 0 ? queue[0].recipe.id : null;
        public IReadOnlyList<CraftJob> Queue => queue;
        /// <summary>How far the first job has come, 0 to 1.</summary>
        public float CraftProgress => queue.Count > 0 ? Mathf.Clamp01(1 - CraftRemaining / queue[0].Seconds) : 0;

        public event System.Action<string> ItemUsed;
        private CharacterHudSession mission;
        private void Awake() { mission = CharacterHudSession.For(gameObject); }

        /// <summary>Nam starts with his rifle (slot 1), his K-54 (slot 2) with two magazines (Map01Balance.json
        /// startingPistolRounds) and his knife (slot 3) — rifle rounds and herbs only come from picking up loot in the field
        /// (see the "tutorial_loot" point). While
        /// Map01Balance.json has testCraftingKit on, he also carries <see cref="TestCraftingKit"/>.</summary>
        public void SeedStartingLoadout(int startingStones)
        {
            if (mission == null) mission = CharacterHudSession.For(gameObject);
            ClearAll();
            // The player's starting rifle is always the AK. M16s come from enemy loot.
            Add("rifle_standard", 1);
            Add("pistol_k54", 1);
            if (mission != null && mission.Settings != null) Add("ammo_pistol", mission.Settings.startingPistolRounds);
            Add("knife", 1);
            Add("stone", Mathf.Min(startingStones, StoneCapacity));
            if (mission != null && mission.Settings != null && mission.Settings.testCraftingKit && !TestKitHeldOff)
                foreach (var (id, count) in TestCraftingKit) Add(id, count);
        }

        /// <summary>TEMPORARY: enough of every material to make each bench recipe several times, and what the
        /// camp chests hold, so the bench can be tried from the start.</summary>
        public static readonly (string id, int count)[] TestCraftingKit =
        {
            ("cloth", 12), ("herb", 6), ("scrap_metal", 12), ("gun_oil", 4), ("smoke_module", 4), ("gunpowder", 12),
            ("ammo_rifle", 60), ("medkit_small", 2), ("rifle_mosin", 1), ("ammo_sniper", 10),
            ("weapon_parts", 2), ("fuse_module", 2), ("blast_charge", 2), ("scope_lens", 1), ("smoke_bomb", 2), ("grenade_f1", 1), ("repair_kit", 2),
        };

        /// <summary>Editor SessionState flag the test base sets, so tests start with the normal loadout whatever
        /// Map01Balance.json says. SessionState survives the domain reload of entering Play.</summary>
        public const string NoTestKitKey = "ShadowVale.NoTestCraftingKit";
        private static bool TestKitHeldOff
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.SessionState.GetBool(NoTestKitKey, false);
#else
                return false;
#endif
            }
        }

        /// <summary>How many of <paramref name="id"/> he carries: in the bag and in the slots.</summary>
        public int Count(string id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            int total = 0;
            foreach (var cell in bag) if (cell != null && cell.id == id) total += cell.count;
            if (primary == id) total++;
            if (pistol == id) total++;
            if (knife == id) total++;
            return total;
        }
        /// <summary>Into the slots and the bag, quietly (loading, the starting loadout). What does not fit is lost:
        /// use <see cref="Receive"/> for anything found in the world, which puts what does not fit on the ground.</summary>
        public void Add(string id, int amount, float condition = NoCondition) => Store(id, amount, condition);
        /// <summary>Something found, picked up or made: what <see cref="Received"/> tells the HUD's pickup feed.
        /// What the bag has no room for is put down at Nam's feet (Map01Drops), so nothing found is lost. A gun comes
        /// with its <paramref name="condition"/> (points; NoCondition = new).</summary>
        public void Receive(string id, int amount, float condition = NoCondition)
        {
            if (amount <= 0) return;
            if (id == "stone") amount = Mathf.Min(amount, StoneRoom);
            int rest = Store(id, amount, condition);
            if (amount - rest > 0) Received?.Invoke(id, amount - rest);
            if (rest > 0 && mission != null)
            {
                mission.Drop(id, rest, condition);
                mission.Say($"Túi đã đầy — {rest} {ForestInventory.Name(id).ToLowerInvariant()} để lại dưới chân.", 4);
            }
        }
        public event System.Action<string, int> Received;
        /// <summary>Takes <paramref name="amount"/> out of the bag, from the last stacks first (a weapon in its slot
        /// last of all).</summary>
        public void Spend(string id, int amount)
        {
            for (int i = BagSize - 1; i >= 0 && amount > 0; i--)
            {
                var cell = bag[i];
                if (cell == null || cell.id != id) continue;
                int take = Mathf.Min(amount, cell.count);
                cell.count -= take; amount -= take;
                if (cell.count <= 0) bag[i] = null;
            }
            if (amount > 0 && primary == id) { SetSlot(Slot.Primary, null); amount--; OnSlotEmptied(); }
            if (amount > 0 && pistol == id) { SetSlot(Slot.Pistol, null); amount--; OnSlotEmptied(); }
            Changed?.Invoke();
        }
        /// <summary>Totals by item (bag and slots), for whatever wants the whole list.</summary>
        public IReadOnlyDictionary<string, int> Items
        {
            get
            {
                var all = new Dictionary<string, int>();
                foreach (var cell in bag) if (cell != null) all[cell.id] = (all.TryGetValue(cell.id, out int n) ? n : 0) + cell.count;
                foreach (var id in new[] { primary, pistol, knife })
                    if (id != null) all[id] = (all.TryGetValue(id, out int n) ? n : 0) + 1;
                return all;
            }
        }
        public int Stones => Count("stone");
        /// <summary>How many stones his pockets hold (Map01Balance.json). Picking up takes only what still fits.</summary>
        public int StoneCapacity => mission != null && mission.Settings != null && mission.Settings.stoneCapacity > 0 ? mission.Settings.stoneCapacity : 15;
        public int StoneRoom => Mathf.Max(0, StoneCapacity - Stones);

        public int StackLimit(string id)
        {
            if (ForestInventory.IsWeapon(id)) return 1;
            if (id == "stone") return StoneCapacity;
            if (id == "supplies" || id == "river_documents") return 1;
            return Mathf.Max(1, mission?.Bundle?.items?.FirstOrDefault(i => i.id == id)?.stack_max ?? 1);
        }
        /// <summary>The bag's stacks in cell order (empty cells left out).</summary>
        public List<ForestInventory.Stack> InventoryStacks() =>
            bag.Where(c => c != null).Select(c => new ForestInventory.Stack(c.id, c.count)).ToList();

        /// <summary>True when <paramref name="id"/> is the weapon in Nam's hands.</summary>
        public bool IsEquipped(string id)
        {
            if (!ForestInventory.IsWeapon(id) || mission == null || mission.ModernCombat == null) return false;
            var kind = mission.ModernCombat.EquippedKind;
            return id == "knife" ? kind == WeaponKind.Knife && knife == id
                : id == "pistol_k54" ? kind == WeaponKind.Pistol && pistol == id
                : id == "rifle_mosin" ? kind == WeaponKind.Sniper && primary == id
                : (id == "rifle_standard" || id == Map01M16.Id) && kind == WeaponKind.Rifle && primary == id;
        }
        public string HeldAmmoId => mission != null && mission.ModernCombat != null
            ? mission.ModernCombat.EquippedKind==WeaponKind.Pistol ? "ammo_pistol" : mission.ModernCombat.EquippedKind==WeaponKind.Sniper ? "ammo_sniper" : PrimaryAmmo : PrimaryAmmo;
        /// <summary>The weapon <paramref name="id"/> into its slot (from the bag, if it is there) and into his hands.</summary>
        public bool EquipItem(string id)
        {
            if (!ForestInventory.IsWeapon(id) || Count(id) <= 0 || mission == null || !mission.IsInitialized ||
                mission.Stopped || ForestMenu.Visible || mission.ModernCombat == null) return false;
            if (SlotOf(id) != null && SlotItem(SlotOf(id).Value) != id)
            {
                int at = System.Array.FindIndex(bag, c => c != null && c.id == id);
                if (at >= 0) EquipFromCell(at);
            }
            SuppressFire = true;
            return Draw(SlotOf(id) ?? Slot.Knife);
        }

        public bool UseItem(string id, Vector3? throwTarget = null)
        {
            // The bench goes on working while he walks off, so a queued craft blocks nothing.
            if (!mission.IsInitialized || mission.Stopped || ForestMenu.Visible || Time.time < nextQuickUse) return false;
            if (!ForestInventory.QuickUsable(id)) { mission.Say("Vật phẩm này không dùng trực tiếp.", 3); return false; }
            if (ForestInventory.IsWeapon(id)) return EquipItem(id);
            if (Count(id) <= 0) { mission.Say("Đã hết " + ForestInventory.Name(id).ToLowerInvariant() + ".", 3); return false; }
            if (id == "medkit_small")
            {
                if (mission.PlayerHealth >= mission.Settings.playerHP) { mission.Say("Máu đã đầy, chưa cần dùng băng cứu thương.", 3); return false; }
                var actions=Map01NamActions.For(mission);
                if(actions?.Busy==true)return false;
                Spend(id, 1);
                mission.RestoreHealthFromSave(Mathf.Min(mission.Settings.playerHP, mission.PlayerHealth + mission.Settings.medkitHeal));
                mission.Say("Đã dùng băng cứu thương.", 3);
                // Down on one knee winding the bandage on (Blender); walking off cuts it short.
                actions?.Play(Map01NamActions.Bandage, 141f / 30f / 1.4f, true);
            }
            else
            {
                // Only the throwable leaves the bag here; its flight and what it does where it lands
                // belong to Map01StoneThrow.
                if (mission.InventoryOpen || mission.MapOpen) { mission.Say("Đóng túi đồ để ngắm hướng ném.", 3); return false; }
                if (throwTarget == null) return false;
                Spend(id, 1);
            }
            ItemUsed?.Invoke(id);
            nextQuickUse = Time.time + .25f;
            return true;
        }
        public bool NearWorkbench() => mission.Interactables.Any(p => p.kind == ForestPointKind.Workbench
            && Vector3.Distance(mission.player.position, p.transform.position) < mission.Settings.interactRange);
        /// <summary>What the workbench makes on Map 1, in the order the bench lists them.</summary>
        public static readonly string[] BenchRecipes = { "craft_medkit_small", "craft_repair_kit", "craft_smoke_bomb", "craft_grenade_f1", "craft_ammo_pistol", "craft_ammo_rifle", "craft_ammo_556", "craft_ammo_sniper" };
        public IEnumerable<ForestRecipe> Recipes => BenchRecipes.Select(Recipe).Where(r => r != null);
        public ForestRecipe Recipe(string id) => mission.Bundle?.craft_recipes?.FirstOrDefault(r => r.id == id);
        public bool HasInputs(ForestRecipe recipe) => recipe.inputs.All(i => Count(i.item_id) >= i.count);
        /// <summary>How many times over the bag holds what <paramref name="recipe"/> needs.</summary>
        public int CanMake(ForestRecipe recipe) => recipe.inputs.Length == 0 ? 0 : recipe.inputs.Min(i => i.count > 0 ? Count(i.item_id) / i.count : int.MaxValue);
        /// <summary>"2 vải + 1 thảo dược".</summary>
        public static string Inputs(ForestRecipe recipe, int times = 1) => string.Join(" + ", recipe.inputs.Select(i => i.count * times + " " + ForestInventory.Name(i.item_id).ToLowerInvariant()));

        /// <summary>[B] at the bench: a field dressing, the first thing anyone needs.</summary>
        public void TryCraft() => TryCraft("craft_medkit_small");
        /// <summary>Queues <paramref name="times"/> crafts of a recipe at the bench. The materials are taken now;
        /// the bench works through the queue one at a time, also while Nam is away from it.</summary>
        public bool TryCraft(string recipeId, int times = 1)
        {
            if (times < 1 || !NearWorkbench()) return false;
            var recipe = Recipe(recipeId);
            if (recipe == null) return false;
            if (CanMake(recipe) < times) { mission.Say("Chưa đủ vật liệu: cần " + Inputs(recipe, times) + "."); return false; }
            foreach (var input in recipe.inputs) Spend(input.item_id, input.count * times);
            for (int n = 0; n < times; n++) queue.Add(new CraftJob { recipe = recipe });
            if (queue.Count == times) queue[0].until = Time.time + queue[0].Seconds;
            return true;
        }
        public void Update()
        {
            if (queue.Count == 0 || Time.time < queue[0].until) return;
            var done = queue[0].recipe;
            queue.RemoveAt(0);
            Receive(done.output_item_id, done.output_count);
            batchMade[done.output_item_id] = (batchMade.TryGetValue(done.output_item_id, out int made) ? made : 0) + done.output_count;
            if (queue.Count > 0) { queue[0].until = Time.time + queue[0].Seconds; return; }
            // The whole batch is done: one line for it, not one per item.
            string list = string.Join(", ", batchMade.Select(p => p.Value + " " + ForestInventory.Name(p.Key).ToLowerInvariant()));
            bool dressing = batchMade.ContainsKey("medkit_small");
            batchMade.Clear();
            mission.Say("Chế tạo xong: " + list + "." + (dressing ? " Nhấn H để dùng băng cứu thương." : ""));
        }

        /// <summary>Loads the totals a save carries (every save has them). A save that also carries the bag's
        /// layout (<see cref="RestoreLayout"/>) puts every stack back where it was afterwards.</summary>
        public void RestoreFromSave(ForestIngredient[] savedItems, int savedStones, string savedCrafting, float craftRemaining)
        {
            restoringRifle = true;
            SuppressFire = false; nextQuickUse = 0; ThrowableEquipped = false;
            ClearAll();
            // Weapons first, so they take their slots; earlier saves carried rifle and knife without entries.
            // Only a missing key is migrated: a saved count of 0 is an explicit absence and stays one.
            bool HasKey(string id) => savedItems.Any(i => i != null && i.item_id == id);
            var saved = savedItems.Where(i => i != null && i.count > 0).OrderBy(i => ForestInventory.IsWeapon(i.item_id) ? 0 : 1).ToList();
            if (!HasKey("rifle_standard") && !HasKey("rifle_mosin") && !HasKey("rifle_m16")) saved.Insert(0, new ForestIngredient { item_id = "rifle_standard", count = 1 });
            if (!HasKey("knife")) saved.Insert(0, new ForestIngredient { item_id = "knife", count = 1 });
            foreach (var i in saved) if (i.item_id != "stone") Store(i.item_id, i.count);
            Store("stone", Mathf.Min(savedStones, StoneCapacity));
            queue.Clear(); batchMade.Clear();
            var recipe = string.IsNullOrEmpty(savedCrafting) ? null : mission.Bundle?.craft_recipes?.FirstOrDefault(r => r.id == savedCrafting);
            if (recipe != null) queue.Add(new CraftJob { recipe = recipe, until = Time.time + craftRemaining });
            restoringRifle = false; ApplyPrimaryRifle(false);
        }
        /// <summary>What to save. A save keeps only the craft under way; the materials of the ones still
        /// waiting behind it go back into the bag, so a load never loses them.</summary>
        public ForestIngredient[] CaptureItems()
        {
            var saved = new Dictionary<string, int>(Items);
            saved.Remove("stone");                                  // stones are saved on their own (CheckpointData.stones)
            for (int n = 1; n < queue.Count; n++)
                foreach (var input in queue[n].recipe.inputs)
                    saved[input.item_id] = (saved.TryGetValue(input.item_id, out int had) ? had : 0) + input.count;
            return saved.Select(p => new ForestIngredient { item_id = p.Key, count = p.Value }).ToArray();
        }
    }
}
