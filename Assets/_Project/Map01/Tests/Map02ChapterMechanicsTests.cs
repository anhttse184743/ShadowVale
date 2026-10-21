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
            Assert.NotNull(m?.Chapter);var c=m.Chapter;var keyboard=InputSystem.AddDevice<Keyboard>();c.enabled=false;
            var tick=typeof(Map02ChapterDirector).GetMethod("TickWork",BindingFlags.NonPublic|BindingFlags.Instance);
            var end=typeof(Map02ChapterDirector).GetMethod("EndScene",BindingFlags.NonPublic|BindingFlags.Instance);
            var finish=typeof(Map02ChapterDirector).GetMethod("Finish",BindingFlags.NonPublic|BindingFlags.Instance);
            void Load(string id){var snapshot=m.Capture();var s=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){s.Complete(t.id);if(t.id==id)break;}snapshot.chapter=s;m.Restore(snapshot);}
            Map02ObjectiveObject Mark(string id)=>Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId==id);
            void Move(Map02ObjectiveObject marker){var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=marker.transform.position;cc.enabled=true;Physics.SyncTransforms();}
            void Key(params UnityEngine.InputSystem.Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();}
            void Set(string field,object value)=>typeof(Map02ChapterDirector).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(c,value);
            object Get(string field)=>typeof(Map02ChapterDirector).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(c);
            try
            {
                Load("D1_02");var repair=Mark("D1_05");Move(repair);Assert.IsTrue(m.Interact(repair));Assert.IsFalse(m.CanSave());end.Invoke(c,null);
                Set("workClock",0f);Set("heldReleased",true);Key(UnityEngine.InputSystem.Key.Space);tick.Invoke(c,new object[]{0f});Assert.AreEqual(0,c.State.Count("D1_05"),"A mistimed hit must not place a plank.");Key();
                for(int i=0;i<5;i++){Set("workClock",.5f/.65f);Set("heldReleased",true);Key(UnityEngine.InputSystem.Key.Space);tick.Invoke(c,new object[]{0f});Key();}
                Assert.IsTrue(c.State.Done("D1_05"),$"planks {c.State.Count("D1_05")}/5, busy {c.Busy}, scene {c.CurrentScene?.id}, active {c.State.activeTask}, kb {Keyboard.current==keyboard}, held {Get("heldReleased")}, clock {Get("workClock")}, work {(Get("workTask") as Map02ChapterTask)?.id}, doing {Get("doingWork")}");
                Load("D2_05");var antenna=Mark("D2_06");Move(antenna);Assert.IsTrue(m.Interact(antenna));end.Invoke(c,null);
                for(int i=0;i<25;i++)tick.Invoke(c,new object[]{.1f});Assert.IsFalse(c.State.Done("D2_06"),"Waiting without tuning cannot solve the antenna.");
                Set("signal",.72f);Key();for(int i=0;i<21&&c.CurrentScene==null;i++)tick.Invoke(c,new object[]{.1f});Assert.AreEqual("D2_06",c.CurrentScene.id);end.Invoke(c,null);Assert.IsTrue(c.State.Done("D2_06"));
                Load("N3_07");var cover=Mark("N3_08");Move(cover);Assert.IsTrue(m.Interact(cover));
                Assert.IsTrue(c.HasBark("m2v3_sacrifice_01"),"\"Đi đi! Tôi che cho!\" opens the cover fire without taking control away.");Assert.IsFalse(c.Busy);
                var controller=m.Player.GetComponent<ShadowVale.Gameplay.Player.PlayerController>();var combat=m.Player.GetComponent<ShadowVale.Gameplay.Combat.PlayerCombat>();var hp=m.Player.GetComponent<ShadowVale.Gameplay.Combat.Health>();
                Assert.IsTrue(c.MovementLocked);var approach=new UnityEngine.AI.NavMeshPath();Assert.IsTrue(UnityEngine.AI.NavMesh.CalculatePath(c.Actor("Hùng").transform.position,c.ChargePoint,UnityEngine.AI.NavMesh.AllAreas,approach));Assert.AreEqual(UnityEngine.AI.NavMeshPathStatus.PathComplete,approach.status);Assert.IsFalse(controller.InputAllowed());Assert.IsTrue(combat.InputAllowed());Assert.IsTrue(hp.CinematicInvulnerable);
                var before=hp.Current;hp.TakeDamage(100000,m.Player.position,null);Assert.AreEqual(before,hp.Current);
                var saved=m.Capture();m.Restore(saved);Assert.IsTrue(c.MovementLocked&&hp.CinematicInvulnerable,"Load during cover fire must preserve the no-failure rule.");
                var startHung=c.Actor("Hùng").transform.position;var initialDistance=Vector3.Distance(startHung,c.ChargePoint);
                float moveDeadline=Time.time+6;while(Time.time<moveDeadline&&Vector3.Distance(startHung,c.Actor("Hùng").transform.position)<2){yield return null;}
                Assert.Greater(Vector3.Distance(startHung,c.Actor("Hùng").transform.position),1,"Hùng must actually approach during cover, including after load.");
                Assert.Less(Vector3.Distance(c.Actor("Hùng").transform.position,c.ChargePoint),initialDistance,"His route must advance toward the charge.");
                finish.Invoke(c,new object[]{Map02ChapterDefinition.Load().Find("N3_08")});
                Assert.AreEqual("N3_09",c.CurrentScene?.id,"The sacrifice follows the cover fire without walking to a pin.");end.Invoke(c,null);
                Assert.IsTrue(c.State.hungDead&&c.State.tankDestroyed&&c.State.radioTransferred);Assert.AreEqual("DAWN",c.CurrentScene.id,"Do not expose Nam to combat between the fixed ending and dawn.");
                end.Invoke(c,null);Assert.IsTrue(c.State.finished);
            }
            finally{InputSystem.RemoveDevice(keyboard);c.Cancel();c.enabled=true;}
            yield return new ExitPlayMode();
        }
        [UnityTest,Timeout(240000)]public IEnumerator WoundBankAndSacrificeRunWithoutPressingEAndRadioCallsKeepControl()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;c.enabled=false;var def=Map02ChapterDefinition.Load();
            var finish=typeof(Map02ChapterDirector).GetMethod("Finish",BindingFlags.NonPublic|BindingFlags.Instance);
            var end=typeof(Map02ChapterDirector).GetMethod("EndScene",BindingFlags.NonPublic|BindingFlags.Instance);
            var update=typeof(Map02ChapterDirector).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Instance);
            void Load(string id){var snapshot=m.Capture();var s=new Map02ChapterState();foreach(var t in def.tasks){s.Complete(t.id);if(t.id==id)break;}snapshot.chapter=s;m.Restore(snapshot);}
            void Place(Vector3 p){var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=p;cc.enabled=true;Physics.SyncTransforms();}
            Map02ObjectiveObject Mark(string id)=>Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId==id);
            try
            {
                // Leaving the first lane: the fallback order is a radio call, Nam keeps playing.
                Load("N3_03");finish.Invoke(c,new object[]{def.Find("N3_04")});
                Assert.IsNull(c.CurrentScene);Assert.IsTrue(c.HasBark("m2v3_wound_01")&&c.HasBark("m2v3_wound_02"));
                Assert.IsFalse(c.ShowInteractPrompt(Mark("N3_WOUND"))||c.ShowInteractPrompt(Mark("N3_07"))||c.ShowInteractPrompt(Mark("N3_09")));
                // Reaching 90 % puts Nam straight into the tank shot.
                Load("N3_05B");finish.Invoke(c,new object[]{def.Find("N3_05C")});
                Assert.AreEqual("N3_WOUND",c.CurrentScene?.id);Assert.AreEqual("m2v3_wound_04",def.Lines("wound")[0].id);end.Invoke(c,null);
                // A checkpoint stored between the hold and the shot resumes into the shot.
                Load("N3_05C");Assert.AreEqual("N3_WOUND",c.CurrentScene?.id);end.Invoke(c,null);
                // The last-boat scene starts on reaching the bank.
                Load("N3_06");Assert.IsNull(c.CurrentScene);Place(c.Sites["ditch"].position+Vector3.forward*3);update.Invoke(c,null);
                Assert.AreEqual("N3_07",c.CurrentScene?.id);end.Invoke(c,null);
                // A checkpoint after the cover fire resumes into the sacrifice, never into combat.
                Load("N3_08");Assert.AreEqual("N3_09",c.CurrentScene?.id);Assert.IsTrue(c.WaitingForLine,"The echo is heard only after the blast.");
                end.Invoke(c,null);Assert.IsTrue(c.State.hungDead);
            }
            finally{c.Cancel();c.enabled=true;}
            yield return new ExitPlayMode();
        }
    }
}
