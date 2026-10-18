using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Solvers;
using ShadowVale.Content;
using ShadowVale.Data.Coordination;
using UnityEngine;

namespace ShadowVale.Tests.EditMode.AI
{
    /// <summary>
    /// M10: the game and the solver sidecar speak the same JSON. A renamed or dropped field would
    /// otherwise only show in game as a silent greedy fallback ("sidecar HTTP 422").
    /// Fixture: <c>python -m svl_solver.tools.export_contract Assets/Tests/EditMode/AI/Fixtures/solver_contract.json</c>
    /// (re-export whenever schemas.py changes).
    /// </summary>
    public class SolverContractTests
    {
        private static JObject _contract;

        private static JObject Contract =>
            _contract ??= JObject.Parse(File.ReadAllText(Path.Combine(Application.dataPath, "Tests/EditMode/AI/Fixtures/solver_contract.json")));

        private static string[] Strings(JToken t) => t.Values<string>().ToArray();

        [Test]
        public void TheRequestTheGameSendsHasExactlyTheSidecarsFields()
        {
            var request = new QuboRequest { InstanceId = "x", AgentCount = 2, NodeCount = 3, QMatrix = new double[36] };
            var sent = JObject.Parse(ContentJson.Serialize(request)).Properties().Select(p => p.Name).ToList();
            var fields = Strings(Contract["qubo_request"]["fields"]);
            CollectionAssert.IsSubsetOf(Strings(Contract["qubo_request"]["required"]), sent, "Every required field is sent.");
            CollectionAssert.IsSubsetOf(sent, fields, "Nothing the sidecar does not know (it would be dropped silently).");
            CollectionAssert.AreEquivalent(fields, sent, "Every field the sidecar reads is filled by the game.");
        }

        [Test]
        public void ASidecarPlanParsesIntoEveryField()
        {
            var plan = ContentJson.Deserialize<CoordinationPlan>(Contract["sample_plan"].ToString());
            var sample = (JObject)Contract["sample_plan"];
            Assert.AreEqual(sample["agent_to_target"].Values<int>().ToArray(), plan.AgentToTarget);
            Assert.AreEqual((string)sample["solver_variant_id"], plan.SolverVariantId);
            Assert.AreEqual((double)sample["energy"], plan.Energy, 1e-12);
            Assert.AreEqual((int)sample["samples_evaluated"], plan.SamplesEvaluated);
            Assert.AreEqual("contract-sample", plan.InstanceId);
            Assert.IsTrue(plan.IsFeasible(2, 6));

            var known = JObject.Parse(ContentJson.Serialize(plan)).Properties().Select(p => p.Name).ToList();
            foreach (var field in Strings(Contract["coordination_plan"]["fields"]).Where(f => f != "fallback_reason"))
                CollectionAssert.Contains(known, field, $"CoordinationPlan has no property for the sidecar's '{field}'.");
        }

        [Test]
        public void EverySolverTheGameOffersExistsInTheSidecar()
        {
            var sidecar = Strings(Contract["variants"]);
            foreach (SolverVariant v in Enum.GetValues(typeof(SolverVariant)))
                CollectionAssert.Contains(sidecar, v.ToId(), $"The game offers '{v.ToId()}', the sidecar does not know it.");
        }

        [Test]
        public void ThePythonSampleRequestParsesInTheGame()
        {
            var r = ContentJson.Deserialize<QuboRequest>(Contract["sample_request"].ToString());
            Assert.AreEqual(2, r.AgentCount);
            Assert.AreEqual(6, r.NodeCount);
            Assert.AreEqual(r.VariableCount() * r.VariableCount(), r.QMatrix.Length);
            Assert.AreEqual("qiea", r.Variant);
        }
    }
}
