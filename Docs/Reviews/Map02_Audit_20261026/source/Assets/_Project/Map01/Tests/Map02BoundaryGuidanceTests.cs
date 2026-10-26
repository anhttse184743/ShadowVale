using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02BoundaryGuidanceTests:ForestSceneTestBase
    {
        [Test]public void EntireSurveyUsesUnclampedCartesianCoordinates()
        {
            var area=new Rect(28,24,230,230);
            foreach(var point in new[]{new Vector3(-105,1,-95),new Vector3(105,1,95),new Vector3(-79,1,20),Vector3.zero})
            {
                var uv=Map02Navigation.ChartUv(point);var at=Map02Navigation.Plot(point,area);
                Assert.AreEqual(area.x+uv.x*area.width,at.x,.001f);Assert.AreEqual(area.y+(1-uv.y)*area.height,at.y,.001f);Assert.True(area.Contains(at));
            }
            Assert.False(Map02Mission.WithinPlayableBounds(new Vector3(111,1,0)));Assert.False(Map02Mission.WithinPlayableBounds(new Vector3(0,1,101)));
        }
        [UnityTest,Timeout(300000)]public IEnumerator RealPerimeterStopsWalkingAndUnderworldSaveRecoversWithoutQuestOrInventoryReset()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1920,1080);yield return new EnterPlayMode();
            yield return CheckSafetyAndGuidance();yield return new ExitPlayMode();
        }
        static IEnumerator CheckSafetyAndGuidance()
        {
            Map02Mission m=null;float until=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;c.Cancel();c.enabled=false;c.State.dayOne.arrivalSeen=true;
            var cc=m.Player.GetComponent<CharacterController>();
            void Place(Vector3 p){cc.enabled=false;m.Player.position=p;cc.enabled=true;m.Player.GetComponent<PlayerController>().RestoreMotion(false);Physics.SyncTransforms();}
            foreach(var d in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})
            {
                var point=d.x!=0?new Vector3(d.x*107,1.5f,0):new Vector3(40,1.5f,d.z*97);
                Place(point);cc.Move(d*6);Assert.Less(Mathf.Abs(m.Player.position.x),109.01f);Assert.Less(Mathf.Abs(m.Player.position.z),99.01f);
            }
            Place(c.Sites["gate"].position);yield return null;
            Assert.Less(Vector3.Distance(c.TrackedMarker.transform.position,c.Actor("Anh Lực").transform.position),.01f);
            var before=m.Capture();var old=m.BoundaryRecoveryCount;
            before.position=new Vector3(170,-12,170);m.Restore(before);
            Assert.Greater(m.BoundaryRecoveryCount,old);Assert.True(Map02Mission.WithinPlayableBounds(m.Player.position));Assert.Greater(m.Player.position.y,-.4f);
            Assert.AreEqual(before.health,m.Capture().health);Assert.AreEqual(before.magazine,m.Capture().magazine);Assert.AreEqual(before.chapter.phase,c.State.phase);Assert.AreEqual(before.chapter.completed.Count,c.State.completed.Count);
            CollectionAssert.AreEquivalent(before.inventory.Select(i=>i.id+":"+i.count),m.Capture().inventory.Select(i=>i.id+":"+i.count));
            Assert.True(NavMesh.SamplePosition(m.Player.position,out _,1,NavMesh.AllAreas));
            // This is the playable Day 1 HUD, not the retired Contact/VillageHelp prototype.
            Place(c.Sites["gate"].position+Vector3.back*4);yield return null;
            Directory.CreateDirectory("Logs/Map02Navigation_20261022");
            yield return null;ScreenCapture.CaptureScreenshot("Logs/Map02Navigation_20261022/day1-hud.png");yield return null;
            c.enabled=true;
        }
    }
}
