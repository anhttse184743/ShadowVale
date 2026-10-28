using System;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Solvers.Remote;
using ShadowVale.Data.Coordination;

namespace ShadowVale.Tests.EditMode.Coordination
{
    /// <summary>
    /// M5: the game never waits past the budget and never goes without a plan — whether the sidecar
    /// is absent, slow, or answering. The live test runs only when a sidecar listens on
    /// SVL_TEST_SIDECAR_URL (default http://127.0.0.1:8011).
    /// </summary>
    public class RemoteSolverClientTests
    {
        private static QuboRequest TwoByThree()
        {
            // 2 agents × 3 nodes, a small feasible-by-greedy upper-triangular Q.
            int size = 6;
            var q = new double[size * size];
            for (int i = 0; i < size; i++) q[i * size + i] = -10 - i;
            for (int a = 0; a < 2; a++)
            for (int u = 0; u < 3; u++)
            for (int v = u + 1; v < 3; v++) q[(a * 3 + u) * size + a * 3 + v] = 40;
            for (int n = 0; n < 3; n++) q[n * size + 3 + n] = 20;
            return new QuboRequest { InstanceId = "t", AgentCount = 2, NodeCount = 3, QMatrix = q, Offset = 40 };
        }

        [SetUp] public void Reset() => RemoteSolverClient.ResetBackoff();

        [Test]
        public void WithoutTheBootObjectTheDefaultProfileStillStartsTheSidecar()
        {
            ShadowVale.Core.Services.ServiceLocator.TryResolve<SolverProfileConfig>(out var before);
            ShadowVale.Core.Services.ServiceLocator.Unregister<SolverProfileConfig>();
            var custom = UnityEngine.ScriptableObject.CreateInstance<SolverProfileConfig>();
            try
            {
                var fallback = SolverProfileConfig.Current;
                Assert.IsNotNull(fallback, "A map played on its own has no GameBootstrap, yet a profile.");
                Assert.IsTrue(fallback.AutoLaunchSidecar, "So choosing a remote solver starts the sidecar.");
                Assert.AreEqual(SolverEndpoint.DefaultBaseUrl, fallback.SolverBaseUrl);
                Assert.AreSame(fallback, SolverProfileConfig.Current, "One instance, not one per call.");

                custom.AutoLaunchSidecar = false;
                ShadowVale.Core.Services.ServiceLocator.Register(custom);
                Assert.AreSame(custom, SolverProfileConfig.Current, "The Boot object's profile wins when there is one.");
            }
            finally
            {
                ShadowVale.Core.Services.ServiceLocator.Unregister<SolverProfileConfig>();
                if (before != null) ShadowVale.Core.Services.ServiceLocator.Register(before);
                UnityEngine.Object.DestroyImmediate(custom);
            }
        }

        [Test]
        public void NoSidecarMeansAnImmediateGreedyPlanWithTheReason()
        {
            var clock = Stopwatch.StartNew();
            var plan = new RemoteSolverClient("sqa", 120, "http://127.0.0.1:9").SolveAsync(TwoByThree(), CancellationToken.None).Result;
            // A closed port refuses at once on Linux/macOS but times out on Windows: either way within the budget.
            Assert.Less(clock.ElapsedMilliseconds, 120 + RemoteSolverClient.NetworkMarginMs + 250);
            Assert.IsTrue(plan.IsFeasible(2, 3));
            Assert.AreEqual("greedy", plan.SolverVariantId, "Telemetry records what actually solved it.");
            StringAssert.Contains("sqa", plan.FallbackReason);
            Assert.IsTrue(plan.FallbackReason.Contains("offline") || plan.FallbackReason.Contains("timeout"), plan.FallbackReason);

            RemoteSolverClient.MarkOffline();
            clock.Restart();
            plan = new RemoteSolverClient("sqa", 120, "http://127.0.0.1:9").SolveAsync(TwoByThree(), CancellationToken.None).Result;
            Assert.Less(clock.ElapsedMilliseconds, 60, "Known offline: greedy straight away, no waiting.");
            StringAssert.Contains("offline", plan.FallbackReason);
        }

        [Test]
        public void ASlowSidecarIsCutOffAtTheBudget()
        {
            int port = 18000 + Environment.TickCount % 1000;
            var listener = new HttpListener();
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.Start();
            var server = Task.Run(() =>
            {
                try
                {
                    var ctx = listener.GetContext();
                    Thread.Sleep(600);
                    ctx.Response.StatusCode = 200;
                    ctx.Response.Close();
                }
                catch (Exception) { /* Listener stopped. */ }
            });
            try
            {
                var clock = Stopwatch.StartNew();
                var plan = new RemoteSolverClient("qaoa", 60, $"http://127.0.0.1:{port}").SolveAsync(TwoByThree(), CancellationToken.None).Result;
                Assert.Less(clock.ElapsedMilliseconds, 400, "Budget 60 ms + margin, not the sidecar's 600 ms.");
                Assert.IsTrue(plan.HitLatencyBudget);
                StringAssert.Contains("timeout", plan.FallbackReason);
                Assert.IsTrue(plan.IsFeasible(2, 3));
            }
            finally { listener.Stop(); listener.Close(); server.Wait(2000); }
        }

        [Test]
        public void ALiveSidecarSolvesWithTheRequestedVariant()
        {
            string url = Environment.GetEnvironmentVariable("SVL_TEST_SIDECAR_URL") ?? "http://127.0.0.1:8011";
            if (!SidecarLauncher.IsHealthyAsync(url).Result) Assert.Ignore($"No sidecar at {url} (start: svl-api --port 8011).");
            var plan = new RemoteSolverClient("qiea", 500, url).SolveAsync(TwoByThree(), CancellationToken.None).Result;
            Assert.IsNull(plan.FallbackReason, plan.FallbackReason);
            Assert.AreEqual("qiea", plan.SolverVariantId);
            Assert.IsTrue(plan.IsFeasible(2, 3));
            Assert.Greater(plan.SamplesEvaluated, 0, "QIEA reports how many samples it evaluated.");
            Assert.AreEqual("t", plan.InstanceId);
        }
    }
}
