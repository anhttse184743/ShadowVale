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
            double poseDeadline = Time.realtimeSinceStartupAsDouble + 20;
            while (opening.BriefingTime < 2.5f && Time.realtimeSinceStartupAsDouble < poseDeadline) yield return null;
            Assert.IsFalse(opening.IsIntroducing);
            var actor = opening.GetComponentsInChildren<Animator>()[0];
            var hand = actor.GetBoneTransform(HumanBodyBones.RightHand);
            var shoulder = actor.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Assert.Greater(hand.position.y - shoulder.position.y, -.25f, "Pointing must raise the hand, not be hidden by idle.");
            var nam = mission.player.GetComponentInChildren<Animator>();
            Assert.IsNull(nam.runtimeAnimatorController, "Gameplay stance must not override the briefing.");
            Assert.Less(nam.GetBoneTransform(HumanBodyBones.RightHand).position.y,
                nam.GetBoneTransform(HumanBodyBones.RightUpperArm).position.y - .3f, "Nam must rest his right arm during briefing.");
            Assert.Less(nam.GetBoneTransform(HumanBodyBones.LeftHand).position.y,
                nam.GetBoneTransform(HumanBodyBones.LeftUpperArm).position.y - .3f, "Nam must rest his left arm during briefing.");
            Capture(mission.gameCamera, "Logs/Cutscene/wide.png");
            while (opening.BriefingTime < opening.SaluteBeginsAt + .85f) yield return null;
            Assert.Less(Vector3.Distance(nam.GetBoneTransform(HumanBodyBones.RightHand).position,
                nam.GetBoneTransform(HumanBodyBones.Head).position), .42f, "Salute must bring the right hand to the forehead.");
            Assert.Less(nam.GetBoneTransform(HumanBodyBones.LeftHand).position.y,
                nam.GetBoneTransform(HumanBodyBones.LeftUpperArm).position.y - .3f, "The other arm must remain down during salute.");
            Capture(mission.gameCamera, "Logs/Cutscene/salute.png");
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (opening.IsPlaying && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsFalse(opening.IsPlaying);
            Assert.IsFalse(Map01OpeningCutscene.Active);
            Assert.IsNotNull(nam.runtimeAnimatorController);
            Assert.IsTrue(mission.ModernPlayer.enabled);
            Assert.IsTrue(mission.ModernCombat.enabled);
            Assert.IsTrue(mission.CameraInputEnabled);
            Assert.AreEqual(Map01Quest.RescueStage, mission.Stage);
            Assert.Less(Vector3.Distance(start, mission.player.position), .3f);
            Assert.IsTrue(mission.GetComponent<Map01SaveSystem>().CanSave());
            Capture(mission.gameCamera, "Logs/Cutscene/gameplay.png");
            // Exercise the river from a low camera angle: body must remain tinted, not sliced.
            mission.ModernPlayer.enabled = false;
            var controller = mission.player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            float riverZ = 35;
            float riverX = mission.riverOffset + mission.riverAmplitude1 * Mathf.Sin(riverZ * mission.riverFrequency1)
                + mission.riverAmplitude2 * Mathf.Sin(riverZ * mission.riverFrequency2);
            mission.player.position = new Vector3(riverX, -.7f, riverZ);
            yield return null;
            Assert.IsTrue(mission.IsWading);
            var rig = mission.gameCamera.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>();
            Assert.IsTrue(rig.SurfaceCameraFloor.Invoke().HasValue);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(ShadowVale.Gameplay.Player.ThirdPersonCamera).GetField("_pitch", flags).SetValue(rig, -25f);
            yield return null; yield return null;
            Assert.GreaterOrEqual(mission.gameCamera.transform.position.y, rig.SurfaceCameraFloor.Invoke().Value - .01f);
            typeof(ShadowVale.Gameplay.Player.ThirdPersonCamera).GetField("_pitch", flags).SetValue(rig, 12f);
            typeof(ShadowVale.Gameplay.Player.ThirdPersonCamera).GetField("_yaw", flags).SetValue(rig, 90f);
            yield return WaitGameSeconds(.5f);
            Capture(mission.gameCamera, "Logs/Cutscene/wading.png");
            if (controller != null) controller.enabled = true;
            mission.ModernPlayer.enabled = true;
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

