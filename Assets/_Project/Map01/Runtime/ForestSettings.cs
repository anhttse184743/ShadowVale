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
        /// <summary>Share of normal detection time after noticing a body. Missing/invalid values use the existing 0.75 default.</summary>
        public float waryDetectionScale = .75f;
        /// <summary>How long an engaged guard keeps firing at the spot he last saw Nam before hunting him.</summary>
        public float suppressSeconds;
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
        public ForestIngredient[] startingItems;
        public float tutorialSlowScale = .2f, tutorialPulseSeconds = 1.1f;
        public float tutorialSprintDistance = 8, tutorialCrouchDistance = 4, tutorialScanSeconds = 2;
        public float tutorialScanRange = 45, tutorialScanAngle = 15, tutorialPracticeDamage = 25;
        public float tutorialCaptureNoise = 35;
        public int tutorialMagazine = 10;
        /// <summary>How well guards (and the commander) shoot; missing from the JSON = the shared defaults.</summary>
        public ShadowVale.Gameplay.Enemies.ShotAccuracyProfile guardAccuracy = new ShadowVale.Gameplay.Enemies.ShotAccuracyProfile();
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
        public bool suppressed;
        public int magazine_size;
        public float reload_seconds;
        /// <summary>Full condition in durability points, and what one shot takes off it (database weapons.max_durability,
        /// durability_per_use).</summary>
        public float durability_max, durability_per_shot;
    }
    [Serializable] public sealed class ForestArchetype
    {
        public string id;
        public float max_hp, move_speed, vision_range, vision_angle, hearing_range, alert_decay_seconds;
        /// <summary>Share of max health below which a fighting guard falls back (Retreat state); 0 = never.</summary>
        public float retreat_hp_threshold;
        public bool is_boss;
        /// <summary>Only read for <see cref="is_boss"/>; missing fields keep Map 1's tuned defaults.</summary>
        public ForestBossProfile boss = new ForestBossProfile();
    }
    /// <summary>JsonUtility mirror of <see cref="ShadowVale.Data.Content.BossProfile"/> (snake_case fields, same defaults).</summary>
    [Serializable] public sealed class ForestBossProfile
    {
        private static readonly ShadowVale.Data.Content.BossProfile D = new ShadowVale.Data.Content.BossProfile();
        public float health_multiplier = D.HealthMultiplier, damage_multiplier = D.DamageMultiplier, vision_multiplier = D.VisionMultiplier;
        public float relocate_interval_seconds = D.RelocateIntervalSeconds, relocate_damage_fraction = D.RelocateDamageFraction;
        public float relocate_min_distance = D.RelocateMinDistance, relocate_max_distance = D.RelocateMaxDistance;
        public float relocate_min_player_distance = D.RelocateMinPlayerDistance;
        public float grenade_camp_seconds = D.GrenadeCampSeconds, grenade_camp_radius = D.GrenadeCampRadius;
        public float grenade_cooldown_seconds = D.GrenadeCooldownSeconds, grenade_range = D.GrenadeRange, grenade_radius = D.GrenadeRadius;
        public float grenade_damage = D.GrenadeDamage, grenade_fuse_seconds = D.GrenadeFuseSeconds, grenade_noise_radius = D.GrenadeNoiseRadius;
        public float reinforce_hp_threshold = D.ReinforceHpThreshold;
        public int reinforce_count = D.ReinforceCount;
        public float reinforce_radius = D.ReinforceRadius;

        public ShadowVale.Data.Content.BossProfile ToProfile() => new ShadowVale.Data.Content.BossProfile
        {
            HealthMultiplier = health_multiplier, DamageMultiplier = damage_multiplier, VisionMultiplier = vision_multiplier,
            RelocateIntervalSeconds = relocate_interval_seconds, RelocateDamageFraction = relocate_damage_fraction,
            RelocateMinDistance = relocate_min_distance, RelocateMaxDistance = relocate_max_distance,
            RelocateMinPlayerDistance = relocate_min_player_distance,
            GrenadeCampSeconds = grenade_camp_seconds, GrenadeCampRadius = grenade_camp_radius,
            GrenadeCooldownSeconds = grenade_cooldown_seconds, GrenadeRange = grenade_range, GrenadeRadius = grenade_radius,
            GrenadeDamage = grenade_damage, GrenadeFuseSeconds = grenade_fuse_seconds, GrenadeNoiseRadius = grenade_noise_radius,
            ReinforceHpThreshold = reinforce_hp_threshold, ReinforceCount = reinforce_count, ReinforceRadius = reinforce_radius,
        };
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
