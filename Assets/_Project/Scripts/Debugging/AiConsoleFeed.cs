using System;
using System.Globalization;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Fsm;
using ShadowVale.AI.Perception;
using ShadowVale.Core.Events;
using UnityEngine;

namespace ShadowVale.Debugging
{
    /// <summary>
    /// Turns AI events from the <see cref="EventBus"/> into console lines. The AI never knows the
    /// console exists; telemetry listens to the same events.
    /// </summary>
    public sealed class AiConsoleFeed : IDisposable
    {
        private readonly AiConsoleLog _log;
        private readonly Action<EnemyStateChangedEvent> _onState;
        private readonly Action<PlayerContactEvent> _onContact;
        private readonly Action<NoiseEvent> _onNoise;
        private readonly Action<TakedownEvent> _onTakedown;
        private readonly Action<SquadAlertEvent> _onAlert;
        private readonly Action<ReplanStartedEvent> _onReplan;
        private readonly Action<PlanAppliedEvent> _onPlan;
        private readonly Action<PlanDiscardedEvent> _onDiscard;
        private readonly Action<SolverChangedEvent> _onSolver;
        private readonly Action<SidecarStatusEvent> _onSidecar;

        /// <summary>Also log footsteps nobody heard (very chatty).</summary>
        public bool VerboseNoise { get; set; }

        public AiConsoleFeed(AiConsoleLog log)
        {
            _log = log;
            _onState = e => Add(AiLogCategory.State, e.AgentId, $"{e.AgentId}  {e.From} → {e.To}");
            _onContact = e => Add(AiLogCategory.Contact, e.AgentId,
                $"{e.AgentId} {(e.Seen ? "thấy Nam" : "bị Nam tấn công")} tại {Fmt(e.PlayerPosition)}");
            _onNoise = e =>
            {
                if (!VerboseNoise && e.HeardBy == 0 && e.Kind == NoiseKind.Footstep) return;
                Add(AiLogCategory.Noise, null, $"{e.Kind} r={e.Radius:0.#} m tại {Fmt(e.Position)} — {e.HeardBy} lính nghe");
            };
            _onTakedown = e => Add(AiLogCategory.Combat, e.VictimId, $"{e.VictimId} bị hạ im lặng tại {Fmt(e.Position)}");
            _onAlert = e => AddGroup(AiLogCategory.Coordination, e.SourceSquad,
                $"BÁO ĐỘNG {e.SourceAgent}: {e.SquadMatesJoined} đồng đội vào trận" +
                (e.ReinforcementsJoined > 0 ? $", {e.ReinforcementsJoined} lính tiếp viện từ {e.ReinforcingSquads}" : ""));
            _onReplan = e => AddGroup(AiLogCategory.Coordination, e.Group,
                $"REPLAN   trigger={e.Trigger}  variant={e.Variant}  agents={e.Agents}  nodes={e.Nodes}  escapes={e.Escapes}  vars={e.Agents * e.Nodes}");
            _onPlan = e =>
            {
                AddGroup(e.FallbackReason != null || e.HitLatencyBudget ? AiLogCategory.Solver : AiLogCategory.Coordination, e.Group,
                    $"PLAN     {e.LatencyMs:0.00} ms  energy={e.Energy:0.##}  F={e.Objective:0.##}  coverage={e.CoordinationScore:0.00}" +
                    (e.FallbackReason != null ? $"  fallback={e.FallbackReason}" : ""));
                foreach (var a in e.Assignments)
                    AddGroup(AiLogCategory.Coordination, e.Group, $"         {a.AgentId} → node {a.Node}  [{RoleLabel(a)}]");
            };
            _onDiscard = e => AddGroup(AiLogCategory.Coordination, e.Group, "PLAN bỏ: " + e.Reason);
            _onSolver = e => Add(AiLogCategory.Solver, null,
                $"SOLVER   → {e.Variant}" + (e.FallbackReason != null ? $"  ({e.FallbackReason})" : ""));
            EventBus.Subscribe(_onState);
            EventBus.Subscribe(_onContact);
            EventBus.Subscribe(_onNoise);
            EventBus.Subscribe(_onTakedown);
            EventBus.Subscribe(_onAlert);
            EventBus.Subscribe(_onReplan);
            EventBus.Subscribe(_onPlan);
            EventBus.Subscribe(_onDiscard);
            _onSidecar = e => Add(AiLogCategory.Solver, null,
                e.Online ? $"SIDECAR  online tại {e.BaseUrl}" : $"SIDECAR  không kết nối được {e.BaseUrl} — dùng greedy tại chỗ");
            EventBus.Subscribe(_onSolver);
            EventBus.Subscribe(_onSidecar);
        }

        private static string RoleLabel(AgentAssignment a) => a.Role switch
        {
            "block" => "chặn lối thoát",
            "cover" => "nấp, hướng về Nam",
            _ => $"vòng sườn {a.FlankAngle:0}°",
        };

        private void AddGroup(AiLogCategory category, string group, string text)
            => _log.Add(new AiLogEntry(Time.time, category, group, text));

        public void Dispose()
        {
            EventBus.Unsubscribe(_onState);
            EventBus.Unsubscribe(_onContact);
            EventBus.Unsubscribe(_onNoise);
            EventBus.Unsubscribe(_onTakedown);
            EventBus.Unsubscribe(_onAlert);
            EventBus.Unsubscribe(_onReplan);
            EventBus.Unsubscribe(_onPlan);
            EventBus.Unsubscribe(_onDiscard);
            EventBus.Unsubscribe(_onSolver);
            EventBus.Unsubscribe(_onSidecar);
        }

        public void Add(AiLogCategory category, string agentId, string text)
        {
            string squad = agentId != null ? AgentRegistry.Find(agentId)?.SquadId : null;
            _log.Add(new AiLogEntry(Time.time, category, squad, text));
        }

        private static string Fmt(Vector3 p) => string.Format(CultureInfo.InvariantCulture, "({0:0.#}, {1:0.#})", p.x, p.z);
    }
}
