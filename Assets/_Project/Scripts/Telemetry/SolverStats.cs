using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Data.Coordination;

namespace ShadowVale.Telemetry
{
    /// <summary>
    /// Latency and quality of every plan the coordinator applied this session (RQ3), folded into
    /// <see cref="SolverMetrics"/>. Percentiles interpolate linearly, as numpy's default does, so the
    /// numbers match what the Python bench reports for the same latencies.
    /// </summary>
    public sealed class SolverStats
    {
        private readonly List<double> _latency = new List<double>();
        private readonly Dictionary<string, int> _byVariant = new Dictionary<string, int>();
        private double _objectiveSum, _scoreSum;

        public int Count => _latency.Count;
        public int Timeouts { get; private set; }
        public int Fallbacks { get; private set; }
        public IReadOnlyDictionary<string, int> PlansByVariant => _byVariant;

        public void Add(string variant, double latencyMs, bool hitBudget, bool fellBack, double objective, double coordinationScore)
        {
            _latency.Add(latencyMs);
            if (hitBudget) Timeouts++;
            if (fellBack) Fallbacks++;
            _objectiveSum += objective;
            _scoreSum += coordinationScore;
            variant ??= "";
            _byVariant[variant] = _byVariant.TryGetValue(variant, out int n) ? n + 1 : 1;
        }

        public double CoordinationScoreMean => Count > 0 ? _scoreSum / Count : 0;
        public double FallbackRate => Count > 0 ? (double)Fallbacks / Count : 0;

        /// <summary>The variant most plans came from; <paramref name="fallback"/> when nothing was planned.</summary>
        public string MainVariant(string fallback) =>
            _byVariant.Count == 0 ? fallback : _byVariant.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).First().Key;

        public SolverMetrics ToMetrics(string configuredVariant)
        {
            var sorted = _latency.OrderBy(v => v).ToList();
            return new SolverMetrics
            {
                SolverVariantId = MainVariant(configuredVariant),
                SolveCount = Count,
                TimeoutCount = Timeouts,
                FallbackCount = Fallbacks,
                LatencyP50Ms = Math.Round(Percentile(sorted, 50), 3),
                LatencyP95Ms = Math.Round(Percentile(sorted, 95), 3),
                LatencyMaxMs = Math.Round(sorted.Count > 0 ? sorted[sorted.Count - 1] : 0, 3),
                ObjectiveMean = Count > 0 ? Math.Round(_objectiveSum / Count, 6) : 0,
                CoordinationScore = Math.Round(CoordinationScoreMean, 4),
            };
        }

        /// <summary>Linear-interpolation percentile of an ascending list (numpy <c>method="linear"</c>); 0 when empty.</summary>
        public static double Percentile(IReadOnlyList<double> sorted, double p)
        {
            if (sorted.Count == 0) return 0;
            double rank = p / 100.0 * (sorted.Count - 1);
            int lo = (int)Math.Floor(rank), hi = (int)Math.Ceiling(rank);
            return sorted[lo] + (sorted[hi] - sorted[lo]) * (rank - lo);
        }
    }
}
