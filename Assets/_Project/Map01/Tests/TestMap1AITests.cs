using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShadowVale.AI.Contracts;
using ShadowVale.Core.Services;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// "Test Map 1 AI" (ShadowVale ▸ Test Map 1 AI): Map 1's copy plays like Map 1 — mission, guards,
    /// its own tactical graph — but saves to files of its own, never into the game's slots.
    /// </summary>
    public sealed class TestMap1AITests : ForestSceneTestBase
    {
        private const string ScenePath = "Assets/_Project/Scenes/Maps/Test Map 1 AI.unity";

        [UnityTest]
        public IEnumerator TheTestMapPlaysLikeMapOneButSavesApart()
        {
            if (!File.Exists(ScenePath)) Assert.Ignore("No Test Map 1 AI yet (ShadowVale ▸ Test Map 1 AI ▸ Tạo).");
            EditorSceneManager.OpenScene(ScenePath);
            yield return new EnterPlayMode();
            yield return InPlayMode();
            yield return new ExitPlayMode();
        }

        private static IEnumerator InPlayMode()
        {
            yield return null;
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            Assert.IsNotNull(mission);
            Assert.IsTrue(mission.IsInitialized);
            Assert.AreEqual("Test Map 1 AI", mission.gameObject.scene.name);
            Assert.GreaterOrEqual(mission.Enemies.Length, 10, "Map 1's guards came along.");
            Assert.IsTrue(ServiceLocator.TryResolve<IWorldQuery>(out var graph), "Its own tactical graph (Resources/TacticalGraphs/Test Map 1 AI).");
            Assert.Greater(graph.NodeCount, 100);

            var save = mission.GetComponent<Map01SaveSystem>();
            Assert.IsTrue(save.IsSandbox);
            string slotZero = ForestSaveSlots.PathFor(0);
            bool hadSlot = File.Exists(slotZero);
            var before = hadSlot ? File.GetLastWriteTimeUtc(slotZero) : default;
            Assert.IsTrue(save.SaveSlot(0, out var error, automatic: true), error);
            Assert.AreEqual(hadSlot, File.Exists(slotZero), "The game's quick-save slot is left alone…");
            if (hadSlot) Assert.AreEqual(before, File.GetLastWriteTimeUtc(slotZero), "…not even rewritten.");
            string sandbox = Path.Combine(Application.persistentDataPath, "sandbox", "Test_Map_1_AI-slot-0.json");
            Assert.IsTrue(File.Exists(sandbox), "Saved to the test map's own file.");
            File.Delete(sandbox);
        }
    }
}
