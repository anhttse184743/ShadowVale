using System;
using NUnit.Framework;
using ShadowVale.AI.Coordination;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// The game plays with the bundle's solver (QIEA, run by the Python sidecar). Map 1's tests plan
    /// with in-process greedy instead, so opening the map in a batch run never starts the sidecar and
    /// every plan lands at once. Tests about the sidecar or the replay choose their solver explicitly
    /// (<see cref="SquadCoordinator.SetVariant"/>), which this does not touch. An environment variable,
    /// not a static: entering play mode reloads the domain.
    /// </summary>
    [SetUpFixture]
    public sealed class SolverForTests
    {
        private string _previous;

        [OneTimeSetUp]
        public void PlanWithGreedy()
        {
            _previous = Environment.GetEnvironmentVariable(SquadCoordinator.VariantEnvironment);
            if (string.IsNullOrEmpty(_previous)) Environment.SetEnvironmentVariable(SquadCoordinator.VariantEnvironment, "greedy");
        }

        [OneTimeTearDown]
        public void Restore() => Environment.SetEnvironmentVariable(SquadCoordinator.VariantEnvironment, _previous);
    }
}
