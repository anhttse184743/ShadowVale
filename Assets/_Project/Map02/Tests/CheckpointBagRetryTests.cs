using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    /// <summary>Dying and going back to the checkpoint gives back exactly the bag the checkpoint holds: what was picked up
    /// after it is gone, what was picked up before it is there (Khoa, 26/11/2026).</summary>
    public sealed class CheckpointBagRetryTests:ForestSceneTestBase
    {
        static string Items(Map01Inventory bag)=>string.Join(";",bag.CaptureItems().OrderBy(i=>i.item_id).Select(i=>i.item_id+"="+i.count))+" stones="+bag.Stones;

        [UnityTest,Timeout(300000)]public IEnumerator Map2CheckpointTakenMidReloadIsKeptAndRetriedExactly()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return Map2InPlay();
            yield return new ExitPlayMode();
        }
        static string Kit(Map02Mission m){var s=m.Capture();return "["+string.Join(";",s.inventory.OrderBy(i=>i.id).Select(i=>i.id+"="+i.count))+"] bag["+Items(m.Bag)+"] magazine "+s.magazine;}
        static IEnumerator Map2InPlay()
        {
            Map02Mission m=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);
            System.Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor",true).GetMethod("ApplyAfter").Invoke(null,new object[]{m,"D3_06"});
            float until=Time.time+2;while(Time.time<until)yield return null;
            var combat=m.Player.GetComponent<PlayerCombat>();combat.Equip(WeaponKind.Rifle);combat.RestoreMagazine(WeaponKind.Rifle,5);yield return null;
            // Rounds off the fallen, then the wave's task closes while he is reloading.
            m.Inventory[m.RifleAmmo]=(m.Inventory.TryGetValue(m.RifleAmmo,out int n)?n:0)+25;yield return null;
            Assert.IsTrue(combat.TryReload(),"A reload starts.");Assert.IsTrue(combat.IsReloading);
            m.StoreCheckpoint();
            Assert.IsTrue(m.CheckpointPending,"Mid-reload the checkpoint waits instead of being dropped.");
            until=Time.time+10;while(Time.time<until&&combat.IsReloading)yield return null;
            yield return null;yield return null;
            Assert.IsFalse(m.CheckpointPending,"…and is taken once the reload is done.");
            string atCheckpoint=Kit(m);
            // After the checkpoint: more loot, then he falls.
            m.Bag.Receive("medkit_small",2);m.Bag.Receive("cloth",3);m.Inventory[m.RifleAmmo]+=40;yield return null;
            var hp=m.Player.GetComponent<Health>();hp.CinematicInvulnerable=false;hp.TakeDamage(100000,m.Player.position,null);
            until=Time.time+2;while(Time.time<until&&!m.World.story.failed)yield return null;
            Assert.IsTrue(m.World.story.failed,"Nam falls.");
            Assert.IsTrue(m.Retry());until=Time.time+2;while(Time.time<until)yield return null;
            Assert.AreEqual(atCheckpoint,Kit(m),"Back at the checkpoint, the bag is the checkpoint's.");
        }

        [Serializable] sealed class SavedBag{public ForestIngredient[] items;public int stones;}
        static string Items(SavedBag bag)=>string.Join(";",bag.items.OrderBy(i=>i.item_id).Select(i=>i.item_id+"="+i.count))+" stones="+bag.stones;
        static readonly FieldInfo Storage=typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic);
        [UnityTest,Timeout(300000)]public IEnumerator Map1FallenDuringTheOperationRetriesAndContinuesWithTheCheckpointBag()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            var previous=Storage.GetValue(null);Storage.SetValue(null,Path.Combine(Application.temporaryCachePath,"checkpoint-bag-"+Guid.NewGuid().ToString("N")));
            try
            {
                var m=Object.FindFirstObjectByType<Map01Mission>();m.Cinematic=false;foreach(var e in m.Enemies)e.enabled=false;
                var bag=m.GetComponent<Map01Inventory>();var story=m.GetComponent<Map01Story>();m.GetComponent<Map01Quest>().RestoreStage(Map01Quest.ScoutStage);
                var progress=story.Capture();progress.rescueAccepted=true;story.Restore(progress,Map01Quest.ScoutStage);
                for(int i=0;i<5;i++)yield return null;
                bag.Add("ammo_rifle",15);   // picked up before the checkpoint: kept
                var save=m.GetComponent<Map01SaveSystem>();save.MarkOperationCheckpoint();Assert.IsTrue(save.HasOperationCheckpoint);
                string atCheckpoint=Items(bag);
                bag.Add("medkit_small",2);bag.Add("cloth",3);bag.Add("ammo_rifle",25);   // after it: gone
                m.ModernHealth.CinematicInvulnerable=false;m.ModernHealth.TakeDamage(100000,m.player.position,null);for(int i=0;i<5;i++)yield return null;
                Assert.LessOrEqual(m.PlayerHealth,0);
                // Quit while fallen: the autosave is that same checkpoint, not the start of the mission.
                Assert.IsTrue(save.AutoSaveOnExit(out var error),error);
                Assert.AreEqual(atCheckpoint,Items(JsonUtility.FromJson<SavedBag>(ForestSaveSlots.Read(ForestSaveSlots.AutoSlot).checkpoint)),"Continue after quitting dead gives the checkpoint's bag.");
                // "Thử lại": the same.
                typeof(Map01Mission).GetMethod("Restart",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(m,null);
                yield return null;double until=Time.realtimeSinceStartupAsDouble+30;
                while((Map01SaveSystem.IsRestoring||Object.FindFirstObjectByType<Map01Mission>()==m)&&Time.realtimeSinceStartupAsDouble<until)yield return null;
                for(int i=0;i<30;i++)yield return null;
                m=Object.FindFirstObjectByType<Map01Mission>();
                Assert.AreEqual(atCheckpoint,Items(m.GetComponent<Map01Inventory>()),"Retry gives the checkpoint's bag.");
            }
            finally{Storage.SetValue(null,previous);}
            yield return new ExitPlayMode();
        }
    }
}
