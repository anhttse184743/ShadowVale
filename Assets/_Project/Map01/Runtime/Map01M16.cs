using System;
using System.Linq;
using UnityEngine;
using ShadowVale.Gameplay.Combat;
namespace ShadowVale.Map01
{
    public static class Map01M16
    {
        public const string Id = "rifle_m16", Ammo = "ammo_556";
        public const float ShotSeconds = 60f / 700f;
        public static readonly Vector3 HeldOffset = new Vector3(0f, -.025f, 0f);
        // Stable shuffled groups keep the same loadout after checkpoint restoration.
        public static string EnemyRifle(Map01Mission mission, Map01EnemyController guard)
        {
            var roster = mission.Enemies.Where(e => e != null).OrderBy(e => e.SaveId, StringComparer.Ordinal).ToArray();
            int index = Array.IndexOf(roster, guard);
            if (index < 0) index = roster.Length;
            return RifleAt(Map01Logistics.For(mission).Seed, index);
        }
        public static string RifleAt(int seed, int index)
        {
            var slots = Enumerable.Range(0, 10).ToArray();
            var rng = new System.Random(unchecked(seed + index / 10 * 7919));
            for (int i = 9; i > 0; --i) { int k = rng.Next(i + 1); (slots[i], slots[k]) = (slots[k], slots[i]); }
            return slots[index % 10] < 7 ? Id : "rifle_standard";
        }
    }
    public sealed partial class Map01Inventory
    {
        bool restoringRifle;
        public string PrimaryAmmo => SlotItem(Slot.Primary) == Map01M16.Id ? Map01M16.Ammo : "ammo_rifle";
        public void ApplyPrimaryRifle(bool unload = true)
        {
            if (mission == null) mission = GetComponent<Map01Mission>();
            var combat = mission != null ? mission.ModernCombat : null;
            if (combat == null) return;
            var gun = combat.GetComponentsInChildren<Weapon>(true).FirstOrDefault(w => w.IsGun);
            if (gun == null) return;
            var visual = gun.GetComponent<Map01RifleVisual>();
            string before = visual != null ? visual.ItemId : "rifle_standard";
            string after = primary == Map01M16.Id ? Map01M16.Id : "rifle_standard";
            if (before != after && unload)
            {
                int rounds = combat.RoundsInMagazine;
                combat.RestoreMagazine(0);
                if (rounds > 0) Receive(before == Map01M16.Id ? Map01M16.Ammo : "ammo_rifle", rounds);
            }
            Map01RifleVisual.Apply(gun, after);
        }
    }
}
