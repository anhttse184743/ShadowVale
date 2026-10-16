using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    public sealed class EndingCutsceneTests : ForestSceneTestBase
    {
        /// <summary>
        /// The officer of point 3, seen and reported, falls: his own death clip plays as the finale (world time
        /// untouched, no saving meanwhile); after it Nam reports by radio for the way out, and that starts the extraction.
        /// </summary>
        [UnityTest]
        public IEnumerator DefeatUsesExistingDeathThenTheRetreatReportStartsExtraction()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var story = mission.GetComponent<Map01Story>();
            foreach (var enemy in mission.Enemies) enemy.enabled = false;
            // He holds his headquarters from the map's start (Map01C3Mission), never spawned at the boss stage.
            var boss = quest.CommanderEnemy;
            Assert.IsNotNull(boss, "The officer is at point 3 from the start.");
            quest.RestoreStoryStage(Map01Quest.BossStage, OfficerReported());
            Assert.AreSame(boss, quest.CommanderEnemy, "The boss stage brings no second officer.");
            Assert.AreEqual(1, mission.Enemies.Count(e => e.IsBoss));
            Assert.IsNotNull(boss.GetComponent<Health>(), "Boss health");

            boss.GetComponent<Health>().TakeDamage(999999, boss.transform.position, null);
            var ending = mission.GetComponent<Map01EndingCutscene>();
            Assert.IsNotNull(ending, "Ending created after lethal damage");
            Assert.IsTrue(ending.IsPlaying);
            Assert.IsTrue(mission.Cinematic);
            Assert.IsFalse(mission.CameraInputEnabled);
            Assert.IsFalse(mission.GetComponent<Map01SaveSystem>().CanSave());
            Assert.AreEqual(1, Time.timeScale, "Slow only the existing death clip, not world time.");
            CollectionAssert.Contains(boss.GetComponentInChildren<Animator>().runtimeAnimatorController.animationClips,
                ending.ReusedDeathClip);
            yield return WaitGameSeconds(.9f);
            Capture(mission.gameCamera, "collapse-start");
            yield return WaitGameSeconds(1.5f);
            Capture(mission.gameCamera, "collapse-middle");
            double deadline = Time.realtimeSinceStartupAsDouble + ending.Duration + 3;
            while (ending.IsPlaying && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsFalse(ending.IsPlaying);
            Assert.IsFalse(mission.Cinematic);
            Assert.IsTrue(boss.GetComponent<Health>().IsDead);
            Assert.AreEqual(Map01Quest.ReportBossStage, quest.Stage, "The finale leaves the retreat report to Nam.");
            var early = mission.GetComponentInChildren<Map01Extraction>();
            Assert.IsTrue(early == null || early.CurrentPhase == Map01Extraction.Phase.Dormant, "No extraction before the report.");
            Capture(mission.gameCamera, "collapse-final");

            // The radio report from where he stands gives the way out.
            Assert.IsTrue(story.SafeToReport);
            Assert.IsTrue(story.TryInteract());
            yield return ConfirmConversation(mission);
            Assert.AreEqual(Map01Quest.ExtractionStage, quest.Stage);
            Assert.AreEqual(Map01Extraction.Phase.Extraction, mission.GetComponentInChildren<Map01Extraction>().CurrentPhase,
                "The report was the radio call: straight on to the boat.");
            Assert.IsFalse(mission.Cinematic);
            Assert.AreEqual(1, Time.timeScale);
            Assert.IsFalse(ForestMenu.Visible);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SkipIsIdempotentAndLegacyVictoryDoesNotReplay()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            foreach (var enemy in mission.Enemies) enemy.enabled = false;
            quest.RestoreStoryStage(Map01Quest.BossStage, OfficerReported());
            var boss = quest.CommanderEnemy;
            Assert.IsNotNull(boss, "The officer is at point 3.");
            boss.GetComponent<Health>().TakeDamage(999999, boss.transform.position, null);
            var ending = mission.GetComponent<Map01EndingCutscene>();
            Assert.IsNotNull(ending, "Ending created after lethal damage");
            Assert.IsTrue(ending.IsPlaying);
            ending.Skip(); ending.Skip();
            Assert.IsFalse(ending.IsPlaying);
            Assert.IsFalse(mission.Cinematic);
            Assert.AreEqual(1, Time.timeScale);
            Assert.AreEqual(Map01Quest.ReportBossStage, quest.Stage, "Skipping the finale does not skip the retreat report.");
            quest.TryCompleteCommanderObjective(true);
            Assert.IsFalse(ending.IsPlaying, "The objective completes once: no second finale.");

            // A save that was already won before the reports existed (no story progress kept): it is done, no replay.
            quest.RestoreStage(Map01Quest.ReportBossStage);
            Assert.AreEqual(Map01Quest.CompleteStage, quest.Stage);
            Assert.IsFalse(ending.IsPlaying, "An old victorious save must not replay the death.");
            yield return new ExitPlayMode();
        }

        private static void Capture(Camera camera, string name)
        {
            Directory.CreateDirectory("Logs/Ending");
            var previous = camera.targetTexture; var active = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1280,720,24);
            var image = new Texture2D(1280,720,TextureFormat.RGB24,false);
            try {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0,0,1280,720),0,0); image.Apply();
                File.WriteAllBytes("Logs/Ending/" + name + ".png", image.EncodeToPNG());
            } finally {
                camera.targetTexture = previous; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image);
            }
        }
    }
}
