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
            var captiveCamera = new GameObject("Hung captive camera").AddComponent<Camera>();
            captiveCamera.fieldOfView = 40;
            captiveCamera.transform.position = mission.hung.position + Vector3.up * 1.1f - mission.hung.forward * 2.4f + mission.hung.right * .9f;
            captiveCamera.transform.LookAt(mission.hung.position + Vector3.up * .7f);
            Capture(captiveCamera, "hung-captive-before-check");
            Debug.Log($"HUNG_POSE head={visual.actor.GetBoneTransform(HumanBodyBones.Head).position}; root={mission.hung.position}; culling={visual.actor.cullingMode}; scale={visual.actor.transform.lossyScale}");
            Assert.Less(visual.actor.GetBoneTransform(HumanBodyBones.Head).position.y - mission.hung.position.y, 1.3f, "He kneels.");
            float knee = Mathf.Min(visual.actor.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position.y,
                visual.actor.GetBoneTransform(HumanBodyBones.RightLowerLeg).position.y) - mission.hung.position.y;
            Debug.Log("HUNG_KNEEL knee joint above ground=" + knee);
            Assert.Less(Mathf.Abs(knee - .08f), .1f, "His knees rest on the deck, not in the air.");
            captiveCamera.transform.position = mission.hung.position + Vector3.up * 1.1f - mission.hung.forward * 2.4f + mission.hung.right * .9f;
            captiveCamera.transform.LookAt(mission.hung.position + Vector3.up * .7f);
            Capture(captiveCamera, "hung-captive-back");
            captiveCamera.transform.position = mission.hung.position + Vector3.up * 1.1f + mission.hung.forward * 2.4f + mission.hung.right * .9f;
            captiveCamera.transform.LookAt(mission.hung.position + Vector3.up * .7f);
            Capture(captiveCamera, "hung-captive-front");
            // Use the real release path; loading an escort checkpoint deliberately skips the rise.
            var rescue = mission.GetComponent<Map01Rescue>();
            quest.RestoreStoryStage(Map01Quest.RescueStage, RescueOrdered());rescue.MarkIntroSeen();
            foreach (var guard in rescue.Squad) BodyDiscoveryTests.Kill(guard);
            var character = mission.player.GetComponent<CharacterController>();character.enabled=false;
            mission.player.SetPositionAndRotation(mission.hung.position-mission.hung.forward*.7f,mission.hung.rotation);
            character.enabled=true;Physics.SyncTransforms();
            yield return WaitGameSeconds(.2f);
            Assert.IsTrue(rescue.BeginFree());
            float risingUntil=Time.time+12;
            while(rescue.CurrentPhase!=Map01Rescue.Phase.Rising&&Time.time<risingUntil)yield return null;
            Assert.AreEqual(Map01Rescue.Phase.Rising,rescue.CurrentPhase);
            yield return WaitGameSeconds(.35f);
            Assert.IsFalse(visual.RopeVisible);
            Assert.IsTrue(visual.actor.GetCurrentAnimatorStateInfo(0).IsName(Map01HungVisual.StandUpState));
            Assert.IsTrue(agent.isStopped, "He does not slide after Nam while still getting up.");
            Capture(captiveCamera, "hung-standing-up");
            yield return WaitGameSeconds(4.5f);
            Assert.IsTrue(visual.actor.GetCurrentAnimatorStateInfo(0).IsName(Map01HungVisual.WoundedState));
            Assert.IsFalse(agent.isStopped);
            Vector3 escortStart=mission.hung.position;
            yield return WaitGameSeconds(.6f);
            Assert.Greater(Vector3.Distance(escortStart,mission.hung.position),.05f,"The real escort still moves on the NavMesh.");
            Assert.Greater(visual.actor.GetFloat("Speed"),.1f);
            captiveCamera.transform.position = mission.hung.position + Vector3.up * 1.1f + mission.hung.right * 3f;
            captiveCamera.transform.LookAt(mission.hung.position + Vector3.up * .9f);
            Capture(captiveCamera, "hung-limp");
            Object.Destroy(captiveCamera.gameObject);
            // The current delivered state rests on the base cot, including during dialogue.
            quest.RestoreStage(Map01Quest.BriefingStage);
            agent.ResetPath();agent.isStopped=true;
            yield return WaitGameSeconds(.3f);
            var rest=visual.GetComponent<Map01HungRest>();
            Assert.NotNull(rest);Assert.IsTrue(rest.IsResting);Assert.NotNull(rest.Cot);
            foreach(var renderer in visual.actor.GetComponentsInChildren<Renderer>())
                Assert.IsTrue(renderer.forceRenderingOff,"The hidden standing actor must not overlap the resting proxy.");
            visual.Speak(5);yield return WaitGameSeconds(.5f);
            Assert.IsTrue(rest.IsResting,"Speaking at base keeps the approved resting pose.");
            var cameraObject=new GameObject("Hung validation camera");
            var camera=cameraObject.AddComponent<Camera>();camera.fieldOfView=40;
            var focus=rest.DialogueFocus;
            camera.transform.position=focus+new Vector3(2,1,-2);camera.transform.LookAt(focus);
            Capture(camera,"hung-resting-at-base");
            // He can only be shot while exposed: at the jetty or limping home beside Nam.
            quest.RestoreStage(Map01Quest.EscortStage);
            yield return null;
            camera.transform.position=mission.hung.position+mission.hung.right*3+Vector3.up;
            camera.transform.LookAt(mission.hung.position+Vector3.up*.7f);
            mission.GetComponent<Map01Rescue>().HitHung(9999);
            yield return WaitGameSeconds(.3f);
            Assert.IsTrue(mission.GetComponent<Map01Rescue>().HungDown);
            yield return WaitGameSeconds(1);
            Assert.IsTrue(visual.actor.GetCurrentAnimatorStateInfo(0).IsName("Die"));
            Capture(camera, "hung-shot");
            mission.GetComponent<Map01Rescue>().Retry();
            yield return WaitGameSeconds(.3f);
            yield return WaitForMap01Ready();
            float restoreUntil=Time.realtimeSinceStartup+30;
            while(Map01SaveSystem.IsRestoring&&Time.realtimeSinceStartup<restoreUntil)yield return null;
            Assert.IsFalse(Map01SaveSystem.IsRestoring);
            mission=Object.FindFirstObjectByType<Map01Mission>();
            visual=mission.hung.GetComponentInChildren<Map01HungVisual>();
            Assert.IsFalse(mission.GetComponent<Map01Rescue>().HungDown);
            Assert.IsFalse(visual.actor.GetCurrentAnimatorStateInfo(0).IsName("Die"));
            Assert.AreEqual(Map01Quest.EscortStage,mission.GetComponent<Map01Quest>().Stage);
            Assert.IsFalse(visual.IsRising,"An escort checkpoint must not replay the release animation.");
            Assert.IsTrue(visual.actor.GetCurrentAnimatorStateInfo(0).IsName(Map01HungVisual.WoundedState), "Retry restores the real escort checkpoint after the release.");
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
