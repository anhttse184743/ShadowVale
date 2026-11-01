using System.Linq;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Fsm;
using ShadowVale.AI.Perception;
using ShadowVale.Core.Events;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Enemies;
using ShadowVale.Gameplay.Player;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The guard's pack and guns, laid beside him when he falls (Map01Logistics).
    /// </summary>
    public sealed partial class Map01EnemyController
    {
        public bool IsSupplyKeyKeeper => carriesSupplyKey && operationIndex == 1 && _mission != null &&
            _mission.GetComponent<Map01Story>()?.Layout?.keyKeeper == this;

        private void CreateLoot()
        {
            if (_lootCreated || _mission == null) return;
            _lootCreated = true;
            if (suppressOperationLoot) return;
            var loot = gameObject.AddComponent<ForestPoint>();
            loot.id = "loot_" + LegacyId; loot.kind = ForestPointKind.Loot;
            _mission.RegisterPoint(loot);
            FillLoot();
        }

        /// <summary>Gun piles of this guard: his rifle, and the commander's K-54.</summary>
        private string GunPoint(int index) => Map01Logistics.GunPointPrefix + LegacyId + (index == 0 ? "" : "_sidearm");

        /// <summary>
        /// His pack, rolled from his kind's loot table (Map01Logistics.GuardPack), and his guns, worn, laid on the ground
        /// beside him; the rifle in his hands goes with them. Called again after a load, which may bring back the rolls
        /// of another run.
        /// </summary>
        public void FillLoot()
        {
            var loot = _lootCreated && !Alive && _mission != null && !suppressOperationLoot ? GetComponent<ForestPoint>() : null;
            if (loot == null) return;
            var logistics = Map01Logistics.For(_mission);
            var pack = new System.Collections.Generic.List<ForestIngredient>(logistics.GuardPack(this, loot.id));
            if (IsSupplyKeyKeeper) pack.Add(new ForestIngredient { item_id = Map01Story.KeyItem, count = 1 });
            loot.items = pack.ToArray();
            loot.label = IsSupplyKeyKeeper ? "Lính gác kho — chìa khóa kho tiếp tế" : logistics.PackName(this);
            int index = 0;
            foreach (var gun in logistics.GuardGuns(this, loot.id))
            {
                // The rifle drops at his right side, the pistol at his left, lying across his line.
                float side = index == 0 ? 1f : -1f;
                var at = transform.position + transform.right * (.75f * side) + transform.forward * .2f;
                Map01Drops.For(_mission).Lay(GunPoint(index), gun, at, transform.eulerAngles.y + 90f + 25f * index);
                index++;
            }
            if (index > 0) ShowRifle(false);
        }
    }
}
