using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02DialogueStanceTests:ForestSceneTestBase
    {
        const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic;
        const string Folder="Logs/DialogueStance_20261123";
        static float VisibleSole(Animator actor)
        {
            var replacement=actor.transform.Find("Nam_AoBaBa_Visual");var mesh=new Mesh();float low=float.PositiveInfinity;
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()){
                if(!skin.enabled||!skin.gameObject.activeInHierarchy||skin.forceRenderingOff||replacement!=null&&!skin.transform.IsChildOf(replacement))continue;
                skin.BakeMesh(mesh,replacement!=null);foreach(var v in mesh.vertices)low=Mathf.Min(low,skin.transform.TransformPoint(v).y);
            }Object.Destroy(mesh);return low;
        }
        [UnityTest,Timeout(300000)]
        public IEnumerator OrdinaryDialoguesKeepNamAboveLocalGroundAndLookingAtSpeaker()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1280,720);yield return new EnterPlayMode();
            yield return Audit();yield return new ExitPlayMode();
        }
        IEnumerator Audit(){
            Map02Mission mission=null;float deadline=Time.realtimeSinceStartup+90; Debug.Log("STANCE: waiting for mission");
            while(Time.realtimeSinceStartup<deadline){mission=Object.FindFirstObjectByType<Map02Mission>();Object.FindFirstObjectByType<Map02Arrival>()?.Skip();if(mission?.Ready==true&&mission.Chapter!=null)break;yield return null;}
            Assert.NotNull(mission?.Chapter);var chapter=mission.Chapter;Directory.CreateDirectory(Folder);var report=new System.Text.StringBuilder();
            object Call(string name,params object[] args)=>typeof(Map02ChapterDirector).GetMethod(name,F).Invoke(chapter,args);
            try{
                foreach(string id in new[]{"D1_01","D1_06","N1_02","N1_03","D2_01","D2_03","D3_06"}){
                    Debug.Log("STANCE: start "+id);chapter.Cancel();var snapshot=mission.Capture();snapshot.chapter=new Map02ChapterState();
                    foreach(var task in Map02ChapterDefinition.Load().tasks){if(task.id==id)break;snapshot.chapter.Complete(task.id);}
                    snapshot.chapter.dayOne=new Map02DayOneState();snapshot.chapter.dayOne.arrivalSeen=true;mission.Restore(snapshot);chapter.Cancel();
                    var taskData=Map02ChapterDefinition.Load().Find(id);var entries=Map02ChapterDefinition.Load().Lines(taskData.scene);
                    int line=Array.FindIndex(entries,e=>!e.radio&&e.speaker!="Nam"&&chapter.Actor(e.speaker)!=null&&chapter.Actor(e.speaker).visual.gameObject.activeInHierarchy);
                    Assert.GreaterOrEqual(line,0,id+" needs an in-person speaker for this audit");var partner=chapter.Actor(entries[line].speaker);
                    foreach(float approach in new[]{0f,120f,240f}){
                        chapter.Cancel();var candidate=partner.transform.position+Quaternion.Euler(0,approach,0)*Vector3.forward*1.85f;
                        if(NavMesh.SamplePosition(candidate,out var nav,.6f,NavMesh.AllAreas))candidate=nav.position;
                        var cc=mission.Player.GetComponent<CharacterController>();cc.enabled=false;mission.Player.SetPositionAndRotation(candidate,Quaternion.Euler(0,approach,0));cc.enabled=true;
                        Debug.Log("STANCE: begin "+id+" at "+candidate); Call("BeginScene",taskData,0,0);
                        var live=(Map02DialogueCatalog.Entry[])typeof(Map02ChapterDirector).GetField("lines",F).GetValue(chapter);
                        int liveLine=Array.FindIndex(live,e=>e.id==entries[line].id);if(liveLine<0)liveLine=Array.FindIndex(live,e=>e.speaker==partner.role);
                        Assert.GreaterOrEqual(liveLine,0);typeof(Map02ChapterDirector).GetField("lineIndex",F).SetValue(chapter,liveLine);typeof(Map02ChapterDirector).GetField("lineClock",F).SetValue(chapter,-100f);
                        yield return WaitGameSeconds(1.6f);
                        var animator=mission.Player.GetComponentInChildren<Animator>();var target=partner.Animator.GetBoneTransform(HumanBodyBones.Head).position;
                        var toward=Vector3.ProjectOnPlane(target-mission.Player.position,Vector3.up).normalized;
                        Debug.Log("STANCE: root="+mission.Player.position+" animator="+animator.transform.position+" local="+animator.transform.localPosition+" baked="+VisibleSole(animator)); float facing=Vector3.Dot(Map02ChapterDirector.BodyForward(animator,mission.Player),toward);float sole=VisibleSole(animator);
                        var surfaces=Physics.RaycastAll(mission.Player.position+Vector3.up*.3f,Vector3.down,2,~(1<<4),QueryTriggerInteraction.Ignore)
                            .Where(h=>h.normal.y>.8f&&!h.transform.IsChildOf(mission.Player)&&h.collider.GetComponentInParent<Map02ChapterActor>()==null).OrderBy(h=>h.distance).ToArray();
                        Assert.IsNotEmpty(surfaces);float ground=surfaces[0].point.y;
                        report.AppendLine($"{id} approach={approach} speaker={partner.role} facing={facing:F4} visibleSole={sole:F4} ground={ground:F4}");File.WriteAllText(Folder+"/results.txt",report.ToString());
                        ScreenCapture.CaptureScreenshot(Folder+"/latest.png");yield return null;yield return null;
                        Assert.Greater(facing,.94f,id+" Nam must face the visible NPC, even when approaching from behind");
                        Assert.That(sole-ground,Is.InRange(-.025f,.045f),id+" soles must rest on this conversation's ground, not the landing's height");
                        if(approach==120){ScreenCapture.CaptureScreenshot(Folder+"/"+id+".png");yield return null;yield return null;}
                    }
                }
            }finally{chapter.Cancel();}
            yield return new ExitPlayMode();
        }
    }
}
