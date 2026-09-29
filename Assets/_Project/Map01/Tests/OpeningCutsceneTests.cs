using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace ShadowVale.Map01.Tests
{
    public sealed class OpeningCutsceneTests : ForestSceneTestBase
    {
        [Test]
        public void AssetsUseExistingIdleAndHumanoidClips()
        {
            var asset = Resources.Load<Map01OpeningCutscene>("Cutscenes/Map01Opening");
            Assert.IsNotNull(asset);
            Assert.IsTrue(asset.salute.humanMotion);
            Assert.IsTrue(asset.talking.humanMotion);
            Assert.IsTrue(asset.pointing.humanMotion);
            Assert.AreEqual("Assets/_Project/Art/Characters/Animations/Generated/Idle_Tuned.anim",
                UnityEditor.AssetDatabase.GetAssetPath(asset.idle));
            Assert.IsNotNull(asset.commanderVoice); Assert.IsNotNull(asset.soldierVoice);
            Assert.IsFalse(File.Exists("Assets/_Project/Map01/Resources/Cutscenes/Intro.mp4"));
        }
        [UnityTest]
        public IEnumerator FullBriefingSalutesAndReturnsControlWithoutChangingQuest()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_MainMenu.unity");
            yield return new EnterPlayMode();
            Map01OpeningCutscene.RequestNewGame();
            Map01SaveSystem.BeginGame(-1);
            yield return null; yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var opening = Object.FindFirstObjectByType<Map01OpeningCutscene>();
            Assert.IsTrue(opening.IsPlaying);
            Assert.IsFalse(mission.CameraInputEnabled);
            Assert.IsTrue(mission.Stopped);
            Assert.IsFalse(mission.GetComponent<Map01SaveSystem>().CanSave());
            var start = mission.player.position;
            yield return WaitGameSeconds(3);
            Capture(mission.gameCamera, "Logs/Cutscene/wide.png");
            yield return WaitGameSeconds(18);
            Capture(mission.gameCamera, "Logs/Cutscene/salute.png");
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (opening.IsPlaying && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsFalse(opening.IsPlaying);
            Assert.IsFalse(Map01OpeningCutscene.Active);
            Assert.IsTrue(mission.CameraInputEnabled);
            Assert.AreEqual(Map01Quest.RescueStage, mission.Stage);
            Assert.Less(Vector3.Distance(start, mission.player.position), .3f);
            Assert.IsTrue(mission.GetComponent<Map01SaveSystem>().CanSave());
            Capture(mission.gameCamera, "Logs/Cutscene/gameplay.png");
            yield return new ExitPlayMode();
        }
        private static void Capture(Camera camera, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var previous = camera.targetTexture; var active = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            } finally {
                camera.targetTexture = previous; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image);
            }
        }
    }
}

