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
        [TestCase(0,false,true)]
        [TestCase(4,false,true)]
        [TestCase(5,false,false)]
        [TestCase(0,true,false)]
        [TestCase(-1,false,false)]
        public void ScoutAmbushKeepsFiveSequentialWaves(int spawned,bool living,bool allowed)
        {Assert.AreEqual(allowed,Map02ChapterDirector.CanSpawnScoutAmbush(spawned,living));}
        [Test] public void ScoutAmbushKeepsItsDepartureAndSafeSpawnDistances()
        {
            var state=new Map02ChapterState{scoutRescueOriginSet=true,scoutRescueOrigin=Vector3.zero};
            Assert.IsFalse(state.TryStartScoutAmbush(Vector3.forward*3.99f));
            Assert.IsTrue(state.TryStartScoutAmbush(Vector3.forward*4));
            Assert.IsTrue(Map02ChapterDirector.ScoutAmbushDistanceClear(Vector3.zero,Vector3.forward*20,Vector3.back*20));
            Assert.IsFalse(Map02ChapterDirector.ScoutAmbushDistanceClear(Vector3.zero,Vector3.forward*19.9f,Vector3.back*20));
            Assert.IsFalse(Map02ChapterDirector.ScoutAmbushDistanceClear(Vector3.zero,Vector3.forward*20,Vector3.back*19.9f));
        }
        [TestCase("m1_rescue_hung_defiance")]
        [TestCase("m2v3_scoutescort_warning_01")]
        [TestCase("m2v3_evac_field_01")]
        [TestCase("m2v3_evac_river_01")]
        [TestCase("m2v3_evac_village_01")]
        public void MissionVoiceCatalogKeepsRecordingsFromBothBranches(string id)
        {
            var line=DialogueVoice.Find(id);Assert.NotNull(line,"Missing mission voice: "+id);
            Assert.NotNull(line.clip,"Missing recording resource: "+line.resource);
        }
        [TestCase("D2_03","D2_04",2,"escortScout")]
        [TestCase("D2_05","D2_06",2,"antenna")]
        [TestCase("N2_03","N2_04",3,"inspectRoute")]
        public void MissionCheckpointsKeepTheNextTaskPending(string completed,string pending,int phase,string kind)
        {
            var type=Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor",true);
            var state=(Map02ChapterState)type.GetMethod("BuildStateAfter").Invoke(null,new object[]{completed});
            var definition=Map02ChapterDefinition.Load();
            Assert.IsTrue(state.Done(completed));Assert.IsFalse(state.Done(pending));
            Assert.AreEqual(phase,state.phase);Assert.AreEqual(pending,state.TrackedTask.id);
            Assert.AreEqual(kind,definition.Find(pending).kind);Assert.IsTrue(state.Available(definition.Find(pending)));
            var loaded=JsonUtility.FromJson<Map02ChapterState>(JsonUtility.ToJson(state));loaded.Validate();
            Assert.AreEqual(pending,loaded.TrackedTask.id);Assert.AreEqual(90,loaded.storedRounds);
            if(pending=="D2_06")Assert.AreEqual(2,loaded.wireStage,"The antenna checkpoint starts after wire delivery.");
        }
        [TestCase("PlayScoutRescue","ShadowVale/Map 2/Checkpoint cứu Tâm · M2_D2_04")]
        [TestCase("PlayAntenna","ShadowVale/Map 2/Checkpoint lắp ăng-ten · M2_D2_06")]
        [TestCase("PlayEvacuationChoices","ShadowVale/Map 2/Checkpoint kiểm tra đường sơ tán · N2_04")]
        public void MissionCheckpointMenusRemainAvailable(string method,string menu)
        {
            var type=Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor",true);
            var entry=type.GetMethod(method,BindingFlags.Public|BindingFlags.Static);Assert.NotNull(entry);
            Assert.IsTrue(entry.GetCustomAttributes(typeof(UnityEditor.MenuItem),false)
                .Cast<UnityEditor.MenuItem>().Any(a=>a.menuItem==menu&&!a.validate));
            Assert.IsTrue(type.GetMethods(BindingFlags.NonPublic|BindingFlags.Static)
                .SelectMany(m=>m.GetCustomAttributes(typeof(UnityEditor.MenuItem),false).Cast<UnityEditor.MenuItem>())
                .Any(a=>a.menuItem==menu&&a.validate),"Every checkpoint must share the Play/compile safety guard.");
        }
        [UnityTest,Timeout(180000)] public IEnumerator MissionCheckpointsRestoreTheirTaskAndRetryPoint()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission mission=null;float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline)
            {
                mission=Object.FindFirstObjectByType<Map02Mission>();
                if(mission?.Ready==true&&mission.Chapter!=null)break;
                Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;
            }
            Assert.NotNull(mission?.Chapter);
            var type=Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor",true);
            var apply=type.GetMethod("ApplyAfter",BindingFlags.Public|BindingFlags.Static);
            var completed=new[]{"D2_03","D2_05","N2_03"};var pending=new[]{"D2_04","D2_06","N2_04"};
            for(int i=0;i<completed.Length;i++)
            {
                apply.Invoke(null,new object[]{mission,completed[i]});yield return null;
                Assert.AreEqual(pending[i],mission.Chapter.TrackedTask.id);Assert.IsTrue(mission.HasCheckpoint);
                Assert.IsTrue(mission.Chapter.State.Done(completed[i]));Assert.IsFalse(mission.Chapter.State.Done(pending[i]));
                Assert.IsFalse(mission.Paused||ForestMenu.Visible);
                if(pending[i]=="D2_04")
                {
                    Assert.IsTrue(mission.Chapter.ScoutEscortActive);Assert.IsTrue(mission.Chapter.Companion.PairedEscort);
                    Assert.AreEqual(1.8f,mission.Player.GetComponent<ShadowVale.Gameplay.Player.PlayerController>().AssistedWalkSpeed);
                    Assert.AreEqual(ShadowVale.Gameplay.Combat.WeaponKind.Pistol,mission.Player.GetComponent<ShadowVale.Gameplay.Combat.PlayerCombat>().EquippedKind);
                    Assert.IsFalse(mission.Bag.Draw(Map01Inventory.Slot.Primary),"Escort must keep the K54 restriction.");
                    Assert.IsTrue(mission.CharacterCanHeal,"Escort must still allow healing.");
                    var health=mission.Player.GetComponent<ShadowVale.Gameplay.Combat.Health>();health.RestoreHealth(40);
                    int kits=mission.Bag.Count("medkit_small");Assert.IsTrue(mission.Bag.UseItem("medkit_small"));
                    Assert.Greater(health.Current,40);Assert.AreEqual(kits-1,mission.Bag.Count("medkit_small"));
                }
                else
                {
                    Assert.IsFalse(mission.Chapter.ScoutEscortActive,"Changing checkpoints must release the paired escort.");
                    Assert.IsNull(mission.Player.GetComponent<ShadowVale.Gameplay.Player.PlayerController>().AssistedWalkSpeed);
                }
                if(pending[i]=="N2_04")
                {
                    Assert.NotNull(mission.Chapter.UndergroundStore,"The underground store must still exist.");
                    Assert.IsTrue(mission.Chapter.NightPatrolStopped,"The first patrol stops after the store checkpoint.");
                }
                var position=mission.Player.position;
                mission.Restore(Map02Mission.Parse(JsonUtility.ToJson(mission.Capture())));yield return null;
                Assert.AreEqual(pending[i],mission.Chapter.TrackedTask.id,"Save/load changed the checkpoint task.");
                Assert.IsTrue(mission.Retry());yield return null;
                Assert.AreEqual(pending[i],mission.Chapter.TrackedTask.id,"Retry changed the checkpoint task.");
                Assert.Less(Vector3.Distance(position,mission.Player.position),1f,"Retry must return to the selected checkpoint.");
            }
            yield return new ExitPlayMode();
        }
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
                var site=phase==2?m.Chapter.Sites["command"].position:m.Chapter.NightObservationSpawn;
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
