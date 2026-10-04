using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
namespace ShadowVale.Map01.Tests
{
    public sealed class HungVisualTests : ForestSceneTestBase
    {
        [UnityTest]
        public IEnumerator ModelAnimatesAndRescueNavigationSurvives()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode(); yield return null; yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var visual = mission.hung.GetComponentInChildren<Map01HungVisual>();
            Assert.IsNotNull(visual);
            Assert.IsTrue(visual.actor.isHuman && visual.actor.avatar.isValid);
            var skin = visual.GetComponentInChildren<SkinnedMeshRenderer>();
            Assert.IsNotNull(skin);
            Assert.IsNotNull(skin.sharedMaterial.GetTexture("_BaseMap"), "Hung must use the supplied skin texture.");
            Assert.IsNotNull(skin.sharedMaterial.GetTexture("_BumpMap"));
            Assert.AreEqual("Assets/_Project/Art/Characters/NPCs/Materials/hung - Wounded.mat",
                UnityEditor.AssetDatabase.GetAssetPath(skin.sharedMaterial));
            Assert.IsFalse(visual.actor.applyRootMotion);
            Assert.AreEqual(1, visual.actor.layerCount, "Hung has no weapon/aim layer.");
            foreach (var enemy in mission.Enemies) enemy.enabled = false;
            var quest = mission.GetComponent<Map01Quest>();
            var agent = mission.hung.GetComponent<NavMeshAgent>();
            // Rescue stage: kneeling at the jetty with his wrists roped behind him.
            Assert.AreEqual(Map01Quest.RescueStage, quest.Stage);
            yield return WaitGameSeconds(.3f);
            Assert.IsTrue(visual.actor.GetCurrentAnimatorStateInfo(0).IsName(Map01HungVisual.CaptiveState));
            Assert.IsTrue(visual.RopeVisible, "Captive Hung wears the rope.");
            var ropeRenderer = visual.GetComponentInChildren<MeshRenderer>();
            Assert.IsNotNull(ropeRenderer);
            Vector3 wrists = (visual.actor.GetBoneTransform(HumanBodyBones.LeftHand).position
                + visual.actor.GetBoneTransform(HumanBodyBones.RightHand).position) * .5f;
            Debug.Log("HUNG_ROPE bounds=" + ropeRenderer.bounds + " wrists=" + wrists + " enabled=" + ropeRenderer.enabled
                + " mat=" + ropeRenderer.sharedMaterial?.shader.name + " lossy=" + ropeRenderer.transform.lossyScale);
            Assert.Less(Vector3.Distance(ropeRenderer.bounds.center, wrists), .12f, "The rope sits on his wrists.");
            Assert.Less(visual.actor.GetBoneTransform(HumanBodyBones.Head).position.y - mission.hung.position.y, 1.3f, "He kneels.");
            float knee = Mathf.Min(visual.actor.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position.y,
                visual.actor.GetBoneTransform(HumanBodyBones.RightLowerLeg).position.y) - mission.hung.position.y;
            Debug.Log("HUNG_KNEEL knee joint above ground=" + knee);
            Assert.Less(Mathf.Abs(knee - .08f), .1f, "His knees rest on the deck, not in the air.");
            var captiveCamera = new GameObject("Hung captive camera").AddComponent<Camera>();
            captiveCamera.fieldOfView = 40;
            captiveCamera.transform.position = mission.hung.position + Vector3.up * 1.1f - mission.hung.forward * 2.4f + mission.hung.right * .9f;
            captiveCamera.transform.LookAt(mission.hung.position + Vector3.up * .7f);
            Capture(captiveCamera, "hung-captive-back");
            captiveCamera.transform.position = mission.hung.position + Vector3.up * 1.1f + mission.hung.forward * 2.4f + mission.hung.right * .9f;
            captiveCamera.transform.LookAt(mission.hung.position + Vector3.up * .7f);
            Capture(captiveCamera, "hung-captive-front");
            // Freed: rope off, he gets up before following, then limps.
            quest.RestoreStage(Map01Quest.EscortStage);
            yield return WaitGameSeconds(.5f);
            Assert.IsFalse(visual.RopeVisible);
            Assert.IsTrue(visual.actor.GetCurrentAnimatorStateInfo(0).IsName(Map01HungVisual.StandUpState));
            Assert.IsTrue(agent.isStopped, "He does not slide after Nam while still getting up.");
            Capture(captiveCamera, "hung-standing-up");
            yield return WaitGameSeconds(4.5f);
            Assert.IsTrue(visual.actor.GetCurrentAnimatorStateInfo(0).IsName(Map01HungVisual.WoundedState));
            Assert.IsFalse(agent.isStopped);
            yield return WaitGameSeconds(.6f);
            captiveCamera.transform.position = mission.hung.position + Vector3.up * 1.1f + mission.hung.right * 3f;
            captiveCamera.transform.LookAt(mission.hung.position + Vector3.up * .9f);
            Capture(captiveCamera, "hung-limp");
            Object.Destroy(captiveCamera.gameObject);
            // Back at base he walks normally; a later stage never replays the stand-up.
            quest.RestoreStage(Map01Quest.BriefingStage);
            agent.ResetPath();
            yield return null; yield return null;
            Assert.IsTrue(visual.actor.GetCurrentAnimatorStateInfo(0).IsName(Map01HungVisual.LocomotionState));
            var cameraObject = new GameObject("Hung validation camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 40;
            var focus = mission.hung.position + Vector3.up;
            camera.transform.position = focus + mission.hung.forward * 3.2f + mission.hung.right * 1.3f + Vector3.up * .3f;
            camera.transform.LookAt(focus);
            yield return WaitGameSeconds(.4f);
            Capture(camera, "hung-idle");
            // Put both actors at equal camera depth for an honest size comparison.
            var nam = mission.player.GetComponentInChildren<Animator>();
            var playerController = mission.ModernPlayer;
            playerController.enabled = false;
            var capsule = mission.player.GetComponent<CharacterController>();
            capsule.enabled = false;
            var oldPosition = mission.player.position; var oldRotation = mission.player.rotation;
            mission.player.SetPositionAndRotation(mission.hung.position + mission.hung.right * 1.2f, mission.hung.rotation);
            yield return null; yield return null;
            float namHead = nam.GetBoneTransform(HumanBodyBones.Head).position.y - mission.player.position.y;
            float hungHead = visual.actor.GetBoneTransform(HumanBodyBones.Head).position.y - mission.hung.position.y;
            Debug.Log("HUNG_SCALE Nam head=" + namHead + " Hung head=" + hungHead + " visual scale=" + visual.transform.localScale);
            Assert.Less(Mathf.Abs(namHead - hungHead), .15f, "Comparable adults should have comparable standing head height.");
            var pairFocus = focus + mission.hung.right * .6f;
            camera.transform.position = pairFocus + mission.hung.forward * 5f + Vector3.up * .3f;
            camera.transform.LookAt(pairFocus);
            Capture(camera, "hung-nam-size");
            mission.player.SetPositionAndRotation(oldPosition,oldRotation);
            capsule.enabled = true; playerController.enabled = true;
            visual.Speak(5);
            yield return WaitGameSeconds(.5f);
            Assert.IsTrue(visual.actor.GetCurrentAnimatorStateInfo(0).IsName("Talking"));
            Capture(camera, "hung-talking");
            Assert.IsTrue(agent.isOnNavMesh);
            Vector3 start = mission.hung.position;
            Assert.IsTrue(NavMesh.SamplePosition(start + mission.hung.forward * 6, out var goal, 4, NavMesh.AllAreas));
            agent.isStopped = false; agent.SetDestination(goal.position);
            yield return WaitGameSeconds(.8f);
            Assert.Greater(Vector3.Distance(start, mission.hung.position), .05f);
            Assert.Greater(visual.actor.GetFloat("Speed"), .1f);
            Assert.IsFalse(visual.actor.GetBool("Talking"));
            Capture(camera, "hung-walk");
            agent.ResetPath();
            // He can only be shot while exposed: at the jetty or limping home beside Nam.
            quest.RestoreStage(Map01Quest.EscortStage);
            yield return null;
            mission.GetComponent<Map01Rescue>().HitHung(9999);
            yield return WaitGameSeconds(.3f);
            Assert.IsTrue(mission.GetComponent<Map01Rescue>().HungDown);
            yield return WaitGameSeconds(1);
            Assert.IsTrue(visual.actor.GetCurrentAnimatorStateInfo(0).IsName("Die"));
            Capture(camera, "hung-shot");
            mission.GetComponent<Map01Rescue>().Retry();
            yield return WaitGameSeconds(.3f);
            Assert.IsFalse(mission.GetComponent<Map01Rescue>().HungDown);
            Assert.IsFalse(visual.actor.GetCurrentAnimatorStateInfo(0).IsName("Die"));
            Assert.IsTrue(visual.actor.GetCurrentAnimatorStateInfo(0).IsName(Map01HungVisual.CaptiveState), "Retry puts him back on his knees.");
            Object.Destroy(cameraObject);
            yield return new ExitPlayMode();
        }
        private static void Capture(Camera camera, string name)
        {
            Directory.CreateDirectory("Logs/Hung");
            var target = RenderTexture.GetTemporary(900, 900, 24);
            var previous = RenderTexture.active;
            var image = new Texture2D(900,900,TextureFormat.RGB24,false);
            camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            image.ReadPixels(new Rect(0,0,900,900),0,0);image.Apply();
            File.WriteAllBytes("Logs/Hung/"+name+".png",image.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=previous;
            RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(image);
        }
    }
}
