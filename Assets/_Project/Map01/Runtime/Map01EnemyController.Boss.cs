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
    /// The commander: a regular guard made tougher and longer-sighted, or set from a boss profile.
    /// </summary>
    public sealed partial class Map01EnemyController
    {
        public bool IsBoss { get; private set; }
        /// <summary>
        /// Turns a regular outpost guard clone into the map's commander: no different model yet
        /// (Map 1's briefing doesn't have one), just a tougher, longer-sighted stand its ground.
        /// </summary>
        public void ConfigureAsBoss(float healthMultiplier, float damageMultiplier, float visionMultiplier = 1.2f)
        {
            IsBoss = true;
            damage *= damageMultiplier;
            visionRange *= visionMultiplier; attackRange *= visionMultiplier;
            var health = GetComponent<Health>();
            if (health != null) health.SetMaxHealth(health.Max * healthMultiplier);
            SquadId = "boss";
            EnemyRegen.Detach(gameObject); // His wounds stay: the fight against him wears him down.
            SyncTuning();
        }

        /// <summary>The commander from his archetype's boss profile (Map01Content.json): his own damage, rate of
        /// fire, reach and eye, where the profile gives them, over a guard's times the multipliers.</summary>
        public void ConfigureAsBoss(ShadowVale.Data.Content.BossProfile profile)
        {
            ConfigureAsBoss(profile.HealthMultiplier, profile.DamageMultiplier, profile.VisionMultiplier);
            if (profile.DamagePerRound > 0) damage = profile.DamagePerRound;
            if (profile.FireIntervalSeconds > 0) fireInterval = profile.FireIntervalSeconds;
            if (profile.AttackRange > 0) attackRange = profile.AttackRange;
            if (profile.DetectionSeconds > 0) _baseDetectionSeconds = _tuning.DetectionSeconds = profile.DetectionSeconds;
            ConfigureBursts(profile.BurstRounds, profile.BurstGapSeconds, profile.BurstIntervalSeconds);
            SyncTuning();
        }

        /// <summary>The commander's body from his archetype (boss_commander): walking speed and sight. 0 keeps the guard's.</summary>
        public void ApplyBossArchetype(float moveSpeed, float vision, float visionAngleDegrees)
        {
            EnsureBrain();
            if (moveSpeed > 0 && _agent != null) _agent.speed = _body.BaseSpeed = moveSpeed;
            if (vision > 0) visionRange = vision;
            if (visionAngleDegrees > 0) visionAngle = visionAngleDegrees;
            SyncTuning();
        }

        public float VisionAngle => visionAngle;
        public float AttackRange => attackRange;
        public float DetectionSeconds => _tuning.DetectionSeconds;
        public float MoveSpeed => _body != null ? _body.BaseSpeed : 0f;
    }
}
