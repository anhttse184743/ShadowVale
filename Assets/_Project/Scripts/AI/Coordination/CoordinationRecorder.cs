using System;
using System.Globalization;
using System.IO;
using System.Text;
using ShadowVale.Data.Coordination;
using UnityEngine;

namespace ShadowVale.AI.Coordination
{
    /// <summary>
    /// Appends every in-game coordination instance and its plan to
    /// <c>persistentDataPath/research/coordination-&lt;session&gt;.jsonl</c>: the raw inputs (positions,
    /// cover, facing, escape nodes, visibility, distances, weights) are enough for the Python bench to
    /// rebuild the exact QUBO and run every solver on real game situations (O4/O5, deterministic replay).
    /// </summary>
    public sealed class CoordinationRecorder
    {
        public string Path { get; }

        public CoordinationRecorder(string session)
        {
            string dir = System.IO.Path.Combine(Application.persistentDataPath, "research");
            Directory.CreateDirectory(dir);
            Path = System.IO.Path.Combine(dir, $"coordination-{session}.jsonl");
        }

        public void Record(string scene, string group, string trigger, CoordinationInstance inst, CoordinationPlan plan, float time)
        {
            var f = inst.Features; var r = inst.Request; var w = inst.Weights;
            var sb = new StringBuilder(4096);
            sb.Append('{');
            Str(sb, "instance_id", r.InstanceId); Num(sb, "seed", r.Seed); Str(sb, "scene", scene);
            Str(sb, "group", group); Str(sb, "trigger", trigger); Num(sb, "time", time);
            Num(sb, "agent_count", r.AgentCount); Num(sb, "node_count", r.NodeCount); Num(sb, "player_local", inst.Sub.PlayerLocal);
            Arr(sb, "agent_local", inst.Sub.AgentLocal); Arr(sb, "global_nodes", inst.Sub.Nodes); Arr(sb, "escape", f.Escape);
            Arr(sb, "x", inst.X); Arr(sb, "z", inst.Z); Arr(sb, "cover_facing", inst.CoverFacing);
            sb.Append("\"is_cover\":\"");
            foreach (bool c in inst.IsCover) sb.Append(c ? '1' : '0');
            sb.Append("\",\"visible\":\"");
            foreach (bool v in f.Visible) sb.Append(v ? '1' : '0');
            sb.Append("\",");
            Arr(sb, "dist", f.Dist);
            sb.Append("\"weights\":{");
            Num(sb, "coverage", w.Coverage); Num(sb, "redundancy", w.Redundancy); Num(sb, "flanking", w.Flanking);
            Num(sb, "cover", w.Cover); Num(sb, "distance", w.Distance, last: true);
            sb.Append("},");
            Num(sb, "lambda", inst.Lambda); Num(sb, "offset", r.Offset);
            Str(sb, "variant", plan.SolverVariantId); Arr(sb, "plan", plan.AgentToTarget);
            Num(sb, "energy", plan.Energy); Num(sb, "latency_ms", plan.SolveLatencyMs);
            Num(sb, "samples", plan.SamplesEvaluated);
            sb.Append("\"hit_budget\":").Append(plan.HitLatencyBudget ? "true" : "false").Append(',');
            Str(sb, "fallback", plan.FallbackReason ?? "", last: true);
            sb.Append('}').Append('\n');
            try { File.AppendAllText(Path, sb.ToString(), new UTF8Encoding(false)); }
            catch (IOException e) { Debug.LogWarning("[Coordination] recorder: " + e.Message); }
        }

        private static void Str(StringBuilder sb, string key, string value, bool last = false)
        {
            sb.Append('"').Append(key).Append("\":\"").Append((value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
            if (!last) sb.Append(',');
        }

        private static void Num(StringBuilder sb, string key, double value, bool last = false)
        {
            sb.Append('"').Append(key).Append("\":").Append(value.ToString("R", CultureInfo.InvariantCulture));
            if (!last) sb.Append(',');
        }

        private static void Arr(StringBuilder sb, string key, int[] values)
        {
            sb.Append('"').Append(key).Append("\":[");
            for (int i = 0; i < values.Length; i++) { if (i > 0) sb.Append(','); sb.Append(values[i].ToString(CultureInfo.InvariantCulture)); }
            sb.Append("],");
        }

        private static void Arr(StringBuilder sb, string key, double[] values)
        {
            sb.Append('"').Append(key).Append("\":[");
            for (int i = 0; i < values.Length; i++) { if (i > 0) sb.Append(','); sb.Append(values[i].ToString("R", CultureInfo.InvariantCulture)); }
            sb.Append("],");
        }
    }
}
