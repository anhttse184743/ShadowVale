using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// Review report: where each named person actually stands at the Day 3 / Night 3 checkpoints, and who stands on whom
    /// (Logs/Map02Chapter/CastSpacing.txt). Not an automatic check.
    /// </summary>
    public sealed class Map02CastSpacingReport:ForestSceneTestBase
    {
        static readonly string[] Cast={"Anh Thành","Anh Lực","Hùng","Chị Lành","Ông Sáu","Bé Tí","Chú Bảy","Tâm"};

        [UnityTest,Explicit("Review report, run on demand."),Timeout(300000)]public IEnumerator WhereTheCastStandsThroughDayThreeAndNightThree()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return InPlayMode();
            yield return new ExitPlayMode();
        }
        [UnityTest,Explicit("Review report, run on demand."),Timeout(300000)]public IEnumerator YardSpotsThatCanWalkToTheShelter()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return SpotsInPlayMode();
            yield return new ExitPlayMode();
        }
        [UnityTest,Explicit("Review capture, run on demand."),Timeout(300000)]public IEnumerator HungWalksOnDayThree()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return WalkInPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator WalkInPlayMode()
        {
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            var c=m.Chapter;var snap=m.Capture();var s=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){if(t.id=="D3_02")break;s.Complete(t.id);}
            snap.chapter=s;m.Restore(snap);float until=Time.time+1.6f;while(Time.time<until)yield return null;
            var hung=c.Actor("Hùng");var start=hung.transform.position;hung.GoTo(start+Vector3.left*9,1.35f);
            var camera=m.GameCamera;var rig=camera.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>();Directory.CreateDirectory("Logs/Map02Chapter");
            for(int shot=0;shot<3;shot++)
            {
                until=Time.time+.6f;while(Time.time<until)yield return null;
                var at=hung.transform.position+Vector3.up*1.1f;var eye=at+Vector3.back*3.2f+Vector3.up*.3f;
                for(int i=0;i<3;i++){rig.SetCinematicView(eye,Quaternion.LookRotation(at-eye),40,1);yield return null;}
                var rt=RenderTexture.GetTemporary(960,540,24);var image=new Texture2D(960,540,TextureFormat.RGB24,false);var old=camera.targetTexture;
                try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes($"Logs/Map02Chapter/HungWalk{shot}.png",image.EncodeToPNG());}
                finally{camera.targetTexture=old;RenderTexture.active=null;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);}
            }
            rig.ClearCinematicView();
        }
        static IEnumerator SpotsInPlayMode()
        {
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            var yard=m.Chapter.Sites["family"].position;var shelter=m.Chapter.Sites["southStore"].position;var lines=new List<string>();
            for(float x=-3;x<=3.01f;x+=.6f)for(float z=-3;z<=3.01f;z+=.6f)
            {
                var at=yard+new Vector3(x,0,z);bool on=UnityEngine.AI.NavMesh.SamplePosition(at,out var hit,.3f,UnityEngine.AI.NavMesh.AllAreas);
                var path=new UnityEngine.AI.NavMeshPath();bool walks=on&&UnityEngine.AI.NavMesh.CalculatePath(hit.position,shelter,UnityEngine.AI.NavMesh.AllAreas,path)&&path.status==UnityEngine.AI.NavMeshPathStatus.PathComplete;
                bool clear=!Physics.CheckCapsule(at+Vector3.up*.4f,at+Vector3.up*1.6f,.3f,~0,QueryTriggerInteraction.Ignore);
                lines.Add($"{x,5:0.0} {z,5:0.0} navmesh={on} walks={walks} clear={clear}");
            }
            File.WriteAllLines("Logs/Map02Chapter/YardSpots.txt",lines);yield return null;
        }
        static IEnumerator InPlayMode()
        {
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;var report=new List<string>();
            // After `done` is complete: the state a player reaches there (or loads into).
            foreach(var done in new[]{"N2_05","D3_02","D3_04","D3_05","D3_06","N3_00","N3_01","N3_02","N3_03","N3_05C","N3_06","N3_07","N3_08"})
            {
                var snap=m.Capture();var s=new Map02ChapterState();
                foreach(var t in Map02ChapterDefinition.Load().tasks){s.Complete(t.id);if(t.id==done)break;}
                snap.chapter=s;m.Restore(snap);float until=Time.time+2.5f;while(Time.time<until)yield return null;   // fade, then a moment to walk
                var next=s.AvailableTasks.FirstOrDefault();
                report.Add($"== after {done} (phase {s.phase}, next {next?.id} {next?.title})");
                var shown=Cast.Select(r=>(role:r,actor:c.Actor(r))).Where(p=>p.actor!=null&&p.actor.visual!=null&&p.actor.visual.gameObject.activeInHierarchy).ToList();
                foreach(var p in shown)report.Add($"   {p.role,-10} {p.actor.transform.position.x,7:0.0} {p.actor.transform.position.z,7:0.0}");
                for(int i=0;i<shown.Count;i++)for(int j=i+1;j<shown.Count;j++)
                {
                    var a=shown[i].actor.transform.position;var b=shown[j].actor.transform.position;float d=Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
                    if(d<1.2f)report.Add($"   !! {shown[i].role} and {shown[j].role} stand {d:0.00} m apart");
                }
                var player=m.Player.position;
                foreach(var p in shown){var a=p.actor.transform.position;float d=Vector2.Distance(new Vector2(a.x,a.z),new Vector2(player.x,player.z));if(d<.8f)report.Add($"   !! {p.role} stands on Nam ({d:0.00} m)");}
            }
            Directory.CreateDirectory("Logs/Map02Chapter");File.WriteAllLines("Logs/Map02Chapter/CastSpacing.txt",report);
        }
    }
}
