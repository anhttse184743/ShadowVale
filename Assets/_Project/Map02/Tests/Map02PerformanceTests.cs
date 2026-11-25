using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using ShadowVale.Gameplay.Combat;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02PerformanceTests:ForestSceneTestBase
    {
        [UnityTest,Timeout(240000)]public IEnumerator DayTwoReactionAndNightSleepRestoreTheirActorsOnFinishSkipAndCancel()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float until=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;c.enabled=false;
            try
            {
                LoadBefore(m,"N1_04");Call(c,"BeginScene",Map02ChapterDefinition.Load().Find("N1_04"),0,0);
                Assert.AreEqual("hung_theme_night1",((AudioSource)typeof(Map02ChapterDirector).GetField("nightTheme",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c)).clip.name);Call(c,"EndScene");
                LoadBefore(m,"D2_01");var hung=c.Actor("Hùng");var hungController=hung.Animator.runtimeAnimatorController;Begin(m,"D2_01");var avatar=hung.Animator.avatar;var nam=m.Player.GetComponentInChildren<Animator>();var namAvatar=nam.avatar;
                Set(c,"lineIndex",3);Set(c,"lineClock",1f);Call(c,"LateUpdate");Assert.AreEqual("Hùng khựng lại",c.PerformanceBeat);Assert.IsTrue(hung.ConversationHeld);Assert.IsNull(hung.Animator.runtimeAnimatorController);
                Set(c,"lineIndex",5);Call(c,"LateUpdate");Assert.AreEqual("Hùng quay về máy",c.PerformanceBeat);Call(c,"EndScene");Assert.IsTrue(c.State.Done("D2_01"));Assert.NotNull(hung.Animator.runtimeAnimatorController);Assert.IsFalse(hung.ConversationHeld);Assert.AreSame(avatar,hung.Animator.avatar);Assert.AreSame(namAvatar,nam.avatar);

                var scenePreview=Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor",true).GetMethod("ApplyPerformancePreview");
                Assert.IsTrue((bool)scenePreview.Invoke(null,new object[]{m,"N2_05"}),"The editor preview must launch the real ending scene.");float hp=m.Player.GetComponent<Health>().Current;int ammo=m.Bag.Count(m.RifleAmmo);
                var walkUntil=(float)Get(c,"nightWalkUntil");
                Assert.Greater(walkUntil,10,"Both actors need a walkable route from the meeting point to the house.");
                var namStart=m.Player.position;var hungStart=hung.transform.position;
                Set(c,"sceneClock",5f);Call(c,"LateUpdate");
                Assert.AreEqual("Vừa đi vừa trò chuyện",c.PerformanceBeat);
                Assert.Greater(Vector3.Distance(namStart,m.Player.position),3);
                Assert.Greater(Vector3.Distance(hungStart,hung.transform.position),3);
                Assert.NotNull(c.CurrentLineId);
                var spacing=hung.transform.position-m.Player.position;
                Assert.That(Mathf.Abs(Vector3.Dot(spacing,m.Player.right)),Is.InRange(.65f,1.2f),"They should walk in adjacent lanes.");
                Assert.Less(Mathf.Abs(Vector3.Dot(spacing,m.Player.forward)),.23f,"Neither actor should trail the other.");
                var hungChest=hung.Animator.GetBoneTransform(HumanBodyBones.Chest).position;
                Assert.Less(hung.Animator.GetBoneTransform(HumanBodyBones.LeftHand).position.y,hungChest.y-.1f);
                Assert.Less(hung.Animator.GetBoneTransform(HumanBodyBones.RightHand).position.y,hungChest.y-.1f);
                yield return new WaitForEndOfFrame();Capture(m,"night2-walk");
                foreach(float t in new[]{5.1f,5.3f,5.5f,walkUntil-1.9f,walkUntil-.25f})
                {
                    Set(c,"sceneClock",t);Call(c,"LateUpdate");
                    var torso=nam.GetBoneTransform(HumanBodyBones.Chest).position-nam.GetBoneTransform(HumanBodyBones.Hips).position;
                    var feet=nam.GetBoneTransform(HumanBodyBones.RightFoot).position-nam.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                    Assert.Greater(Vector3.Dot(torso.normalized,Vector3.up),.98f);
                    Assert.Greater(Vector3.Dot(feet,m.Player.right),.14f,$"Nam's steps cross at {t:F2}s.");
                }

                var house=(Transform)Get(c,"nightRestHouse");Assert.AreEqual("Bamboo thatch house 6",house.name);
                var mat=GameObject.Find("Chiếu cói");Assert.NotNull(mat);
                var lamp=GameObject.Find("Đèn dầu treo")?.GetComponent<Light>();Assert.NotNull(lamp);
                var pillow=GameObject.Find("Gối tre đan")?.GetComponent<MeshFilter>();Assert.NotNull(pillow);
                Assert.Greater(lamp.transform.position.y,mat.transform.position.y+1.5f);
                Set(c,"sceneClock",walkUntil-3.9f);Call(c,"LateUpdate");
                yield return new WaitForEndOfFrame();Capture(m,"night2-arrival");
                Set(c,"sceneClock",walkUntil-1.25f);Call(c,"LateUpdate");
                yield return new WaitForEndOfFrame();Capture(m,"night2-doorway");
                Set(c,"lineIndex",5);Set(c,"sceneClock",walkUntil+.1f);Call(c,"Speak");Call(c,"LateUpdate");
                Assert.AreEqual("Trò chuyện trong nhà",c.PerformanceBeat);
                Assert.Less(Vector3.Distance(hung.transform.position,mat.transform.position),.1f);
                Assert.Less(Vector3.Distance(m.Player.position,mat.transform.position),1.6f);
                yield return new WaitForEndOfFrame();Capture(m,"night2-house");
                var blanket=GameObject.Find("Đêm 2 — chăn Nam đắp cho Hùng").GetComponent<MeshFilter>();
                Assert.IsTrue(c.NightJacketVisible,"The folded blanket is already on the mat when they enter.");
                float footEdge=blanket.sharedMesh.bounds.min.z;
                Assert.Less(blanket.sharedMesh.bounds.max.z,-.6f);
                var dialogue=(Map02DialogueCatalog.Entry[])Get(c,"lines");
                Assert.AreEqual("Tôi mệt quá, tôi nghỉ trước nhé",dialogue[dialogue.Length-2].text);
                Assert.AreEqual("Cậu cứ nghỉ ngơi",dialogue[dialogue.Length-1].text);
                Assert.IsTrue(Map02DialogueCatalog.HasApprovedRecording(dialogue[dialogue.Length-2]));
                Assert.IsTrue(Map02DialogueCatalog.HasApprovedRecording(dialogue[dialogue.Length-1]));
                Set(c,"sceneClock",walkUntil-.5f);Set(c,"lineIndex",dialogue.Length-2);Call(c,"StartLine");
                Assert.IsTrue(c.WaitingForLine,"Hùng should say the final line inside the house.");
                Set(c,"sceneClock",walkUntil+.4f);Call(c,"TickScene",0f);
                Assert.AreEqual("m2v3_reconcile_10",c.CurrentLineId);Assert.IsFalse(c.WaitingForLine);
                Set(c,"lineClock",20f);DialogueVoice.StopAll();Call(c,"TickScene",0f);
                Assert.AreEqual("m2v3_reconcile_11",c.CurrentLineId);Assert.Less((float)Get(c,"nightOutroAt"),0);
                float outroAt=Mathf.Max(50,walkUntil+1);
                Set(c,"sceneClock",outroAt);Set(c,"lineClock",20f);DialogueVoice.StopAll();Call(c,"TickScene",0f);
                Assert.IsTrue(c.ConversationActive);Assert.IsFalse(c.State.Done("N2_05"));
                Set(c,"sceneClock",outroAt+1.53f);Call(c,"LateUpdate");
                var loweringHips=hung.Animator.GetBoneTransform(HumanBodyBones.Hips).position;
                var loweringChest=hung.Animator.GetBoneTransform(HumanBodyBones.Chest).position;
                Assert.That(loweringHips.y-mat.transform.position.y,Is.InRange(.20f,.6f));
                Assert.Greater(Vector3.Dot((loweringChest-loweringHips).normalized,Vector3.up),.86f,"Hùng first sits upright before leaning back.");
                yield return new WaitForEndOfFrame();Capture(m,"night2-lowering");
                foreach(float t in new[]{1.12f,1.56f,2.08f,2.6f,3.18f,3.77f})
                {
                    Set(c,"sceneClock",outroAt+t);Call(c,"LateUpdate");
                    var footLine=hung.Animator.GetBoneTransform(HumanBodyBones.LeftFoot).position-hung.Animator.GetBoneTransform(HumanBodyBones.RightFoot).position;
                    var kneeLine=hung.Animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position-hung.Animator.GetBoneTransform(HumanBodyBones.RightLowerLeg).position;
                    Assert.Greater(Vector3.Dot(footLine,house.right),.19f,$"Hùng crosses his ankles at {t:F2}s.");
                    Assert.Greater(Vector3.Dot(kneeLine,house.right),.18f,$"Hùng crosses his knees at {t:F2}s.");
                }
                Set(c,"sceneClock",outroAt+2.75f);Call(c,"LateUpdate");
                yield return new WaitForEndOfFrame();Capture(m,"night2-reclining");
                Set(c,"sceneClock",outroAt+3.8f);Call(c,"LateUpdate");
                Assert.AreEqual("Hùng nằm xuống chiếu",c.PerformanceBeat);Assert.AreEqual(footEdge,blanket.sharedMesh.bounds.min.z,.01f);
                Assert.Less(hung.Animator.GetBoneTransform(HumanBodyBones.Hips).position.y-mat.transform.position.y,.45f);
                Assert.That(nam.GetBoneTransform(HumanBodyBones.Hips).position.y-mat.transform.position.y,Is.InRange(.08f,.44f),"Nam needs to sit above the floor boards.");
                var leftKnee=m.Player.InverseTransformPoint(nam.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position);
                var rightKnee=m.Player.InverseTransformPoint(nam.GetBoneTransform(HumanBodyBones.RightLowerLeg).position);
                var leftAnkle=m.Player.InverseTransformPoint(nam.GetBoneTransform(HumanBodyBones.LeftFoot).position);
                var rightAnkle=m.Player.InverseTransformPoint(nam.GetBoneTransform(HumanBodyBones.RightFoot).position);
                Assert.Greater(rightKnee.x-leftKnee.x,.4f);
                Assert.Less(Mathf.Abs(leftAnkle.x-rightAnkle.x),.16f,"Both ankles should fold inward between the knees.");
                Assert.That(hung.Animator.GetBoneTransform(HumanBodyBones.LeftFoot).position.y-mat.transform.position.y,Is.InRange(.055f,.22f));
                yield return new WaitForEndOfFrame();Capture(m,"night2-before-pull");
                Capture(m,"night2-joints",m.Player.position+m.Player.forward*2.2f+m.Player.right*.95f+Vector3.up*.84f,m.Player.position+m.Player.forward*.46f+Vector3.up*.32f);
                Set(c,"sceneClock",outroAt+5.2f);Call(c,"LateUpdate");
                Assert.IsTrue(c.NightJacketVisible);Assert.AreEqual("Nam đắp chăn cho Hùng",c.PerformanceBeat);
                yield return new WaitForEndOfFrame();Capture(m,"night2-cover");
                foreach(float pullTime in new[]{4.75f,5.2f,5.8f,6.4f})
                {
                    Set(c,"sceneClock",outroAt+pullTime);Call(c,"LateUpdate");
                    var cloth=blanket.sharedMesh.vertices;
                    var grip=blanket.transform.TransformPoint(cloth[cloth.Length-1]);
                    Assert.Less(Vector3.Distance(nam.GetBoneTransform(HumanBodyBones.RightHand).position,grip),.19f,$"The right wrist loses the moving corner at {pullTime:F2}s.");
                    Assert.Less(Vector3.Distance(nam.GetBoneTransform(HumanBodyBones.LeftHand).position,nam.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position),.17f,$"The left wrist drifts away from the knee at {pullTime:F2}s.");
                }
                Set(c,"sceneClock",outroAt+8);Call(c,"LateUpdate");yield return new WaitForEndOfFrame();Capture(m,"night2-sleep");
                Assert.AreEqual(footEdge,blanket.sharedMesh.bounds.min.z,.01f,"The foot edge should stay in place while Nam pulls.");
                Assert.Greater(blanket.sharedMesh.bounds.max.z,.25f);
                Assert.Greater(blanket.transform.InverseTransformPoint(hung.Animator.GetBoneTransform(HumanBodyBones.Head).position).z-blanket.sharedMesh.bounds.max.z,.12f,"The blanket must leave Hùng's face uncovered.");
                var headOnPillow=pillow.transform.InverseTransformPoint(hung.Animator.GetBoneTransform(HumanBodyBones.Head).position);
                Assert.Less(Mathf.Abs(headOnPillow.x),.13f);
                Assert.Less(Mathf.Abs(headOnPillow.z),.16f);
                Assert.That(headOnPillow.y,Is.InRange(.045f,.20f));
                Assert.AreEqual("hung_theme_night2",((AudioSource)Get(c,"nightTheme")).clip.name);
                m.SetPaused(true);Call(c,"Update");m.SetPaused(false);Call(c,"Update");
                Set(c,"sceneClock",outroAt+12.1f);Set(c,"lineClock",30f);DialogueVoice.StopAll();Call(c,"TickScene",0f);
                Assert.IsTrue(c.State.Done("N2_05"));Assert.AreEqual(4,c.State.phase);Assert.IsFalse(c.NightJacketVisible||hung.ConversationHeld);
                // Day 3 temporarily takes control of the animator after the cutscene releases it.
                hung.ReleasePerformance();Assert.NotNull(hungController);Assert.AreSame(hungController,hung.Animator.runtimeAnimatorController);hung.PrimeLocomotion();
                Assert.AreSame(avatar,hung.Animator.avatar);Assert.AreSame(namAvatar,nam.avatar);Assert.AreEqual(hp,m.Player.GetComponent<Health>().Current);Assert.AreEqual(ammo,m.Bag.Count(m.RifleAmmo));
                Call(c,"EndScene");Assert.AreEqual(1,c.State.completed.Count(id=>id=="N2_05"));

                LoadBefore(m,"N2_05");Begin(m,"N2_05");Call(c,"EndScene");Assert.IsTrue(c.State.Done("N2_05"),"Skip must reach the same next morning.");Assert.IsFalse(hung.ConversationHeld||c.NightJacketVisible);
                LoadBefore(m,"N2_05");Begin(m,"N2_05");Set(c,"lineIndex",5);Call(c,"Speak");Call(c,"LateUpdate");c.Cancel();Assert.IsFalse(c.State.Done("N2_05"));Assert.IsFalse(hung.ConversationHeld||c.NightJacketVisible);Assert.NotNull(hung.Animator.runtimeAnimatorController);Assert.NotNull(nam.runtimeAnimatorController);yield return null;
                Assert.AreEqual(1,Object.FindObjectsByType<Map02ChapterActor>(FindObjectsSortMode.None).Count(a=>a.role=="Hùng"));
            }
            finally{m.SetPaused(false);c.Cancel();c.enabled=true;}
            yield return new ExitPlayMode();
        }
        static void LoadBefore(Map02Mission m,string id)
        {var saved=m.Capture();saved.chapter=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){if(t.id==id)break;Assert.IsTrue(saved.chapter.Complete(t.id),t.id);}if(id=="N2_05"){saved.chapter.night2GuidanceStage=7;saved.chapter.night2ChainCuePlayed=true;}saved.patrolCheckpoint=null;m.Restore(saved);}
        static void Begin(Map02Mission m,string id)
        {var marker=Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId==id);var body=m.Player.GetComponent<CharacterController>();body.enabled=false;m.Player.position=marker.transform.position;body.enabled=true;Physics.SyncTransforms();Assert.IsTrue(m.Interact(marker),id);Assert.AreEqual(id,m.Chapter.CurrentScene?.id);}
        static object Get(Map02ChapterDirector c,string name)=>typeof(Map02ChapterDirector).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c);
        static void Set(Map02ChapterDirector c,string name,object value)=>typeof(Map02ChapterDirector).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,value);
        static void Call(Map02ChapterDirector c,string method,params object[] args)=>typeof(Map02ChapterDirector).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,args);
        static void Capture(Map02Mission mission,string name,Vector3? eye=null,Vector3? focus=null)
        {
            var camera=mission.GameCamera.GetComponent<Camera>();
            var rt=new RenderTexture(1280,720,24);var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;var target=camera.targetTexture;
            var originalPosition=camera.transform.position;var originalRotation=camera.transform.rotation;
            try
            {
                if(eye.HasValue&&focus.HasValue)camera.transform.SetPositionAndRotation(eye.Value,Quaternion.LookRotation(focus.Value-eye.Value));
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();
                Directory.CreateDirectory("Logs/Map02Chapter/Phase4");File.WriteAllBytes("Logs/Map02Chapter/Phase4/"+name+".png",tex.EncodeToPNG());
            }
            finally{camera.transform.SetPositionAndRotation(originalPosition,originalRotation);RenderTexture.active=previous;camera.targetTexture=target;Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);}
        }
    }
}
