using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ShadowVale.AI.Boss;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Fsm;
using ShadowVale.AI.Perception;
using ShadowVale.Core.Events;
using ShadowVale.Data.Telemetry;
using UnityEngine;

namespace ShadowVale.Telemetry
{
    /// <summary>
    /// One map session's telemetry, built purely from the <see cref="EventBus"/>: the AI, the map and
    /// the coordinator publish what happens and never call telemetry themselves (the debug console
    /// listens to the same stream). Turns the stream into sampled events, encounters (capture rate,
    /// escape time), solver metrics and plan diversity, and at <see cref="Finish"/> into one
    /// <see cref="TelemetrySessionDto"/>.
    /// Plain C# with an explicit clock, so tests and the replay harness drive it without a scene.
    /// </summary>
    public sealed class TelemetrySession
    {
        public SessionContext Context { get; }
        public TelemetryConfig Config { get; }
        public EventRecorder Events { get; }
        public EncounterTracker Encounters { get; } = new EncounterTracker();
        public SolverStats Solver { get; } = new SolverStats();
        public PlanDiversity Diversity { get; } = new PlanDiversity();
        public bool Finished => _result != null;

        private readonly Func<double> _clock;
        private readonly double _t0;
        private readonly Dictionary<string, (string trigger, string variant, int nodes, double t)> _replans = new Dictionary<string, (string, string, int, double)>();

        /// <summary>One row per applied plan, for the backend's coordination_results (AI comparison, scalability).</summary>
        public List<PlanRecord> Plans { get; } = new List<PlanRecord>();

        public sealed class PlanRecord
        {
            public string Id = Guid.NewGuid().ToString();
            public string Variant, Group;
            /// <summary>RouteCoverage | CoverAssignment | Flanking: the role most agents got (block | cover | flank).</summary>
            public string TaskType;
            public bool UsedFallback, WithinBudget;
            public int Agents, Nodes;
            public double Objective, LatencyMs, CoordinationScore;
            /// <summary>Seconds since session start, when the solve was asked for.</summary>
            public double T;
            public Dictionary<string, int> Assignment = new Dictionary<string, int>();
        }
        private readonly HashSet<string> _requestedVariants = new HashSet<string>();
        private readonly List<Action> _unsubscribe = new List<Action>();
        private string _missionResult;
        private TelemetrySessionDto _result;
        private double _nextPositionAt;
        private int _deaths, _kills, _takedowns, _attacks, _objectives;

        /// <summary>Every shot and gun kill by weapon, before the event list's rate limit and cap (backend stats).</summary>
        public Dictionary<string, int> ShotsByWeapon { get; } = new Dictionary<string, int>();
        public Dictionary<string, int> KillsByWeapon { get; } = new Dictionary<string, int>();
        public int Takedowns => _takedowns;
        public int WeaponKills => _kills - _takedowns;

        /// <param name="clock">Game seconds (paused time does not count).</param>
        public TelemetrySession(SessionContext context, TelemetryConfig config, Func<double> clock)
        {
            Context = context; Config = config ?? new TelemetryConfig(); _clock = clock;
            _t0 = clock();
            Events = new EventRecorder(context.MapId, Config.MaxEvents);
        }

        /// <summary>Seconds since the session started.</summary>
        public double Now => _clock() - _t0;

