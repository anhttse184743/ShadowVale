using System;
using System.Globalization;
using ShadowVale.AI.Agents;
using ShadowVale.AI.Boss;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Fsm;
using ShadowVale.AI.Perception;
using ShadowVale.Core.Events;
using UnityEngine;

namespace ShadowVale.Debugging
{
    /// <summary>
    /// Turns AI events from the <see cref="EventBus"/> into console lines. The AI never knows the
    /// console exists; telemetry listens to the same events. Every line starts with who decided it:
    /// [SOLVER x] the squad plan from solver x, [GREEDY] the in-game fallback when x did not answer
    /// in time, [FSM] one guard's own state machine, [CẢM BIẾN] sight and hearing, [RADIO] the
    /// coordinator passing a contact on (no solving), [BOSS] the commander's own behaviour.
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
        private readonly Action<BossRelocatedEvent> _onRelocate;
        private readonly Action<GrenadeThrownEvent> _onThrow;
        private readonly Action<GrenadeExplodedEvent> _onBlast;
        private readonly Action<BossReinforcementsEvent> _onReinforce;
        private readonly Action<BossOrderEvent> _onOrder;

        /// <summary>Also log footsteps nobody heard (very chatty).</summary>
        public bool VerboseNoise { get; set; }

        public AiConsoleFeed(AiConsoleLog log)
        {
            _log = log;
            _onState = e => Add(AiLogCategory.State, e.AgentId, $"{Fsm} {e.AgentId}  {e.From} → {e.To}{WhyState(e)}");
            _onContact = e => Add(AiLogCategory.Contact, e.AgentId,
                $"{Sense} {e.AgentId} {(e.Seen ? "thấy Nam" : "bị Nam tấn công")} tại {Fmt(e.PlayerPosition)}");
            _onNoise = e =>
            {
                if (!VerboseNoise && e.HeardBy == 0 && e.Kind == NoiseKind.Footstep) return;
                Add(AiLogCategory.Noise, null, $"{Sense} {e.Kind} r={e.Radius:0.#} m tại {Fmt(e.Position)} — {e.HeardBy} lính nghe");
            };
            _onTakedown = e => Add(AiLogCategory.Combat, e.VictimId, $"{Tag("GAMEPLAY")} {e.VictimId} bị hạ im lặng tại {Fmt(e.Position)}");
            _onAlert = e => AddGroup(AiLogCategory.Coordination, e.SourceSquad,
                $"{Tag("RADIO")} BÁO ĐỘNG {e.SourceAgent}: {e.SquadMatesJoined} đồng đội vào trận" +
                (e.ReinforcementsJoined > 0 ? $", {e.ReinforcementsJoined} lính tiếp viện từ {e.ReinforcingSquads}" : ""));
            _onReplan = e => AddGroup(AiLogCategory.Coordination, e.Group,
                $"{Tag("SOLVER " + e.Variant)} REPLAN  trigger={e.Trigger}  agents={e.Agents}  nodes={e.Nodes}  escapes={e.Escapes}  vars={e.Agents * e.Nodes}");
            _onPlan = e =>
            {
                // Who actually produced this plan: the requested solver, or greedy standing in for it.
                string by = e.FallbackReason != null ? Tag("GREEDY dự phòng") : Tag("SOLVER " + e.Variant);
                AddGroup(e.FallbackReason != null || e.HitLatencyBudget ? AiLogCategory.Solver : AiLogCategory.Coordination, e.Group,
                    $"{by} PLAN    {e.LatencyMs:0.00} ms  energy={e.Energy:0.##}  F={e.Objective:0.##}  coverage={e.CoordinationScore:0.00}" +
                    (e.FallbackReason != null ? $"  (fallback: {e.FallbackReason})" : ""));
                foreach (var a in e.Assignments)
                    AddGroup(AiLogCategory.Coordination, e.Group, $"{by}   giao {a.AgentId} → node {a.Node}  [{RoleLabel(a)}]");
            };
            _onDiscard = e => AddGroup(AiLogCategory.Coordination, e.Group, $"{Tag("SOLVER")} PLAN bỏ (không áp dụng): {e.Reason}");
            _onSolver = e => Add(AiLogCategory.Solver, null,
                $"{Tag("SOLVER")} đổi sang {e.Variant}" + (e.FallbackReason != null ? $"  ({e.FallbackReason})" : ""));
            EventBus.Subscribe(_onState);
            EventBus.Subscribe(_onContact);
            EventBus.Subscribe(_onNoise);
            EventBus.Subscribe(_onTakedown);
            EventBus.Subscribe(_onAlert);
            EventBus.Subscribe(_onReplan);
            EventBus.Subscribe(_onPlan);
            EventBus.Subscribe(_onDiscard);
            _onSidecar = e => Add(AiLogCategory.Solver, null,
                e.Online ? $"{Tag("SOLVER")} sidecar online tại {e.BaseUrl}" : $"{Tag("SOLVER")} không kết nối được sidecar {e.BaseUrl} — dùng greedy tại chỗ");
            EventBus.Subscribe(_onSolver);
            EventBus.Subscribe(_onSidecar);
            _onRelocate = e => Add(AiLogCategory.Boss, e.BossId,
                $"{Tag("BOSS")} đổi vị trí → node {e.Node} {Fmt(e.To)}  ({BossMoveReason(e.Reason)})");
            _onThrow = e => Add(AiLogCategory.Boss, e.ThrowerId, $"{Tag("BOSS")} ném lựu đạn vào {Fmt(e.Target)} — Nam nấp quá lâu");
            _onBlast = e => Add(AiLogCategory.Boss, e.ThrowerId,
                e.PlayerDamage > 0 ? $"{Tag("BOSS")} lựu đạn nổ, Nam mất {e.PlayerDamage:0} HP" : $"{Tag("BOSS")} lựu đạn nổ, Nam đã tránh được");
            _onReinforce = e => Add(AiLogCategory.Boss, e.BossId,
                $"{Tag("BOSS")} gọi viện: {e.Arrived}/{e.Requested} lính tới ({e.Agents})");
            _onOrder = e => Add(AiLogCategory.Boss, e.BossId, $"{Tag("BOSS")} {BossOrder(e)}");
            EventBus.Subscribe(_onRelocate);
            EventBus.Subscribe(_onThrow);
            EventBus.Subscribe(_onBlast);
            EventBus.Subscribe(_onReinforce);
            EventBus.Subscribe(_onOrder);
        }

