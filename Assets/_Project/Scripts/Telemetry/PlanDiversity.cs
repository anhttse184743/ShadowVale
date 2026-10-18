using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ShadowVale.Telemetry
{
    /// <summary>
    /// How predictable the squad's plans are — the first of the two "less-exploitable" measures
    /// (the other is exploit bots in the replay harness). Positions are snapped to a
    /// <see cref="CellSize"/> grid, so a plan is "the same" when every guard is sent to the same
    /// spot, whatever the local node numbering of the solve was.
    /// <list type="bullet">
    /// <item><see cref="PlanEntropyBits"/>: entropy of the whole-plan distribution; 0 = one plan over and over, log2(n) = never twice the same.</item>
    /// <item><see cref="AgentPositionEntropyBits"/>: mean over guards (with two plans or more) of the entropy of where each was sent.</item>
    /// </list>
    /// Only meaningful across comparable situations: in-game, per session as a rough signal; in the
    /// replay harness, across runs of the same seeded scenario.
    /// </summary>
    public sealed class PlanDiversity
    {
        public const float CellSize = 4f;

        private readonly Dictionary<string, int> _plans = new Dictionary<string, int>();
        private readonly Dictionary<string, Dictionary<long, int>> _agentCells = new Dictionary<string, Dictionary<long, int>>();

        public int Plans { get; private set; }
        public int DistinctPlans => _plans.Count;

        public void Add(IReadOnlyList<(string agentId, Vector3 position)> assignments)
        {
            if (assignments == null || assignments.Count == 0) return;
            var sb = new StringBuilder();
            foreach (var (agent, pos) in assignments.OrderBy(a => a.agentId, StringComparer.Ordinal))
            {
                long cell = Cell(pos);
                sb.Append(agent).Append('@').Append(cell.ToString(CultureInfo.InvariantCulture)).Append(';');
                if (!_agentCells.TryGetValue(agent, out var cells)) _agentCells[agent] = cells = new Dictionary<long, int>();
                cells[cell] = cells.TryGetValue(cell, out int c) ? c + 1 : 1;
            }
            string key = sb.ToString();
            _plans[key] = _plans.TryGetValue(key, out int n) ? n + 1 : 1;
            Plans++;
        }

        public double PlanEntropyBits => Entropy(_plans.Values);

        public double AgentPositionEntropyBits
        {
            get
            {
                var perAgent = _agentCells.Values.Where(c => c.Values.Sum() >= 2).Select(c => Entropy(c.Values)).ToList();
                return perAgent.Count > 0 ? perAgent.Average() : 0;
            }
        }

        private static long Cell(Vector3 p)
        {
            long x = (long)Mathf.Floor(p.x / CellSize), z = (long)Mathf.Floor(p.z / CellSize);
            return (x << 32) ^ (z & 0xffffffffL);
        }

        /// <summary>Shannon entropy in bits of a frequency table.</summary>
        public static double Entropy(IEnumerable<int> counts)
        {
            var list = counts.Where(c => c > 0).ToList();
            double total = list.Sum();
            if (total <= 0) return 0;
            double h = 0;
            foreach (int c in list) { double p = c / total; h -= p * Math.Log(p, 2); }
            return h;
        }
    }
}
