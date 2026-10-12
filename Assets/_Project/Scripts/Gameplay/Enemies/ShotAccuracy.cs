using System;
using ShadowVale.AI.Contracts;
using ShadowVale.Gameplay.Player;
using UnityEngine;

namespace ShadowVale.Gameplay.Enemies
{
    /// <summary>
    /// How well an enemy shoots, as numbers a map can tune (Map 1: <c>guardAccuracy</c> in
    /// Map01Balance.json). Until 12/10/2026 every aimed shot with a clear line hit.
    /// </summary>
    [Serializable]
    public sealed class ShotAccuracyProfile
    {
        /// <summary>Off = every shot with a clear line hits (the old behaviour).</summary>
        public bool enabled = true;
        /// <summary>Hit chance at <see cref="closeDistance"/> or nearer, against a still, upright target.</summary>
        public float closeHit = .85f;
        public float closeDistance = 5f;
        /// <summary>Hit chance at the edge of his attack range; linear in between.</summary>
        public float farHit = .35f;
        // Multipliers for the target.
        public float sprintingTarget = .55f;
        public float walkingTarget = .8f;
        public float crouchedTarget = .8f;
        /// <summary>Visible but in cover: only part of him shows.</summary>
        public float coverTarget = .6f;
        /// <summary>Firing on the move.</summary>
        public float movingShooter = .6f;
        /// <summary>Aim settles over this long after he starts firing at the target: the first shot at
        /// <see cref="firstShot"/> of the full chance, rising to all of it.</summary>
        public float settleSeconds = 2.5f;
        public float firstShot = .5f;
        /// <summary>A commander shoots better: his chance times this.</summary>
        public float bossSkill = 1.25f;
        public float minHit = .05f, maxHit = .95f;
    }

    /// <summary>What decides one shot.</summary>
    public readonly struct ShotContext
    {
        public readonly float Distance, AttackRange, AimedFor;
        public readonly bool TargetSprinting, TargetMoving, TargetCrouched, TargetInCover, ShooterMoving, Boss;

        public ShotContext(float distance, float attackRange, float aimedFor, bool targetSprinting, bool targetMoving,
            bool targetCrouched, bool targetInCover, bool shooterMoving, bool boss)
        {
            Distance = distance; AttackRange = attackRange; AimedFor = aimedFor;
            TargetSprinting = targetSprinting; TargetMoving = targetMoving; TargetCrouched = targetCrouched;
            TargetInCover = targetInCover; ShooterMoving = shooterMoving; Boss = boss;
        }
    }

    /// <summary>
    /// Whether an enemy's shot hits, shared by every enemy (Map 1's guards and commander, the generic
    /// <see cref="EnemyAgent"/>). Pure, so every rule is unit-tested; the roll uses the shooter's own
    /// seeded generator, so a replay with the same seed fires the same hits.
    /// </summary>
    public static class ShotAccuracy
    {
        public static float HitChance(ShotAccuracyProfile p, in ShotContext c)
        {
            if (p == null || !p.enabled) return 1f;
            float span = Mathf.Max(.01f, c.AttackRange - p.closeDistance);
            float chance = Mathf.Lerp(p.closeHit, p.farHit, Mathf.Clamp01((c.Distance - p.closeDistance) / span));
            if (c.TargetSprinting) chance *= p.sprintingTarget;
            else if (c.TargetMoving) chance *= p.walkingTarget;
            if (c.TargetCrouched) chance *= p.crouchedTarget;
            if (c.TargetInCover) chance *= p.coverTarget;
            if (c.ShooterMoving) chance *= p.movingShooter;
            if (p.settleSeconds > 0) chance *= Mathf.Lerp(p.firstShot, 1f, Mathf.Clamp01(c.AimedFor / p.settleSeconds));
            if (c.Boss) chance *= p.bossSkill;
            return Mathf.Clamp(chance, p.minHit, p.maxHit);
        }

        /// <summary>The context for a shot at the player, read off the scene.</summary>
        public static ShotContext For(Vector3 shooter, Vector3 shooterVelocity, float attackRange, float aimedFor, bool boss,
            Vector3 target, IPlayerObservable view, PlayerController controller)
        {
            Vector3 flat = target - shooter; flat.y = 0;
            return new ShotContext(flat.magnitude, attackRange, aimedFor,
                controller != null && controller.IsSprinting && controller.IsMoving,
                controller != null && controller.IsMoving,
                view != null && view.IsCrouching,
                view != null && view.IsInCover,
                new Vector3(shooterVelocity.x, 0, shooterVelocity.z).sqrMagnitude > .25f,
                boss);
        }

        /// <summary>
        /// Where a miss goes: beside or over the target (0.75–1.4 m off, clear of his body) and on past
        /// him, so its tracer visibly flies by and lands behind.
        /// </summary>
        public static Vector3 MissPoint(Vector3 from, Vector3 target, System.Random rng)
        {
            Vector3 dir = target - from;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            dir.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            side.Normalize();
            float sign = rng.NextDouble() < .5 ? -1f : 1f;
            float off = .75f + (float)rng.NextDouble() * .65f; // Clear of his capsule (r ≈ 0.5).
            float lift = -.3f + (float)rng.NextDouble() * .9f;
            // Off by that much where he stands, then on along the same line.
            Vector3 beside = target + side * (sign * off) + Vector3.up * lift - from;
            return from + beside.normalized * (beside.magnitude + 8f);
        }

        /// <summary>A generator per shooter, from his id: the same guard rolls the same way every run.</summary>
        public static System.Random RngFor(string id)
        {
            unchecked
            {
                int h = 17;
                foreach (char ch in id ?? "") h = h * 31 + ch;
                return new System.Random(h);
            }
        }
    }

    /// <summary>How long a shooter has been firing at his target: resets after a pause.</summary>
    public sealed class AimTracker
    {
        /// <summary>A gap longer than this between shots and his aim starts over.</summary>
        public const float ResetSeconds = 3f;
        private float _since = float.NegativeInfinity, _last = float.NegativeInfinity;

        /// <summary>Call on each aimed shot; returns the seconds he has been aiming, this shot included.</summary>
        public float Shot(float now)
        {
            if (now - _last > ResetSeconds) _since = now;
            _last = now;
            return now - _since;
        }
    }
}
