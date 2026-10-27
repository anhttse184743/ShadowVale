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
        [UnityTest,Timeout(180000)]public IEnumerator M16ChapterStockUsesMatchingAmmoAndSurvivesSaveLoad()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float until=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);var snapshot=m.Capture();snapshot.rifleItemId=Map01M16.Id;
            snapshot.chapter=new Map02ChapterState();foreach(var t in Map02ChapterDefinition.Load().tasks){snapshot.chapter.Complete(t.id);if(t.id=="N3_00")break;}
            snapshot.chapter.storedRounds=90;snapshot.chapter.gearStored=true;m.Restore(snapshot);
            m.Inventory[Map01M16.Ammo]=17;m.Inventory["ammo_rifle"]=4;
            Assert.AreEqual(Map01M16.Ammo,m.RifleAmmo);
            var marker=Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId=="N3_01");
            var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=marker.transform.position;cc.enabled=true;Physics.SyncTransforms();
            Assert.IsTrue(m.Interact(marker));Assert.AreEqual(107,m.Inventory[Map01M16.Ammo]);Assert.AreEqual(4,m.Inventory["ammo_rifle"]);
            snapshot=m.Capture();Assert.AreEqual(Map01M16.Id,snapshot.rifleItemId);m.Restore(snapshot);
            Assert.AreEqual(Map01M16.Ammo,m.RifleAmmo);Assert.AreEqual(107,m.Inventory[Map01M16.Ammo]);
            yield return new ExitPlayMode();
        }
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
            Assert.IsFalse(m.Chapter.State.Done("D1_04"));Assert.AreEqual(0,m.Chapter.State.storedRounds);
            Move(m,crate.transform.position);Assert.True(m.Interact(crate));
            bool Solve(Map02DayOneState state,int i=0){if(i==3)return state.Packed;for(int r=0;r<2;r++)for(int x=2;x<6;x++)for(int y=0;y<4;y++){if(!state.PlaceCrate(i,x,y,r==1,out _))continue;if(Solve(state,i+1))return true;state.crates[i]=new Map02DayOneState.Placement();}return false;}
            Assert.True(Solve(m.Chapter.State.dayOne));typeof(Map02ChapterDirector).GetMethod("CompletePacking",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(m.Chapter,null);
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