        public void Subscribe()
        {
            if (_unsubscribe.Count > 0) return;
            Listen<PlayerContactEvent>(OnContact);
            Listen<EnemyStateChangedEvent>(OnStateChanged);
            Listen<NoiseEvent>(OnNoise);
            Listen<TakedownEvent>(OnTakedown);
            Listen<EnemyKilledEvent>(OnEnemyKilled);
            Listen<PlayerAttackEvent>(OnPlayerAttack);
            Listen<PlayerDiedEvent>(OnPlayerDied);
            Listen<ObjectiveCompletedEvent>(OnObjective);
            Listen<MissionEndedEvent>(OnMissionEnded);
            Listen<SquadAlertEvent>(OnSquadAlert);
            Listen<ReplanStartedEvent>(OnReplanStarted);
            Listen<PlanAppliedEvent>(OnPlanApplied);
            Listen<PlanDiscardedEvent>(e => Add("plan_discarded", ("group", e.Group), ("reason", e.Reason)));
            Listen<SolverChangedEvent>(e => Add("solver_changed", ("variant", e.Variant), ("fallback", e.FallbackReason ?? "")));
            Listen<SidecarStatusEvent>(e => Add("sidecar_status", ("online", e.Online)));
            Listen<BossRelocatedEvent>(e => Add("boss_relocated", ("boss", e.BossId), ("reason", e.Reason), ("node", e.Node),
                ("x", R(e.To.x)), ("z", R(e.To.z))));
            Listen<GrenadeThrownEvent>(e => Add("grenade_thrown", ("thrower", e.ThrowerId), ("x", R(e.Target.x)), ("z", R(e.Target.z))));
            Listen<GrenadeExplodedEvent>(e => Add("grenade_exploded", ("thrower", e.ThrowerId), ("player_damage", R(e.PlayerDamage)),
                ("x", R(e.Position.x)), ("z", R(e.Position.z))));
            Listen<BossReinforcementsEvent>(e => Add("boss_reinforcements", ("boss", e.BossId), ("requested", e.Requested), ("arrived", e.Arrived)));
        }

        public void Unsubscribe()
        {
            foreach (var off in _unsubscribe) off();
            _unsubscribe.Clear();
        }

        private void Listen<T>(Action<T> handler) where T : IEvent
        {
            EventBus.Subscribe(handler);
            _unsubscribe.Add(() => EventBus.Unsubscribe(handler));
        }

        // ---- Handlers -------------------------------------------------------------------------

        private void OnContact(PlayerContactEvent e)
        {
            double t = Now;
            Encounters.OnContact(e.AgentId, t);
            Add("player_spotted", ("agent", e.AgentId), ("seen", e.Seen), ("x", R(e.PlayerPosition.x)), ("z", R(e.PlayerPosition.z)));
        }

        private void OnStateChanged(EnemyStateChangedEvent e)
        {
            Encounters.OnStateChanged(e.AgentId, e.To, Now);
            Add("enemy_state_changed", ("agent", e.AgentId), ("from", Snake(e.From.ToString())), ("to", Snake(e.To.ToString())));
        }

        private void OnNoise(NoiseEvent e)
        {
            // Footsteps are most of all noise: one a second is enough for a heatmap of loud movement.
            bool frequent = e.Kind == NoiseKind.Footstep;
            Events.Add("noise_emitted", Now, Payload(("kind", Snake(e.Kind.ToString())), ("radius", R(e.Radius)), ("heard_by", e.HeardBy),
                ("x", R(e.Position.x)), ("z", R(e.Position.z))), frequent ? 1.0 : 0, "noise_footstep");
        }

        private void OnTakedown(TakedownEvent e) =>
            Add("takedown", ("victim", e.VictimId), ("x", R(e.Position.x)), ("z", R(e.Position.z)));

        private void OnEnemyKilled(EnemyKilledEvent e)
        {
            _kills++;
            if (e.Method == "takedown") _takedowns++;
            else if (!string.IsNullOrEmpty(e.WeaponId)) KillsByWeapon[e.WeaponId] = KillsByWeapon.TryGetValue(e.WeaponId, out int k) ? k + 1 : 1;
            Encounters.OnEnemyKilled(e.VictimId, Now);
            Add("enemy_killed", ("victim", e.VictimId), ("method", e.Method), ("weapon", e.WeaponId ?? ""),
                ("x", R(e.Position.x)), ("z", R(e.Position.z)));
        }

        private void OnPlayerAttack(PlayerAttackEvent e)
        {
            _attacks++;
            if (!string.IsNullOrEmpty(e.Weapon)) ShotsByWeapon[e.Weapon] = ShotsByWeapon.TryGetValue(e.Weapon, out int n) ? n + 1 : 1;
            Events.Add("shot_fired", Now, Payload(("weapon", e.Weapon), ("x", R(e.Position.x)), ("z", R(e.Position.z))),
                0.25, "shot_" + e.Weapon);
        }

