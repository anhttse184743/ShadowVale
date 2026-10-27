using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShadowVale.AI.Coordination;
using ShadowVale.Core.Events;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    /// <summary>"Thử lại checkpoint" after dying on Night 3 puts back what Nam had at the checkpoint: the bag the HUD shows,
    /// health and stamina.</summary>
    public sealed class Map02CheckpointRetryTests:ForestSceneTestBase
    {
        [UnityTest,Timeout(300000)]public IEnumerator RetryAfterDyingRestoresTheBagHealthAndStaminaOfTheCheckpoint()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return InPlayMode();
            yield return new ExitPlayMode();
        }
        [UnityTest,Timeout(300000)]public IEnumerator NightThreeRiflemenTakeCoverAndFightWithTheSharedBrain()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return BrainInPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator BrainInPlayMode()
        {
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            var c=m.Chapter;m.Player.GetComponent<Health>().CinematicInvulnerable=true;
            var snap=m.Capture();var s=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){if(t.id=="N3_04")break;s.Complete(t.id);}
            snap.chapter=s;m.Restore(snap);float until=Time.time+1.5f;while(Time.time<until)yield return null;
            var lane=Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(o=>o.chapterId=="N3_04").transform.position;
            var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=lane;cc.enabled=true;Physics.SyncTransforms();
            var seen=new System.Collections.Generic.HashSet<ShadowVale.AI.Fsm.EnemyStateId>();until=Time.time+25;
            var plans=new System.Collections.Generic.List<PlanAppliedEvent>();System.Action<PlanAppliedEvent> onPlan=plans.Add;EventBus.Subscribe(onPlan);
            var allies=new[]{"Anh Lực","Tâm","Anh Thành"}.Select(r=>c.Actor(r)).Where(a=>a!=null&&a.visual!=null&&a.visual.gameObject.activeInHierarchy).ToArray();
            var start=allies.ToDictionary(a=>a,a=>a.transform.position);float moved=0;
            try
            {
                while(Time.time<until)
                {
                    yield return null;foreach(var e in c.Enemies)if(e!=null&&!e.Dead&&e.group=="N3_04"&&e.BrainState.HasValue)seen.Add(e.BrainState.Value);
                    foreach(var a in allies)moved=Mathf.Max(moved,Vector3.Distance(a.transform.position,start[a]));
                }
            }
            finally{EventBus.Unsubscribe(onPlan);}
            Assert.IsTrue(plans.Any(p=>p.Group.Contains("N3_04")),"The squad coordinator plans the wave with the solver, as on Map 1 ("+string.Join(", ",plans.Select(p=>p.Group+":"+p.Variant))+").");
            foreach(var a in allies)Assert.IsTrue(c.AllyInCombatPose(a.role),a.role+" fights with the rifleman's animations.");
            if(allies.Length>0)Assert.Greater(moved,.7f,"Nam's comrades shift between firing spots.");
            var wave=c.Enemies.Where(e=>e!=null&&e.group=="N3_04").ToArray();
            Assert.Greater(wave.Length,0,"The lane's wave comes in.");
            Assert.IsTrue(wave.All(e=>e.BrainState.HasValue),"Every rifleman of the wave runs on the shared brain.");
            Assert.IsTrue(seen.Contains(ShadowVale.AI.Fsm.EnemyStateId.TakeCover)||seen.Contains(ShadowVale.AI.Fsm.EnemyStateId.Engage),"They make for cover or fight: "+string.Join(",",seen));
            Assert.Greater(c.AllyShotsFired,0,"Nam's comrades fire on the soldiers they can see.");
            Assert.IsFalse(seen.Contains(ShadowVale.AI.Fsm.EnemyStateId.Patrol)&&!seen.Contains(ShadowVale.AI.Fsm.EnemyStateId.Engage)&&!seen.Contains(ShadowVale.AI.Fsm.EnemyStateId.TakeCover),"A wave does not stroll about.");
        }
        [UnityTest,Timeout(300000)]public IEnumerator DayTwoAmbushFightsUnderTheCoordinatorAndThePatrolKeepsItsStealth()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return DayTwoInPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator DayTwoInPlayMode()
        {
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            var c=m.Chapter;m.Player.GetComponent<Health>().CinematicInvulnerable=true;
            var snap=m.Capture();var s=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){if(t.id=="D2_04")break;s.Complete(t.id);}
            snap.chapter=s;m.Restore(snap);float until=Time.time+1.5f;while(Time.time<until)yield return null;
            Assert.AreEqual(2,c.State.phase);
            var spawn=typeof(Map02ChapterDirector).GetMethod("SpawnEnemy",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var nam=m.Player.position;var spots=new System.Collections.Generic.List<Vector3>();
            for(int i=0;i<16&&spots.Count<4;i++){var p=nam+Quaternion.Euler(0,i*22.5f,0)*Vector3.forward*16;if(UnityEngine.AI.NavMesh.SamplePosition(p,out var hit,3,UnityEngine.AI.NavMesh.AllAreas))spots.Add(hit.position);}
            Assert.GreaterOrEqual(spots.Count,4);
            for(int i=0;i<3;i++)spawn.Invoke(c,new object[]{"test_ambush_"+i,Map02ChapterDirector.ScoutAmbushGroup,spots[i],100f,true,false});
            spawn.Invoke(c,new object[]{"test_patrol_0","scout_patrol",spots[3],100f,false,false});
            var plans=new System.Collections.Generic.List<PlanAppliedEvent>();System.Action<PlanAppliedEvent> onPlan=plans.Add;EventBus.Subscribe(onPlan);
            try{until=Time.time+8;while(Time.time<until)yield return null;}
            finally{EventBus.Unsubscribe(onPlan);}
            var ambush=c.Enemies.Where(e=>e!=null&&e.group==Map02ChapterDirector.ScoutAmbushGroup).ToArray();var patrol=c.Enemies.First(e=>e!=null&&e.id=="test_patrol_0");
            Assert.IsTrue(ambush.All(e=>e.BrainState.HasValue),"The ambush fights on the shared brain.");
            Assert.IsTrue(plans.Any(p=>p.Group.Contains(Map02ChapterDirector.ScoutAmbushGroup)),"…and the squad coordinator plans it with the solver ("+string.Join(", ",plans.Select(p=>p.Group))+").");
            if(!patrol.Alerted)Assert.IsFalse(patrol.BrainState.HasValue,"A patrol that has not found Nam keeps its stealth rules.");
        }
        [UnityTest,Timeout(120000)]public IEnumerator AWallTakesTheRoundsAndOpenGroundDoesNot()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            yield return WallInPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator WallInPlayMode()
        {
            var nam=GameObject.CreatePrimitive(PrimitiveType.Capsule);nam.name="Nam (cover test)";nam.transform.position=new Vector3(0,1,10);var hp=nam.AddComponent<Health>();
            var go=new GameObject("Rifleman (cover test)");var rifleman=go.AddComponent<Map02ChapterEnemy>();rifleman.enabled=false;
            yield return null;
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            var shoot=typeof(Map02ChapterEnemy).GetMethod("Shoot",flags);typeof(Map02ChapterEnemy).GetField("sightSince",flags).SetValue(rifleman,-10f);
            Vector3 from=new Vector3(0,1.45f,0),to=nam.transform.position;
            void Volley(){for(int i=0;i<200;i++)shoot.Invoke(rifleman,new object[]{nam.transform,from,to,Vector3.Distance(from,to)});}
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,1,5);wall.transform.localScale=new Vector3(4,3,.3f);Physics.SyncTransforms();
            float before=hp.Current;Volley();Assert.AreEqual(before,hp.Current,"Behind a wall no round reaches Nam.");
            Object.Destroy(wall);yield return null;Physics.SyncTransforms();
            Volley();Assert.Less(hp.Current,before,"In the open, some rounds hit.");
        }
        [Test]public void RiflemenHitLessFarOffAndNeverWithTheFirstShot()
        {
            var nam=new GameObject("Nam (aim test)");nam.AddComponent<CharacterController>();nam.AddComponent<ShadowVale.Gameplay.Player.PlayerController>();
            var go=new GameObject("Rifleman (aim test)");var rifleman=go.AddComponent<Map02ChapterEnemy>();rifleman.enabled=false;
            try
            {
                var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                var hits=typeof(Map02ChapterEnemy).GetMethod("Hits",flags);var since=typeof(Map02ChapterEnemy).GetField("sightSince",flags);
                float Rate(float distance){int n=0;for(int i=0;i<4000;i++)if((bool)hits.Invoke(rifleman,new object[]{nam.transform,distance}))n++;return n/4000f;}
                since.SetValue(rifleman,-10f);
                float near=Rate(5),mid=Rate(15),far=Rate(30);
                Assert.AreEqual(.75f,near,.04f,"Close up, most shots hit.");Assert.AreEqual(.52f,mid,.04f);Assert.AreEqual(.21f,far,.04f,"Far off, most miss.");
                since.SetValue(rifleman,Time.time);Assert.AreEqual(0,Rate(5),"The shot as he first sights Nam is a warning.");
            }
            finally{Object.DestroyImmediate(go);Object.DestroyImmediate(nam);}
        }
        static int Held(Map02Mission m,string id)=>m.Inventory.TryGetValue(id,out var n)?n:0;
        static string Kit(Map02Mission m)=>$"ammo {Held(m,m.RifleAmmo)} (bag {m.Bag.Count(m.RifleAmmo)}), medkits {Held(m,"medkit_small")} (bag {m.Bag.Count("medkit_small")}), health {m.Player.GetComponent<Health>().Current:0}, stamina {m.Stamina:0}, failed {m.World.story.failed}";
        static IEnumerator InPlayMode()
        {
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var log=new System.Collections.Generic.List<string>();
            // The Night 3 test run: Nam beside the shift change with the test kit, checkpoint stored there.
            System.Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor",true).GetMethod("ApplyAfter").Invoke(null,new object[]{m,"D3_06"});
            float until=Time.time+2;while(Time.time<until)yield return null;
            string atCheckpoint=Kit(m);log.Add("checkpoint: "+atCheckpoint);
            // Spend: three medkits and a magazine's worth, run the stamina down, take hits, then fall.
            m.Bag.Spend("medkit_small",3);m.Bag.Spend(m.RifleAmmo,60);yield return null;
            var hp=m.Player.GetComponent<Health>();hp.CinematicInvulnerable=false;hp.TakeDamage(70,m.Player.position,null);yield return null;
            typeof(Map02Mission).GetProperty("Stamina").SetValue(m,0f);m.StoreCheckpoint();atCheckpoint=Kit(m);log.Add("checkpoint while spent and winded: "+atCheckpoint);
            log.Add("spent: "+Kit(m));
            hp.TakeDamage(100000,m.Player.position,null);until=Time.time+1.5f;while(Time.time<until&&!m.World.story.failed)yield return null;
            log.Add("dead: "+Kit(m));Assert.IsTrue(m.World.story.failed,"Nam falls.");
            Assert.IsTrue(m.Retry(),"There is a checkpoint to go back to.");until=Time.time+2;while(Time.time<until)yield return null;
            string after=Kit(m);log.Add("retried: "+after);
            Directory.CreateDirectory("Logs/Map02Chapter");File.WriteAllLines("Logs/Map02Chapter/CheckpointRetry.txt",log);
            Assert.AreEqual(435,Held(m,m.RifleAmmo),"The rounds of the checkpoint.");Assert.AreEqual(7,Held(m,"medkit_small"),"The medkits of the checkpoint.");
            Assert.AreEqual(m.Bag.Count("medkit_small"),Held(m,"medkit_small"),"The bag the HUD shows agrees.");
            Assert.AreEqual(m.CharacterSettings.stamina,m.Stamina,1,"Fresh legs after a retry.");
            Assert.GreaterOrEqual(m.Player.GetComponent<Health>().Current,m.Player.GetComponent<Health>().Max*.5f-.01f,"At least half health after a retry.");
        }
    }
}
