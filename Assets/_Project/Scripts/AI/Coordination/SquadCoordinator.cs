using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Fsm;
using ShadowVale.AI.Solvers;
using ShadowVale.AI.Solvers.Classical;
using ShadowVale.Core.Events;
using ShadowVale.Core.Services;
using ShadowVale.Data.Coordination;
using UnityEngine;

namespace ShadowVale.AI.Coordination
{
    /// <summary>
    /// The Enemy Coordination Engine in the game (A02/A03). Every frame it groups the fighting
    /// enemies into combat groups (squads, merged when close), and when a group needs a plan
    /// (first contact, members changed, player moved, periodic refresh — with a cooldown) it builds
    /// one QUBO over the local tactical graph, solves it with the session's <see cref="ISquadSolver"/>
    /// and assigns each agent a node and a role. The agents' own FSM then takes cover / holds there.
    /// A contact is also shared over the radio with squad-mates and nearby squads.
    /// Map-agnostic: a map's mission adds it with <see cref="Attach"/>.
    /// </summary>
    public sealed class SquadCoordinator : MonoBehaviour
    {
        private sealed class GroupState
        {
            public float LastPlanTime = float.NegativeInfinity;
            public string[] LastMembers = Array.Empty<string>();
            public int LastPlayerNode = -1;
            public bool ForceReplan;
            public Task<CoordinationPlan> Pending;
            public CancellationTokenSource Cancel;
            public CoordinationInstance PendingInstance;
            public IEnemyAgent[] PendingMembers;
            public Vector3 PendingPlayer;
            public float PendingSeenAt;
            public string PendingTrigger;
        }

        private sealed class Group
        {
            public string Key;
            public List<IEnemyAgent> Members;
            public Vector3 LastKnown;
            public float LastSeenAt;
        }

        public CoordinationSettings Settings { get; private set; } = new CoordinationSettings();
        public ICoordinationTask CoordinationTask { get; set; } = ContainPlayerTask.Instance;
        public string SolverFallbackReason { get; private set; }
        public string RecordPath => _recorder?.Path;

        private readonly Dictionary<string, GroupState> _states = new Dictionary<string, GroupState>();
        private readonly HashSet<string> _forceSquads = new HashSet<string>();
        private ISquadSolver _solver;
        private CoordinationRecorder _recorder;
        private Action<PlayerContactEvent> _onContact;
        private int _serial;
        private readonly string _session = Guid.NewGuid().ToString("N").Substring(0, 12);

        public ISquadSolver Solver
        {
            get
            {
                if (_solver != null) return _solver;
                if (ServiceLocator.TryResolve<ISquadSolver>(out var shared)) return _solver = shared;
                _solver = SolverFactory.Create(Settings.Variant, Settings.LatencyBudgetMs, out var reason);
                SolverFallbackReason = reason;
                return _solver;
            }
        }

        public static SquadCoordinator Attach(GameObject owner, CoordinationSettings settings)
        {
            var c = owner.GetComponent<SquadCoordinator>();
            if (c == null) c = owner.AddComponent<SquadCoordinator>();
            c.Settings = settings ?? new CoordinationSettings();
            return c;
        }

        /// <summary>Switch solver for the rest of the session (debug console, A/B runs).</summary>
        public void SetVariant(string variantId)
        {
            _solver = SolverFactory.Create(variantId, Settings.LatencyBudgetMs, out var reason);
            SolverFallbackReason = reason;
            EventBus.Publish(new SolverChangedEvent(_solver.VariantId, reason));
        }

        /// <summary>Re-plan the group containing <paramref name="squadId"/> on the next frame.</summary>
        public void RequestReplan(string squadId) { if (!string.IsNullOrEmpty(squadId)) _forceSquads.Add(squadId); }

        private void OnEnable()
        {
            _onContact = OnContact;
            EventBus.Subscribe(_onContact);
            ServiceLocator.Register(this);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe(_onContact);
            if (ServiceLocator.TryResolve<SquadCoordinator>(out var c) && c == this) ServiceLocator.Unregister<SquadCoordinator>();
            foreach (var s in _states.Values) s.Cancel?.Cancel();
        }

        // ---- radio -------------------------------------------------------------------------------

