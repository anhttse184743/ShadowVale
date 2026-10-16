using System;
using UnityEngine;

namespace ShadowVale.Map01
{
    // Map-specific tutorial balance is authored in Map01Balance.json; weapon/guard values
    // come from the existing offline content bundle, without a web/solver dependency.
    [Serializable]
    public sealed class ForestSettings
    {
        public float playerHP, stamina, walkSpeed, sprintSpeed, crouchSpeed;
        public float staminaDrain, staminaRecovery, interactRange, stoneRange, stoneNoise;
        public float guardDamageScale, guardShotInterval, detectionSeconds, hiddenVisionScale, crouchVisionScale;
        public float medkitHeal, followDistance;
        /// <summary>Hùng's health while he is in the line of fire (the rescue and the walk home).</summary>
        public float hungHP;
        /// <summary>How long a guard looks around where he heard something before heading back.</summary>
        public float searchSeconds;
        public int startingStones;
        /// <summary>Rounds for the K-54 Nam starts with in slot 2.</summary>
        public int startingPistolRounds;
        /// <summary>No longer read: a fallen guard's pack is rolled from his loot table (Resources/Loot/Map01Loot.json).
        /// Kept so older balance files still parse.</summary>
        public int guardDropAmmo, guardDropMedkits;
        /// <summary>How many stones fit in his pockets. More lie around the map (Map01StoneSites).</summary>
        public int stoneCapacity;
        /// <summary>TEMPORARY, for testing the bench: Nam starts with plenty of every material and the camp
        /// chests' weapons (Map01Inventory.TestCraftingKit). Set false in Map01Balance.json to play normally.</summary>
        public bool testCraftingKit;
    }

    [Serializable] public sealed class ForestBundle
    {
        public ForestItem[] items;
        public ForestWeapon[] weapons;
        public ForestArchetype[] enemy_archetypes;
        public ForestRecipe[] craft_recipes;
    }
    [Serializable] public sealed class ForestItem { public string id; public int stack_max; }
    [Serializable] public sealed class ForestWeapon
    {
        public string id;
        public float damage, range, noise_radius, fire_rate;
        /// <summary>Full condition in durability points, and what one shot takes off it (database weapons.max_durability,
        /// durability_per_use).</summary>
        public float durability_max, durability_per_shot;
    }
    [Serializable] public sealed class ForestArchetype
    {
        public string id;
        public float max_hp, move_speed, vision_range, vision_angle, hearing_range, alert_decay_seconds;
    }
    /// <summary>An item and how many. A gun lying somewhere (a chest, a fallen guard, a pile) also carries its condition in
    /// durability points; -1 (anything else, or a new gun) means none (Map01Inventory.Condition.cs).</summary>
    [Serializable] public sealed class ForestIngredient { public string item_id; public int count; public float condition = -1f; }
    [Serializable] public sealed class ForestRecipe
    {
        public string id, output_item_id;
        public int output_count;
        public float craft_seconds;
        public ForestIngredient[] inputs;
    }
}
