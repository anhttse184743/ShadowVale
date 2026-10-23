using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02ChapterMechanicsTests:ForestSceneTestBase
    {
        [UnityTest,Timeout(240000)]public IEnumerator RepairAndAntennaRequireActualInputAndCoverFireLocksOnlyMovement()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;var arrival=Object.FindFirstObjectByType<Map02Arrival>();arrival?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var routing=RouteInputToGame();var c=m.Chapter;var keyboard=InputSystem.AddDevice<Keyboard>();c.enabled=false;
            var tick=typeof(Map02ChapterDirector).GetMethod("TickWork",BindingFlags.NonPublic|BindingFlags.Instance);
            var end=typeof(Map02ChapterDirector).GetMethod("EndScene",BindingFlags.NonPublic|BindingFlags.Instance);
            void Load(string id){var snapshot=m.Capture();var s=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){s.Complete(t.id);if(t.id==id)break;}snapshot.chapter=s;m.Restore(snapshot);}
            Map02ObjectiveObject Mark(string id)=>Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId==id);
            void Move(Map02ObjectiveObject marker){var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=marker.transform.position;cc.enabled=true;Physics.SyncTransforms();}
            void Key(params UnityEngine.InputSystem.Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();keyboard.MakeCurrent();}
            void Set(string field,object value)=>typeof(Map02ChapterDirector).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(c,value);
            try
            {
                Load("D1_02");var repair=Mark("D1_05");Move(repair);Assert.IsTrue(m.Interact(repair));Assert.IsFalse(m.CanSave());end.Invoke(c,null);
                var dayTick=typeof(Map02ChapterDirector).GetMethod("TickDay",BindingFlags.NonPublic|BindingFlags.Instance);
                Set("dayClock",0f);Set("daySpaceReleased",true);Key(UnityEngine.InputSystem.Key.Space);dayTick.Invoke(c,new object[]{0f});Assert.AreEqual(0,c.State.Count("D1_05"));Key();dayTick.Invoke(c,new object[]{0f});
                for(int i=0;i<6;i++){Set("dayClock",Map02DayOneState.SweepSeconds(c.State.dayOne.stakeHits/3)*.5f);Set("dayAttemptCycle",-1);Set("daySpaceReleased",true);Key(UnityEngine.InputSystem.Key.Space);Assert.True(keyboard.spaceKey.wasPressedThisFrame,"Fresh Space must reach the active keyboard");dayTick.Invoke(c,new object[]{0f});Assert.AreEqual(i+1,c.State.dayOne.stakeHits,"activity="+c.DayActivityName+" needle="+typeof(Map02ChapterDirector).GetField("dayClock",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(c));Key();dayTick.Invoke(c,new object[]{0f});}
                Assert.AreEqual(2,c.State.Count("D1_05"));Assert.False(c.State.Done("D1_05"));
                for(int i=0;i<3;i++)Assert.True(c.State.dayOne.LayBoard(i,i,false,out _));typeof(Map02ChapterDirector).GetMethod("SyncRepairCount",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,null);typeof(Map02ChapterDirector).GetMethod("CompleteDock",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,null);end.Invoke(c,null);Assert.IsTrue(c.State.Done("D1_05"));
                Load("D2_05");var antenna=Mark("D2_06");Move(antenna);Assert.IsTrue(m.Interact(antenna));end.Invoke(c,null);
                for(int i=0;i<25;i++)tick.Invoke(c,new object[]{.1f});Assert.IsFalse(c.State.Done("D2_06"),"Waiting without tuning cannot solve the antenna.");
                Set("signal",.72f);Key();for(int i=0;i<21&&c.CurrentScene==null;i++)tick.Invoke(c,new object[]{.1f});Assert.AreEqual("D2_06",c.CurrentScene.id);end.Invoke(c,null);Assert.IsTrue(c.State.Done("D2_06"));
                Load("N3_07");var cover=Mark("N3_08");Move(cover);Assert.IsTrue(m.Interact(cover));
                var controller=m.Player.GetComponent<ShadowVale.Gameplay.Player.PlayerController>();var combat=m.Player.GetComponent<ShadowVale.Gameplay.Combat.PlayerCombat>();var hp=m.Player.GetComponent<ShadowVale.Gameplay.Combat.Health>();
                Assert.IsTrue(c.MovementLocked);var approach=new UnityEngine.AI.NavMeshPath();Assert.IsTrue(UnityEngine.AI.NavMesh.CalculatePath(c.Actor("Hùng").transform.position,c.ChargePoint,UnityEngine.AI.NavMesh.AllAreas,approach));Assert.AreEqual(UnityEngine.AI.NavMeshPathStatus.PathComplete,approach.status);Assert.IsFalse(controller.InputAllowed());Assert.IsTrue(combat.InputAllowed());Assert.IsTrue(hp.CinematicInvulnerable);
                var before=hp.Current;hp.TakeDamage(100000,m.Player.position,null);Assert.AreEqual(before,hp.Current);
                var saved=m.Capture();m.Restore(saved);Assert.IsTrue(c.MovementLocked&&hp.CinematicInvulnerable,"Load during cover fire must preserve the no-failure rule.");
                var startHung=c.Actor("Hùng").transform.position;var initialDistance=Vector3.Distance(startHung,c.ChargePoint);
                float moveDeadline=Time.time+6;while(Time.time<moveDeadline&&Vector3.Distance(startHung,c.Actor("Hùng").transform.position)<2){yield return null;}
                Assert.Greater(Vector3.Distance(startHung,c.Actor("Hùng").transform.position),1,"Hùng must actually approach during cover, including after load.");
                Assert.Less(Vector3.Distance(c.Actor("Hùng").transform.position,c.ChargePoint),initialDistance,"His route must advance toward the charge.");
                c.State.Complete("N3_08");var final=Mark("N3_09");Move(final);Assert.IsTrue(m.Interact(final));end.Invoke(c,null);
                Assert.IsTrue(c.State.hungDead&&c.State.tankDestroyed&&c.State.radioTransferred);Assert.AreEqual("DAWN",c.CurrentScene.id,"Do not expose Nam to combat between the fixed ending and dawn.");
                end.Invoke(c,null);Assert.IsTrue(c.State.finished);
            }
            finally{RestoreInputRouting(routing);InputSystem.RemoveDevice(keyboard);c.Cancel();c.enabled=true;}
            yield return new ExitPlayMode();
        }
    }
}
