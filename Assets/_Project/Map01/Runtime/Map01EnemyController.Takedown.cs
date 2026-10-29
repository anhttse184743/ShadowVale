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
    /// The guard's side of Nam's silent kills: the bush pull (Map01BushTakedown), the bedside kill, and
    /// the knife takedown from behind.
    /// </summary>
    public sealed partial class Map01EnemyController
    {
        // ---- The bush pull (Map01BushTakedown) --------------------------------------------------
        private bool grappleOwned;
        public bool BedPoseOwned { get; set; }
        public bool WeaponPoseAvailable => !BedPoseOwned && !grappleOwned && !TakenDownSilently && (_mission == null || !_mission.Cinematic);
        /// <summary>Rounds fired with their sound (tests count them).</summary>
        public int AudibleShots { get; private set; }
        private int silentKind;
        private Map01CinematicPose bushCorpse;
        public void SetGrappleWeaponReady(bool ready) { if (grappleOwned && Alive) ShowRifle(ready); }
        public void BeginGrapple() { grappleOwned = true; ShowRifle(false); }
        /// <summary>Approved bedside execution: silent credit, visible discoverable corpse (not a bush hide).</summary>
        public void CommitBedKill(string weaponId="pistol")
        {
            if (!Alive) return;
            TakenDownSilently = true; silentKind = 2;
            _health.TakeDamage(_health.Current, transform.position + Vector3.up, _mission.player.gameObject);
            _lastHealth = _health.Current;
            if (_health.IsDead) _mission.Skills?.Award(ShadowVale.Gameplay.Skills.SkillKind.Stealth);
            CreateLoot(); FillLoot();
            EventBus.Publish(new TakedownEvent(LegacyId, transform.position));
            EventBus.Publish(new EnemyKilledEvent(LegacyId, "takedown", weaponId, transform.position));
        }
        public void CommitBushKill()
        {
            if (!Alive) return; TakenDownSilently = true; silentKind = 1;
            _health.TakeDamage(_health.Current, transform.position + Vector3.up, _mission.player.gameObject); _lastHealth = _health.Current;
            if (_health.IsDead) _mission.Skills?.Award(ShadowVale.Gameplay.Skills.SkillKind.Stealth);
        }
        public void EndGrapple(bool won)
        {
            grappleOwned = false; _heldPosition = transform.position; _heldRotation = transform.rotation;
            if (_agent != null && _agent.isOnNavMesh) _agent.nextPosition = transform.position;
            if (won) HoldBushCorpse(); else { TakenDownSilently = false; silentKind = 0; SetSilent(false); ShowRifle(true); }
        }
        private void HoldBushCorpse()
        {
            bushCorpse?.Dispose(); bushCorpse = new Map01CinematicPose(_animator);
            var clip = Map01CinematicPose.Clip("Enemy_Bush_Finish"); bushCorpse.Sample(clip, clip.length); bushCorpse.Ground(transform.position.y);
        }
        private void LateUpdate()
        {
            if (_muzzleFlash != null && Time.time >= _flashUntil) _muzzleFlash.enabled = false;
            if (bushCorpse == null) return;
            var clip = Map01CinematicPose.Clip("Enemy_Bush_Finish"); bushCorpse.Sample(clip, clip.length); bushCorpse.Ground(transform.position.y);
        }
        /// <summary>He broke free of the bush pull: straight into the fight, where Nam is.</summary>
        public void ReportGrappleEscape()
        {
            if (!Alive || _mission == null) return;
            var m = Memory; float now = Time.time; var at = _mission.player.position;
            m.Suspicion = 1f; m.EngagedUntil = now + 10f; m.LastKnownPlayerPosition = at;
            Brain.BeginInvestigation(at, now); _mission.Alarmed = true;
            ReportDetection(Map01DetectionCause.Sight, at);
        }

        /// <summary>Nam, knife in hand, right behind him, unseen, nothing in between.</summary>
        public bool CanSilentTakedown()
        {
            if (!Alive || Engaged || IsBoss || _mission == null || _mission.Stopped || _mission.ModernCombat == null
                || _mission.ModernCombat.EquippedKind != WeaponKind.Knife) return false;
            var delta = Vector3.ProjectOnPlane(_mission.player.position - transform.position, Vector3.up);
            var layout = _rescue != null ? _rescue.Layout : null;
            bool overseerApproach = _overseer && _rescue != null && _rescue.CurrentPhase == Map01Rescue.Phase.Captive;
            float range = layout != null ? (overseerApproach ? layout.overseerBackstabRange : layout.backstabRange) : 1.6f;
            if (!_overseer && _mission.Skills != null) range = _mission.Skills.TakedownRange(range);
            float rear = layout != null ? (overseerApproach ? layout.overseerBackstabAngle : layout.backstabAngle) : 130f;
            if (Vector3.Distance(_mission.player.position, transform.position) > range) return false;
            if (delta.magnitude > range || Vector3.Angle(transform.forward, delta) < rear) return false;
            var origin = _mission.player.position + Vector3.up;
            return !Physics.Linecast(origin, transform.position + Vector3.up, out var hit, _mission.ObstructionMask, QueryTriggerInteraction.Ignore)
                || hit.transform == transform || hit.transform.IsChildOf(transform);
        }

        /// <summary>The knife takedown: Nam's animation, the guard down without a sound, the cinematic.</summary>
        public bool TrySilentTakedown()
        {
            if (!CanSilentTakedown()) return false;
            if (!Map01NamActions.For(_mission).PlayTakedown(transform)) return false;
            TakenDownSilently = true; SetSilent(true); ShowRifle(false);
            if (_agent.isOnNavMesh) { _agent.ResetPath(); _agent.isStopped = true; }
            _health.TakeDamage(_health.Current, transform.position + Vector3.up, _mission.player.gameObject);
            if (_health.IsDead) _mission.Skills?.Award(ShadowVale.Gameplay.Skills.SkillKind.Stealth);
            EventBus.Publish(new TakedownEvent(LegacyId, transform.position));
            EventBus.Publish(new EnemyKilledEvent(LegacyId, "takedown", "knife", transform.position));
            var cinematic = _mission.GetComponent<Map01RescueCinematic>();
            if (cinematic == null) cinematic = _mission.gameObject.AddComponent<Map01RescueCinematic>();
            cinematic.ShowTakedown(_rescue, this);
            return true;
        }
    }
}
