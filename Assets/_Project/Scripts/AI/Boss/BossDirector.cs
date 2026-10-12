using ShadowVale.Data.Content;
using UnityEngine;

namespace ShadowVale.AI.Boss
{
    /// <summary>What the boss knows this frame.</summary>
    public readonly struct BossSense
    {
        public readonly float Now;
        public readonly bool Engaged;
        public readonly float HealthFraction;
        public readonly Vector3 Position;
        /// <summary>Last known (seen, shot at or radioed) player position.</summary>
        public readonly Vector3 PlayerPosition;
        /// <summary>The player is out of the boss's sight or in cover — sitting it out.</summary>
        public readonly bool PlayerSheltered;
        /// <summary>At his position (or given up on reaching it), not on the way there.</summary>
        public readonly bool Settled;

        public BossSense(float now, bool engaged, float healthFraction, Vector3 position, Vector3 playerPosition, bool playerSheltered,
            bool settled = true)
        {
            Now = now; Engaged = engaged; HealthFraction = healthFraction; Position = position;
            PlayerPosition = playerPosition; PlayerSheltered = playerSheltered; Settled = settled;
        }
    }

    /// <summary>What the boss should do this frame; the body carries it out.</summary>
    public struct BossDecision
    {
        public bool Relocate;
        /// <summary>interval | hurt | regroup (falling back towards the reinforcements he just called).</summary>
        public string RelocateReason;
        public bool ThrowGrenade;
        public Vector3 GrenadeTarget;
        public bool CallReinforcements;
        public bool Any => Relocate || ThrowGrenade || CallReinforcements;
    }

    /// <summary>
    /// When a boss relocates, throws a grenade or calls reinforcements (A04) — on top of the shared
    /// FSM, not as a seventh state: relocating is a new position handed to TakeCover, the grenade
    /// and the radio call are actions. Pure and clock-driven, so every rule is unit-tested.
    /// <list type="bullet">
    /// <item>Relocate: after <see cref="BossProfile.RelocateIntervalSeconds"/> (±<see cref="IntervalJitter"/>) of
    /// fighting in one spot, counted from his arrival, or at once after losing
    /// <see cref="BossProfile.RelocateDamageFraction"/> of his health there. A short interval keeps him
    /// on the move: run (firing), stop briefly, run on.</item>
    /// <item>Grenade: the player has stayed within <see cref="BossProfile.GrenadeCampRadius"/> for
    /// <see cref="BossProfile.GrenadeCampSeconds"/>, sheltered, in throwing range, cooldown over.</item>
    /// <item>Reinforcements: once, the first time health falls below <see cref="BossProfile.ReinforceHpThreshold"/> in a fight,
    /// with a relocation away from the player ("regroup") so he lives until they arrive.</item>
    /// </list>
    /// </summary>
    public sealed class BossDirector
    {
        public BossProfile Profile { get; }
        public bool ReinforcementsCalled { get; private set; }

        /// <summary>Each stop lasts the interval times 1 ± this, so the rhythm cannot be timed.</summary>
        public const float IntervalJitter = .3f;

        private readonly System.Random _rng;
        private bool _fighting;
        private float _spotSince, _hpAtSpot = 1f, _interval;
        private float _campSince = float.NaN;
        private Vector3 _campAnchor;
        private float _grenadeReadyAt;

        /// <param name="rng">Varies each stop's length; null = exactly the profile's interval.</param>
        public BossDirector(BossProfile profile, System.Random rng = null)
        {
            Profile = profile ?? new BossProfile();
            _rng = rng;
            _interval = NextInterval();
        }

        private float NextInterval()
        {
            float i = Profile.RelocateIntervalSeconds;
            return _rng == null ? i : i * (1f + IntervalJitter * (float)(_rng.NextDouble() * 2 - 1));
        }

        public BossDecision Tick(in BossSense s)
        {
            var d = new BossDecision();
            var p = Profile;
            if (!s.Engaged)
            {
                _fighting = false;
                _campSince = float.NaN;
                return d;
            }
            if (!_fighting)
            {
                // A fight starts: he fights from here first, and the first grenade waits for a real camp.
                _fighting = true;
                _spotSince = s.Now; _hpAtSpot = s.HealthFraction;
                _grenadeReadyAt = Mathf.Max(_grenadeReadyAt, s.Now);
            }

            if (!ReinforcementsCalled && p.ReinforceHpThreshold > 0 && s.HealthFraction < p.ReinforceHpThreshold)
            {
                ReinforcementsCalled = true;
                d.CallReinforcements = true;
                // And falls back towards them rather than holding a spot the player is winning.
                d.Relocate = true;
                d.RelocateReason = "regroup";
                _spotSince = s.Now; _hpAtSpot = s.HealthFraction;
            }
            else if (!s.Settled)
            {
                // On the way: the stop and the damage taken there count from his arrival.
                _spotSince = s.Now; _hpAtSpot = s.HealthFraction;
            }

            bool overstayed = p.RelocateIntervalSeconds > 0 && s.Now - _spotSince >= _interval;
            bool hurtHere = p.RelocateDamageFraction > 0 && _hpAtSpot - s.HealthFraction >= p.RelocateDamageFraction;
            if (overstayed || hurtHere)
            {
                d.Relocate = true;
                d.RelocateReason = hurtHere ? "hurt" : "interval";
                _spotSince = s.Now; _hpAtSpot = s.HealthFraction;
                _interval = NextInterval();
            }

            if (!s.PlayerSheltered || Flat(s.PlayerPosition - _campAnchor) > p.GrenadeCampRadius || float.IsNaN(_campSince))
            {
                _campAnchor = s.PlayerPosition;
                _campSince = s.PlayerSheltered ? s.Now : float.NaN;
            }
            else if (s.Now - _campSince >= p.GrenadeCampSeconds && s.Now >= _grenadeReadyAt)
            {
                float range = Flat(s.PlayerPosition - s.Position);
                // Never at his own feet.
                if (range <= p.GrenadeRange && range > p.GrenadeRadius + 1f)
                {
                    d.ThrowGrenade = true;
                    d.GrenadeTarget = s.PlayerPosition;
                    _grenadeReadyAt = s.Now + p.GrenadeCooldownSeconds;
                    _campSince = s.Now; // Another one only if he stays put again.
                }
            }
            return d;
        }

        /// <summary>After a checkpoint restore: whether he already called for help.</summary>
        public void Restore(bool reinforcementsCalled) => ReinforcementsCalled = reinforcementsCalled;

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
