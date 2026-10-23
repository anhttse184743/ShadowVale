using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Solvers.Classical;
using ShadowVale.Content;
using ShadowVale.Data.Coordination;
using ShadowVale.Networking;

namespace ShadowVale.AI.Solvers.Remote
{
    /// <summary>
    /// A research solver (ga, sa, sqa, qiea, qaoa) running in the Python sidecar on loopback. The
    /// game waits at most the latency budget plus a small network margin; on timeout, an HTTP error
    /// or no sidecar, it answers with the in-process greedy plan at once and says why
    /// (<see cref="CoordinationPlan.FallbackReason"/>), so a re-plan is never late or missing.
    /// After a connection failure the sidecar is not asked again for a few seconds.
    /// Runs off the main thread: serialising a few hundred kB of Q never hitches a frame.
    /// </summary>
    public sealed class RemoteSolverClient : ISquadSolver
    {
        /// <summary>Loopback HTTP + JSON on top of the solver's own budget.</summary>
        public const int NetworkMarginMs = 40;
        private const double OfflineBackoffSeconds = 10;

        private static readonly HttpClient Http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        private static long _offlineUntilTicks;

        private readonly string _baseUrl;

        public string VariantId { get; }
        public int LatencyBudgetMs { get; }

        public RemoteSolverClient(string variantId, int latencyBudgetMs, string baseUrl = null)
        {
            VariantId = variantId; LatencyBudgetMs = Math.Max(1, latencyBudgetMs);
            _baseUrl = string.IsNullOrEmpty(baseUrl) ? SolverEndpoint.BaseUrl : baseUrl;
        }

        public static bool BackingOff => DateTime.UtcNow.Ticks < Interlocked.Read(ref _offlineUntilTicks);

        public Task<CoordinationPlan> SolveAsync(QuboRequest request, CancellationToken ct)
            => Task.Run(() => SolveCore(request, ct), ct);

        private async Task<CoordinationPlan> SolveCore(QuboRequest request, CancellationToken ct)
        {
            var clock = Stopwatch.StartNew();
            request.Variant = VariantId;
            request.LatencyBudgetMs = LatencyBudgetMs;
            if (BackingOff) return Fallback(request, "sidecar offline (retrying shortly)", false, clock);

            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(LatencyBudgetMs + NetworkMarginMs);
            try
            {
                using var content = new StringContent(ContentJson.Serialize(request), Encoding.UTF8, "application/json");
                using var response = await Http.PostAsync(Endpoints.SolverSolve(_baseUrl), content, deadline.Token).ConfigureAwait(false);
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) return Fallback(request, $"sidecar HTTP {(int)response.StatusCode}", false, clock);
                var plan = ContentJson.Deserialize<CoordinationPlan>(body);
                if (plan?.AgentToTarget == null) return Fallback(request, "sidecar sent no plan", false, clock);
                // What the game actually waited for, not just the solve inside the sidecar.
                plan.SolveLatencyMs = clock.Elapsed.TotalMilliseconds;
                plan.HitLatencyBudget |= plan.SolveLatencyMs > LatencyBudgetMs + NetworkMarginMs;
                if (string.IsNullOrEmpty(plan.InstanceId)) plan.InstanceId = request.InstanceId;
                return plan;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return Fallback(request, $"sidecar timeout ({LatencyBudgetMs + NetworkMarginMs} ms)", true, clock);
            }
            catch (HttpRequestException e)
            {
                MarkOffline();
                return Fallback(request, "sidecar offline: " + (e.InnerException?.Message ?? e.Message), false, clock);
            }
        }

        private CoordinationPlan Fallback(QuboRequest request, string reason, bool hitBudget, Stopwatch clock)
        {
            var plan = new GreedySolver(LatencyBudgetMs).Solve(request);
            plan.FallbackReason = $"{VariantId}: {reason}";
            plan.HitLatencyBudget = hitBudget;
            plan.SolveLatencyMs = clock.Elapsed.TotalMilliseconds;
            return plan;
        }

        /// <summary>
        /// Stop asking the sidecar for a while. Called on a refused connection and when a /health check
        /// fails — on Windows a closed port does not refuse, it times out, which would cost every
        /// re-plan its full budget.
        /// </summary>
        public static void MarkOffline(double seconds = OfflineBackoffSeconds)
            => Interlocked.Exchange(ref _offlineUntilTicks, DateTime.UtcNow.AddSeconds(seconds).Ticks);

        /// <summary>Forget a previous failure (a new variant was chosen; tests).</summary>
        public static void ResetBackoff() => Interlocked.Exchange(ref _offlineUntilTicks, 0);
    }
}
