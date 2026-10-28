using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ShadowVale.Gameplay.Combat;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The bag grid and the equipment slots (the approved "Trang bị" design, 16/10/2026):
    /// <list type="bullet">
    /// <item>1 long gun (AK or Mosin), 2 pistol (K-54), 3 knife, 4 throwable selected for left mouse. There are no bare
    /// hands: the knife never leaves slot 3, so something is always in hand.</item>
    /// <item>The bag is 6 x 4 = 24 cells, one stack each, at most the item's stack size. Dropping a stack on an
    /// empty cell moves it, on the same item merges up to the limit (the rest stays), on anything else swaps.</item>
    /// <item>Split puts half a stack in the first empty cell; Sort merges stacks and orders them guns, rounds,
    /// throwables, healing, materials, mission things.</item>
    /// <item>A weapon goes into its slot from the bag and the one that was there takes its cell.</item>
    /// <item>Anything but mission things and the knife can be put on the ground (Map01Drops) and picked up again.</item>
    /// </list>
    /// </summary>
    public sealed partial class Map01Inventory
    {
        /// <summary>8 x 5 = 40 cells (24 before 2026-10-22). A save with fewer cells loads into the first ones.</summary>
        public const int BagColumns = 8, BagRows = 5, BagSize = BagColumns * BagRows;
        public enum Slot { Primary, Pistol, Knife }
        /// <summary>One stack. A gun is a stack of one and carries its own condition (durability points, see
        /// Map01Inventory.Condition.cs); for anything else <see cref="condition"/> is <see cref="NoCondition"/>.</summary>
        public sealed class BagCell { public string id; public int count; public float condition = NoCondition; }
        [System.Serializable] public sealed class Layout
        {
            public int fieldKitVersion;
            public string[] ids = new string[BagSize];
            public int[] counts = new int[BagSize];
            public string primary, pistol, throwable;
            /// <summary>Each cell's gun condition (points); absent in saves made before guns wore.</summary>
            public float[] conditions;
            public float primaryCondition = NoCondition, pistolCondition = NoCondition;
        }

        private readonly BagCell[] bag = new BagCell[BagSize];
        private string primary, pistol, knife;
        private float primaryCondition = NoCondition, pistolCondition = NoCondition;
        private string throwable = "stone";
        /// <summary>Raised whenever the bag or the slots change, for anything drawn from them.</summary>
        public event System.Action Changed;

        public static readonly string[] Throwables = { "stone", "smoke_bomb", "grenade_f1" };
        public BagCell Cell(int i) => i >= 0 && i < BagSize ? bag[i] : null;
        public string SlotItem(Slot slot) => slot == Slot.Primary ? primary : slot == Slot.Pistol ? pistol : knife;
        /// <summary>The slot a weapon goes in, or null for anything that is not one.</summary>
        public static Slot? SlotOf(string id) => id == "rifle_standard" || id == "rifle_m16" || id == "rifle_mosin" ? Slot.Primary
            : id == "pistol_k54" ? Slot.Pistol : id == "knife" ? Slot.Knife : (Slot?)null;
        /// <summary>The throwable used by holding and releasing left mouse while slot 4 is equipped.</summary>
        public string Throwable => throwable;
        public bool ThrowableEquipped { get; private set; }
        public int FreeCells => bag.Count(c => c == null);
        public static bool IsMissionItem(string id) => id == "supplies" || id == "river_documents"
            || id == Map01Story.KeyItem || id == Map01Story.CargoItem;
        /// <summary>Weapons with Nam's own animations so far: the others go in their slot but stay holstered.</summary>
        public static bool Drawable(string id) => id == "rifle_standard" || id == Map01M16.Id || id == "knife" || id == "pistol_k54" || id=="rifle_mosin";

        private void ClearAll()
        {
            CancelFieldCraft(false);
            for (int i = 0; i < BagSize; i++) bag[i] = null;
            primary = pistol = knife = null; throwable = "stone"; ThrowableEquipped = false;
            mission?.ModernCombat?.SetHandsOccupied(false);
            primaryCondition = pistolCondition = NoCondition;
        }

        /// <summary>A weapon into its empty slot, then stacks of the same item with room, then empty cells.
        /// Returns how many did not fit. A gun goes in with <paramref name="condition"/> (points; NoCondition = new).</summary>
        private int Store(string id, int amount, float condition = NoCondition)
        {
            if (string.IsNullOrEmpty(id) || amount <= 0) return 0;
            if (id == "stone") amount = Mathf.Min(amount, Mathf.Max(0, StoneCapacity - Stones));
            float worn = Worn(id, condition);
            var slot = SlotOf(id);
            if (slot != null && SlotItem(slot.Value) == null && amount > 0)
            {
                SetSlot(slot.Value, id, worn); amount--;
            }
            int limit = StackLimit(id);
            for (int i = 0; i < BagSize && amount > 0; i++)
            {
                var cell = bag[i];
                if (cell == null || cell.id != id || cell.count >= limit) continue;
                int put = Mathf.Min(amount, limit - cell.count); cell.count += put; amount -= put;
            }
            for (int i = 0; i < BagSize && amount > 0; i++)
            {
                if (bag[i] != null) continue;
                int put = Mathf.Min(amount, limit); bag[i] = new BagCell { id = id, count = put, condition = worn }; amount -= put;
            }
            Changed?.Invoke();
            return amount;
        }

        private void SetSlot(Slot slot, string id, float condition = NoCondition)
        {
            if (slot == Slot.Primary) { primary = id; primaryCondition = id == null ? NoCondition : condition; if (!restoringRifle) ApplyPrimaryRifle(); }
            else if (slot == Slot.Pistol) { pistol = id; pistolCondition = id == null ? NoCondition : condition; }
            else knife = id;
        }

        /// <summary>Moves the stack in <paramref name="from"/> onto <paramref name="to"/>: into it if empty, onto it
        /// if it is the same item with room (what does not fit stays behind), else the two swap.</summary>
        public bool Move(int from, int to)
        {
            if (from == to || Cell(from) == null || to < 0 || to >= BagSize) return false;
            var a = bag[from]; var b = bag[to];
            if (b == null) { bag[to] = a; bag[from] = null; }
            else if (b.id == a.id && b.count < StackLimit(a.id))
            {
                int put = Mathf.Min(a.count, StackLimit(a.id) - b.count);
                b.count += put; a.count -= put;
                if (a.count <= 0) bag[from] = null;
            }
            else { bag[to] = a; bag[from] = b; }
            Changed?.Invoke();
            return true;
        }

        /// <summary>Half the stack (rounded down) into the first empty cell.</summary>
        public bool Split(int i)
        {
            var cell = Cell(i);
            if (cell == null || cell.count < 2) return false;
            int to = System.Array.IndexOf(bag, null);
            if (to < 0) { mission?.Say("Túi hết ô trống để tách.", 3); return false; }
            int half = cell.count / 2; cell.count -= half;
            bag[to] = new BagCell { id = cell.id, count = half };
            Changed?.Invoke();
            return true;
        }

        /// <summary>SẮP XẾP: stacks of an item merged, then guns, rounds, throwables, healing, materials and
        /// mission things, in the bench's own item order within each group.</summary>
        public void Sort()
        {
            var totals = new Dictionary<string, int>();
            // Guns keep their own condition, so they are carried over cell by cell (best first), not re-counted.
            var guns = bag.Where(c => c != null && HasCondition(c.id)).ToList();
            foreach (var c in bag) if (c != null && !HasCondition(c.id)) totals[c.id] = (totals.TryGetValue(c.id, out int n) ? n : 0) + c.count;
            foreach (var g in guns) totals[g.id] = totals.TryGetValue(g.id, out int n) ? n : 0;
            for (int i = 0; i < BagSize; i++) bag[i] = null;
            int at = 0;
            foreach (var p in totals.OrderBy(p => SortGroup(p.Key)).ThenBy(p => OrderIndex(p.Key)).ThenBy(p => p.Key, System.StringComparer.Ordinal))
            {
                if (HasCondition(p.Key))
                {
                    foreach (var g in guns.Where(g => g.id == p.Key).OrderByDescending(g => g.condition))
                        if (at < BagSize) bag[at++] = g;
                    continue;
                }
                int limit = StackLimit(p.Key);
                for (int left = p.Value; left > 0 && at < BagSize; at++)
                {
                    int put = Mathf.Min(left, limit); bag[at] = new BagCell { id = p.Key, count = put }; left -= put;
                }
            }
            Changed?.Invoke();
        }
        public static int SortGroup(string id)
        {
            if (ForestInventory.IsWeapon(id)) return 0;
            if (id.StartsWith("ammo_") && id != RecoveredAmmo) return 1;
            if (System.Array.IndexOf(Throwables, id) >= 0) return 2;
            if (id == "medkit_small" || id == "herb") return 3;
            if (IsMissionItem(id)) return 5;
            return 4;
        }
        private static int OrderIndex(string id) { int i = System.Array.IndexOf(ForestInventory.Order, id); return i < 0 ? int.MaxValue : i; }

        /// <summary>The weapon in cell <paramref name="i"/> into its slot; the one that was there takes the cell.
        /// A drawn weapon taken out of the slot leaves the knife in hand.</summary>
        public bool EquipFromCell(int i)
        {
            if(mission?.MapTwo?.CharacterGearLocked==true){mission.Say("Hoàn tất việc đang mang trước khi đổi trang bị.");return false;}
            var cell = Cell(i);
            var slot = cell != null ? SlotOf(cell.id) : null;
            if (slot == null) { if (cell != null) mission?.Say(ForestInventory.Name(cell.id) + " không trang bị được.", 3); return false; }
            if (slot == Slot.Knife) return false;                          // one knife, always in its slot
            string old = SlotItem(slot.Value);
            float oldCondition = Condition(slot.Value);
            bool wasHeld = old != null && IsEquipped(old);
            SetSlot(slot.Value, cell.id, cell.condition);
            bag[i] = old != null ? new BagCell { id = old, count = 1, condition = oldCondition } : null;
            if (wasHeld) Draw(Slot.Knife);
            Changed?.Invoke();
            return true;
        }

        /// <summary>The weapon in <paramref name="slot"/> back into the bag: into cell <paramref name="to"/> (onto a
        /// weapon of the same kind, they swap) or the first empty one. The knife stays.</summary>
        public bool Unequip(Slot slot, int to = -1)
        {
            string id = SlotItem(slot);
            if (id == null || slot == Slot.Knife) return false;
            if (to >= 0 && Cell(to) != null && SlotOf(Cell(to).id) == slot) return EquipFromCell(to);
            if (to < 0 || Cell(to) != null) to = System.Array.IndexOf(bag, null);
            if (to < 0) { mission?.Say("Túi đã đầy, không cất được.", 3); return false; }
            bool wasHeld = IsEquipped(id);
            bag[to] = new BagCell { id = id, count = 1, condition = Condition(slot) };
            SetSlot(slot, null);
            if (wasHeld) Draw(Slot.Knife);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Puts <paramref name="amount"/> from cell <paramref name="i"/> on the ground at Nam's feet.</summary>
        public bool Drop(int i, int amount)
        {
            var cell = Cell(i);
            if (cell == null || amount <= 0) return false;
            if (IsMissionItem(cell.id)) { mission?.Say("Đồ nhiệm vụ không vứt được.", 3); return false; }
            amount = Mathf.Min(amount, cell.count);
            string id = cell.id;
            cell.count -= amount;
            if (cell.count <= 0) bag[i] = null;
            mission?.Drop(id, amount, cell.condition);
            Changed?.Invoke();
            return true;
        }
        /// <summary>The weapon in <paramref name="slot"/> on the ground. The knife cannot go.</summary>
        public bool DropSlot(Slot slot)
        {
            string id = SlotItem(slot);
            if (id == null) return false;
            if (slot == Slot.Knife) { mission?.Say("Dao luôn mang theo, không vứt được.", 3); return false; }
            bool wasHeld = IsEquipped(id);
            float condition = Condition(slot);
            SetSlot(slot, null);
            if (wasHeld) Draw(Slot.Knife);
            mission?.Drop(id, 1, condition);
            Changed?.Invoke();
            return true;
        }

        /// <summary>Puts an integrated weapon in Nam's hands.</summary>
        public bool Draw(Slot slot)
        {
            CancelFieldCraft();
            if(mission?.MapTwo?.CharacterGearLocked==true)return false;
            string id = SlotItem(slot);
            if (id == null || mission == null || mission.ModernCombat == null) return false;
            if (!Drawable(id))
            {
                mission.Say(ForestInventory.Name(id) + ": chưa có hoạt ảnh cầm (bản thiết kế đang chờ duyệt). Vẫn giữ " +
                    (mission.ModernCombat.EquippedKind == WeaponKind.Knife ? "dao" : "súng trường") + " trên tay.", 4);
                return false;
            }
            if (slot == Slot.Primary) ApplyPrimaryRifle();
            var kind = id == "knife" ? WeaponKind.Knife : id == "pistol_k54" ? WeaponKind.Pistol : id=="rifle_mosin" ? WeaponKind.Sniper : WeaponKind.Rifle;
            SuppressFire = true;
            bool drawn = mission.ModernCombat.Equip(kind) && mission.ModernCombat.EquippedKind == kind;
            if (drawn) {
                bool wasThrowable = ThrowableEquipped;
                ThrowableEquipped = false; GetComponent<Map01StoneThrow>()?.Cancel();
                if (wasThrowable) Map01NamActions.For(mission.MapOne)?.CancelAction();
                mission.ModernCombat.SetHandsOccupied(false);
            }
            return drawn;
        }

        /// <summary>What a weapon slot emptied under him leaves in hand: the knife.</summary>
        private void OnSlotEmptied()
        {
            if (mission != null && mission.ModernCombat != null &&
                ((mission.ModernCombat.EquippedKind == WeaponKind.Rifle && primary != "rifle_standard" && primary != Map01M16.Id) ||
                 (mission.ModernCombat.EquippedKind == WeaponKind.Pistol && pistol != "pistol_k54") ||
                 (mission.ModernCombat.EquippedKind == WeaponKind.Sniper && primary != "rifle_mosin")))
                Draw(Slot.Knife);
        }

        /// <summary>Number keys: 1 long gun, 2 pistol, 3 knife, 4 equip/cycle available throwables.</summary>
        public bool OnSlotKey(int key)
        {
            switch (key)
            {
                case 0: Draw(Slot.Primary); return true;
                case 1: Draw(Slot.Pistol); return true;
                case 2: Draw(Slot.Knife); return true;
                case 3:
                    CycleThrowable(); return true;
                default: return false;
            }
        }

        /// <summary>Select the next throwable he has any of (stone, smoke bomb, F1). With only one kind left the
        /// choice stays on it.</summary>
        public void CycleThrowable()
        {
            var have = Throwables.Where(t => Count(t) > 0).ToList();
            if (have.Count == 0) return;
            int at = have.IndexOf(throwable);
            SelectThrowable(have[at < 0 ? 0 : ThrowableEquipped ? (at + 1) % have.Count : at]);
        }
        public void SelectThrowable(string id)
        {
            CancelFieldCraft();
            if(mission?.MapTwo?.CharacterGearLocked==true)return;
            if (System.Array.IndexOf(Throwables, id) < 0) return;
            GetComponent<Map01StoneThrow>()?.Cancel();
            Map01NamActions.For(mission.MapOne)?.CancelAction();
            throwable = id; ThrowableEquipped = true; ThrowableChosenAt = Time.time;
            mission?.ModernCombat?.SetHandsOccupied(true);
            Changed?.Invoke();
        }
        /// <summary>When key 4 last changed the throwable, for the hotbar's short highlight.</summary>
        public float ThrowableChosenAt { get; private set; } = -10f;
        /// <summary>The picked throwable, or the next one he has if it ran out.</summary>
        public string ReadyThrowable()
        {
            if (Count(throwable) > 0) return throwable;
            if (ThrowableEquipped) return null;
            var next = Throwables.FirstOrDefault(t => Count(t) > 0);
            if (next != null) { throwable = next; Changed?.Invoke(); }
            return next;
        }

        /// <summary>The bag as saved: every cell, the slots, the throwable picked.</summary>
        public Layout CaptureLayout()
        {
            CancelFieldCraft(false);
            var l = new Layout { primary = primary, pistol = pistol, throwable = throwable, conditions = new float[BagSize],
                primaryCondition = primaryCondition, pistolCondition = pistolCondition, fieldKitVersion = fieldKitVersion };
            for (int i = 0; i < BagSize; i++) { l.ids[i] = bag[i]?.id; l.counts[i] = bag[i]?.count ?? 0; l.conditions[i] = bag[i]?.condition ?? NoCondition; }
            return l;
        }
        /// <summary>Puts a saved layout back over what <see cref="RestoreFromSave"/> loaded: cells where they were,
        /// then anything the totals have beyond the layout (materials of crafts still queued at save time) stored
        /// as usual. Totals win where the two disagree.</summary>
        public void RestoreLayout(Layout l)
        {
            if (l == null || l.ids == null || l.counts == null) return;
            var totals = new Dictionary<string, int>(Items);
            for (int i = 0; i < BagSize; i++) bag[i] = null;
            primary = l.primary; pistol = l.pistol;
            // A save from before guns wore has no conditions at all: every gun in it comes back as new.
            bool wore = l.conditions != null;
            primaryCondition = Worn(primary, wore ? l.primaryCondition : NoCondition); pistolCondition = Worn(pistol, wore ? l.pistolCondition : NoCondition);
            knife = totals.ContainsKey("knife") ? "knife" : null;
            throwable = System.Array.IndexOf(Throwables, l.throwable) >= 0 ? l.throwable : "stone";
            var left = new Dictionary<string, int>(totals);
            foreach (var id in new[] { primary, pistol, knife }) if (id != null) left[id] = (left.TryGetValue(id, out int n) ? n : 0) - 1;
            if (primary != null && (!totals.ContainsKey(primary))) SetSlot(Slot.Primary, null);
            if (pistol != null && (!totals.ContainsKey(pistol))) SetSlot(Slot.Pistol, null);
            for (int i = 0; i < Mathf.Min(BagSize, l.ids.Length, l.counts.Length); i++)
            {
                string id = l.ids[i];
                if (string.IsNullOrEmpty(id) || l.counts[i] <= 0) continue;
                int have = left.TryGetValue(id, out int n) ? n : 0;
                int put = Mathf.Min(have, l.counts[i]);
                if (put <= 0) continue;
                float saved = l.conditions != null && i < l.conditions.Length ? l.conditions[i] : NoCondition;
                bag[i] = new BagCell { id = id, count = put, condition = Worn(id, saved) }; left[id] = have - put;
            }
            foreach (var p in left) if (p.Value > 0) Store(p.Key, p.Value);
            Changed?.Invoke();
        }
    }
}
