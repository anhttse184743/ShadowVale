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
    /// How a Map 1 guard fires (IEnemyWeapon: bursts, the M16 fire cycle, rounds) and his tactics on top of
    /// the shared FSM (IEnemyTactics: Hùng as a second target, cover, retreat).
    /// </summary>
    public sealed partial class Map01EnemyController
    {
        /// <summary>Fires <paramref name="rounds"/> at a time, <paramref name="gap"/> apart, <paramref name="interval"/> from one burst to the next.</summary>
        public void ConfigureBursts(int rounds, float gap, float interval)
        {
            _burstRounds = Mathf.Max(1, rounds); _burstLeft = _burstRounds;
            _burstGap = Mathf.Max(.02f, gap); _burstInterval = interval;
        }

        public int BurstRounds => UsesGuardM16Cycle ? GuardFire.BurstRounds : _burstRounds;

        // Other ordinary M16 guards wait their turn to start a burst. This never grants a contact or looks up Nam.
        private bool CanStartGuardBurst()
        {
            if (!UsesGuardM16Cycle || _mission == null) return true;
            if (_guardFireCycle.BurstShots > 0 && _guardBurstLeaseUntil > Time.time) return true;
            int active = _mission.Enemies.Count(g => g != null && g != this && g.Alive && g.enabled
                && g.UsesGuardM16Cycle && g._guardBurstLeaseUntil > Time.time);
            if (active < Mathf.Clamp(GuardFire.maxConcurrentBursts, 1, 4)) return true;
            _nextShot = Time.time + .15f + GuardFireStagger;
            return false;
        }

        /// <summary>Until the next round may go: inside a burst the gap, after its last round the pause.</summary>
        private float NextShotDelay()
        {
            if (UsesGuardM16Cycle) return _guardFireCycle.AfterShot(GuardFire, Map01M16.ShotSeconds, GuardFireStagger);
            if (_burstRounds <= 1) return fireInterval;
            if (--_burstLeft > 0) return _burstGap;
            _burstLeft = _burstRounds;
            return _burstInterval > 0 ? _burstInterval : fireInterval;
        }

        private void ScheduleNextShot()
        {
            float delay = NextShotDelay();
            // NPCs never repay overdue rounds after a slow frame; each visible shot starts a fresh deadline.
            _nextShot = Time.time + delay;
            if (UsesGuardM16Cycle) _guardBurstLeaseUntil = Time.time + .2f;
        }

        // ---- IEnemyWeapon: how a Map 1 guard fires ----------------------------------------------

        /// <summary>The old name of <see cref="FireAtPlayer"/>, kept for tests that still call it.</summary>
        private void Shoot() => FireAtPlayer();

        public void FireAtPlayer()
        {
            if (Time.time < _nextShot || _player == null || !CanStartGuardBurst()) return;
            ScheduleNextShot();
            AudibleShots++;
            _animator?.SetTrigger(PlayerCombat.AnimatorParams.Attack);
            // Hùng is in the line of fire while he is being held or walked home: every third
            // round goes his way when he is in reach.
            if (++_shots % 3 == 0 && HostageInReach(out var hostage) && SafeShot(hostage)) { FireAtHostage(hostage, damage * (RifleItemId == Map01M16.Id ? Map01M16.DamageRelativeToAk : 1f)); return; }
            Vector3 aim = _player.position + Vector3.up;
            // Not every round finds him (ShotAccuracy): a miss flies past him, tracer and all.
            var shot = ShotAccuracy.For(transform.position, _agent != null ? _agent.velocity : Vector3.zero, attackRange,
                _aim.Shot(Time.time), IsBoss, _player.position, _playerView, _playerController);
            _shotRng ??= ShotAccuracy.RngFor(LegacyId);
            if (_shotRng.NextDouble() >= ShotAccuracy.HitChance(_accuracy, shot))
            {
                Vector3 chest = transform.position + Vector3.up * 1.3f;
                FireRound(ShotAccuracy.MissPoint(chest, aim, _shotRng), null, out _);
                return;
            }
            if (FireRound(aim, _player, out var impact))
                _playerHealth.TakeBulletDamage(damage * (RifleItemId == Map01M16.Id ? Map01M16.DamageRelativeToAk : 1f), impact, gameObject, impact - transform.position);
        }

        /// <summary>Rounds at the spot Nam was last seen: noise and muzzle flash, no hit.</summary>
        public void FireSuppressive(Vector3 point)
        {
            if (Time.time < _nextShot || !CanStartGuardBurst()) return;
            ScheduleNextShot();
            AudibleShots++;
            _animator?.SetTrigger(PlayerCombat.AnimatorParams.Attack);
            FireRound(point + Vector3.up, null, out _);
        }

        private static AudioClip rifleReport;
        /// <summary>The guards' rifle report: the AK of the extraction film, not Nam's own gunshot.</summary>
        private static AudioClip RifleReport => rifleReport != null ? rifleReport
            : rifleReport = Resources.Load<Map01Extraction>("Cutscenes/Map01Extraction")?.rifleAudio;
        private Light _muzzleFlash;
        private float _flashUntil;

        /// <summary>One round through the shared feedback (tracer, report heard within 65 m), with his rifle's muzzle flash.</summary>
        private bool FireRound(Vector3 at, Transform victim, out Vector3 impact, bool cinematicListener = false)
        {
            _feedback ??= EnemyFeedback.Attach(gameObject);
            _feedback.GunshotClip ??= RifleReport;
            bool reached = _feedback.Fire(at, victim, obstructionMask, out impact, cinematicListener: cinematicListener);
            var muzzle = GetComponentInChildren<Map01Rifle>()?.weapon?.Muzzle;
            if (muzzle != null)
            {
                if (_muzzleFlash == null)
                {
                    var go = new GameObject("Rifle muzzle flash"); go.transform.SetParent(muzzle, false);
                    _muzzleFlash = go.AddComponent<Light>(); _muzzleFlash.color = new Color(1f, .7f, .25f); _muzzleFlash.range = 1.5f; _muzzleFlash.intensity = 1.6f;
                }
                _muzzleFlash.enabled = true; _flashUntil = Time.time + .045f;
            }
            return reached;
        }

        // ---- IEnemyTactics: Map 1's rules on top of the shared FSM ------------------------------

        /// <summary>
        /// Engaged but Nam out of sight: with Hùng in reach and a clear line, the guard turns on him
        /// instead, at half the rate — Nam hiding does not keep Hùng safe.
        /// </summary>
        public bool TryGetAlternativeTarget(EnemyContext ctx, out Vector3 target) => HostageInReach(out target) && SafeShot(target);

        public void FireAtAlternative(EnemyContext ctx, Vector3 target)
        {
            if (Time.time < _nextShot || !CanStartGuardBurst()) return;
            ScheduleNextShot();
            // The hostage remains a slower target; M16 also keeps its magazine and burst pauses.
            _nextShot = Mathf.Max(_nextShot, Time.time + (UsesGuardM16Cycle ? Map01M16.ShotSeconds : fireInterval) * 2f);
            FireAtHostage(target, damage * (RifleItemId == Map01M16.Id ? Map01M16.DamageRelativeToAk : 1f));
        }

        /// <summary>The nearest of Map 1's placed cover points, if one is close enough to reach under fire.</summary>
        public bool TryFindCover(EnemyContext ctx, Vector3 threat, out Vector3 cover)
        {
            cover = default;
            if (_mission == null) return false;
            float best = CoverSearchRadius * CoverSearchRadius;
            bool found = false;
            foreach (var point in _mission.CoverPoints)
            {
                if (point == null) continue;
                Vector3 p = point.transform.position;
                float d = (p - transform.position).sqrMagnitude;
                if (d >= best || (p - threat).sqrMagnitude < 16f) continue; // Not into Nam's arms.
                best = d; cover = p; found = true;
            }
            return found;
        }

        public bool TryFindRetreatPoint(EnemyContext ctx, Vector3 threat, out Vector3 point)
            => DefaultEnemyTactics.Instance.TryFindRetreatPoint(ctx, threat, out point);

        private bool HostageInReach(out Vector3 hostage)
        {
            hostage = default;
            if (_rescue == null || !_rescue.Exposed || _mission.hung == null) return false;
            hostage = _mission.hung.position;
            Vector3 origin = transform.position + Vector3.up * 1.3f, target = hostage + Vector3.up * 1.1f;
            if (Vector3.Distance(origin, target) > attackRange) return false;
            return !Physics.Linecast(origin, target, out var hit, _mission.ObstructionMask, QueryTriggerInteraction.Ignore)
                || hit.transform == _mission.hung || hit.transform.IsChildOf(_mission.hung);
        }
    }
}
