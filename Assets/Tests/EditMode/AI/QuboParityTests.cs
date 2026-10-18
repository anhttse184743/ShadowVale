using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Solvers.Classical;
using ShadowVale.Data.Coordination;
using UnityEngine;

namespace ShadowVale.Tests.EditMode.AI
{
    /// <summary>
    /// The C# QUBO must equal the Python solver repo's, number for number, or the in-game
    /// solver and the offline benchmark would be optimising different problems (RQ2).
    /// Fixture: <c>python -m svl_solver.tools.export_parity Assets/Tests/EditMode/AI/Fixtures/qubo_parity.json</c>.
    /// </summary>
    public class QuboParityTests
    {
        private const double Tolerance = 1e-9;

        private static JArray Cases()
        {
            string path = Path.Combine(Application.dataPath, "Tests/EditMode/AI/Fixtures/qubo_parity.json");
            return (JArray)JObject.Parse(File.ReadAllText(path))["cases"];
        }

        private static double[] D(JToken t) => t.Select(v => (double)v).ToArray();

        private static (InstanceFeatures f, CoordinationWeights w) Rebuild(JToken c)
        {
            int n = (int)c["node_count"], a = (int)c["agent_count"];
            var pos = c["positions"].Select(p => new[] { (double)p[0], (double)p[1] }).ToArray();
            var w = c["weights"];
            var weights = new CoordinationWeights((double)w["coverage"], (double)w["redundancy"], (double)w["flanking"],
                (double)w["cover"], (double)w["distance"]);
            var f = InstanceFeatures.Compute(
                pos.Select(p => p[0]).ToArray(), pos.Select(p => p[1]).ToArray(), (int)c["player"],
                c["is_cover"].Select(v => (double)v > .5).ToArray(), D(c["cover_facing"]),
                c["escape"].Select(v => (int)v).ToArray(), c["visible"].Select(v => (int)v == 1).ToArray(),
                D(c["dist"]), a);
            Assert.AreEqual(n, f.NodeCount);
            return (f, weights);
        }

        private static void Same(double[] expected, double[] actual, string what)
        {
            Assert.AreEqual(expected.Length, actual.Length, what + " length");
            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i], Tolerance, $"{what}[{i}]");
        }

        [Test]
        public void FeaturesMatchThePythonInstance()
        {
            foreach (var c in Cases())
            {
                var (f, _) = Rebuild(c);
                string id = (string)c["instance_id"];
                Same(D(c["cov"]), f.Cov, id + " cov");
                Same(D(c["overlap"]), f.Overlap, id + " overlap");
                Same(D(c["flank"]), f.Flank, id + " flank");
                Same(D(c["cover_q"]), f.CoverQ, id + " cover_q");
            }
        }

        [Test]
        public void LambdaQAndOffsetMatchThePythonFormulation()
        {
            foreach (var c in Cases())
            {
                var (f, w) = Rebuild(c);
                string id = (string)c["instance_id"];
                var q = QuboBuilder.Build(f, w, out double offset, out double lambda);
                Assert.AreEqual((double)c["lambda"], lambda, Tolerance, id + " λ");
                Assert.AreEqual((double)c["offset"], offset, Tolerance, id + " offset");
                Same(D(c["q"]), q, id + " Q");

                // Agent i on node i (feasible): same energy as the Python problem gives it.
                int a = f.AgentCount, n = f.NodeCount;
                var x = new bool[a * n];
                for (int i = 0; i < a; i++) x[i * n + i] = true;
                Assert.AreEqual((double)c["greedy_energy_sample"], GreedySolver.Energy(q, x, a * n) + offset, 1e-6, id + " energy");
            }
        }

        [Test]
        public void GreedyOnTheCSharpQuboIsAlwaysFeasible()
        {
            foreach (var c in Cases())
            {
                var (f, w) = Rebuild(c);
                var q = QuboBuilder.Build(f, w, out double offset, out _);
                var plan = new GreedySolver().Solve(new QuboRequest
                    { AgentCount = f.AgentCount, NodeCount = f.NodeCount, QMatrix = q, Offset = offset });
                Assert.IsTrue(plan.IsFeasible(f.AgentCount, f.NodeCount), (string)c["instance_id"]);
            }
        }
    }
}