        private void OnPlayerDied(PlayerDiedEvent e)
        {
            _deaths++;
            bool fighting = Encounters.InEncounter;
            Encounters.OnPlayerDied(Now);
            Add("player_death", ("x", R(e.Position.x)), ("z", R(e.Position.z)), ("in_encounter", fighting));
        }

        private void OnObjective(ObjectiveCompletedEvent e)
        {
            _objectives++;
            Add("objective_completed", ("objective", e.ObjectiveId), ("index", e.Index));
        }

        private void OnMissionEnded(MissionEndedEvent e)
        {
            _missionResult = e.Result;
            Add("mission_result", ("result", e.Result));
        }

        private void OnSquadAlert(SquadAlertEvent e) =>
            Add("squad_alert", ("agent", e.SourceAgent), ("squad", e.SourceSquad), ("mates", e.SquadMatesJoined),
                ("reinforcements", e.ReinforcementsJoined));

        private void OnReplanStarted(ReplanStartedEvent e)
        {
            if (!string.IsNullOrEmpty(e.InstanceId)) _replans[e.InstanceId] = (e.Trigger, e.Variant, e.Nodes, Now);
        }

        private void OnPlanApplied(PlanAppliedEvent e)
        {
            // The variant asked for, not the one that answered: a sqa plan that fell back to greedy
            // is still a sqa session's plan (and counts against it as a fallback).
            _replans.TryGetValue(e.InstanceId ?? "", out var started);
            _replans.Remove(e.InstanceId ?? "");
            string variant = started.variant ?? e.Variant;
            bool fellBack = !string.IsNullOrEmpty(e.FallbackReason);
            _requestedVariants.Add(variant ?? "");
            Solver.Add(variant, e.LatencyMs, e.HitLatencyBudget, fellBack, e.Objective, e.CoordinationScore);
            Encounters.OnPlan(variant, e.CoordinationScore, fellBack);
            if (e.Assignments != null)
                Diversity.Add(e.Assignments.Select(a => (a.AgentId, a.Position)).ToList());
            if (e.Assignments != null && e.Assignments.Length > 0 && started.nodes > 0)
                Plans.Add(new PlanRecord
                {
                    Variant = variant, Group = e.Group, UsedFallback = fellBack, WithinBudget = !e.HitLatencyBudget,
                    Agents = e.Assignments.Length, Nodes = started.nodes, Objective = e.Objective, LatencyMs = e.LatencyMs,
                    CoordinationScore = e.CoordinationScore, T = started.variant != null ? started.t : Now,
                    Assignment = e.Assignments.GroupBy(a => a.AgentId ?? "").ToDictionary(g => g.Key, g => g.First().Node),
                    TaskType = TaskOf(e.Assignments),
                });

            Add("coordination_replan", ("group", e.Group), ("trigger", started.trigger ?? ""), ("variant", variant ?? ""),
                ("agents", e.Assignments?.Length ?? 0), ("latency_ms", R(e.LatencyMs)), ("energy", R(e.Energy)),
                ("objective", R(e.Objective)), ("coordination_score", Math.Round(e.CoordinationScore, 4)), ("fallback", fellBack));
            if (e.HitLatencyBudget)
                Add("solver_timeout", ("variant", variant ?? ""), ("latency_ms", R(e.LatencyMs)), ("reason", e.FallbackReason ?? ""));
            else if (fellBack)
                Add("solver_fallback", ("variant", variant ?? ""), ("reason", e.FallbackReason));
        }

        // ---- Per-frame -------------------------------------------------------------------------

        /// <summary>Closes encounters whose guards have all given up.</summary>
        public void Tick() => Encounters.Tick(Now);

