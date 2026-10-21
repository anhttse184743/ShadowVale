using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Boss;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Coordination;
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
    /// hides, and — once he has called his men — falling back and staying behind them. He commands
    /// them too: squads near him are planned as a pincer (<see cref="Command"/>), his grenades drive the
    /// player towards them, a reload is their cue to push, and when he falls they lose heart. At his
    /// cover he fights in bursts (<see cref="PeekCycle"/>): out to fire, back behind it, out again.
    /// Since 18/10/2026 he fights more than he runs: longer at each cover, short hops between them,
    /// firing on the move at a player ahead of him, ducking (not running) when a rifle is on him; and
    /// his men look after him: they are planned to block the way to him (<see cref="GuardCommanderTask"/>),
    /// give covering fire while he moves, and re-plan at once when he is hurt.
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
        /// <summary>Where his cover positions may be (a map keeping him in his camp); null = anywhere on the graph.</summary>
        public Func<Vector3, bool> AllowedPosition { get; set; }
        public int Relocations { get; private set; }
        public int GrenadesThrown { get; private set; }

        /// <summary>Longest he runs to a new cover position before fighting from wherever he is.</summary>
        public const float CoverTimeoutSeconds = 10f;
        /// <summary>How long he keeps fighting (relocating, throwing grenades) after losing sight of the player.</summary>
        public const float EngagedMemorySeconds = 15f;
        /// <summary>Suppressive fire at the last-seen spot after losing sight, at least (was 2.5 s until 18/10/2026).</summary>
        public const float MinSuppressSeconds = 4f;
        /// <summary>
        /// Running between positions: ×1.15 of his walk (Map 1: 3.5 → 4 m/s), a little under Nam's walk
        /// (4.2) — a guard's ×1.5 (5.25) made a commander who never stops look like he charges.
        /// </summary>
        public const float CoverPace = 1.15f;
        /// <summary>Once his men are called he keeps this many times his usual distance from the player, behind them.</summary>
        public const float BehindHisMenFactor = 1.75f;
        /// <summary>Engaged without a position (no cover near where the fight found him): how often to look for one.</summary>
        private const float CoverRetrySeconds = 2f;
        /// <summary>A rifle pointed within this many degrees of him counts as aimed at him.</summary>
        public const float AimConeDegrees = 8f;

        /// <summary>How far behind his cover node he ducks, metres.</summary>
        public const float DuckDistance = 1.2f;

        private readonly List<IEnemyAgent> _men = new List<IEnemyAgent>();
        private bool _down;
        private PeekCycle _peek;
        public int Pushes { get; private set; }
        /// <summary>Firing in bursts from his current cover (null before Attach).</summary>
        public PeekCycle Peek => _peek;

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
            b._peek = new PeekCycle(b.Profile.PeekOutSeconds, b.Profile.PeekHideSeconds, new System.Random(seed ^ 0x7ee4));
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
            t.ReassignKeepDistance = 0f;    // A step out of cover and back is walked, not taken as already there.
            t.FireOnTheMove = true;         // Walks and fires at a player ahead; runs without firing otherwise.
        }

        private void Update()
        {
            if (_agent == null || Director == null) return;
            if (!_agent.Alive)
            {
                if (!_down) { _down = true; BreakMorale(Time.time); }
                return;
            }
            if (Suspended?.Invoke() ?? false) return;
            var memory = _agent.Brain.Memory;
            float now = Time.time;
            ServiceLocator.TryResolve<IPlayerObservable>(out var player);
            bool sheltered = !_agent.SeesPlayer || (player != null && (player.IsInCover || player.IsHidden));
            // Stepping out and back at his cover is staying there, as far as when to move on is concerned.
            bool settled = _peek.Active || !memory.HasAssignment || memory.ReachedAssignment || now >= memory.TakeCoverUntil;
            var combat = player as IPlayerCombatState;
            bool aimedAt = combat != null && combat.IsAiming && _agent.SeesPlayer
                           && AimsAt(player.Position, player.Forward, _agent.Position, AimConeDegrees);
            MenNearby(now, _men);
            var d = Director.Tick(new BossSense(now, memory.Engaged(now), _agent.HealthFraction, _agent.Position,
                memory.LastKnownPlayerPosition, sheltered, settled, combat != null && combat.IsReloading, aimedAt, Centre(_men)));
            if (d.Push) Push(_men, memory.LastKnownPlayerPosition);
            if (d.Duck && _peek.Duck(now, Profile.DuckSeconds)) _agent.Brain.Assign(_peek.HidePoint, _peek.Node, "duck");
            if (d.CallReinforcements) CallReinforcements(memory.LastKnownPlayerPosition, now);
            if (d.Relocate) Relocate(memory.LastKnownPlayerPosition, d.RelocateReason);
            else if (memory.Engaged(now) && !memory.HasAssignment && now >= _coverRetryAt
                     && !Relocate(memory.LastKnownPlayerPosition, "cover"))
                _coverRetryAt = now + CoverRetrySeconds; // Without a position he would close in on the player.
            if (d.ThrowGrenade) ThrowGrenade(d.GrenadeTarget);
            UpdatePeek(memory, now);
        }

        /// <summary>At his cover: out to fire, back behind it, out again — until he moves on or the fight ends.</summary>
        private void UpdatePeek(ShadowVale.AI.Fsm.EnemyBlackboard memory, float now)
        {
            if (!Profile.PeekFromCover || !memory.Engaged(now) || !memory.HasAssignment) { _peek.Stop(); return; }
            if (!_peek.Active)
            {
                // Just arrived at a position of his own choosing (not a push or a step of the cycle).
                bool atCover = memory.ReachedAssignment && (memory.AssignedRole == "relocate" || memory.AssignedRole == "local");
                if (atCover && TryDuckPoint(memory.AssignedPosition, memory.LastKnownPlayerPosition, out var duck))
                    _peek.Begin(memory.AssignedPosition, duck, memory.AssignedNode, now);
                return;
            }
            switch (_peek.Tick(now))
            {
                case PeekCycle.Step.Out: _agent.Brain.Assign(_peek.FiringPoint, _peek.Node, "peek"); break;
                case PeekCycle.Step.Back: _agent.Brain.Assign(_peek.HidePoint, _peek.Node, "duck"); break;
            }
        }

        /// <summary>
        /// A reachable spot just behind <paramref name="cover"/> (away from the player) from which the player
        /// cannot be seen: the cover is between them. False on open ground — then there is nothing to duck behind.
        /// </summary>
        private bool TryDuckPoint(Vector3 cover, Vector3 player, out Vector3 duck)
        {
            duck = cover;
            if (!ServiceLocator.TryResolve<IWorldQuery>(out var graph)) return false;
            Vector3 away = cover - player; away.y = 0;
            if (away.sqrMagnitude < 1e-4f) return false;
            away.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, away);
            var body = _agent.Brain.Context.Body;
            foreach (var candidate in new[] { cover + away * DuckDistance, cover + (away + side * .7f).normalized * DuckDistance,
                         cover + (away - side * .7f).normalized * DuckDistance })
            {
                if (!body.TrySampleReachable(candidate, .8f, out var p)) continue;
                if (graph.HasLineOfSight(p + Vector3.up * 1.5f, player + Vector3.up * 1.2f)) continue;
                duck = p;
                return true;
            }
            return false;
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
            bool fallingBack = reason == "regroup" || Director.ReinforcementsCalled;
            if (reason == "regroup") keep = Mathf.Max(keep, Vector3.Distance(from, playerPosition));
            var men = Centre(_men);
            if (!BossRelocation.TryPick(graph, Profile, from, playerPosition, here, _rng, out int node, out var to, range, keep, men, fallingBack, allowed: AllowedPosition)
                && !(keep > 0 && BossRelocation.TryPick(graph, Profile, from, playerPosition, here, _rng, out node, out to, range, 0f, men, fallingBack, allowed: AllowedPosition)))
                return false;
            _agent.Brain.Assign(to, node, "relocate");
            _peek?.Stop(); // A new cover: the burst cycle starts again once he is there.
            Relocations++;
            EventBus.Publish(new BossRelocatedEvent(_agent.Id, node, from, to, reason ?? ""));
            float now = Time.time;
            if (reason != "cover") CoverFire(_men, now);
            // Hurt or pinned: his men re-plan now, round where he is going.
            if (reason == "hurt" || reason == "aimed") RequestReplan(_men);
            return true;
        }

        /// <summary>
        /// He is on the move: the nearest <see cref="BossProfile.CoverFireMen"/> of his men stop and fire at the
        /// player (or where he was last seen) for <see cref="BossProfile.CoverFireSeconds"/>, keeping his head down.
        /// </summary>
        public int CoverFire(IReadOnlyList<IEnemyAgent> men, float now)
        {
            if (Profile.CoverFireMen <= 0 || men == null || men.Count == 0) return 0;
            var chosen = men.OrderBy(m => (m.Position - _agent.Position).sqrMagnitude).ThenBy(m => m.Id, StringComparer.Ordinal)
                .Where(m => m.Brain.CoverFire(now, Profile.CoverFireSeconds)).Take(Profile.CoverFireMen).ToList();
            if (chosen.Count == 0) return 0;
            CoverFireOrders++;
            EventBus.Publish(new BossOrderEvent(_agent.Id, "cover_fire", string.Join("+", chosen.Select(m => m.Id))));
            return chosen.Count;
        }

        public int CoverFireOrders { get; private set; }

        /// <summary>Asks the squad coordinator to plan his men's groups again at the next chance.</summary>
        private static void RequestReplan(IReadOnlyList<IEnemyAgent> men)
        {
            if (men.Count == 0) return;
            if (_coordinator == null) _coordinator = FindFirstObjectByType<SquadCoordinator>();
            if (_coordinator == null) return;
            foreach (var squad in men.Select(m => m.SquadId).Distinct()) _coordinator.RequestReplan(squad);
        }

        private static SquadCoordinator _coordinator;

        public Grenade ThrowGrenade(Vector3 target)
        {
            Vector3 hand = _agent.Position + Vector3.up * 1.6f + _agent.Forward * .4f;
            var grenade = Grenade.Launch(hand, target, Profile, _agent.Id);
            GrenadesThrown++;
            EventBus.Publish(new GrenadeThrownEvent(_agent.Id, hand, target));
            return grenade;
        }

        /// <summary>Whether a shooter at <paramref name="from"/> facing <paramref name="forward"/> points within <paramref name="cone"/>° of <paramref name="target"/>.</summary>
        public static bool AimsAt(Vector3 from, Vector3 forward, Vector3 target, float cone)
        {
            Vector3 to = target - from; to.y = 0; forward.y = 0;
            return to.sqrMagnitude > 1e-4f && forward.sqrMagnitude > 1e-4f && Vector3.Angle(forward, to) <= cone;
        }

        /// <summary>His men: the living, fighting, non-boss agents within <see cref="Command.Range"/> of him.</summary>
        private void MenNearby(float now, List<IEnemyAgent> into)
        {
            into.Clear();
            foreach (var a in AgentRegistry.All)
                if (a != _agent && a.Alive && a.Brain != null && !a.Brain.Tuning.IsBoss && a.Brain.Memory.Engaged(now)
                    && (a.Position - _agent.Position).sqrMagnitude <= Command.Range * Command.Range)
                    into.Add(a);
        }

        private static Vector3? Centre(List<IEnemyAgent> men)
        {
            if (men.Count == 0) return null;
            Vector3 sum = Vector3.zero;
            foreach (var m in men) sum += m.Position;
            return sum / men.Count;
        }

        /// <summary>The player is reloading: every man near him closes in on where the player is.</summary>
        public int Push(IReadOnlyList<IEnemyAgent> men, Vector3 playerPosition)
        {
            foreach (var m in men) m.Brain.Assign(playerPosition, -1, "push");
            if (men.Count == 0) return 0;
            Pushes++;
            EventBus.Publish(new BossOrderEvent(_agent.Id, "push", string.Join("+", men.Select(m => m.Id))));
            return men.Count;
        }

        /// <summary>He fell: the men around him fall back sooner from now on.</summary>
        public int BreakMorale(float now)
        {
            var broken = new List<string>();
            foreach (var a in AgentRegistry.All)
            {
                if (a == _agent || !a.Alive || a.Brain == null || a.Brain.Tuning.IsBoss) continue;
                if ((a.Position - _agent.Position).sqrMagnitude > Command.Range * Command.Range) continue;
                var t = a.Brain.Tuning;
                t.RetreatHealthFraction = Mathf.Max(t.RetreatHealthFraction, Command.MoraleBreakRetreat);
                broken.Add(a.Id);
            }
            if (broken.Count > 0) EventBus.Publish(new BossOrderEvent(_agent.Id, "morale_break", string.Join("+", broken)));
            return broken.Count;
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
