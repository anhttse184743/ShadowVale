using NUnit.Framework;
using UnityEngine;

namespace ShadowVale.Map01.Tests
{
    public sealed class MapExplorationTests
    {
        private static readonly Rect Bounds = new Rect(-110, -100, 220, 200);
        [Test] public void VisitingRevealsOnlyNearbyCellsAndNeverClearsPreviousVisits()
        {
            var map = new MapExploration(Bounds);
            Assert.IsFalse(map.IsExplored(Vector3.zero));
            Assert.IsTrue(map.Reveal(Vector3.zero, 14));
            Assert.IsTrue(map.IsExplored(new Vector3(5, 100, 5)));
            Assert.IsFalse(map.IsExplored(new Vector3(30, 0, 0)));
            Assert.IsFalse(map.Reveal(Vector3.zero, 14));
            map.Reveal(new Vector3(70, 0, 70), 14);
            Assert.IsTrue(map.IsExplored(Vector3.zero));
            Assert.IsFalse(map.IsExplored(new Vector3(35, 0, 35)), "Teleporting must not reveal a line through unvisited land.");
        }
        [Test] public void DiscoveryClipsAtPhysicalBoundary()
        {
            var map = new MapExploration(Bounds);
            map.Reveal(new Vector3(110, 0, 100), 14);
            Assert.IsTrue(map.IsExplored(new Vector3(109, 0, 99)));
            Assert.IsFalse(map.IsExplored(new Vector3(111, 0, 100)));
            Assert.IsFalse(map.Reveal(new Vector3(200, 0, 100), 14));
        }
        [Test] public void SaveRoundTripKeepsExplorationAndUsesSmallPayload()
        {
            var map = new MapExploration(Bounds);map.Reveal(new Vector3(-65, 0, -63), 14);
            var state = JsonUtility.FromJson<MapExplorationState>(JsonUtility.ToJson(map.Capture()));
            var restored = new MapExploration(Bounds);restored.Restore(state);
            Assert.IsTrue(restored.IsExplored(new Vector3(-65, 0, -63)));
            Assert.IsFalse(restored.IsExplored(new Vector3(65, 0, 63)));
            Assert.Less(state.cells.Length, 3000);
            map.Reveal(new Vector3(65, 0, 63), 14);
            Assert.IsFalse(restored.IsExplored(new Vector3(65, 0, 63)), "Checkpoint must own a copy of discovery state.");
        }
        [Test] public void MissingMalformedOrDifferentLayoutStateStartsUnexplored()
        {
            var map = new MapExploration(Bounds);
            foreach(var state in new[] { null, new MapExplorationState {resolution=128,bounds=Bounds,cells="invalid"},
                new MapExplorationState {resolution=128,bounds=Bounds,cells="AA=="},
                new MapExplorationState {resolution=64,bounds=Bounds,cells="AA=="} }) {
                map.Reveal(Vector3.zero,14);Assert.DoesNotThrow(()=>map.Restore(state));Assert.IsFalse(map.IsExplored(Vector3.zero));
            }
            var other=new MapExploration(new Rect(0,0,220,200));other.Reveal(new Vector3(10,0,10),14);
            map.Restore(other.Capture());Assert.IsFalse(map.IsExplored(new Vector3(10,0,10)));
        }
        [Test] public void BakedSurveyMatchesPhysicalPlayableBoundsAndSafeCamp()
        {
            var survey=Resources.Load<Map01MapSurvey>("Maps/Map01Survey");
            Assert.NotNull(survey);Assert.NotNull(survey.chart);Assert.AreEqual(Bounds,survey.bounds);
            Assert.AreEqual(1100,survey.chart.width);Assert.AreEqual(1000,survey.chart.height);
            Assert.Less(Vector3.Distance(survey.safeCamp,new Vector3(-65,.12f,-63)),.01f);
        }
    }
}
