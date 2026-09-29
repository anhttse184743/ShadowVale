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
        [UnityTest]
        public IEnumerator DefeatUsesExistingDeathAndFinishesMapWithoutReport()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            // Match the real route: all outpost guards have fallen before the boss arrives.
            foreach (var guard in mission.Enemies.Where(e => e.name.StartsWith("Outpost guard ")))
                guard.GetComponent<Health>().TakeDamage(999999, guard.transform.position, null);
            yield return WaitGameSeconds(5);
            quest.RestoreStage(Map01Quest.BossStage);
            var boss = mission.Enemies.Single(e => e.IsBoss);
            Assert.IsFalse(mission.Enemies.Any(e => e != boss && !e.Alive &&
                Vector3.Distance(e.transform.position, boss.transform.position) < 2.5f),
                "The boss must not spawn inside a fallen guard.");
            boss.GetComponent<Health>().TakeDamage(999999, boss.transform.position, null);
            var ending = mission.GetComponent<Map01EndingCutscene>();
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
            Assert.AreEqual(Map01Quest.CompleteStage, quest.Stage);
            Assert.IsTrue(boss.GetComponent<Health>().IsDead);
            Assert.AreEqual(1, Time.timeScale);
            Assert.IsFalse(ForestMenu.Visible);
            Capture(mission.gameCamera, "collapse-final");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SkipIsIdempotentAndLegacyVictoryDoesNotReplay()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            quest.RestoreStage(Map01Quest.BossStage);
            var boss = mission.Enemies.Single(e => e.IsBoss);
            boss.GetComponent<Health>().TakeDamage(999999, boss.transform.position, null);
            var ending = mission.GetComponent<Map01EndingCutscene>();
            ending.Skip(); ending.Skip();
            Assert.IsFalse(ending.IsPlaying);
            Assert.IsFalse(mission.Cinematic);
            Assert.AreEqual(Map01Quest.CompleteStage, quest.Stage);
            Assert.AreEqual(1, Time.timeScale);
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

