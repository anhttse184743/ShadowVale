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
        public float guardDamageScale, guardShotInterval, detectionSeconds, hiddenVisionScale;
        public float medkitHeal, followDistance;
        public int startingAmmo, startingStones;
    }

    [Serializable] public sealed class ForestBundle
    {
        public ForestWeapon[] weapons;
        public ForestArchetype[] enemy_archetypes;
        public ForestRecipe[] craft_recipes;
    }
    [Serializable] public sealed class ForestWeapon
    {
        public string id;
        public float damage, range, noise_radius, fire_rate;
    }
    [Serializable] public sealed class ForestArchetype
    {
        public string id;
        public float max_hp, move_speed, vision_range, vision_angle, hearing_range, alert_decay_seconds;
    }
    [Serializable] public sealed class ForestIngredient { public string item_id; public int count; }
    [Serializable] public sealed class ForestRecipe
    {
        public string id, output_item_id;
        public int output_count;
        public float craft_seconds;
        public ForestIngredient[] inputs;
    }
}
