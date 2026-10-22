using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02CastTests:ForestSceneTestBase
    {
        static readonly string[] Roles={"Anh Thành","Anh Lực","Tâm","Chú Bảy","Ông Sáu","Chị Lành","Bé Tí"};
        [Test]public void EveryNamedSourceModelHasAnAnimatedCharacterPrefab()
        {
            var layout=Resources.Load<Map02ChapterLayout>("Story/Map02ChapterLayout");
            foreach(var role in Roles)
            {
                var prefab=layout.actors.Single(a=>a.role==role).prefab;Assert.NotNull(prefab,role);
                var actor=prefab.GetComponentInChildren<Animator>();Assert.NotNull(actor,role);Assert.IsTrue(actor.avatar.isHuman&&actor.avatar.isValid,role);
                Assert.NotNull(actor.runtimeAnimatorController,role);Assert.IsFalse(actor.applyRootMotion);
                Assert.IsTrue(actor.runtimeAnimatorController.animationClips.Any(c=>c.name=="Idle_Tuned"),role);
                foreach(var material in prefab.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials))
                    Assert.IsTrue(material!=null&&material.shader!=null&&material.shader.isSupported,role+" material");
            }
        }
        [UnityTest,Timeout(240000)]public IEnumerator DayTwoAndNightTwoKeepVisibleActorsAcrossPreviewAndSaveLoad()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var chapter=m.Chapter;
            Assert.IsNotNull(chapter.Actor("Hùng").visual,"Direct Map 2 Play needs Hùng even without an arriving Map 1 party.");
            var preview=System.Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor",true).GetMethod("ApplyToMission");
            preview.Invoke(null,new object[]{m,2});yield return WaitGameSeconds(2);
            foreach(var role in Roles)
            {
                var actor=chapter.Actor(role);Assert.NotNull(actor.visual,role);Assert.IsTrue(actor.visual.gameObject.activeInHierarchy,role);
                Assert.IsTrue(actor.Animator.isHuman&&actor.Animator.isActiveAndEnabled,role);
                Assert.AreEqual(1,Object.FindObjectsByType<Map02ChapterActor>(FindObjectsSortMode.None).Count(a=>a.role==role),role);
                CaptureActor(actor,role);
            }
            Assert.Less(Vector3.Distance(chapter.Actor("Bé Tí").transform.position,chapter.Actor("Chị Lành").transform.position),2);
            Assert.IsFalse(chapter.Actor("Bé Tí").Animator.GetBool("Sitting"));
            Assert.Less(chapter.Actor("Bé Tí").Animator.transform.lossyScale.y,chapter.Actor("Tâm").Animator.transform.lossyScale.y*.8f);
            // A real dialogue must frame the assigned named character model.
            var mark=Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId=="D2_01");
            Place(m,mark.transform.position);Assert.IsTrue(m.Interact(mark));yield return WaitGameSeconds(.4f);
            Assert.AreEqual("D2_01",chapter.CurrentScene.id);Assert.IsTrue(chapter.Actor("Anh Thành").visual.gameObject.activeInHierarchy);
            typeof(Map02ChapterDirector).GetMethod("EndScene",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(chapter,null);
            var save=Map02Mission.Parse(JsonUtility.ToJson(m.Capture()));m.Restore(save);yield return null;
            Assert.IsNotNull(chapter.Actor("Tâm").visual);Assert.IsTrue(chapter.State.Done("D2_01"));
            preview.Invoke(null,new object[]{m,3});yield return WaitGameSeconds(2);
            Assert.IsFalse(chapter.Actor("Bé Tí").Animator.GetBool("Sitting"));
            Assert.IsTrue(chapter.Actor("Hùng").visual.gameObject.activeInHierarchy);
            save=Map02Mission.Parse(JsonUtility.ToJson(m.Capture()));m.Restore(save);yield return null;
            Assert.IsTrue(chapter.Actor("Anh Thành").visual.gameObject.activeInHierarchy);Assert.IsTrue(chapter.Actor("Tâm").visual.gameObject.activeInHierarchy);
            yield return new ExitPlayMode();
        }
        static void Place(Map02Mission m,Vector3 at){var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=at;cc.enabled=true;Physics.SyncTransforms();}
        [UnityTest,Timeout(240000)] public IEnumerator ConversationFacesPartnerAtExactSpacingFromEveryApproach()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var chapter=m.Chapter;
            var preview=System.Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor",true).GetMethod("ApplyToMission");
            preview.Invoke(null,new object[]{m,2});yield return WaitGameSeconds(1);
            var begin=typeof(Map02ChapterDirector).GetMethod("BeginScene",BindingFlags.Instance|BindingFlags.NonPublic);
            var task=Map02ChapterDefinition.Load().Find("D2_01");var npc=chapter.Actor("Anh Thành");
            for(int angle=0;angle<360;angle+=45)
            {
                Place(m,npc.transform.position+Quaternion.Euler(0,angle,0)*Vector3.forward*2.5f);
                var anchor=npc.transform.position;
                begin.Invoke(chapter,new object[]{task,0,0});
                yield return null;yield return null;
                Assert.IsTrue(npc.ConversationHeld);
                var delta=Vector3.ProjectOnPlane(npc.transform.position-m.Player.position,Vector3.up);
                Assert.AreEqual(1.8f,delta.magnitude,.015f,"Approach "+angle);
                Assert.Greater(Vector3.Dot(m.Player.forward,delta.normalized),.999f);
                Assert.Greater(Vector3.Dot(npc.transform.forward,-delta.normalized),.999f);
                Assert.Less(Vector3.Distance(anchor,npc.transform.position),.001f);
                var nam=m.Player.position;yield return WaitGameSeconds(.2f);
                Assert.Less(Vector3.Distance(nam,m.Player.position),.001f);
                chapter.Cancel();Assert.IsFalse(npc.ConversationHeld);
            }
            yield return new ExitPlayMode();
        }
        static void CaptureActor(Map02ChapterActor actor,string label)
        {
            var go=new GameObject("Cast review camera");var camera=go.AddComponent<Camera>();
            go.transform.position=actor.transform.position+Vector3.forward*3+Vector3.right*1.6f+Vector3.up*1.5f;go.transform.LookAt(actor.transform.position+Vector3.up*.85f);camera.fieldOfView=40;camera.nearClipPlane=.1f;
            if(label=="Bé Tí"){go.transform.position=actor.transform.position+Vector3.forward*2.5f-Vector3.right*1.6f+Vector3.up*1.4f;go.transform.LookAt(actor.transform.position+Vector3.up*.5f);}
            var target=new RenderTexture(512,768,24);var texture=new Texture2D(512,768,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,512,768),0,0);texture.Apply();Directory.CreateDirectory("Logs/Map02Chapter/Cast");File.WriteAllBytes("Logs/Map02Chapter/Cast/"+label+".png",texture.EncodeToPNG());}
            finally{RenderTexture.active=previous;camera.targetTexture=null;Object.DestroyImmediate(go);Object.DestroyImmediate(texture);target.Release();Object.DestroyImmediate(target);}
        }
    }
}
