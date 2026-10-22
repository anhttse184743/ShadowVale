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
    public sealed class Map02ChapterTests : ForestSceneTestBase
    {
        [Test] public void EveryTaskHasValidDependenciesUniqueIdentityAndNoOrphanedDialogue()
        {
            var d=Map02ChapterDefinition.Load();Assert.AreEqual(43,d.tasks.Length);
            Assert.AreEqual(d.tasks.Length,d.tasks.Select(t=>t.id).Distinct().Count());
            Assert.AreEqual(d.entries.Length,d.entries.Select(e=>e.id).Distinct().Count());
            foreach(var t in d.tasks){foreach(var id in t.requires)Assert.NotNull(d.Find(id),id);if(!string.IsNullOrEmpty(t.scene))Assert.Greater(d.Lines(t.scene).Length,0,t.scene);}
            Assert.IsTrue(d.Lines("promise").Any(l=>l.text.Contains("U tôi là u cậu")));
            Assert.IsTrue(d.Lines("coordinates").Any(l=>l.text.Contains("pháo không bắn vào đây")));
            Assert.IsTrue(d.Lines("dawn").Any(l=>l.text.Contains("Tôi hứa với nó rồi")));
        }
        [Test] public void EntireChapterHasSevenPhasesAndExactlyOneSacrifice()
        {
            var state=new Map02ChapterState();Assert.IsFalse(state.Complete("N3_09"));
            foreach(var task in Map02ChapterDefinition.Load().tasks){Assert.IsTrue(state.Available(task),task.id);Assert.IsTrue(state.Complete(task.id),task.id);Assert.IsFalse(state.Complete(task.id));state.Validate();}
            Assert.AreEqual(6,state.phase);Assert.IsTrue(state.finished&&state.hungDead&&state.tankDestroyed&&state.artilleryFired&&state.radioTransferred);Assert.AreEqual(100,state.evacuation);
        }
        [Test] public void DayOneAllowsFreeOrderButDoesNotOpenNightBeforeReport()
        {
            var p=new Map02ChapterState();p.Complete("D1_01");p.Complete("D1_02");Assert.IsTrue(p.Complete("D1_05"));Assert.IsFalse(p.Complete("D1_06"));p.Complete("D1_04");p.Complete("D1_03");Assert.AreEqual(0,p.phase);Assert.IsTrue(p.Complete("D1_06"));Assert.AreEqual(1,p.phase);
        }
        [Test]public void SaveRoundTripKeepsCargoCountsNightWarningAmmoAndEnding()
        {
            var state=Through("N2_02");state.carry="D3_01";state.workCount=1;state.storedRounds=17;state.patrolWarnings=1;
            var load=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(state));load.Validate();Assert.AreEqual(17,load.storedRounds);Assert.AreEqual(1,load.patrolWarnings);Assert.AreEqual(state.completed,load.completed);
            load=Through("N3_09");Assert.IsTrue(load.hungDead);Assert.IsFalse(load.Complete("N3_09"));Assert.IsTrue(load.Complete("DAWN"));
        }
        [Test]public void InvalidAndOutOfOrderSavesAreRejected()
        {
            var p=new Map02ChapterState();p.completed.Add("N3_09");Assert.Throws<InvalidDataException>(()=>p.Validate());
            p=new Map02ChapterState{hungDead=true};Assert.Throws<InvalidDataException>(()=>p.Validate());
            p=new Map02ChapterState();p.completed.Add("unknown_task");Assert.Throws<InvalidDataException>(()=>p.Validate());
        }
        static Map02ChapterState Through(string id){var s=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){Assert.IsTrue(s.Complete(t.id));if(t.id==id)break;}return s;}
        [UnityTest,Timeout(240000)]public IEnumerator LiveMapHasReachableObjectivesAndSkipSaveRestorePreserveChapter()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m!=null&&m.Ready&&m.Chapter!=null)break;var a=Object.FindFirstObjectByType<Map02Arrival>();if(a!=null&&a.IsPlaying){var method=a.GetType().GetMethod("Skip",BindingFlags.Public|BindingFlags.Instance);method?.Invoke(a,null);}yield return null;}
            Assert.NotNull(m);Assert.NotNull(m.Chapter);var c=m.Chapter;Assert.Greater(m.AvailableObjectiveCount,0);
            Directory.CreateDirectory("Logs/Map02Chapter");SetPreviewResolution(1920,1080);
            var report=new System.Text.StringBuilder();var origin=c.Sites["command"].position;
            foreach(var pair in c.Sites){var path=new NavMeshPath();bool valid=NavMesh.CalculatePath(origin,pair.Value.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete;report.AppendLine(pair.Key+" "+pair.Value.position+" "+valid);Assert.IsTrue(valid,pair.Key);}
            File.WriteAllText("Logs/Map02Chapter/navigation.txt",report.ToString());
            var target=Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId=="D1_01");
            if(!c.State.dayOne.arrivalSeen){if(!c.Busy)typeof(Map02ChapterDirector).GetMethod("TickDay",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,new object[]{0f});typeof(Map02ChapterDirector).GetMethod("EndScene",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,null);Assert.True(c.State.dayOne.arrivalSeen);Assert.False(c.State.Done("D1_01"));}
            Move(m,target.transform.position);Assert.IsTrue(m.Interact(target));Assert.IsTrue(c.Busy);Assert.IsFalse(m.CanSave());
            typeof(Map02ChapterDirector).GetMethod("EndScene",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(c,null);yield return null;
            Assert.IsTrue(c.State.Done("D1_01"));Assert.IsFalse(c.Busy);Assert.IsTrue(m.HasCheckpoint);
            var snapshot=m.Capture();m.Restore(snapshot);Assert.IsTrue(c.State.Done("D1_01"));
            ScreenCapture.CaptureScreenshot("Logs/Map02Chapter/day1.png");yield return null;
            // Exercise checkpoint states in every period, and both sides of the fixed ending.
            foreach(var id in new[]{"D1_06","D2_07","D3_06","N3_07","N3_09","DAWN"})
            {
                snapshot=m.Capture();snapshot.chapter=Through(id);m.Restore(snapshot);yield return WaitGameSeconds(2f);
                Assert.AreEqual(snapshot.chapter.phase,c.State.phase);Assert.AreEqual(snapshot.chapter.hungDead,c.State.hungDead);Assert.AreEqual(snapshot.magazine,m.Player.GetComponent<ShadowVale.Gameplay.Combat.PlayerCombat>().RoundsInMagazine);
                if(c.State.phase==1||c.State.phase==3||c.State.phase==5)
                {Assert.IsTrue(RenderSettings.skybox.HasProperty("_Exposure"));Assert.Less(RenderSettings.skybox.GetFloat("_Exposure"),c.State.phase==1?.25f:.15f);}
                foreach(var fx in c.GetComponentsInChildren<ParticleSystemRenderer>(true))
                {Assert.NotNull(fx.sharedMaterial,fx.name);Assert.IsTrue(fx.sharedMaterial.shader.isSupported,fx.name);Assert.IsTrue(fx.sharedMaterial.shader.name.StartsWith("Universal Render Pipeline/Particles/"),fx.name);}
                ScreenCapture.CaptureScreenshot("Logs/Map02Chapter/"+id+".png");yield return null;
            }
            Assert.IsTrue(c.State.finished);Assert.IsTrue(c.State.hungDead);Assert.IsTrue(c.State.tankDestroyed);
            yield return new ExitPlayMode();
        }
        static void Move(Map02Mission m,Vector3 p){var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=p;cc.enabled=true;Physics.SyncTransforms();}
    }
}