        private static string RoleLabel(AgentAssignment a) => a.Role switch
        {
            "block" => "chặn lối thoát",
            "cover" => "nấp, hướng về Nam",
            _ => $"vòng sườn {a.FlankAngle:0}°",
        };

        private static string BossOrder(BossOrderEvent e) => e.Order switch
        {
            "pincer" => $"chỉ huy nhóm {e.Detail}: kế hoạch gọng kìm (tăng vòng sườn, chặn lối thoát)",
            "push" => $"Nam đang thay đạn: ra lệnh áp sát ({e.Detail})",
            "morale_break" => $"chỉ huy ngã: lính mất tinh thần, dễ rút lui hơn ({e.Detail})",
            _ => e.Order + " " + e.Detail,
        };

        private static string BossMoveReason(string reason) => reason switch
        {
            "hurt" => "bị thương",
            "regroup" => "rút về phía viện binh",
            "aimed" => "né vì Nam đang ngắm",
            "cover" => "tìm chỗ nấp",
            _ => "đứng lâu",
        };

        /// <summary>"[WHO]" padded to one width, so the lines line up in the monospaced console.</summary>
        private static string Tag(string who) => ("[" + who + "]").PadRight(18);
        private static readonly string Fsm = Tag("FSM"), Sense = Tag("CẢM BIẾN");

        /// <summary>
        /// For a guard heading to a position (TakeCover): whose position it is — the squad plan's
        /// (solver) or the nearest cover he found himself. Read off his blackboard at the moment
        /// he switches, which is when the assignment that sent him there is current.
        /// </summary>
        private static string WhyState(EnemyStateChangedEvent e)
        {
            if (e.To != EnemyStateId.TakeCover) return "";
            var m = AgentRegistry.Find(e.AgentId)?.Brain?.Memory;
            if (m == null || !m.HasAssignment) return "";
            return m.AssignedNode >= 0
                ? $"  (vị trí do solver giao: node {m.AssignedNode}, {RoleName(m.AssignedRole)})"
                : "  (tự tìm chỗ nấp gần nhất)";
        }

        private static string RoleName(string role) => role switch
        {
            "block" => "chặn lối thoát",
            "cover" => "nấp, hướng về Nam",
            "flank" => "vòng sườn",
            _ => role,
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
            EventBus.Unsubscribe(_onRelocate);
            EventBus.Unsubscribe(_onThrow);
            EventBus.Unsubscribe(_onBlast);
            EventBus.Unsubscribe(_onReinforce);
            EventBus.Unsubscribe(_onOrder);
        }

        public void Add(AiLogCategory category, string agentId, string text)
        {
            string squad = agentId != null ? AgentRegistry.Find(agentId)?.SquadId : null;
            _log.Add(new AiLogEntry(Time.time, category, squad, text));
        }

        private static string Fmt(Vector3 p) => string.Format(CultureInfo.InvariantCulture, "({0:0.#}, {1:0.#})", p.x, p.z);
    }
}