        /// <summary>Call every frame with the player's position; kept every <see cref="TelemetryConfig.PositionSampleSeconds"/>.</summary>
        public void SamplePlayer(Vector3 position)
        {
            double t = Now;
            if (t < _nextPositionAt) return;
            _nextPositionAt = t + Math.Max(.1f, Config.PositionSampleSeconds);
            Add("player_position", ("x", R(position.x)), ("z", R(position.z)), ("in_encounter", Encounters.InEncounter));
        }

        // ---- End -----------------------------------------------------------------------------

        /// <summary>
        /// Ends the session (once; later calls return the same batch). The mission result is the
        /// map's own <see cref="MissionEndedEvent"/> if it sent one, else <c>died</c> when the session ends
        /// on a dead player, else <c>quit</c>.
        /// </summary>
        public TelemetrySessionDto Finish(bool playerDead = false, string coordinationLog = null)
        {
            if (_result != null) return _result;
            Unsubscribe();
            double end = Now;
            Encounters.Finish(end);
            string result = _missionResult ?? (playerDead ? "died" : "quit");

            var summary = new TelemetrySummaryDto
            {
                CoordinationScoreMean = Math.Round(Solver.CoordinationScoreMean, 4),
                PlayerDeaths = _deaths, EnemiesKilled = _kills, Takedowns = _takedowns,
                PlayerAttacks = _attacks, ObjectivesCompleted = _objectives,
                Plans = Diversity.Plans, DistinctPlans = Diversity.DistinctPlans,
                PlanEntropyBits = Math.Round(Diversity.PlanEntropyBits, 4),
                AgentPositionEntropyBits = Math.Round(Diversity.AgentPositionEntropyBits, 4),
                FallbackRate = Math.Round(Solver.FallbackRate, 4),
                Variants = string.Join("+", _requestedVariants.Where(v => v.Length > 0).OrderBy(v => v, StringComparer.Ordinal)),
                ValidForComparison = _requestedVariants.Count <= 1 && Solver.FallbackRate <= Config.MaxFallbackRateForComparison,
                CoordinationLog = coordinationLog,
                EventCounts = new Dictionary<string, int>(Events.Counts),
                EventsDropped = Events.Dropped,
            };
            Encounters.Summarise(summary);

            _result = new TelemetrySessionDto
            {
                SessionId = Context.SessionId,
                ContentVersion = Context.ContentVersion,
                AiVariant = Solver.MainVariant(Context.ConfiguredVariant),
                BuildVersion = Context.BuildVersion,
                Platform = Context.Platform,
                StartedAt = Context.StartedAtUtc.ToString("o", CultureInfo.InvariantCulture),
                EndedAt = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                MapId = Context.MapId,
                DurationSeconds = EventRecorder.Round(end),
                MissionResult = result,
                EscapeTimeSeconds = summary.EscapeTimeMeanSeconds,
                Solver = Solver.ToMetrics(Context.ConfiguredVariant),
                Summary = summary,
                Encounters = Encounters.Finished,
                Events = Events.Events,
            };
            return _result;
        }

        // ---- Helpers ---------------------------------------------------------------------------

        private static string TaskOf(AgentAssignment[] assignments)
        {
            var role = assignments.GroupBy(a => a.Role ?? "").OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).First().Key;
            return role == "cover" ? "CoverAssignment" : role == "flank" ? "Flanking" : "RouteCoverage";
        }

        private void Add(string type, params (string key, object value)[] fields) => Events.Add(type, Now, Payload(fields));

        private static Dictionary<string, object> Payload(params (string key, object value)[] fields)
        {
            var d = new Dictionary<string, object>(fields.Length);
            foreach (var (k, v) in fields) d[k] = v;
            return d;
        }

        private static double R(double v) => EventRecorder.Round(v);

        /// <summary>TakeCover → take_cover.</summary>
        public static string Snake(string pascal)
        {
            var sb = new StringBuilder(pascal.Length + 4);
            for (int i = 0; i < pascal.Length; i++)
            {
                char c = pascal[i];
                if (char.IsUpper(c) && i > 0) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
