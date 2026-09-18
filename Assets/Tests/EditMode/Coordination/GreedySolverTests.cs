using System.Threading;
using NUnit.Framework;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Solvers;
using ShadowVale.AI.Solvers.Classical;
using ShadowVale.Content;
using ShadowVale.Data.Coordination;

namespace ShadowVale.Tests.EditMode.Coordination
{
    /// <summary>
    /// The C# greedy must behave exactly like svl_solver/solvers/greedy.py. Q matrices here are tiny
    /// and hand-written (upper-triangular, flat row-major), same layout as the Python side.
    /// </summary>
    public class GreedySolverTests
    {
        /// <summary>2 agents × 3 nodes. Diagonal = linear terms; conflicts (same node) +10; same agent two nodes +20.</summary>
        private static QuboRequest TwoAgentsThreeNodes()
        {
            const int a = 2, n = 3, size = a * n;
            var q = new double[size * size];
            void Set(int i, int j, double v) => q[i * size + j] = v;

            // agent 0 prefers node 1 (−5), then node 0 (−2); agent 1 prefers node 1 (−5), then node 2 (−3)
            Set(0, 0, -2); Set(1, 1, -5); Set(2, 2, -1);
            Set(3, 3, -1); Set(4, 4, -5); Set(5, 5, -3);
            // C1: same agent, two nodes → +20
            for (var ag = 0; ag < a; ag++)
                for (var u = 0; u < n; u++)
                    for (var v = u + 1; v < n; v++)
                        Set(ag * n + u, ag * n + v, 20);
            // C2: two agents same node → +10
            for (var node = 0; node < n; node++) Set(node, n + node, 10);

            return new QuboRequest { InstanceId = "hand", AgentCount = a, NodeCount = n, QMatrix = q, Offset = 0, LatencyBudgetMs = 120 };
        }

        [Test]
        public void Greedy_PlacesAgentsInOrder_SecondAgentAvoidsTakenNode()
        {
            var plan = new GreedySolver(120).Solve(TwoAgentsThreeNodes());
            // agent 0 takes node 1 (−5). agent 1: node 1 would cost −5+10 = +5, node 2 costs −3 → node 2.
            CollectionAssert.AreEqual(new[] { 1, 2 }, plan.AgentToTarget);
            Assert.AreEqual(-8.0, plan.Energy, 1e-9);
            Assert.AreEqual(8.0, plan.ObjectiveValue, 1e-9);
            Assert.IsTrue(plan.FeasibleRaw);
            Assert.IsFalse(plan.HitLatencyBudget);
            Assert.AreEqual("greedy", plan.SolverVariantId);
            Assert.IsTrue(plan.IsFeasible(2, 3));
        }

        [Test]
        public void Greedy_IsFastOnGameSizedInstance()
        {
            // 12 agents × 100 nodes = 1200 vars → 1.44 M entries; random-ish upper-triangular Q
            const int a = 12, n = 100, size = a * n;
            var q = new double[size * size];
            var rng = new System.Random(7);
            for (var i = 0; i < size; i++)
            {
                q[i * size + i] = -rng.NextDouble() * 5;
                for (var j = i + 1; j < size; j++)
                    if ((j % 37) == 0) q[i * size + j] = rng.NextDouble();
            }
            var req = new QuboRequest { AgentCount = a, NodeCount = n, QMatrix = q, LatencyBudgetMs = 120 };
            var plan = new GreedySolver(120).Solve(req);
            Assert.IsTrue(plan.IsFeasible(a, n));
            Assert.Less(plan.SolveLatencyMs, 120, "greedy must fit the latency budget on the largest in-scope instance");
        }

        [Test]
        public void Greedy_AcceptsSymmetricMatrixWithSameResult()
        {
            var upper = TwoAgentsThreeNodes();
            var size = upper.VariableCount;
            var sym = new double[size * size];
            for (var i = 0; i < size; i++)
                for (var j = 0; j < size; j++)
                    sym[i * size + j] = i == j ? upper.QMatrix[i * size + j] : (upper.QMatrix[i * size + j] + upper.QMatrix[j * size + i]) / 2.0;
            var req = new QuboRequest { AgentCount = 2, NodeCount = 3, QMatrix = sym };
            var plan = new GreedySolver().Solve(req);
            CollectionAssert.AreEqual(new[] { 1, 2 }, plan.AgentToTarget);
            Assert.AreEqual(-8.0, plan.Energy, 1e-9);
        }

        [Test]
        public void SolverFactory_FallsBackToGreedyForRemoteVariantsUntilP4()
        {
            var s = SolverFactory.Create("sqa", 120, out var reason);
            Assert.AreEqual("greedy", s.VariantId);
            StringAssert.Contains("sqa", reason);

            var g = SolverFactory.Create("greedy", 90, out var none);
            Assert.IsNull(none);
            Assert.AreEqual(90, g.LatencyBudgetMs);
        }

        [Test]
        public void QuboRequest_RoundTripsThroughSnakeCaseJson()
        {
            var req = TwoAgentsThreeNodes();
            var json = ContentJson.Serialize(req);
            StringAssert.Contains("\"q_matrix\"", json);
            StringAssert.Contains("\"agent_count\"", json);
            StringAssert.Contains("\"latency_budget_ms\"", json);
            var back = ContentJson.Deserialize<QuboRequest>(json);
            Assert.AreEqual(req.QMatrix.Length, back.QMatrix.Length);
            Assert.AreEqual(2, back.AgentCount);

            var plan = new GreedySolver().SolveAsync(back, CancellationToken.None).Result;
            var planJson = ContentJson.Serialize(plan);
            StringAssert.Contains("\"agent_to_target\"", planJson);
            StringAssert.Contains("\"hit_latency_budget\"", planJson);
            StringAssert.Contains("\"solver_variant_id\"", planJson);
        }
    }
}
