using ShadowVale.Data.Content;

namespace ShadowVale.AI.Coordination
{
    /// <summary>
    /// Objective weights of the coordination QUBO — the same five as <c>svl_solver.model.instance.Weights</c>.
    /// All non-negative. See <see cref="QuboBuilder"/> for how they enter the energy.
    /// </summary>
    public readonly struct CoordinationWeights
    {
        /// <summary>Reward per escape node seen from an agent's node.</summary>
        public readonly double Coverage;
        /// <summary>Penalty per escape node seen by two agents (pushes distinct coverage).</summary>
        public readonly double Redundancy;
        /// <summary>Reward for agent pairs on opposite sides of the player.</summary>
        public readonly double Flanking;
        /// <summary>Reward for standing at cover that faces the player.</summary>
        public readonly double Cover;
        /// <summary>Cost per graph hop of travel.</summary>
        public readonly double Distance;

        public CoordinationWeights(double coverage, double redundancy, double flanking, double cover, double distance)
        {
            Coverage = coverage; Redundancy = redundancy; Flanking = flanking; Cover = cover; Distance = distance;
        }

        public static CoordinationWeights Default => new CoordinationWeights(1.0, 0.5, 1.0, 0.5, 0.1);

        public static CoordinationWeights From(AiSettings ai) => ai == null ? Default
            : new CoordinationWeights(ai.WeightCoverage, ai.WeightRedundancy, ai.WeightFlanking, ai.WeightCover, ai.WeightDistance);
    }
}