        private void OnContact(PlayerContactEvent e)
        {
            var source = AgentRegistry.Find(e.AgentId);
            if (source == null || string.IsNullOrEmpty(source.SquadId)) return;
            float now = Time.time;
            int mates = 0, reinforcements = 0;
            var reinforcing = new SortedSet<string>();
            foreach (var other in AgentRegistry.All)
            {
                if (other == source || !other.Alive || string.IsNullOrEmpty(other.SquadId) || other.Brain.Memory.Engaged(now)) continue;
                bool mate = other.SquadId == source.SquadId
                            && (other.Position - source.Position).sqrMagnitude <= Settings.SquadAlertRange * Settings.SquadAlertRange;
                bool help = !mate && other.SquadId != source.SquadId && Settings.ReinforcementRange > 0
                            && (other.Position - e.PlayerPosition).sqrMagnitude <= Settings.ReinforcementRange * Settings.ReinforcementRange;
                if (!mate && !help) continue;
                other.Brain.ShareContact(e.PlayerPosition, now, now);
                if (mate) mates++; else { reinforcements++; reinforcing.Add(other.SquadId); }
            }
            _forceSquads.Add(source.SquadId);
            if (mates + reinforcements > 0)
                EventBus.Publish(new SquadAlertEvent(source.Id, source.SquadId, mates, reinforcements, string.Join("+", reinforcing)));
        }

        // ---- planning loop -----------------------------------------------------------------------

        private void Update()
        {
            if (!ServiceLocator.TryResolve<IWorldQuery>(out var graph)) return;
            float now = Time.time;
            var groups = BuildGroups(now);
            var live = new HashSet<string>();
            foreach (var group in groups)
            {
                live.Add(group.Key);
                if (!_states.TryGetValue(group.Key, out var state)) _states[group.Key] = state = new GroupState();
                if (group.Members.Any(m => _forceSquads.Contains(m.SquadId))) state.ForceReplan = true;
                if (state.Pending != null)
                {
                    if (state.Pending.IsCompleted) Finish(group.Key, state, graph, now);
                    continue;
                }
                string trigger = NeedsReplan(state, group, graph, now);
                if (trigger != null) Begin(group, state, graph, trigger, now);
            }
            _forceSquads.Clear();
            foreach (var key in _states.Keys.Where(k => !live.Contains(k)).ToList())
            {
                var stale = _states[key];
                if (stale.Pending != null && !stale.Pending.IsCompleted) continue; // Let it finish, then drop.
                _states.Remove(key);
            }
        }

        /// <summary>Fighting agents by squad; squads within merge range of each other form one group.</summary>
        private List<Group> BuildGroups(float now)
        {
            var bySquad = new Dictionary<string, List<IEnemyAgent>>();
            foreach (var a in AgentRegistry.All)
            {
                if (!a.Alive || string.IsNullOrEmpty(a.SquadId)) continue;
                var m = a.Brain.Memory;
                if (!m.Engaged(now) || m.Retreating) continue;
                if (!bySquad.TryGetValue(a.SquadId, out var list)) bySquad[a.SquadId] = list = new List<IEnemyAgent>();
                list.Add(a);
            }
            var squads = bySquad.Keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
            var parent = squads.ToDictionary(s => s, s => s);
            string Find(string s) { while (parent[s] != s) s = parent[s] = parent[parent[s]]; return s; }
            float merge2 = Settings.GroupMergeRange * Settings.GroupMergeRange;
            for (int i = 0; i < squads.Count; i++)
            for (int j = i + 1; j < squads.Count; j++)
            {
                bool close = bySquad[squads[i]].Any(a => bySquad[squads[j]].Any(b => (a.Position - b.Position).sqrMagnitude <= merge2));
                if (close) parent[Find(squads[j])] = Find(squads[i]);
            }

            var groups = new List<Group>();
            foreach (var root in squads.Where(s => Find(s) == s))
            {
                var names = squads.Where(s => Find(s) == root).ToList();
                var members = names.SelectMany(s => bySquad[s]).ToList();
                // The freshest contact any member has (seen, shot at, or radioed), and when it was.
                var freshest = members.OrderByDescending(a => a.Brain.Memory.EngagedUntil).First().Brain;
                Vector3 known = freshest.Memory.LastKnownPlayerPosition;
                float seenAt = freshest.Memory.EngagedUntil - freshest.Tuning.EngagedMemorySeconds;
                members = members.OrderBy(a => (a.Position - known).sqrMagnitude).ThenBy(a => a.Id, StringComparer.Ordinal)
                    .Take(Settings.MaxGroupAgents).OrderBy(a => a.Id, StringComparer.Ordinal).ToList();
                if (members.Count < Settings.MinGroupAgents) continue;
                groups.Add(new Group { Key = string.Join("+", names), Members = members, LastKnown = known, LastSeenAt = seenAt });
            }
            return groups;
        }

        private string NeedsReplan(GroupState state, Group group, IWorldQuery graph, float now)
        {
            if (now - state.LastPlanTime < Settings.ReplanCooldownSeconds) return null;
            if (float.IsNegativeInfinity(state.LastPlanTime)) return "first_contact";
            if (state.ForceReplan) return "requested";
            if (!group.Members.Select(m => m.Id).SequenceEqual(state.LastMembers)) return "members_changed";
            int playerNode = graph.NearestNode(group.LastKnown);
            if (state.LastPlayerNode >= 0 && graph.Hops(state.LastPlayerNode, playerNode) >= Settings.ReplanHopDelta) return "player_moved";
            if (now - state.LastPlanTime >= Settings.RefreshSeconds) return "refresh";
            return null;
        }

