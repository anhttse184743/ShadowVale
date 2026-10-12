using System;
using System.Linq;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Boss;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Fsm;
using ShadowVale.Core.Events;
using ShadowVale.Core.Services;
using ShadowVale.Data.Content;
using UnityEngine;

namespace ShadowVale.Gameplay.Enemies.Boss
{
    /// <summary>
    /// A boss's extra behaviour on top of the shared FSM (A04), for any map: every frame
    /// <see cref="BossDirector"/> decides, this component acts — a new cover position handed to the
    /// brain (TakeCover runs there, firing on the way), a grenade at a player who camps behind cover,
    /// one call for reinforcements. The boss is left out of squad plans so his positions stay his.
    /// He fights like a commander, not a rifleman (<see cref="ApplyBossTactics"/>): always on the move
    /// between cover positions (firing on the way, short stops), never walking up to where the player
    /// hides, and — once he has called his men — falling back and staying behind them.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossBehaviour : MonoBehaviour
    {
        public BossDirector Director { get; private set; }
        public BossProfile Profile => Director.Profile;
        /// <summary>Who answers the call; nearby idle enemies unless the map says otherwise.</summary>
        public IReinforcementProvider Reinforcements { get; set; }
        /// <summary>True while the map is paused, in a cutscene or restoring: the boss does nothing then.</summary>
        public Func<bool> Suspended { get; set; }
        public int Relocations { get; private set; }
        public int GrenadesThrown { get; private set; }

        /// <summary>Longest he runs to a new cover position before fighting from wherever he is.</summary>
        public const float CoverTimeoutSeconds = 10f;
        /// <summary>How long he keeps fighting (relocating, throwing grenades) after losing sight of the player.</summary>
        public const float EngagedMemorySeconds = 15f;
        /// <summary>Suppressive fire at the last-seen spot after losing sight, at least.</summary>
        public const float MinSuppressSeconds = 2.5f;
        /// <summary>
        /// Running between positions: ×1.15 of his walk (Map 1: 3.5 → 4 m/s), a little under Nam's walk
        /// (4.2) — a guard's ×1.5 (5.25) made a commander who never stops look like he charges.
        /// </summary>
        public const float CoverPace = 1.15f;
        /// <summary>Once his men are called he keeps this many times his usual distance from the player, behind them.</summary>
        public const float BehindHisMenFactor = 1.75f;
        /// <summary>Engaged without a position (no cover near where the fight found him): how often to look for one.</summary>
        private const float CoverRetrySeconds = 2f;

        private IEnemyAgent _agent;
        private System.Random _rng;
        private float _coverRetryAt;

        public static BossBehaviour Attach(GameObject enemy, IEnemyAgent agent, BossProfile profile,
            IReinforcementProvider reinforcements = null, int seed = 0)
        {
            var b = enemy.GetComponent<BossBehaviour>();
            if (b == null) b = enemy.AddComponent<BossBehaviour>();
            b._agent = agent;
            b.Director = new BossDirector(profile, new System.Random(seed ^ 0x5eed));
            b.Reinforcements = reinforcements;
            b._rng = new System.Random(seed);
            ApplyBossTactics(agent.Brain.Tuning);
            CommanderMarker.Attach(enemy); // The star over his head: which one is the commander.
            return b;
        }

        /// <summary>
        /// The shared FSM's rules tuned for a commander. A rifleman's defaults (fight first, give up on
        /// cover after 3 s, close in on a player out of range, hunt the last-seen spot) were tuned for
        /// squads chasing a fleeing player; on a boss they read as charging: a relocation was dropped
        /// the moment the player was in range, so "move when hurt" never happened, and losing sight
        /// sent him straight to the player's hiding place.
        /// </summary>
        public static void ApplyBossTactics(EnemyTuning t)
        {
            t.EngageInRangeFirst = false;   // TakeCover fires on the way; a relocation is carried out.
            t.HoldRoles = "*";              // Every position he takes he holds.
            t.HuntWhenLost = false;         // Watches and suppresses instead of walking up to the player.
            t.TakeCoverTimeoutSeconds = Mathf.Max(t.TakeCoverTimeoutSeconds, CoverTimeoutSeconds);
            t.EngagedMemorySeconds = Mathf.Max(t.EngagedMemorySeconds, EngagedMemorySeconds);
            t.SuppressSeconds = Mathf.Max(t.SuppressSeconds, MinSuppressSeconds);
            t.InvestigatePace = CoverPace;
        }

        private void Update()
        {
            if (_agent == null || Director == null || !_agent.Alive || (Suspended?.Invoke() ?? false)) return;
            var memory = _agent.Brain.Memory;
            float now = Time.time;
            ServiceLocator.TryResolve<IPlayerObservable>(out var player);
            bool sheltered = !_agent.SeesPlayer || (player != null && (player.IsInCover || player.IsHidden));
            bool settled = !memory.HasAssignment || memory.ReachedAssignment || now >= memory.TakeCoverUntil;
            var d = Director.Tick(new BossSense(now, memory.Engaged(now), _agent.HealthFraction, _agent.Position,
                memory.LastKnownPlayerPosition, sheltered, settled));
            if (d.CallReinforcements) CallReinforcements(memory.LastKnownPlayerPosition, now);
            if (d.Relocate) Relocate(memory.LastKnownPlayerPosition, d.RelocateReason);
            else if (memory.Engaged(now) && !memory.HasAssignment && now >= _coverRetryAt
                     && !Relocate(memory.LastKnownPlayerPosition, "cover"))
                _coverRetryAt = now + CoverRetrySeconds; // Without a position he would close in on the player.
            if (d.ThrowGrenade) ThrowGrenade(d.GrenadeTarget);
        }

        /// <summary>Moves to another cover node; false when the map has no graph or no node fits.</summary>
        public bool Relocate(Vector3 playerPosition, string reason = "interval")
        {
            if (!ServiceLocator.TryResolve<IWorldQuery>(out var graph)) return false;
            Vector3 from = _agent.Position;
            int here = graph.NearestNode(from);
            float range = _agent.Brain.Tuning.AttackRange;
            // Behind his men once they are coming; regrouping, never closer to the player than he is.
            float keep = Director.ReinforcementsCalled ? Profile.RelocateMinPlayerDistance * BehindHisMenFactor : 0f;
            if (reason == "regroup") keep = Mathf.Max(keep, Vector3.Distance(from, playerPosition));
            if (!BossRelocation.TryPick(graph, Profile, from, playerPosition, here, _rng, out int node, out var to, range, keep)
                && !(keep > 0 && BossRelocation.TryPick(graph, Profile, from, playerPosition, here, _rng, out node, out to, range)))
                return false;
            _agent.Brain.Assign(to, node, "relocate");
            Relocations++;
            EventBus.Publish(new BossRelocatedEvent(_agent.Id, node, from, to, reason ?? ""));
            return true;
        }

        public Grenade ThrowGrenade(Vector3 target)
        {
            Vector3 hand = _agent.Position + Vector3.up * 1.6f + _agent.Forward * .4f;
            var grenade = Grenade.Launch(hand, target, Profile, _agent.Id);
            GrenadesThrown++;
            EventBus.Publish(new GrenadeThrownEvent(_agent.Id, hand, target));
            return grenade;
        }

        public int CallReinforcements(Vector3 playerPosition, float now)
        {
            var provider = Reinforcements ?? NearbyAgentsReinforcements.Instance;
            var sent = provider.Summon(_agent, playerPosition, Profile.ReinforceCount, Profile.ReinforceRadius, now);
            EventBus.Publish(new BossReinforcementsEvent(_agent.Id, Profile.ReinforceCount, sent.Count, string.Join("+", sent.Select(a => a.Id))));
            return sent.Count;
        }
    }
}
