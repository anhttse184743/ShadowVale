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
    public sealed class Map02FenceRouteSafetyTests:ForestSceneTestBase
    {
        [UnityTest,Timeout(300000)]public IEnumerator FencesLeaveBuildingsRoadsAndEveryExistingMissionRouteClear()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1280,720);yield return new EnterPlayMode();
            yield return Check();yield return new ExitPlayMode();
        }
        IEnumerator Check()
        {
            Map02Mission mission=null;float deadline=Time.realtimeSinceStartup+120;
            while(Time.realtimeSinceStartup<deadline){mission=Object.FindFirstObjectByType<Map02Mission>();Object.FindFirstObjectByType<Map02Arrival>()?.Skip();if(mission?.Ready==true&&mission.Chapter!=null)break;yield return null;}
            Assert.NotNull(mission?.Chapter);var chapter=mission.Chapter;chapter.Cancel();
            var clearance=(Map02FenceClearance)typeof(Map02ChapterDirector).GetField("fenceClearance",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chapter);
            Assert.Greater(clearance.Buildings.Count,5,"Must detect the real village houses");Assert.Greater(clearance.Routes.Count,100,"Protect routes across all mission sites");
            Directory.CreateDirectory("Logs/FenceReview_20261128");var report=new System.Text.StringBuilder();
            float Length(Vector3[] points){float total=0;for(int i=1;i<points.Length;i++)total+=Vector3.Distance(points[i-1],points[i]);return total;}
            foreach(string first in new[]{"D1_01","D2_01","N2_01","D3_01","N3_00"}){
                var save=mission.Capture();save.chapter=new Map02ChapterState();foreach(var task in Map02ChapterDefinition.Load().tasks){if(task.id==first)break;save.chapter.Complete(task.id);}save.chapter.dayOne.arrivalSeen=true;mission.Restore(save);chapter.Cancel();yield return WaitGameSeconds(1.2f);
                var fenceObjects=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.StartsWith("Continuous village boundary fence")).ToArray();
                // Compare the same phase with and without the new boundary decoration. Other mission props stay identical.
                foreach(var fence in fenceObjects)fence.gameObject.SetActive(false);yield return WaitGameSeconds(.8f);
                var baseline=new System.Collections.Generic.List<(Vector3 start,Vector3 end,float length,Vector3[] corners)>();var path=new NavMeshPath();
                foreach(var route in clearance.Routes){
                    if(route.Length<2)continue;
                    if(!NavMesh.SamplePosition(route[0],out var a,.4f,NavMesh.AllAreas)||!NavMesh.SamplePosition(route[route.Length-1],out var b,.4f,NavMesh.AllAreas))continue;
                    if(NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete)baseline.Add((a.position,b.position,Length(path.corners),path.corners));
                }
                Assert.Greater(baseline.Count,100,"Each phase must have meaningful independent route coverage");
                foreach(var fence in fenceObjects)fence.gameObject.SetActive(true);yield return WaitGameSeconds(.8f);
                int checkedRoutes=0;
                foreach(var route in baseline){
                    Assert.True(NavMesh.SamplePosition(route.start,out var a,.4f,NavMesh.AllAreas),"Fence removed route start "+route.start);
                    Assert.True(NavMesh.SamplePosition(route.end,out var b,.4f,NavMesh.AllAreas),"Fence removed route endpoint "+route.end);
                    bool found=NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path);
                    Assert.True(found&&path.status==NavMeshPathStatus.PathComplete,first+" fence blocked route "+route.start+" -> "+route.end);
                    // NavMesh re-triangulation can choose slightly different corners even when a corridor is unchanged.
                    // Require the original corridor itself to remain navigable, and physically clear for Nam/escorts.
                    for(int i=1;i<route.corners.Length;i++){
                        if(NavMesh.Raycast(route.corners[i-1],route.corners[i],out var edge,NavMesh.AllAreas))
                            Assert.True(Vector3.Distance(edge.position,route.corners[i-1])<.1f||Vector3.Distance(edge.position,route.corners[i])<.1f,first+" original corridor blocked away from its existing corner "+edge.position);
                        var from=route.corners[i-1]+Vector3.up*.9f;var delta=route.corners[i]-route.corners[i-1];
                        foreach(var hit in Physics.SphereCastAll(from,.4f,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                            Assert.False(hit.collider.GetComponentsInParent<Transform>().Any(t=>t.name.StartsWith("Continuous village boundary fence")),first+" fence collision on original mission corridor "+hit.point);
                    }
                    checkedRoutes++;
                }                report.AppendLine(first+" routes="+checkedRoutes+" sections="+chapter.NightFenceSections);File.WriteAllText("Logs/FenceReview_20261128/routes.txt",report.ToString());
            }
            foreach(var section in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.StartsWith("Continuous village boundary fence"))){
                foreach(var renderer in section.GetComponentsInChildren<Renderer>())foreach(var building in clearance.Buildings){
                    var overlap=renderer.bounds;Assert.False(overlap.min.x<building.max.x-.1f&&overlap.max.x>building.min.x+.1f&&overlap.min.z<building.max.z-.1f&&overlap.max.z>building.min.z+.1f,"Fence intersects house footprint: "+section.position);
                }
            }
            var day=mission.Capture();day.chapter=new Map02ChapterState();day.chapter.dayOne.arrivalSeen=true;mission.Restore(day);chapter.Cancel();yield return WaitGameSeconds(.5f);
            var rig=typeof(Map02ChapterDirector).GetField("rig",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(chapter);
            rig.GetType().GetMethod("SetCinematicView").Invoke(rig,new object[]{new Vector3(-77,92,-18),Quaternion.Euler(80,0,0),65f,1f});
            yield return WaitGameSeconds(.3f);ScreenCapture.CaptureScreenshot("Logs/FenceReview_20261128/layout.png");yield return null;yield return null;
            // Reminders describe the required action before opening the binoculars, and clear with completion.
            var snapshot=mission.Capture();snapshot.chapter=new Map02ChapterState();foreach(var task in Map02ChapterDefinition.Load().tasks){if(task.id=="N2_01")break;snapshot.chapter.Complete(task.id);}snapshot.chapter.dayOne.arrivalSeen=true;mission.Restore(snapshot);chapter.Cancel();
            StringAssert.Contains("[F]",chapter.RequiredObservationReminder);chapter.State.Complete("N2_01");Assert.IsNull(chapter.RequiredObservationReminder);
        }
    }
}
