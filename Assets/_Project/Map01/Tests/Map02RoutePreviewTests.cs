using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class Map02RoutePreviewTests:ForestSceneTestBase
    {
        [UnityTest,Timeout(180000)] public IEnumerator PreviewStartsAtTheSelectedRouteAndCreatesItsOwnRetryPoint()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);
            var type=Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor",true);
            var apply=type.GetMethod("ApplyToMission",BindingFlags.Public|BindingFlags.Static);
            foreach(int phase in new[]{2,3})
            {
                apply.Invoke(null,new object[]{m,phase});yield return null;
                Assert.AreEqual(phase,m.Chapter.State.phase);Assert.IsTrue(m.HasCheckpoint);Assert.IsFalse(m.Paused||ForestMenu.Visible);
                Assert.AreEqual(phase==2?"D2_01":"N2_01",m.Chapter.TrackedTask.id);
                Assert.AreEqual(snapshotRifle(m),m.Bag.SlotItem(Map01Inventory.Slot.Primary),"Preview must seed an actual owned primary, not just a weapon visual.");
                Assert.AreEqual(phase==3,m.Chapter.State.Done("D2_07"));Assert.IsFalse(m.Chapter.State.Done(phase==2?"D2_01":"N2_01"));
<<<<<<< HEAD
                var site=m.Chapter.Sites[phase==2?"command":"bridge"].position; // Start beside the selected route, matching ApplyToMission.
=======
                var site=phase==2?m.Chapter.Sites["command"].position:m.Chapter.NightObservationSpawn;
>>>>>>> Tai
                Assert.Less(Vector3.Distance(m.Player.position,site),8);Assert.AreEqual(90,m.Inventory[m.RifleAmmo]);
                var snapshot=Map02Mission.Parse(JsonUtility.ToJson(m.Capture()));m.Restore(snapshot);
                Assert.AreEqual(phase,m.Chapter.State.phase);Assert.AreEqual(90,m.Chapter.State.storedRounds);
                Assert.IsTrue(m.Retry());Assert.AreEqual(phase,m.Chapter.State.phase);
                if(phase==2)
                {
                    var marker=Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId=="D2_01");
                    var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=marker.transform.position;cc.enabled=true;Physics.SyncTransforms();
                    Assert.IsTrue(m.Interact(marker));Assert.AreEqual("D2_01",m.Chapter.CurrentScene.id);
                    typeof(Map02ChapterDirector).GetMethod("EndScene",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(m.Chapter,null);
                }
            }
            yield return new ExitPlayMode();
        }
        static string snapshotRifle(Map02Mission m)=>m.Capture().rifleItemId;
    }
}
