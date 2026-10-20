using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// The supply and radio guards walk fixed beats round their area (Map01PatrolBeats), like the jetty
    /// squad: a sentry stays, the others share one loop well beyond the old 3 m triangles, spread round it,
    /// at a calm walk. C3's garrison keeps its own posts.
    /// </summary>
    public sealed class PatrolBeatTests : ForestSceneTestBase
    {
        [UnityTest]
        public IEnumerator SupplyAndRadioGuardsWalkFixedBeatsRoundTheirArea()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return InPlayMode();
            yield return new ExitPlayMode();
        }

        private static IEnumerator InPlayMode()
        {
            yield return null;
            yield return null;
            var layout = Object.FindFirstObjectByType<Map01OperationsLayout>();
            Assert.IsNotNull(layout, "Map 1's operations layout.");

            Check("supply", layout.supplyGuards, layout.keyKeeper, Map01PatrolBeats.SupplyBeat(layout));
            Check("radio", layout.radioGuards, layout.radioGuards.First(), Map01PatrolBeats.RadioBeat(layout));

            var c3 = JsonUtility.FromJson<Map01C3Mission.Layout>(Resources.Load<TextAsset>(Map01C3Mission.ResourcePath).text);
            var garrison = c3.roles.Select(r => r.guard).Where(n => !string.IsNullOrEmpty(n)).ToHashSet();
            foreach (var g in layout.commanderGuards.Where(g => g != null))
                Assert.AreNotEqual(Map01PatrolBeats.Pace, g.PatrolPace, g.name + " is C3's: it keeps the posts C3 gives it.");
            foreach (var g in layout.supplyGuards.Concat(layout.radioGuards).Where(g => g != null && garrison.Contains(g.name)))
                Assert.AreNotEqual(Map01PatrolBeats.Pace, g.PatrolPace, g.name + " belongs to C3's garrison.");
        }

        private static void Check(string area, Map01EnemyController[] guards, Map01EnemyController sentry, Map01PatrolBeats.Beat beat)
        {
            Assert.GreaterOrEqual(beat.Count, 4, $"The {area} beat goes round the area, not a triangle.");
            Assert.Greater(beat.Span, 12f, $"The {area} beat covers ground (the old loops were 3 m).");
            Assert.IsTrue(beat.Stops.All(s => s > 0) && beat.Yaws.All(y => !float.IsNaN(y)), "Every point: a stop and a way to look.");

            Assert.AreEqual(0, sentry.PatrolPoints.Count, $"The {area} sentry stays at his post.");
            var walkers = guards.Where(g => g != null && g != sentry && g.PatrolPace == Map01PatrolBeats.Pace).ToArray();
            Assert.GreaterOrEqual(walkers.Length, 2, $"The other {area} guards walk the beat.");
            foreach (var g in walkers)
            {
                Assert.AreEqual(beat.Count, g.PatrolPoints.Count, g.name);
                CollectionAssert.IsSubsetOf(g.PatrolPoints.ToArray(), beat.Points, g.name + " walks the area's beat.");
            }
            Assert.AreEqual(walkers.Length, walkers.Select(g => g.PatrolPoints[0]).Distinct().Count(),
                $"Each {area} guard starts at a different point: they do not walk in a clump.");
        }
    }
}
