using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using ShadowVale.AI.Solvers.Remote;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests.Replay
{
    /// <summary>
    /// The replay harness. <see cref="ExploitBotMatrix"/> is the experiment itself — explicit, so the
    /// normal test run skips it; start it with
    /// <c>-testFilter ShadowVale.Map01.Tests.Replay.ReplayHarnessTests.ExploitBotMatrix</c> and
    /// configure it through the environment:
    /// <list type="bullet">
    /// <item><c>SVL_REPLAY_RUNS</c> (30) runs per bot and variant, seeds <c>SVL_REPLAY_SEED</c> (1000) upward;</item>
    /// <item><c>SVL_REPLAY_VARIANTS</c> (greedy,qiea) — remote ones need the sidecar, skipped if it is down;</item>
    /// <item><c>SVL_REPLAY_BOTS</c> (same_route,same_bush,stone_lure), <c>SVL_REPLAY_CAMP</c> (1), <c>SVL_REPLAY_FPS</c> (30);</item>
    /// <item><c>SVL_REPLAY_OUT</c> the jsonl to append to (default research/replay-&lt;time&gt;.jsonl).</item>
    /// </list>
    /// Then: <c>svl-bench replay &lt;file&gt;</c>.
    /// </summary>
    public sealed class ReplayHarnessTests : ForestSceneTestBase
    {
        [UnityTest]
        public IEnumerator ASingleReplayRunPlaysTheBotAndWritesARecord()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return SmokeInPlayMode();
            yield return new ExitPlayMode();
        }

        private static IEnumerator SmokeInPlayMode()
        {
            var results = new List<ReplayHarness.Result>();
            var spec = new ReplayHarness.Spec { Bot = "same_route", Variant = "greedy", Seed = 7, MaxSeconds = 45f, SettleSeconds = 5f };
            yield return ReplayHarness.RunOne(spec, results.Add);
            var r = results.Single();
            Assert.Greater(r.Distance, 30f, "The bot walked the lane through the camp.");
            CollectionAssert.Contains(new[] { "captured", "escaped", "undetected" }, r.Outcome);
            Assert.Greater(r.Session.Summary.Encounters, 0, "Walking upright through a camp gets him seen.");
            Assert.Greater(r.Session.Solver.SolveCount, 0, "The camp's squad planned the fight.");
            Assert.AreEqual("greedy", r.Session.AiVariant);
            Assert.LessOrEqual(r.Seconds, spec.MaxSeconds + .1f);

            string json = ReplayHarness.ToJson(r);
            foreach (var key in new[] { "\"bot\":\"same_route\"", "\"variant\":\"greedy\"", "\"seed\":7", "\"outcome\":", "\"coordination_score\":", "\"plan_entropy_bits\":" })
                StringAssert.Contains(key, json);

            // The resume checkpoint reads back what was written.
            string tmp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "svl-replay-" + Guid.NewGuid().ToString("N") + ".jsonl");
            ReplayHarness.Append(tmp, r);
            Assert.IsTrue(ReplayHarness.FinishedRuns(tmp).Contains(("same_route", "greedy", 7)), "A finished run is skipped on resume.");
            System.IO.File.Delete(tmp);

            // Same seed, same bot, same variant: the same lane at the same moment.
            var again = new List<ReplayHarness.Result>();
            yield return ReplayHarness.RunOne(spec, again.Add);
            Assert.AreEqual(r.Distance, again[0].Distance, .5f, "A replay follows the same script.");
        }

        [UnityTest, Explicit("The experiment: minutes to hours. Run it on purpose."), Category("Replay"), Timeout(int.MaxValue)]
        public IEnumerator ExploitBotMatrix()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return MatrixInPlayMode();
            yield return new ExitPlayMode();
        }

        private static IEnumerator MatrixInPlayMode()
        {
            int runs = Int("SVL_REPLAY_RUNS", 30), seed0 = Int("SVL_REPLAY_SEED", 1000), camp = Int("SVL_REPLAY_CAMP", 1), fps = Int("SVL_REPLAY_FPS", 30);
            var variants = List("SVL_REPLAY_VARIANTS", "greedy,qiea");
            var bots = List("SVL_REPLAY_BOTS", string.Join(",", ReplayScenario.Bots));
            string url = SolverEndpoint.BaseUrl;
            bool sidecar = SidecarLauncher.IsHealthyAsync(url).Result;
            var usable = variants.Where(v => v == "greedy" || sidecar).ToList();
            foreach (var skipped in variants.Except(usable))
                Debug.LogWarning($"[Replay] {skipped} skipped: no sidecar at {url} (svl-api --port …, SVL_SOLVER_URL).");
            string output = ReplayHarness.DefaultOutputPath();
            // Checkpoint: the output is append-only, one line per finished run, so a run cut short
            // (crash, power, Ctrl+C) restarts where it stopped — runs already in the file are skipped.
            var finished = ReplayHarness.FinishedRuns(output);
            Debug.Log($"[Replay] {runs} runs × {bots.Count} bots × {usable.Count} variants → {output}" +
                      (finished.Count > 0 ? $" ({finished.Count} already done, resuming)" : ""));

            int done = 0, total = runs * bots.Count * usable.Count;
            // Run-major: run i of every bot and variant before run i+1, so a cut-short experiment stays paired.
            for (int i = 0; i < runs; i++)
            foreach (var bot in bots)
            foreach (var variant in usable)
            {
                var spec = new ReplayHarness.Spec { Bot = bot, Variant = variant, Seed = seed0 + i, Run = i, Camp = camp, FramesPerSecond = fps };
                if (finished.Contains((bot, variant, spec.Seed))) { done++; continue; }
                ReplayHarness.Result result = null;
                yield return ReplayHarness.RunOne(spec, r => result = r);
                ReplayHarness.Append(output, result);
                done++;
                Debug.Log($"[Replay] {done}/{total} {bot} {variant} seed {spec.Seed}: {result.Outcome} in {result.Seconds:0.0} s, " +
                          $"{result.Session.Summary.Plans} plans, coordination {result.Session.Summary.CoordinationScoreMean:0.00}");
            }
            Assert.AreEqual(total, done);
        }

        /// <summary>
        /// Calibration of the bots' difficulty knobs on the greedy baseline: each bot at a few knob
        /// values, <c>SVL_CAL_SEEDS</c> (8) seeds each (seeds 500+, apart from the experiment's 1000+).
        /// Prints capture rate and HP left per value and the value closest to 50 % capture, and writes
        /// them to research/replay-calibration-&lt;time&gt;.json. The chosen values become
        /// ReplayScenario.DefaultKnob, fixed before any solver is compared.
        /// </summary>
        [UnityTest, Explicit("Calibration run, minutes."), Category("Replay"), Timeout(int.MaxValue)]
        public IEnumerator CalibrateBots()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return CalibrateInPlayMode();
            yield return new ExitPlayMode();
        }

        private static IEnumerator CalibrateInPlayMode()
        {
            int seeds = Int("SVL_CAL_SEEDS", 8), camp = Int("SVL_REPLAY_CAMP", 1);
            // Coarse grid by default; a finer one per bot with e.g. SVL_CAL_GRID_SAME_ROUTE=10,11,12,13.
            var grid = new Dictionary<string, float[]>
            {
                ["same_route"] = Floats("SVL_CAL_GRID_SAME_ROUTE", new[] { 0f, 3f, 6f, 9f }),
                ["stone_lure"] = Floats("SVL_CAL_GRID_STONE_LURE", new[] { 0f, 3f, 6f, 9f }),
                ["same_bush"] = Floats("SVL_CAL_GRID_SAME_BUSH", new[] { 5f, 10f, 20f, 40f }),
            };
            var report = new List<string>();
            foreach (var bot in List("SVL_REPLAY_BOTS", string.Join(",", ReplayScenario.Bots)))
            {
                float bestKnob = float.NaN, bestDistance = float.MaxValue;
                var cells = new List<string>();
                foreach (float knob in grid[bot])
                {
                    int caught = 0; float hp = 0;
                    for (int s = 0; s < seeds; s++)
                    {
                        var spec = new ReplayHarness.Spec { Bot = bot, Variant = "greedy", Seed = 500 + s, Run = s, Camp = camp, Knob = knob };
                        ReplayHarness.Result result = null;
                        yield return ReplayHarness.RunOne(spec, r => result = r);
                        if (result.Outcome == "captured") caught++;
                        hp += result.PlayerHealth;
                    }
                    float rate = caught / (float)seeds;
                    Debug.Log($"[Calibrate] {bot} knob {knob}: capture {rate:P0}, HP left {hp / seeds:0.0}");
                    cells.Add($"{{\"knob\":{knob.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"capture_rate\":{rate.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)},\"hp_left\":{(hp / seeds).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)}}}");
                    if (Mathf.Abs(rate - .5f) < bestDistance) { bestDistance = Mathf.Abs(rate - .5f); bestKnob = knob; }
                }
                Debug.Log($"[Calibrate] {bot}: closest to 50 % capture at knob {bestKnob}");
                report.Add($"\"{bot}\":{{\"chosen\":{bestKnob.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"grid\":[{string.Join(",", cells)}]}}");
            }
            string path = System.IO.Path.Combine(Application.persistentDataPath, "research",
                "replay-calibration-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            System.IO.File.WriteAllText(path, "{\"game_version\":\"" + ReplayHarness.GameVersion + "\",\"seeds\":" + seeds + "," + string.Join(",", report) + "}");
            Debug.Log("[Calibrate] " + path);
        }

        private static float[] Floats(string key, float[] fallback)
        {
            var raw = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(raw)) return fallback;
            return raw.Split(',').Select(v => float.Parse(v.Trim(), System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        }

        private static int Int(string key, int fallback) =>
            int.TryParse(Environment.GetEnvironmentVariable(key), out int v) && v > 0 ? v : fallback;

        private static List<string> List(string key, string fallback) =>
            (Environment.GetEnvironmentVariable(key) ?? fallback).Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
    }
}
