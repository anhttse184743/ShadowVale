using System.Linq;
using NUnit.Framework;
using ShadowVale.AI.Coordination;
using ShadowVale.Content;
using ShadowVale.Data.Content;

namespace ShadowVale.Tests.EditMode.AI
{
    /// <summary>M9: coordination comes from content — bundle-wide ai_settings.squad, overridden per map.</summary>
    public class CoordinationSettingsTests
    {
        [Test]
        public void AMapOverridesOnlyWhatItStatesOverTheBundle()
        {
            var ai = new AiSettings
            {
                DefaultSolverVariant = "qiea", LatencyBudgetMs = 90, ReplanCooldownMs = 500,
                Squad = new SquadAiSettings { MaxGroupAgents = 6, GroupMergeRange = 50, SquadAlertRange = 35 },
            };
            var village = new SquadAiSettings { GroupMergeRange = 25, ReinforcementRange = 0 };

            var bundleWide = CoordinationSettings.From(ai);
            Assert.AreEqual("qiea", bundleWide.Variant);
            Assert.AreEqual(90, bundleWide.LatencyBudgetMs);
            Assert.AreEqual(.5f, bundleWide.ReplanCooldownSeconds, 1e-6);
            Assert.AreEqual(6, bundleWide.MaxGroupAgents);
            Assert.AreEqual(50f, bundleWide.GroupMergeRange);
            Assert.AreEqual(2, bundleWide.MinGroupAgents, "Not stated anywhere: the coordinator's default.");

            var map = CoordinationSettings.From(ai, village);
            Assert.AreEqual(25f, map.GroupMergeRange, "The map's own value wins.");
            Assert.AreEqual(0f, map.ReinforcementRange, "0 is a value (no reinforcements), not 'unset'.");
            Assert.AreEqual(35f, map.SquadAlertRange, "Not overridden: the bundle's.");
            Assert.AreEqual(6, map.MaxGroupAgents);
        }

        [Test]
        public void TheFallbackBundleGivesMapOneItsTunedValues()
        {
            var s = CoordinationSettings.ForScene("Map 1");
            var d = new CoordinationSettings();
            Assert.AreEqual(d.MaxGroupAgents, s.MaxGroupAgents);
            Assert.AreEqual(d.GroupMergeRange, s.GroupMergeRange);
            Assert.AreEqual(d.SquadAlertRange, s.SquadAlertRange);
            Assert.AreEqual(d.ReinforcementRange, s.ReinforcementRange);
            Assert.AreEqual("greedy", s.Variant);
        }

        [Test]
        public void ContradictorySquadSettingsAreRejected()
        {
            Assert.IsTrue(new FallbackBundleProvider().TryLoad(out var bundle, out var error), error);
            Assert.IsNotNull(bundle.AiSettings.Squad, "ai_settings.squad ships in the fallback bundle.");
            CollectionAssert.IsEmpty(SchemaValidator.Validate(bundle));
            bundle.AiSettings.Squad.MinGroupAgents = 9;
            bundle.Maps.First().Coordination = new SquadAiSettings { RefreshSeconds = 0 };
            var errors = SchemaValidator.Validate(bundle);
            Assert.IsTrue(errors.Any(e => e.Contains("min_group_agents > max_group_agents")), string.Join("; ", errors));
            Assert.IsTrue(errors.Any(e => e.Contains(".coordination")), string.Join("; ", errors));
        }
    }
}
