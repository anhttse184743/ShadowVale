using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02DayOneIntegrationTests:ForestSceneTestBase
    {
        [UnityTest,Timeout(240000)] public IEnumerator ActualChapterRequiresPackingDockAndPostSafetyWithoutTouchingLaterDays()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;c.enabled=false;
            object Call(string method,params object[] args)=>typeof(Map02ChapterDirector).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,args);
            Map02ObjectiveObject Mark(string id)=>Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(x=>x.chapterId==id);
            void Move(Map02ObjectiveObject marker){var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=marker.transform.position;cc.enabled=true;Physics.SyncTransforms();}
            void LoadBefore(string id){var snapshot=m.Capture();var state=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){if(t.id==id)break;state.Complete(t.id);}snapshot.chapter=state;m.Restore(snapshot);}
            bool Solve(Map02DayOneState s,int i=0){if(i==3)return s.Packed;for(int r=0;r<2;r++)for(int x=2;x<6;x++)for(int y=0;y<4;y++){if(!s.PlaceCrate(i,x,y,r==1,out _))continue;if(Solve(s,i+1))return true;s.crates[i]=new Map02DayOneState.Placement();}return false;}
            try
            {
                LoadBefore("D1_03");var cargo=Mark("D1_04");
                for(int i=0;i<3;i++){Move(cargo);Assert.True(m.Interact(cargo));Assert.AreEqual("D1_04",c.State.carry);Move(cargo);Assert.True(m.Interact(cargo));Assert.AreEqual(i+1,c.State.Count("D1_04"));}
                Assert.False(c.State.Done("D1_04"));Assert.AreEqual(0,c.State.storedRounds);Assert.Less(Vector3.Distance(cargo.transform.position,c.Sites["store"].position),.1f);
                Move(cargo);Assert.True(m.Interact(cargo));Assert.True(c.DayActivityActive);Assert.False(m.CanSave());Call("CompletePacking");Assert.False(c.State.Done("D1_04"));
                Assert.True(Solve(c.State.dayOne));Call("CompletePacking");Assert.True(c.State.Done("D1_04"));Assert.AreEqual(90,c.State.storedRounds);Assert.False(c.DayActivityActive);
                var saved=m.Capture();m.Restore(saved);Assert.True(c.State.dayOne.Packed);Assert.AreEqual(90,c.State.storedRounds);
                var dock=Mark("D1_05");Move(dock);Assert.True(m.Interact(dock));Assert.NotNull(c.CurrentScene);Call("EndScene");Assert.AreEqual("Dock",c.DayActivityName);
                for(int i=0;i<6;i++)Assert.True(c.State.dayOne.Strike(.5f));Call("SyncRepairCount");Assert.AreEqual(2,c.State.Count("D1_05"));Call("CompleteDock");Assert.False(c.State.Done("D1_05"));
                for(int i=0;i<3;i++)Assert.True(c.State.dayOne.LayBoard(i,i,false,out _));Call("SyncRepairCount");Call("CompleteDock");Assert.NotNull(c.CurrentScene);Call("EndScene");Assert.True(c.State.Done("D1_05"));
                LoadBefore("N1_02");var post=Mark("N1_02");Call("StartPost",Map02ChapterDefinition.Load().Find("N1_02"));Move(post);Assert.True(m.Interact(post));Assert.IsNull(c.CurrentScene);Assert.False(c.State.Done("N1_02"));c.State.dayOne.posts[0].lampAngle=180;Assert.True(m.Interact(post));Call("EndScene");Assert.True(c.State.Done("N1_02"));
                var gate=Mark("N1_03");Call("StartPost",Map02ChapterDefinition.Load().Find("N1_03"));var p=c.State.dayOne.posts[1];p.lampAngle=180;p.gateClosed=p.latchSeated=p.bundleStowed=true;Move(gate);Assert.True(m.Interact(gate));Call("EndScene");Assert.True(c.State.Done("N1_03"));Assert.AreEqual(3,c.State.dayOne.medicineMask);
                var afterWatch=m.Capture();m.Restore(afterWatch);Assert.True((bool)typeof(Map02ChapterActor).GetField("travelling",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(c.Actor("Hùng")),"Loading after the watch must resume Hùng returning to rest.");
                LoadBefore("D2_06");var antenna=Mark("D2_06");Move(antenna);Assert.True(m.Interact(antenna));Call("EndScene");Assert.False(c.DayActivityActive,"Day 2 antenna must keep its own interaction.");c.Cancel();
            }
            finally{c.Cancel();c.enabled=true;}
            yield return new ExitPlayMode();
        }
    }
}
