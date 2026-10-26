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
                LoadBefore(m,"D2_01");Begin(m,"D2_01");var hung=c.Actor("Hùng");var avatar=hung.Animator.avatar;var nam=m.Player.GetComponentInChildren<Animator>();var namAvatar=nam.avatar;
                Set(c,"lineIndex",3);Set(c,"lineClock",1f);Call(c,"LateUpdate");Assert.AreEqual("Hùng khựng lại",c.PerformanceBeat);Assert.IsTrue(hung.ConversationHeld);Assert.IsNull(hung.Animator.runtimeAnimatorController);
                Set(c,"lineIndex",5);Call(c,"LateUpdate");Assert.AreEqual("Hùng quay về máy",c.PerformanceBeat);Call(c,"EndScene");Assert.IsTrue(c.State.Done("D2_01"));Assert.NotNull(hung.Animator.runtimeAnimatorController);Assert.IsFalse(hung.ConversationHeld);Assert.AreSame(avatar,hung.Animator.avatar);Assert.AreSame(namAvatar,nam.avatar);

                var scenePreview=Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor",true).GetMethod("ApplyPerformancePreview");
                Assert.IsTrue((bool)scenePreview.Invoke(null,new object[]{m,"N2_05"}),"The editor preview must launch the real ending scene.");float hp=m.Player.GetComponent<Health>().Current;int ammo=m.Bag.Count(m.RifleAmmo);
                Set(c,"lineIndex",5);Set(c,"sceneClock",24f);Call(c,"Speak");Call(c,"LateUpdate");Assert.AreEqual("Đổi ca tại chỗ nghỉ",c.PerformanceBeat);Assert.Less(Vector3.Distance(hung.transform.position,c.Sites["rest"].position),.1f);
                Set(c,"lineIndex",8);Set(c,"sceneClock",50f);Set(c,"lineClock",20f);Call(c,"TickScene",0f);Assert.IsTrue(c.ConversationActive,"The last spoken line must not skip the silent cover action.");Assert.IsFalse(c.State.Done("N2_05"));
                Set(c,"sceneClock",55f);Call(c,"LateUpdate");yield return new WaitForEndOfFrame();Assert.IsTrue(c.NightJacketVisible);Assert.AreEqual("Ngủ và đắp áo",c.PerformanceBeat);Assert.Less(hung.Animator.GetBoneTransform(HumanBodyBones.Hips).position.y-c.Sites["rest"].position.y,.8f,"Sleeping Hùng must lie close to the floor.");Capture(c,"night2-cover");
                Set(c,"sceneClock",57f);Call(c,"LateUpdate");yield return new WaitForEndOfFrame();Capture(c,"night2-sleep");var coat=GameObject.Find("Đêm 2 — áo khoác Nam đắp cho Hùng").transform;Assert.Greater(Vector3.Dot(hung.Animator.GetBoneTransform(HumanBodyBones.Head).position-coat.position,coat.forward),.6f,"The coat must leave sleeping Hùng's face uncovered.");Assert.AreEqual("hung_theme_night2",((AudioSource)typeof(Map02ChapterDirector).GetField("nightTheme",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(c)).clip.name);
                m.SetPaused(true);Call(c,"Update");m.SetPaused(false);Call(c,"Update");
                Set(c,"sceneClock",62.1f);Set(c,"lineClock",30f);Call(c,"TickScene",0f);Assert.IsTrue(c.State.Done("N2_05"));Assert.AreEqual(4,c.State.phase);Assert.IsFalse(c.NightJacketVisible||hung.ConversationHeld);Assert.NotNull(hung.Animator.runtimeAnimatorController);Assert.AreSame(avatar,hung.Animator.avatar);Assert.AreSame(namAvatar,nam.avatar);Assert.AreEqual(hp,m.Player.GetComponent<Health>().Current);Assert.AreEqual(ammo,m.Bag.Count(m.RifleAmmo));
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
        static void Set(Map02ChapterDirector c,string name,object value)=>typeof(Map02ChapterDirector).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(c,value);
        static void Call(Map02ChapterDirector c,string method,params object[] args)=>typeof(Map02ChapterDirector).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(c,args);
        static void Capture(Map02ChapterDirector c,string name)
        {var go=new GameObject("Performance QA camera");var camera=go.AddComponent<Camera>();var rest=c.Sites["rest"].position;camera.transform.position=rest+new Vector3(-3.2f,2.2f,-3.5f);camera.transform.LookAt(rest+Vector3.up*.5f+Vector3.right*.6f);camera.fieldOfView=55;var rt=new RenderTexture(1200,900,24);var tex=new Texture2D(1200,900,TextureFormat.RGB24,false);var previous=RenderTexture.active;try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1200,900),0,0);tex.Apply();Directory.CreateDirectory("Logs/Map02Chapter/Phase4");File.WriteAllBytes("Logs/Map02Chapter/Phase4/"+name+".png",tex.EncodeToPNG());}finally{RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}}
    }
}
