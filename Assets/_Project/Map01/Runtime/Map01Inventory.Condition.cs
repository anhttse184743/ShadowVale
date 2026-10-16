using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Skills;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Gun condition (the approved design "Độ bền, sửa chữa, rã súng", 17/10/2026). Every gun - AK, K-54, Mosin - has
    /// its own condition in durability points (its maximum and its wear per shot are the content's durability_max and
    /// durability_per_shot, as the database's weapons.max_durability / durability_per_use). A gun with condition left
    /// fires as well as a new one; at 0 it does not fire until repaired.
    /// <list type="bullet">
    /// <item>Field repair, anywhere: one repair kit restores 35% of the maximum plus the Engineering bonus, up to the
    /// field cap (80%, more at higher Engineering).</item>
    /// <item>Bench repair, at the workbench in base A: to 100%, for weapon parts (one per started quarter missing, one
    /// fewer from Engineering 3) and a gun oil when half or more is missing. A paid repair to full counts for
    /// Engineering (PlayerSkills.RecordFullRepair).</item>
    /// <item>Salvage: a gun taken apart gives scrap metal and weapon parts (the Mosin also its lens); more parts from a
    /// gun in good condition, more scrap from Engineering 2.</item>
    /// </list>
    /// </summary>
    public sealed partial class Map01Inventory
    {
        public const float NoCondition = -1f;
        /// <summary>Under these fractions a gun shows as worn (amber) and badly worn (red) - a captured gun is badly worn.</summary>
        public const float WornBelow = .5f, BadBelow = .25f;

        /// <summary>A gun in the bag (<see cref="Cell"/>) or in a slot (<see cref="Slot"/>).</summary>
        public readonly struct Gun
        {
            public readonly int Cell; public readonly Slot? Slot;
            private Gun(int cell, Slot? slot) { Cell = cell; Slot = slot; }
            public static Gun InCell(int cell) => new Gun(cell, null);
            public static Gun InSlot(Slot slot) => new Gun(-1, slot);
            public override string ToString() => Slot != null ? "slot " + Slot : "cell " + Cell;
        }

        /// <summary>Guns wear; the knife and everything else do not.</summary>
        public static bool HasCondition(string id) => id == "rifle_standard" || id == "pistol_k54" || id == "rifle_mosin";

        private ForestWeapon WeaponData(string id) => mission?.Bundle?.weapons?.FirstOrDefault(w => w != null && w.id == id);
        /// <summary>A gun's full condition in points (content durability_max).</summary>
        public float MaxCondition(string id) { float m = WeaponData(id)?.durability_max ?? 0; return m > 0 ? m : 100f; }
        /// <summary>Points a shot takes (content durability_per_shot).</summary>
        public float WearPerShot(string id) => Mathf.Max(0f, WeaponData(id)?.durability_per_shot ?? 0f);

        /// <summary>A gun's condition as stored: a new gun (NoCondition) is at its maximum; anything else has none.</summary>
        private float Worn(string id, float condition)
        {
            if (string.IsNullOrEmpty(id) || !HasCondition(id)) return NoCondition;
            float max = MaxCondition(id);
            return condition < 0 ? max : Mathf.Clamp(condition, 0f, max);
        }

        public float Condition(Slot slot) => slot == Slot.Primary ? primaryCondition : slot == Slot.Pistol ? pistolCondition : NoCondition;
        public string IdOf(Gun gun) => gun.Slot != null ? SlotItem(gun.Slot.Value) : Cell(gun.Cell)?.id;
        public float ConditionOf(Gun gun) => gun.Slot != null ? Condition(gun.Slot.Value) : Cell(gun.Cell)?.condition ?? NoCondition;
        private void SetConditionOf(Gun gun, float value)
        {
            string id = IdOf(gun);
            if (!HasCondition(id)) return;
            value = Mathf.Clamp(value, 0f, MaxCondition(id));
            if (gun.Slot == Slot.Primary) primaryCondition = value;
            else if (gun.Slot == Slot.Pistol) pistolCondition = value;
            else if (Cell(gun.Cell) != null) bag[gun.Cell].condition = value;
            Changed?.Invoke();
        }
        /// <summary>0..1 of the gun's maximum; 1 for things without condition.</summary>
        public float Fraction(string id, float condition) => HasCondition(id) && condition >= 0 ? Mathf.Clamp01(condition / MaxCondition(id)) : 1f;
        public float FractionOf(Gun gun) => Fraction(IdOf(gun), ConditionOf(gun));
        public bool IsBroken(Gun gun) => HasCondition(IdOf(gun)) && ConditionOf(gun) <= 0f;
        /// <summary>Every gun he carries, slots first.</summary>
        public IEnumerable<Gun> Guns()
        {
            if (HasCondition(primary)) yield return Gun.InSlot(Slot.Primary);
            if (HasCondition(pistol)) yield return Gun.InSlot(Slot.Pistol);
            for (int i = 0; i < BagSize; i++) if (bag[i] != null && HasCondition(bag[i].id)) yield return Gun.InCell(i);
        }

        /// <summary>The gun in Nam's hands, if a gun with condition is: the slot it is in.</summary>
        public Slot? HeldGun()
        {
            if (HasCondition(primary) && IsEquipped(primary)) return Slot.Primary;
            if (HasCondition(pistol) && IsEquipped(pistol)) return Slot.Pistol;
            return null;
        }
        /// <summary>The gun in hand has worn to nothing: it does not fire until repaired.</summary>
        public bool HeldGunBroken() { var held = HeldGun(); return held != null && IsBroken(Gun.InSlot(held.Value)); }

        /// <summary>Raised when a gun's condition reaches 0 (the gun, its id).</summary>
        public event System.Action<Gun, string> GunBroke;

        /// <summary>One shot's wear on the gun in <paramref name="slot"/>.</summary>
        public void Wear(Slot slot, float points)
        {
            var gun = Gun.InSlot(slot);
            string id = IdOf(gun);
            if (!HasCondition(id) || points <= 0) return;
            float before = ConditionOf(gun);
            SetConditionOf(gun, before - points);
            if (before > 0 && ConditionOf(gun) <= 0) GunBroke?.Invoke(gun, id);
        }

        private Map01Loot Loot => Map01Loot.Load();
        private PlayerSkills Skills => mission != null ? mission.Skills : null;
        private int Engineering => Skills != null ? Skills.Level(SkillKind.Engineering) : 0;

        // ---- field repair ----------------------------------------------------------------------------------------

        /// <summary>How far a repair kit can bring a gun in the field, as a fraction (80% at Engineering 0).</summary>
        public float FieldCap => Skills != null ? Skills.FieldRepairCap : .8f;
        /// <summary>Points a repair kit would restore to <paramref name="gun"/> now (0 if it cannot).</summary>
        public float FieldGain(Gun gun)
        {
            string id = IdOf(gun);
            if (!HasCondition(id)) return 0;
            float max = MaxCondition(id), now = ConditionOf(gun), cap = FieldCap * max;
            if (now >= cap) return 0;
            float gain = Skills != null ? Skills.RepairGain(Loot.repair.fieldFraction * max, max) : Loot.repair.fieldFraction * max;
            return Mathf.Max(0f, Mathf.Min(cap, now + gain) - now);
        }
        /// <summary>Why a field repair of <paramref name="gun"/> cannot be done, or null.</summary>
        public string FieldRepairRefusal(Gun gun)
        {
            string id = IdOf(gun);
            if (!HasCondition(id)) return "Không phải súng.";
            if (Count("repair_kit") <= 0) return "Cần 1 bộ sửa chữa (chế ở bàn: 3 kim loại vụn + 1 dầu súng).";
            if (FractionOf(gun) >= FieldCap - .0001f)
                return $"Sửa dã chiến chỉ tới {FieldCap * 100:0}% — về bàn chế tạo ở căn cứ A để sửa đầy.";
            return null;
        }
        /// <summary>One repair kit on <paramref name="gun"/>, anywhere: the condition goes up at once while Nam kneels over
        /// it (walking off cuts the take short, not the repair).</summary>
        public bool FieldRepair(Gun gun)
        {
            string why = FieldRepairRefusal(gun);
            if (why != null) { mission?.Say(why, 4); return false; }
            string id = IdOf(gun);
            float max = MaxCondition(id), before = ConditionOf(gun), after = before + FieldGain(gun);
            Spend("repair_kit", 1);
            SetConditionOf(gun, after);
            bool full = after >= max - .001f;
            if (full) Skills?.RecordFullRepair(before, max, max, true, System.Guid.NewGuid().ToString("N"));
            mission?.Say($"Đã sửa {ForestInventory.Name(id).ToLowerInvariant()}: {before / max * 100:0}% → {after / max * 100:0}%." +
                (full ? " Đầy độ bền — kỹ năng Kỹ thuật tăng." : $" Sửa dã chiến tối đa {FieldCap * 100:0}%."), 4);
            if (mission != null) Map01NamActions.For(mission)?.Play(Map01NamActions.OpenChest, Loot.repair.fieldSeconds, true);
            return true;
        }

        // ---- bench repair ----------------------------------------------------------------------------------------

        /// <summary>What a bench repair of <paramref name="gun"/> to 100% takes.</summary>
        public (int parts, int oil) BenchCost(Gun gun)
        {
            string id = IdOf(gun);
            if (!HasCondition(id)) return (0, 0);
            float missing = 1f - FractionOf(gun);
            if (missing <= .0001f) return (0, 0);
            int parts = Mathf.CeilToInt(missing * 4f - .0001f) * Mathf.Max(1, Loot.repair.benchQuarterParts);
            if (Engineering >= Loot.repair.benchDiscountLevel) parts = Mathf.Max(1, parts - 1);
            int oil = missing >= Loot.repair.benchOilFrom ? 1 : 0;
            return (parts, oil);
        }
        public string BenchRepairRefusal(Gun gun)
        {
            string id = IdOf(gun);
            if (!HasCondition(id)) return "Không phải súng.";
            if (FractionOf(gun) >= .9999f) return "Súng đã đầy độ bền.";
            if (!NearWorkbench()) return "Đến bàn chế tạo ở căn cứ A để sửa đầy.";
            var (parts, oil) = BenchCost(gun);
            var missing = new List<string>();
            if (Count("weapon_parts") < parts) missing.Add((parts - Count("weapon_parts")) + " linh kiện súng");
            if (Count("gun_oil") < oil) missing.Add((oil - Count("gun_oil")) + " dầu súng");
            return missing.Count > 0 ? "Thiếu " + string.Join(", ", missing) + "." : null;
        }
        /// <summary>At the bench: <paramref name="gun"/> to 100% for its parts and oil.</summary>
        public bool BenchRepair(Gun gun)
        {
            string why = BenchRepairRefusal(gun);
            if (why != null) { mission?.Say(why, 4); return false; }
            string id = IdOf(gun);
            float max = MaxCondition(id), before = ConditionOf(gun);
            var (parts, oil) = BenchCost(gun);
            Spend("weapon_parts", parts);
            if (oil > 0) Spend("gun_oil", oil);
            SetConditionOf(gun, max);
            Skills?.RecordFullRepair(before, max, max, true, System.Guid.NewGuid().ToString("N"));
            mission?.Say($"Sửa xong {ForestInventory.Name(id).ToLowerInvariant()}: {before / max * 100:0}% → 100% ({parts} linh kiện{(oil > 0 ? ", 1 dầu súng" : "")}).", 4);
            return true;
        }

        // ---- salvage --------------------------------------------------------------------------------------------

        /// <summary>What taking <paramref name="gun"/> apart gives.</summary>
        public List<Map01LootRoller.Drop> SalvageYield(Gun gun)
        {
            var list = new List<Map01LootRoller.Drop>();
            string id = IdOf(gun);
            var item = Loot.ItemOf(id);
            if (item?.salvage == null) return list;
            void Add(string what, int n)
            {
                if (n <= 0 || string.IsNullOrEmpty(what)) return;
                int at = list.FindIndex(d => d.item == what);
                if (at >= 0) list[at] = new Map01LootRoller.Drop { item = what, count = list[at].count + n };
                else list.Add(new Map01LootRoller.Drop { item = what, count = n });
            }
            foreach (var y in item.salvage) Add(y.item, y.min);
            if (FractionOf(gun) >= item.salvageGood) Add(item.salvageGoodItem, 1);
            if (Engineering >= Loot.repair.salvageBonusLevel) Add("scrap_metal", 1);
            return list;
        }
        public string SalvageRefusal(Gun gun)
        {
            string id = IdOf(gun);
            if (Loot.ItemOf(id)?.salvage == null) return "Không rã được món này.";
            return null;
        }
        /// <summary>Takes <paramref name="gun"/> apart, anywhere: the gun goes, its parts go into the bag.</summary>
        public bool Salvage(Gun gun)
        {
            string why = SalvageRefusal(gun);
            if (why != null) { mission?.Say(why, 3); return false; }
            string id = IdOf(gun);
            var yields = SalvageYield(gun);
            if (!RemoveGun(gun)) return false;
            foreach (var y in yields) Receive(y.item, y.count);
            mission?.Say($"Đã rã {ForestInventory.Name(id).ToLowerInvariant()}: " +
                string.Join(", ", yields.Select(y => y.count + " " + ForestInventory.Name(y.item).ToLowerInvariant())) + ".", 4);
            if (mission != null) Map01NamActions.For(mission)?.Play(Map01NamActions.OpenChest, Loot.repair.salvageSeconds, true);
            return true;
        }

        /// <summary>Takes a gun out of the bag or its slot for good (salvaged, handed in). A drawn gun leaves the knife in hand.</summary>
        public bool RemoveGun(Gun gun)
        {
            string id = IdOf(gun);
            if (!HasCondition(id)) return false;
            if (gun.Slot != null)
            {
                bool held = IsEquipped(id);
                SetSlot(gun.Slot.Value, null);
                if (held) Draw(Slot.Knife);
            }
            else bag[gun.Cell] = null;
            Changed?.Invoke();
            return true;
        }

        /// <summary>A gun's worth handed in at the base (Map01Logistics): its base value, less as it wears down to the floor.</summary>
        public int TurnInValue(Gun gun)
        {
            string id = IdOf(gun);
            float floor = Loot.logistics.turnInFloor;
            return Mathf.RoundToInt(Loot.BaseValue(id) * (floor + (1f - floor) * FractionOf(gun)));
        }
    }
}
