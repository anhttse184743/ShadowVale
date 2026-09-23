using System.Collections.Generic;

namespace ShadowVale.AI.Contracts
{
    /// <summary>
    /// Snapshot of one squad at re-plan time. AgentRegistry (AI) implements it from live agents;
    /// the deterministic replay harness implements it from a recorded instance.
    /// </summary>
    public interface ISquadStateProvider
    {
        string SquadId { get; }
        int AgentCount { get; }
        /// <summary>Current nav-graph node of each living agent, index-aligned with the QUBO's agent axis.</summary>
        IReadOnlyList<int> AgentNodes { get; }
        int PlayerNode { get; }
    }
}