        private void Begin(Group group, GroupState state, IWorldQuery graph, string trigger, float now)
        {
            state.ForceReplan = false;
            var solver = Solver;
            int serial = ++_serial;
            string id = $"{gameObject.scene.name}-{_session}-{serial}";
            var inst = CoordinationInstance.Build(graph, group.LastKnown, group.Members.Select(m => m.Position).ToList(),
                CoordinationTask, Settings.Weights, Settings.SubgraphNodes, id, Settings.Seed + serial, solver.LatencyBudgetMs, solver.VariantId);
            if (inst == null) { state.LastPlanTime = now; return; }

            EventBus.Publish(new ReplanStartedEvent(group.Key, trigger, solver.VariantId, id,
                inst.Request.AgentCount, inst.Request.NodeCount, inst.Features.Escape.Length));
            state.Cancel = new CancellationTokenSource();
            state.PendingInstance = inst;
            state.PendingMembers = group.Members.ToArray();
            state.PendingPlayer = group.LastKnown;
            state.PendingSeenAt = group.LastSeenAt;
            state.PendingTrigger = trigger;
            try { state.Pending = solver.SolveAsync(inst.Request, state.Cancel.Token); }
            catch (Exception e) { state.Pending = System.Threading.Tasks.Task.FromException<CoordinationPlan>(e); }
            if (state.Pending.IsCompleted) Finish(group.Key, state, graph, now); // In-process solvers finish at once.
        }

        private void Finish(string key, GroupState state, IWorldQuery graph, float now)
        {
            var inst = state.PendingInstance; var members = state.PendingMembers;
            CoordinationPlan plan = null; string fallback = null;
            if (state.Pending.Status == TaskStatus.RanToCompletion) plan = state.Pending.Result;
            else fallback = "solver failed: " + (state.Pending.Exception?.GetBaseException().Message ?? "cancelled");
            state.Pending = null; state.Cancel = null;
            int a = inst.Request.AgentCount, n = inst.Request.NodeCount;
            if (plan == null || !plan.IsFeasible(a, n))
            {
                fallback ??= "infeasible plan";
                plan = new GreedySolver(inst.Request.LatencyBudgetMs).Solve(inst.Request);
                plan.FallbackReason = fallback;
            }

            // Thrown away if the fight changed under it; re-planned right away.
            if (members.Any(m => !m.Alive || !m.Brain.Memory.Engaged(now)))
            {
                Discard(key, state, "a member fell or stopped fighting", now);
                return;
            }
            var latest = members.OrderByDescending(m => m.Brain.Memory.EngagedUntil).First().Brain.Memory.LastKnownPlayerPosition;
            if ((latest - state.PendingPlayer).sqrMagnitude > Settings.StaleDistance * Settings.StaleDistance)
            {
                Discard(key, state, "the player moved while it was solving", now);
                return;
            }

            var targets = plan.AgentToTarget;
            var assignments = new AgentAssignment[a];
            for (int i = 0; i < a; i++)
            {
                int local = targets[i], node = inst.Sub.Nodes[local];
                string role = inst.RoleOf(local);
                Vector3 position = graph.NodePosition(node);
                members[i].Brain.ShareContact(state.PendingPlayer, state.PendingSeenAt, now);
                members[i].Brain.Assign(position, node, role);
                assignments[i] = new AgentAssignment(members[i].Id, node, role, position, inst.FlankAngle(local, targets));
            }
            double score = inst.CoordinationScore(targets);
            EventBus.Publish(new PlanAppliedEvent(key, plan.SolverVariantId, inst.Request.InstanceId, plan.SolveLatencyMs,
                plan.Energy, plan.ObjectiveValue, score, plan.HitLatencyBudget, plan.FallbackReason, assignments));

            if (Settings.Record)
            {
                _recorder ??= new CoordinationRecorder(_session);
                _recorder.Record(gameObject.scene.name, key, state.PendingTrigger, inst, plan, now);
            }
            state.LastPlanTime = now;
            state.LastMembers = members.Select(m => m.Id).ToArray();
            state.LastPlayerNode = inst.PlayerNode;
        }

        private void Discard(string key, GroupState state, string reason, float now)
        {
            EventBus.Publish(new PlanDiscardedEvent(key, reason));
            state.LastPlanTime = now - Settings.ReplanCooldownSeconds; // Allowed to re-plan on the next frame.
            state.ForceReplan = true;
        }
    }
}
