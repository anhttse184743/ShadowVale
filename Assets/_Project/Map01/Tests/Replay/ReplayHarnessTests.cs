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
            Debug.Log($"[Replay] {runs} runs × {bots.Count} bots × {usable.Count} variants → {output}");

            int done = 0, total = runs * bots.Count * usable.Count;
            // Run-major: run i of every bot and variant before run i+1, so a cut-short experiment stays paired.
            for (int i = 0; i < runs; i++)
            foreach (var bot in bots)
            foreach (var variant in usable)
            {
                var spec = new ReplayHarness.Spec { Bot = bot, Variant = variant, Seed = seed0 + i, Run = i, Camp = camp, FramesPerSecond = fps };
                ReplayHarness.Result result = null;
                yield return ReplayHarness.RunOne(spec, r => result = r);
                ReplayHarness.Append(output, result);
                done++;
                Debug.Log($"[Replay] {done}/{total} {bot} {variant} seed {spec.Seed}: {result.Outcome} in {result.Seconds:0.0} s, " +
                          $"{result.Session.Summary.Plans} plans, coordination {result.Session.Summary.CoordinationScoreMean:0.00}");
            }
            Assert.AreEqual(total, done);
        }

        private static int Int(string key, int fallback) =>
            int.TryParse(Environment.GetEnvironmentVariable(key), out int v) && v > 0 ? v : fallback;

        private static List<string> List(string key, string fallback) =>
            (Environment.GetEnvironmentVariable(key) ?? fallback).Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
    }
}
