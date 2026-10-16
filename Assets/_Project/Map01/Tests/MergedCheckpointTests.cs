using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using ShadowVale.Gameplay.Skills;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class MergedCheckpointTests:ForestSceneTestBase
    {
        static readonly FieldInfo Storage=typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic);
        static int Cell(Map01Inventory bag,string id)=>Enumerable.Range(0,Map01Inventory.BagSize).First(i=>bag.Cell(i)?.id==id);
        [Test]public void EditorIsolationStillProtectsTheDefaultSlotsWhenAFixtureClearsItsOverride()
        {
            var previous=Storage.GetValue(null);var key=ForestSaveSlots.EditorTestRootKey;var previousTest=SessionState.GetString(key,"");
            var isolated=Path.Combine(Application.temporaryCachePath,"save-isolation-"+Guid.NewGuid().ToString("N"));
            try{SessionState.SetString(key,isolated);Storage.SetValue(null,null);Assert.AreEqual(Path.Combine(isolated,"shadowvale-slot-4.json"),ForestSaveSlots.PathFor(ForestSaveSlots.AutoSlot));
                var explicitRoot=Path.Combine(isolated,"fixture");Storage.SetValue(null,explicitRoot);Assert.AreEqual(Path.Combine(explicitRoot,"shadowvale-slot-4.json"),ForestSaveSlots.PathFor(ForestSaveSlots.AutoSlot));
                Storage.SetValue(null,null);Assert.AreEqual(Path.Combine(isolated,"shadowvale-slot-4.json"),ForestSaveSlots.PathFor(ForestSaveSlots.AutoSlot));
            }finally{Storage.SetValue(null,previous);if(string.IsNullOrEmpty(previousTest))SessionState.EraseString(key);else SessionState.SetString(key,previousTest);}
        }
        [UnityTest]public IEnumerator OneCheckpointKeepsBagDropsStorySkillsAndExplorationTogether()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            var previous=Storage.GetValue(null);var root=Path.Combine(Application.temporaryCachePath,"merged-checkpoint-"+Guid.NewGuid().ToString("N"));Storage.SetValue(null,root);
            try{
                var m=Object.FindFirstObjectByType<Map01Mission>();m.Cinematic=false;foreach(var e in m.Enemies)e.enabled=false;m.ModernHealth.CinematicInvulnerable=true;
                var bag=m.GetComponent<Map01Inventory>();var story=m.GetComponent<Map01Story>();var quest=m.GetComponent<Map01Quest>();quest.RestoreStage(3);
                var progress=story.Capture();progress.rescueAccepted=true;progress.keyCollected=true;progress.doorUnlocked=false;progress.lockHistory=new[]{new Map01LockGuess{}};story.Restore(progress,3);
                bag.Add(Map01Story.KeyItem,1);bag.Add(Map01Story.CargoItem,1);
                Assert.IsFalse(bag.Drop(Cell(bag,Map01Story.KeyItem),1));Assert.IsFalse(bag.Drop(Cell(bag,Map01Story.CargoItem),1));
                bag.Add("cloth",6);Assert.IsTrue(bag.Drop(Cell(bag,"cloth"),4));Assert.IsTrue(bag.Move(Cell(bag,"cloth"),Map01Inventory.BagSize-1));
                bag.Add("smoke_bomb",2);bag.SelectThrowable("smoke_bomb");
                m.Skills.Award(SkillKind.Engineering,2);m.GetComponent<Map01Minimap>().Reveal(new Vector3(60,6,-20));
                int engineering=m.Skills.Xp(SkillKind.Engineering);var save=m.GetComponent<Map01SaveSystem>();Assert.IsTrue(save.SaveSlot(1,out var error,true),error);
                Map01SaveSystem.BeginGame(1);yield return null;
                double until=Time.realtimeSinceStartupAsDouble+20;while(Map01SaveSystem.IsRestoring&&Time.realtimeSinceStartupAsDouble<until)yield return null;Assert.IsFalse(Map01SaveSystem.IsRestoring);
                m=Object.FindFirstObjectByType<Map01Mission>();bag=m.GetComponent<Map01Inventory>();story=m.GetComponent<Map01Story>();
                Assert.AreEqual(3,m.Stage);Assert.IsTrue(story.Progress.keyCollected);Assert.IsFalse(story.Progress.doorUnlocked);Assert.AreEqual(1,story.Progress.lockHistory.Length);
                Assert.AreEqual("cloth",bag.Cell(Map01Inventory.BagSize-1)?.id);Assert.AreEqual(2,bag.Count("cloth"));Assert.AreEqual("smoke_bomb",bag.ReadyThrowable());
                Assert.AreEqual(1,bag.Count(Map01Story.KeyItem));Assert.AreEqual(1,bag.Count(Map01Story.CargoItem));Assert.AreEqual(4,Map01Drops.For(m).Piles.Single().count);
                Assert.AreEqual(engineering,m.Skills.Xp(SkillKind.Engineering));Assert.IsTrue(m.GetComponent<Map01Minimap>().IsExplored(new Vector3(60,6,-20)));
            }finally{Storage.SetValue(null,previous);}
            yield return new ExitPlayMode();
        }
    }
}
