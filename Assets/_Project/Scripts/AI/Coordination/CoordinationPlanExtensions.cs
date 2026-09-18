using System.Collections.Generic;
using ShadowVale.Data.Coordination;

namespace ShadowVale.AI.Coordination
{
    public static class CoordinationPlanExtensions
    {
        /// <summary>Every agent has a target inside [0, nodeCount) and no two agents share one.</summary>
        public static bool IsFeasible(this CoordinationPlan plan, int agentCount, int nodeCount)
        {
            if (plan?.AgentToTarget == null || plan.AgentToTarget.Length != agentCount) return false;
            var seen = new HashSet<int>();
            foreach (var t in plan.AgentToTarget)
            {
                if (t < 0 || t >= nodeCount) return false;
                if (!seen.Add(t)) return false;
            }
            return true;
        }
    }
}
