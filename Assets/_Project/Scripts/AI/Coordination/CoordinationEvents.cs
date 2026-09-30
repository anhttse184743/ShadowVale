using ShadowVale.Core.Events;
using UnityEngine;

namespace ShadowVale.AI.Coordination
{
    /// <summary>One agent's part of an applied plan.</summary>
    public readonly struct AgentAssignment
    {
        public readonly string AgentId;
        public readonly int Node;
        /// <summary>block (covers an escape node) | cover (cover facing the player) | flank.</summary>
        public readonly string Role;
        public readonly Vector3 Position;
        /// <summary>Degrees around the player between this position and the group's centre line.</summary>
        public readonly float FlankAngle;

        public AgentAssignment(string agentId, int node, string role, Vector3 position, float flankAngle)
        {
            AgentId = agentId; Node = node; Role = role; Position = position; FlankAngle = flankAngle;
        }
    }

    /// <summary>A guard's contact brought squad-mates and neighbouring squads into the fight.</summary>
    public readonly struct SquadAlertEvent : IEvent
    {
        public readonly string SourceAgent, SourceSquad;
        public readonly int SquadMatesJoined, ReinforcementsJoined;
        public readonly string ReinforcingSquads;

        public SquadAlertEvent(string sourceAgent, string sourceSquad, int squadMates, int reinforcements, string reinforcingSquads)
        {
            SourceAgent = sourceAgent; SourceSquad = sourceSquad;
            SquadMatesJoined = squadMates; ReinforcementsJoined = reinforcements; ReinforcingSquads = reinforcingSquads;
        }
    }

    /// <summary>A combat group started a solve (telemetry <c>coordination_replan</c>).</summary>
    public readonly struct ReplanStartedEvent : IEvent
    {
        public readonly string Group, Trigger, Variant, InstanceId;
        public readonly int Agents, Nodes, Escapes;

        public ReplanStartedEvent(string group, string trigger, string variant, string instanceId, int agents, int nodes, int escapes)
        {
            Group = group; Trigger = trigger; Variant = variant; InstanceId = instanceId;
            Agents = agents; Nodes = nodes; Escapes = escapes;
        }
    }

    /// <summary>A plan was applied to the group's agents.</summary>
    public readonly struct PlanAppliedEvent : IEvent
    {
        public readonly string Group, Variant, InstanceId, FallbackReason;
        public readonly double LatencyMs, Energy, Objective;
        /// <summary>Share of the escape nodes seen from at least one assigned node (0..1).</summary>
        public readonly double CoordinationScore;
        public readonly bool HitLatencyBudget;
        public readonly AgentAssignment[] Assignments;

        public PlanAppliedEvent(string group, string variant, string instanceId, double latencyMs, double energy,
            double objective, double coordinationScore, bool hitLatencyBudget, string fallbackReason, AgentAssignment[] assignments)
        {
            Group = group; Variant = variant; InstanceId = instanceId; LatencyMs = latencyMs; Energy = energy;
            Objective = objective; CoordinationScore = coordinationScore; HitLatencyBudget = hitLatencyBudget;
            FallbackReason = fallbackReason; Assignments = assignments;
        }
    }

    /// <summary>A finished solve was thrown away (the situation changed while it ran).</summary>
    public readonly struct PlanDiscardedEvent : IEvent
    {
        public readonly string Group, Reason;
        public PlanDiscardedEvent(string group, string reason) { Group = group; Reason = reason; }
    }

    /// <summary>Whether the solver sidecar answers (checked when a remote variant is chosen).</summary>
    public readonly struct SidecarStatusEvent : IEvent
    {
        public readonly bool Online;
        public readonly string BaseUrl;
        public SidecarStatusEvent(bool online, string baseUrl) { Online = online; BaseUrl = baseUrl; }
    }

    /// <summary>The coordinator switched solver (debug console, A/B assignment).</summary>
    public readonly struct SolverChangedEvent : IEvent
    {
        public readonly string Variant, FallbackReason;
        public SolverChangedEvent(string variant, string fallbackReason) { Variant = variant; FallbackReason = fallbackReason; }
    }
}
