using System.Collections.Generic;
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
        /// <summary>The player is reloading.</summary>
        public readonly bool PlayerReloading;
        /// <summary>The player, in his sight, is aiming at him.</summary>
        public readonly bool AimedAt;
        /// <summary>Where his men stand (their centre), if any are fighting near him.</summary>
        public readonly Vector3? Squad;

        public BossSense(float now, bool engaged, float healthFraction, Vector3 position, Vector3 playerPosition, bool playerSheltered,
            bool settled = true, bool playerReloading = false, bool aimedAt = false, Vector3? squad = null)
        {
            Now = now; Engaged = engaged; HealthFraction = healthFraction; Position = position;
            PlayerPosition = playerPosition; PlayerSheltered = playerSheltered; Settled = settled;
            PlayerReloading = playerReloading; AimedAt = aimedAt; Squad = squad;
        }
    }

    /// <summary>What the boss should do this frame; the body carries it out.</summary>
    public struct BossDecision
    {
        public bool Relocate;
        /// <summary>interval | hurt | regroup (falling back towards the reinforcements he just called) | aimed (dodging a raised rifle).</summary>
        public string RelocateReason;
        public bool ThrowGrenade;
        public Vector3 GrenadeTarget;
        public bool CallReinforcements;
        /// <summary>The player is reloading: order the men near him to close in now.</summary>
        public bool Push;
        /// <summary>A rifle is on him: duck behind his cover for a moment, staying where he is.</summary>
        public bool Duck;
        public bool Any => Relocate || ThrowGrenade || CallReinforcements || Push || Duck;
    }

    /// <summary>
    /// When a boss relocates, throws a grenade or calls reinforcements (A04) — on top of the shared
    /// FSM, not as a seventh state: relocating is a new position handed to TakeCover, the grenade
    /// and the radio call are actions. Pure and clock-driven, so every rule is unit-tested.
    /// <list type="bullet">
    /// <item>Relocate: after <see cref="BossProfile.RelocateIntervalSeconds"/> (±<see cref="IntervalJitter"/>) of
    /// fighting in one spot, counted from his arrival, or at once after losing
    /// <see cref="BossProfile.RelocateDamageFraction"/> of his health there: a few seconds of fighting
    /// from each cover, then a short hop (firing on the way when the player is ahead) to the next.</item>
    /// <item>Grenade: the player has lingered within <see cref="BossProfile.GrenadeCampRadius"/> of where he settled for
    /// <see cref="BossProfile.GrenadeCampSeconds"/>, sheltered for at least <see cref="BossProfile.GrenadeShelteredShare"/>
    /// of that time (peeking out to shoot does not reset it), in throwing range, cooldown over; sooner
    /// (<see cref="BossProfile.KnownHideCampFactor"/>) where he already hid once. Or, hopping between hides,
    /// within the wider <see cref="BossProfile.GrenadeWideRadius"/> for <see cref="BossProfile.GrenadeWideSeconds"/>,
    /// as sheltered. It lands <see cref="BossProfile.GrenadeFlushOffset"/> beyond the player, away from the
    /// commander's men, so running from it means running at them. A player on the move in the open is shot, not grenaded.</item>
    /// <item>Reading the player (<see cref="BossProfile.ReadsPlayer"/>): aimed at for
    /// <see cref="BossProfile.AimDuckSeconds"/>, he ducks behind his cover; still aimed at once he is out again,
    /// <see cref="BossProfile.AimDodgeSeconds"/> in all, he relocates. The moment the player starts reloading, he
    /// orders his men to push.</item>
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
        /// <summary>An area the player lingers in: where he settled, for how long, how much of it sheltered.</summary>
        private sealed class Area
        {
            public bool In;
            public Vector3 Anchor;
            public float Time, Sheltered;

            public void Track(Vector3 player, bool sheltered, float dt, float radius)
            {
                if (!In || Flat(player - Anchor) > radius)
                {
                    // Somewhere new: the clock starts here.
                    In = true; Anchor = player; Time = 0; Sheltered = 0;
                    return;
                }
                Time += dt;
                if (sheltered) Sheltered += dt;
            }

            public bool Camped(float seconds, float shelteredShare) => Time >= seconds && Sheltered >= shelteredShare * Time;
        }

        // The close area (one hiding place) and the wide one (hopping between hides near each other).
        private readonly Area _zone = new Area(), _wide = new Area();
        private float _lastTick = float.NaN;
        private float _grenadeReadyAt;
        private readonly List<Vector3> _knownHides = new List<Vector3>();
        private float _aimedSince = float.NaN, _lastAimed = float.NegativeInfinity, _duckReadyAt, _dodgeReadyAt, _pushReadyAt;
        private bool _wasReloading;

        /// <summary>Where the player camped long enough to draw a grenade: he is expected there again.</summary>
        public IReadOnlyList<Vector3> KnownHides => _knownHides;

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
                _zone.In = false; _wide.In = false;
                _lastTick = float.NaN;
                _aimedSince = float.NaN;
                _wasReloading = s.PlayerReloading;
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

            if (p.ReadsPlayer)
            {
                // A rifle raised at him: he ducks first, and moves only if it is still on him when he comes out.
                if (s.AimedAt)
                {
                    if (float.IsNaN(_aimedSince) || s.Now - _lastAimed > p.AimForgetSeconds) _aimedSince = s.Now;
                    _lastAimed = s.Now;
                    float aimed = s.Now - _aimedSince;
                    if (!d.Relocate && s.Settled && aimed >= p.AimDodgeSeconds && s.Now >= _dodgeReadyAt)
                    {
                        d.Relocate = true;
                        d.RelocateReason = "aimed";
                        _spotSince = s.Now; _hpAtSpot = s.HealthFraction;
                        _dodgeReadyAt = s.Now + p.DodgeCooldownSeconds;
                        _aimedSince = float.NaN;
                    }
                    else if (!d.Relocate && s.Settled && p.DuckSeconds > 0 && aimed >= p.AimDuckSeconds && s.Now >= _duckReadyAt)
                    {
                        d.Duck = true;
                        _duckReadyAt = s.Now + p.DuckSeconds + p.AimDuckSeconds;
                    }
                }
                else if (s.Now - _lastAimed > p.AimForgetSeconds) _aimedSince = float.NaN;
                // The first moment of a reload is the window to close in.
                if (s.PlayerReloading && !_wasReloading && s.Now >= _pushReadyAt)
                {
                    d.Push = true;
                    _pushReadyAt = s.Now + p.PushCooldownSeconds;
                }
            }
            _wasReloading = s.PlayerReloading;

            float dt = float.IsNaN(_lastTick) ? 0f : Mathf.Max(0f, s.Now - _lastTick);
            _lastTick = s.Now;
            _zone.Track(s.PlayerPosition, s.PlayerSheltered, dt, p.GrenadeCampRadius);
            if (p.GrenadeWideRadius > 0) _wide.Track(s.PlayerPosition, s.PlayerSheltered, dt, p.GrenadeWideRadius);
            bool camped = _zone.Camped(CampSecondsAt(_zone.Anchor), p.GrenadeShelteredShare);
            bool hopping = p.GrenadeWideRadius > 0 && _wide.Camped(p.GrenadeWideSeconds, p.GrenadeShelteredShare);
            if ((camped || hopping) && s.Now >= _grenadeReadyAt)
            {
                float range = Flat(s.PlayerPosition - s.Position);
                // Never at his own feet.
                if (range <= p.GrenadeRange && range > p.GrenadeRadius + 1f)
                {
                    d.ThrowGrenade = true;
                    d.GrenadeTarget = FlushTarget(s.PlayerPosition, s.Squad, p.GrenadeFlushOffset);
                    _grenadeReadyAt = s.Now + p.GrenadeCooldownSeconds;
                    Vector3 hide = camped ? _zone.Anchor : s.PlayerPosition;
                    if (!IsKnownHide(hide)) _knownHides.Add(hide);
                    // Another one only if he stays on.
                    _zone.Time = 0; _zone.Sheltered = 0; _wide.Time = 0; _wide.Sheltered = 0;
                }
            }
            return d;
        }

        /// <summary>How long a camp must last before a grenade: shorter where the player already hid.</summary>
        private float CampSecondsAt(Vector3 anchor)
            => Profile.GrenadeCampSeconds * (IsKnownHide(anchor) ? Mathf.Clamp01(Profile.KnownHideCampFactor) : 1f);

        private bool IsKnownHide(Vector3 at)
        {
            foreach (var h in _knownHides)
                if (Flat(h - at) <= Profile.GrenadeCampRadius) return true;
            return false;
        }

        /// <summary>
        /// Where to throw so the blast drives the player towards the commander's men: <paramref name="offset"/>
        /// beyond him on the side away from them (still inside the blast). With no men near, or offset 0, on him.
        /// </summary>
        public static Vector3 FlushTarget(Vector3 player, Vector3? squad, float offset)
        {
            if (squad == null || offset <= 0) return player;
            Vector3 away = player - squad.Value; away.y = 0;
            return away.sqrMagnitude < 1e-4f ? player : player + away.normalized * offset;
        }

        /// <summary>After a checkpoint restore: whether he already called for help.</summary>
        public void Restore(bool reinforcementsCalled) => ReinforcementsCalled = reinforcementsCalled;

        private static float Flat(Vector3 v) => new Vector2(v.x, v.z).magnitude;
    }
}
