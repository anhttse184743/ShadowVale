using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using ShadowVale.Gameplay.Combat;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02RuntimeTests:ForestSceneTestBase
    {
        [UnityTest,Timeout(240000)]public IEnumerator ObjectCargoFlowSharesPauseMenuAndRestoresExactInventory()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m!=null&&m.Ready&&m.Chapter!=null)break;var arrival=Object.FindFirstObjectByType<Map02Arrival>();arrival?.GetType().GetMethod("Skip",BindingFlags.Public|BindingFlags.Instance)?.Invoke(arrival,null);yield return null;}
            Assert.NotNull(m?.Chapter);Assert.Greater(m.AvailableObjectiveCount,0);Assert.IsTrue(m.HasCheckpoint);
            var h=m.Player.GetComponent<Health>();var gun=m.Player.GetComponent<PlayerCombat>();h.RestoreHealth(61);gun.RestoreMagazine(9);m.Inventory["ammo_rifle"]=13;
            var snapshot=m.Capture();var phase=new Map02ChapterState();phase.Complete("D1_01");phase.Complete("D1_02");snapshot.chapter=phase;m.Restore(snapshot);
            Map02ObjectiveObject Mark(string id)=>Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId==id);
            var crate=Mark("D1_04");Move(m,crate.transform.position);Assert.IsTrue(m.Interact(crate));Assert.AreEqual("D1_04",m.Chapter.State.carry);
            Move(m,crate.transform.position);Assert.IsTrue(m.Interact(crate));Assert.AreEqual(1,m.Chapter.State.Count("D1_04"));
            snapshot=m.Capture();m.Restore(snapshot);Assert.AreEqual(1,m.Chapter.State.Count("D1_04"));
            // Alternate one freely ordered task, then continue the remaining crate trips.
            var medicine=Mark("D1_03");Move(m,medicine.transform.position);Assert.IsTrue(m.Interact(medicine));
            typeof(Map02ChapterDirector).GetMethod("EndScene",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(m.Chapter,null);
            for(int i=0;i<2;i++){Move(m,crate.transform.position);Assert.IsTrue(m.Interact(crate));Move(m,crate.transform.position);Assert.IsTrue(m.Interact(crate));}
            Assert.IsTrue(m.Chapter.State.Done("D1_04"));Assert.AreEqual(m.Chapter.Layout.supplyCrateRounds*3,m.Chapter.State.storedRounds);Assert.AreEqual(13,m.Inventory["ammo_rifle"]);Assert.AreEqual(61,h.Current);Assert.AreEqual(9,gun.RoundsInMagazine);
            var storage=typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic);var old=storage.GetValue(null);
            try
            {
                storage.SetValue(null,Path.GetFullPath("Logs/Map02Chapter/Slots/"+Guid.NewGuid().ToString("N")));
                m.SetPaused(true);Assert.IsTrue(m.SaveSlot(1,out var error),error);var entry=ForestSaveSlots.Read(1);Assert.AreEqual("Map 2",entry.sceneName);
                var loaded=Map02Mission.Parse(entry.checkpoint);m.SetPaused(false);h.RestoreHealth(12);gun.RestoreMagazine(1);m.Restore(loaded);
                Assert.AreEqual(61,h.Current);Assert.AreEqual(9,gun.RoundsInMagazine);Assert.AreEqual(m.Chapter.Layout.supplyCrateRounds*3,m.Chapter.State.storedRounds);Assert.IsTrue(m.Chapter.State.Done("D1_03"));
                Assert.AreEqual(1,Object.FindObjectsByType<ForestMenu>(FindObjectsSortMode.None).Length);Assert.AreEqual(43,Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Count(t=>!string.IsNullOrEmpty(t.chapterId)));
            }
            finally{storage.SetValue(null,old);m.SetPaused(false);}
            yield return new ExitPlayMode();
        }
        static void Move(Map02Mission m,Vector3 p){var c=m.Player.GetComponent<CharacterController>();c.enabled=false;m.Player.position=p;c.enabled=true;Physics.SyncTransforms();}
    }
}
