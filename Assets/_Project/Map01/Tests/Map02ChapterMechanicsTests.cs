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
                c.enabled=true; // Hùng's crawl is paced by the director.
                var startHung=c.Actor("Hùng").transform.position;var initialDistance=Vector3.Distance(startHung,c.ChargePoint);
                float moveDeadline=Time.time+6;while(Time.time<moveDeadline&&Vector3.Distance(startHung,c.Actor("Hùng").transform.position)<2){yield return null;}
                Assert.Greater(Vector3.Distance(startHung,c.Actor("Hùng").transform.position),1,"Hùng must actually approach during cover, including after load.");
                Assert.Less(Vector3.Distance(c.Actor("Hùng").transform.position,c.ChargePoint),initialDistance,"His route must advance toward the charge.");
                c.enabled=false;
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
        [UnityTest,Timeout(240000)]public IEnumerator LaneHoldsFollowEvacuationAndTheFallenDropRounds()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;var def=Map02ChapterDefinition.Load();var hp=m.Player.GetComponent<ShadowVale.Gameplay.Combat.Health>();
            void Load(string id){var snapshot=m.Capture();var s=new Map02ChapterState();foreach(var t in def.tasks){s.Complete(t.id);if(t.id==id)break;}snapshot.chapter=s;m.Restore(snapshot);}
            void Place(Vector3 p){var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=p;cc.enabled=true;Physics.SyncTransforms();}
            Map02ObjectiveObject Mark(string id)=>Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId==id);
            void Kill(string group){foreach(var e in c.Enemies.Where(e=>e!=null&&e.group==group&&!e.Dead).ToArray())e.HitPoints.TakeDamage(1000,e.transform.position,m.Player.gameObject);}
            try
            {
                Load("N3_04");var lane=def.Find("N3_05A");var mark=Mark("N3_05A");var away=mark.transform.position+Vector3.right*30;
                Assert.IsFalse(c.ShowInteractPrompt(mark),"Holding a lane is never started with E.");
                Place(away);yield return null;yield return null;Assert.AreNotEqual("N3_05A",c.State.activeTask,"Holding starts only in the lane.");
                hp.CinematicInvulnerable=true;Place(mark.transform.position);
                float until=Time.time+6;while(Time.time<until&&c.SoldiersPressing("N3_05A")==0)yield return null;
                Assert.AreEqual("N3_05A",c.State.activeTask);Assert.Greater(c.SoldiersPressing("N3_05A"),0);
                float p0=c.State.taskProgress,t0=Time.time;yield return WaitGameSeconds(2f);
                Assert.Greater(c.State.taskProgress,p0,"Evacuation moves while Nam holds the lane.");
                Assert.Less((c.State.taskProgress-p0)/(Time.time-t0),.6f/lane.seconds,"Soldiers in the lane slow the evacuation.");
                Assert.IsFalse(c.Instruction.Contains("giây"),"No countdown: "+c.Instruction);StringAssert.Contains("Sơ tán",c.Instruction);
                // Leaving the lane neither advances nor loses evacuation.
                Place(away);yield return null;float held=c.State.taskProgress;yield return WaitGameSeconds(1f);
                Assert.AreEqual(held,c.State.taskProgress,1e-4f);
                // The second wave comes half way; the mark alone does not hold the lane while soldiers remain.
                Kill("N3_05A");c.State.taskProgress=.49f;Place(mark.transform.position);
                until=Time.time+8;while(Time.time<until&&!c.State.combatGroups.Contains("N3_05A_b"))yield return null;
                Assert.IsTrue(c.State.combatGroups.Contains("N3_05A_b"));Assert.AreEqual(Mathf.FloorToInt(Mathf.Lerp(60,70,c.State.taskProgress)),c.Evacuation);
                c.State.taskProgress=1;yield return null;yield return null;Assert.IsFalse(c.State.Done("N3_05A"));
                Kill("N3_05A_b");until=Time.time+3;while(Time.time<until&&!c.State.Done("N3_05A"))yield return null;
                Assert.IsTrue(c.State.Done("N3_05A"));Assert.AreEqual(70,c.State.evacuation);
                // The fallen give their rounds once; a load does not give them again.
                hp.CinematicInvulnerable=true;var body=c.Enemies.First(e=>e!=null&&e.Dead&&!e.Looted);int before=m.Inventory.TryGetValue(m.RifleAmmo,out int had)?had:0;
                Place(body.transform.position);yield return null;yield return null;
                int after=m.Inventory[m.RifleAmmo];Assert.That(after-before,Is.InRange(5,8));Assert.IsTrue(body.Looted);
                var saved=m.Capture();m.Restore(saved);hp.CinematicInvulnerable=true;var again=c.Enemies.First(e=>e.id==body.id);Assert.IsTrue(again.Looted);
                Place(again.transform.position);yield return null;yield return null;Assert.AreEqual(after,m.Inventory[m.RifleAmmo]);
                // Budget: the 90 stored rounds plus the smallest drops cover every Night 3 soldier at 60 % hits.
                var rifle=m.Player.GetComponent<ShadowVale.Gameplay.Combat.PlayerCombat>().WeaponFor(ShadowVale.Gameplay.Combat.WeaponKind.Rifle);
                int soldiers=4+4*(4+3),cost=Mathf.CeilToInt(Mathf.Ceil(100f/rifle.Damage)/.6f);
                Assert.GreaterOrEqual(90+soldiers*5,soldiers*cost,$"rifle damage {rifle.Damage}");
            }
            finally{c.Cancel();hp.CinematicInvulnerable=false;}
            yield return new ExitPlayMode();
        }
        [UnityTest,Timeout(300000)]public IEnumerator CoverFireTakesSixtyToNinetySecondsWhateverNamHits()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return CoverFireInPlayMode(); // Closures must be created after the domain reload.
            yield return new ExitPlayMode();
        }
        static IEnumerator CoverFireInPlayMode()
        {
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;var def=Map02ChapterDefinition.Load();
            void Load(string id){var snapshot=m.Capture();var s=new Map02ChapterState();foreach(var t in def.tasks){s.Complete(t.id);if(t.id==id)break;}snapshot.chapter=s;m.Restore(snapshot);}
            void Place(Vector3 p){var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=p;cc.enabled=true;Physics.SyncTransforms();}
            Map02ObjectiveObject Mark(string id)=>Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId==id);
            Map02ChapterEnemy[] Squad()=>c.Enemies.Where(e=>e!=null&&e.group=="N3_08").ToArray();
            IEnumerator Begin(){Load("N3_07");var cover=Mark("N3_08");Place(cover.transform.position);Assert.IsTrue(m.Interact(cover));yield return null;yield return null;}
            float Off(Vector3 p,Vector3[] line){float best=float.MaxValue;for(int i=1;i<line.Length;i++){var a=line[i-1];var b=line[i];var t=Mathf.Clamp01(Vector3.Dot(p-a,b-a)/Mathf.Max(.0001f,(b-a).sqrMagnitude));best=Mathf.Min(best,Vector3.Distance(p,a+(b-a)*t));}return best;}
            try
            {
                yield return Begin();
                var squad=Squad();Assert.AreEqual(4,squad.Length,"The tank's escort is four men behind it.");Assert.IsTrue(squad.All(e=>e.HoldPosition));
                var spots=squad.Select(e=>e.transform.position).ToArray();yield return WaitGameSeconds(2f);
                for(int i=0;i<squad.Length;i++)Assert.Less(Vector3.Distance(spots[i],squad[i].transform.position),.6f,"The escort holds behind the tank, it never chases Nam.");
                Assert.Less(Off(c.Actor("Hùng").transform.position,c.HungCanal),.5f,"Hùng moves along the ditch.");
                Assert.IsFalse(c.Instruction.Contains("giây"),c.Instruction);
                // A round past a soldier's head makes him duck; a soldier ducking does not fire.
                var target=squad[0];var chest=target.transform.position+Vector3.up*1.3f;
                typeof(Map02ChapterDirector).GetMethod("SuppressAlong",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,new object[]{new Ray(chest+Vector3.right*20+Vector3.up*.8f,(chest+Vector3.up*.8f-(chest+Vector3.right*20+Vector3.up*.8f)).normalized)});
                Assert.IsTrue(target.Suppressed);
                // Nobody hit: Hùng still reaches the tracks by 90 s.
                c.State.coverClock=89f;c.State.taskProgress=89f/90f;float until=Time.time+5;while(Time.time<until&&c.CurrentScene==null)yield return null;
                Assert.AreEqual("N3_09",c.CurrentScene?.id);Assert.LessOrEqual(c.State.coverClock,91f);
                // Everyone down at once: Hùng goes faster, but not before 60 s.
                yield return Begin();
                foreach(var e in Squad())e.HitPoints.TakeDamage(1000,e.transform.position,m.Player.gameObject);
                c.State.coverClock=30;c.State.taskProgress=.3f;yield return WaitGameSeconds(2f);
                Assert.IsNull(c.CurrentScene);Assert.LessOrEqual(c.State.taskProgress,c.State.coverClock/60f+.001f);Assert.Greater(c.State.taskProgress,.3f+2f/90f,"With the escort down he crawls faster than the slowest pace.");
                c.State.coverClock=59.5f;c.State.taskProgress=59.5f/60f;float started=Time.time;until=Time.time+5;while(Time.time<until&&c.CurrentScene==null)yield return null;
                Assert.AreEqual("N3_09",c.CurrentScene?.id);Assert.GreaterOrEqual(Time.time-started,.45f,"Not before the 60 s mark.");
            }
            finally{c.Cancel();}
        }
        [UnityTest,Timeout(240000)]public IEnumerator TankAndBoatsAreWhereTheNightThreeStoryPutsThem()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");SetPreviewResolution(1920,1080);yield return new EnterPlayMode();
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var c=m.Chapter;var def=Map02ChapterDefinition.Load();var hp=m.Player.GetComponent<ShadowVale.Gameplay.Combat.Health>();
            var finish=typeof(Map02ChapterDirector).GetMethod("Finish",BindingFlags.NonPublic|BindingFlags.Instance);
            var end=typeof(Map02ChapterDirector).GetMethod("EndScene",BindingFlags.NonPublic|BindingFlags.Instance);
            void Load(string id){var snapshot=m.Capture();var s=new Map02ChapterState();foreach(var t in def.tasks){s.Complete(t.id);if(t.id==id)break;}snapshot.chapter=s;m.Restore(snapshot);}
            void Place(Vector3 p){var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=p;cc.enabled=true;Physics.SyncTransforms();}
            float Flat(Vector3 a,Vector3 b)=>Vector3.Distance(new Vector3(a.x,0,a.z),new Vector3(b.x,0,b.z));
            System.IO.Directory.CreateDirectory("Logs/GD4");
            try
            {
                // Lanes are far enough apart that holding one never starts the next.
                Assert.Greater(Flat(c.Sites["lane1"].position,c.Sites["lane2"].position),15);Assert.Greater(Flat(c.Sites["lane2"].position,c.Sites["lane3"].position),15);
                Load("N3_05B");yield return null;Assert.IsTrue(c.Tank==null||!c.Tank.activeSelf,"No tank before the third lane.");
                Assert.Less(Flat(c.LastBoat.position,c.WaterPoint("boatDock")),.5f,"Boat 1 has left and come back for the last trip.");
                finish.Invoke(c,new object[]{def.Find("N3_05C")});yield return null;
                Assert.AreEqual("N3_WOUND",c.CurrentScene?.id);Assert.AreEqual("breach",c.TankStage);Assert.IsTrue(c.Tank.activeSelf);
                Assert.Less(Flat(c.Tank.transform.position,c.Sites["tankBreach"].position),1);end.Invoke(c,null);
                Assert.Less(Flat(c.Actor("Anh Lực").transform.position,c.Sites["cover"].position),3,"Anh Lực is in the cover Nam crawls to.");
                // After the crawl the tank turns away into the village and drops out of sight.
                Load("N3_CRAWL");yield return null;Assert.AreEqual("leaving",c.TankStage);Assert.IsFalse(c.Tank.activeSelf);
                Assert.Less(Flat(c.Actor("Tâm").transform.position,c.Sites["tamCover"].position),2,"Tâm covers the fallen tree from the bamboo.");
                // Reaching the bank: the last boat is mid-river with the family, the tank comes out at the head of the dock road.
                Load("N3_06");yield return null;Assert.AreEqual("waiting",c.TankStage);Assert.IsFalse(c.Tank.activeSelf);
                Assert.Less(Flat(c.LastBoat.position,c.WaterPoint("boatMid")),.5f);
                foreach(var person in new[]{"Chị Lành","Ông Sáu","Bé Tí","Chú Bảy"})Assert.Less(Vector3.Distance(c.Actor(person).transform.position,c.LastBoat.position),3.5f,person+" is on the last boat.");
                Assert.Less(Flat(c.SecondBoat.position,c.WaterPoint("boatFar2")),.5f,"Boat 2 left at 60 %.");
                Place(c.Sites["ditch"].position+Vector3.forward*2);yield return null;yield return null;
                Assert.AreEqual("N3_07",c.CurrentScene?.id);float until=Time.time+6;while(Time.time<until&&c.TankStage!="blocking")yield return null;
                Assert.AreEqual("blocking",c.TankStage);Assert.Less(Flat(c.Tank.transform.position,c.Sites["tankDock"].position),1);
                yield return WaitGameSeconds(1.5f);ScreenCapture.CaptureScreenshot("Logs/GD4/n3_07-bank.png");yield return null;
                // Hùng's run from the bank to the tracks is short and walkable, not across the village.
                var run=new UnityEngine.AI.NavMeshPath();Assert.IsTrue(UnityEngine.AI.NavMesh.CalculatePath(c.Sites["ditch"].position,c.ChargePoint,UnityEngine.AI.NavMesh.AllAreas,run));
                float length=0;for(int i=1;i<run.corners.Length;i++)length+=Vector3.Distance(run.corners[i-1],run.corners[i]);
                Assert.AreEqual(UnityEngine.AI.NavMeshPathStatus.PathComplete,run.status);Assert.Less(length,30,"ditch → charge point");
                end.Invoke(c,null);
                // After the sacrifice the tank burns where it blocked the road; at dawn the village is back on its bank.
                Load("N3_09");end.Invoke(c,null);yield return null;Assert.AreEqual("destroyed",c.TankStage);Assert.Less(Flat(c.Tank.transform.position,c.Sites["tankDock"].position),1);
                Assert.AreEqual(6,c.State.phase);m.Restore(m.Capture());Assert.Less(Flat(c.LastBoat.position,c.WaterPoint("boatDock")),.5f);
                // An earlier save hands the boat back to the river physics.
                Load("D3_05");yield return null;Assert.IsTrue(c.LastBoat.GetComponent<RiverBuoyantBody>().enabled);Assert.IsNull(c.SecondBoat);
            }
            finally{c.Cancel();hp.CinematicInvulnerable=false;}
            yield return new ExitPlayMode();
        }
    }
}
