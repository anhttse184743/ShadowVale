using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02StartSaveExportTests:ForestSceneTestBase
    {
        [UnityTest,Timeout(240000)] public IEnumerator CreateAndReloadDayOneStartCheckpoint()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float until=Time.realtimeSinceStartup+90;
            while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);m.Chapter.Cancel();m.Chapter.enabled=false;
            var snapshot=m.Capture();snapshot.chapter=new Map02ChapterState();snapshot.chapter.dayOne.Initialize(UnityEngine.Random.Range(1,int.MaxValue));
            snapshot.world=new Map02WorldState();snapshot.playSeconds=0;snapshot.checkpoint=null;
            Assert.True(NavMesh.SamplePosition(m.Chapter.Sites["arrival"].position+Vector3.back*1.6f,out var start,3,NavMesh.AllAreas),"Start must be on a real walkable bank.");
            snapshot.position=start.position;snapshot.yaw=180;
            var initial=JsonUtility.ToJson(snapshot);snapshot.checkpoint=initial;
            var entry=new ForestSaveSlots.Entry{sceneName="Map 2",savedAt=DateTime.UtcNow.ToString("o"),playSeconds=0,location="MAP 2 — NGÀY 1 · CÓ CHỖ NGHỈ CHÂN",checkpoint=JsonUtility.ToJson(snapshot)};
            ForestSaveSlots.Write(1,entry);var parsed=ForestSaveSlots.Read(1);Assert.AreEqual("Map 2",parsed.sceneName);m.Restore(Map02Mission.Parse(parsed.checkpoint));
            Assert.AreEqual(0,m.Chapter.State.phase);Assert.False(m.Chapter.State.Done("D1_01"));Assert.False(m.Chapter.State.dayOne.arrivalSeen);
            Assert.Less(Vector3.Distance(m.Player.position,m.Chapter.Actor("Hùng").transform.position),4,"Starting party must be together.");
            Assert.Less(Vector3.Distance(m.Player.position,m.Chapter.Actor("Anh Thành").transform.position),4);
            Assert.Greater(m.AvailableObjectiveCount,0);
            Directory.CreateDirectory("Logs/Map01RigDialogue_20261022");File.WriteAllText("Logs/Map01RigDialogue_20261022/map2-start-save.json",JsonUtility.ToJson(entry));
            // Exercise the same scene-loading entry point used by the Load menu.
            m.Chapter.enabled=true;Map02Mission.BeginGame(parsed);yield return null;
            until=Time.realtimeSinceStartup+90;
            do{m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;yield return null;}while(Time.realtimeSinceStartup<until);
            Assert.NotNull(m?.Chapter);Assert.AreEqual(0,m.Chapter.State.phase);Assert.False(m.Chapter.State.Done("D1_01"));Assert.Less(Vector3.Distance(m.Player.position,start.position),.2f);
            yield return new ExitPlayMode();
        }
    }
}
