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
using ShadowVale.Gameplay.Player;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class Map02RuntimeTests : ForestSceneTestBase
    {
        [UnityTest,Timeout(180000)] public IEnumerator ObjectCargoFlowSharesPauseMenuAndRestoresExactInventory()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");
            yield return new EnterPlayMode();
            float deadline=Time.realtimeSinceStartup+40;
            Map02Mission mission=null;
            while(Time.realtimeSinceStartup<deadline)
            {
                mission=Object.FindFirstObjectByType<Map02Mission>();
                if(mission!=null&&mission.Ready)break;
                yield return null;
            }
            Assert.IsNotNull(mission);Assert.IsTrue(mission.Ready);
            Assert.Greater(mission.AvailableObjectiveCount,0,"A fresh Map 2 must expose the contact marker immediately.");
            Assert.IsTrue(mission.HasCheckpoint,"The landing must establish a retry checkpoint.");
            Assert.NotNull(mission.TrackedObjective);
            Assert.IsFalse(mission.Player.GetComponent<PlayerCombat>().DrawsOwnWeaponHud,"Map 2 owns the HUD; do not overlay the fallback combat labels.");
            Assert.IsNull(Object.FindFirstObjectByType<Map01Mission>(),"Map 2 must not instantiate Map 1's rescue systems.");
            Assert.AreEqual(1,Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(1,Object.FindObjectsByType<ForestMenu>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(16,Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Length);
            var storageField=typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic);
            string previous=(string)storageField.GetValue(null);
            string storage=Path.GetFullPath("Logs/Map02ObjectFlow/saves/"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(storage);storageField.SetValue(null,storage);
            try
            {
                var health=mission.Player.GetComponent<Health>();var combat=mission.Player.GetComponent<PlayerCombat>();
                health.RestoreHealth(61);combat.RestoreMagazine(9);mission.Inventory["ammo_rifle"]=13;
                var contact=Target(Map02Work.Contact);Move(mission,contact);
                Assert.IsTrue(mission.Interact(contact));
                Assert.IsFalse(mission.CanSave());Assert.IsTrue(mission.InputBlocked);
                for(int i=0;i<4;i++){yield return WaitGameSeconds(.32f);Assert.IsTrue(mission.AdvanceConversation());}
                Assert.AreEqual(Map02Stage.VillageHelp,mission.World.story.stage);
                Assert.IsFalse(mission.InputBlocked);
                Assert.IsFalse(mission.Interact(Target(Map02Work.MedicineDelivery)));
                foreach(var work in new[]{Map02Work.MedicinePickup,Map02Work.CratePickup,Map02Work.CrateDelivery})
                {var target=Target(work);Move(mission,target);Assert.IsTrue(mission.Interact(target),work.ToString());}
                Assert.IsTrue(mission.World.medicineCarried);Assert.IsTrue(mission.World.story.crateStored);
                Assert.AreEqual(61,health.Current);Assert.AreEqual(9,combat.RoundsInMagazine);Assert.AreEqual(13,mission.Inventory["ammo_rifle"]);
                var menu=Object.FindFirstObjectByType<ForestMenu>();
                typeof(ForestMenu).GetField("visible",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(menu,true);mission.SetPaused(true);
                typeof(ForestMenu).GetField("selected",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(menu,1);
                typeof(ForestMenu).GetMethod("Save",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,new object[]{1});
                var saved=ForestSaveSlots.Read(1);Assert.IsNotNull(saved);Assert.AreEqual("Map 2",saved.sceneName);
                var snapshot=Map02Mission.Parse(saved.checkpoint);
                Assert.IsTrue(snapshot.world.medicineCarried);Assert.IsTrue(snapshot.world.story.crateStored);
                mission.SetPaused(false);health.RestoreHealth(12);combat.RestoreMagazine(1);mission.Inventory["ammo_rifle"]=0;
                mission.Restore(snapshot);Assert.AreEqual(61,health.Current);Assert.AreEqual(9,combat.RoundsInMagazine);Assert.AreEqual(13,mission.Inventory["ammo_rifle"]);
                Assert.IsFalse(mission.World.Transfer(Map02Work.MedicinePickup));
                typeof(ForestMenu).GetMethod("Resume",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,null);
                Assert.IsFalse(ForestMenu.Visible);Assert.IsFalse(mission.Paused);
                SetPreviewResolution(1920,1080);
                yield return WaitGameSeconds(.4f);
                var gameCamera=Camera.main;
                File.WriteAllText("Logs/Map02ObjectFlow/camera.txt","Nam="+mission.Player.position+" yaw="+mission.Player.eulerAngles.y+" camera="+gameCamera.transform.position+" rotation="+gameCamera.transform.eulerAngles+"\nContaining renderers:\n"+string.Join("\n",Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.bounds.Contains(gameCamera.transform.position)).Select(r=>r.name+" "+r.bounds)));
                ScreenCapture.CaptureScreenshot("Logs/Map02ObjectFlow/gameplay.png");yield return WaitGameSeconds(.2f);
                int previousMission=mission.GetInstanceID();
                typeof(ForestMenu).GetMethod("StartGame",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(menu,new object[]{1});
                float reloadDeadline=Time.realtimeSinceStartup+45;
                while(Time.realtimeSinceStartup<reloadDeadline)
                {
                    mission=Object.FindFirstObjectByType<Map02Mission>();
                    if(mission!=null&&mission.GetInstanceID()!=previousMission&&mission.Ready)break;
                    yield return null;
                }
                Assert.IsNotNull(mission);Assert.IsTrue(mission.Ready);Assert.AreNotEqual(previousMission,mission.GetInstanceID());
                Assert.AreEqual(Map02Stage.VillageHelp,mission.World.story.stage);
                Assert.IsTrue(mission.World.medicineCarried);Assert.IsTrue(mission.World.story.crateStored);
                Assert.AreEqual(61,mission.Player.GetComponent<Health>().Current);
                Assert.AreEqual(9,mission.Player.GetComponent<PlayerCombat>().RoundsInMagazine);
                Assert.AreEqual(13,mission.Inventory["ammo_rifle"]);
                Assert.AreEqual(1,Object.FindObjectsByType<ForestMenu>(FindObjectsSortMode.None).Length);
                Assert.AreEqual(16,Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Length);
            }
            finally{storageField.SetValue(null,previous);Time.timeScale=1;}
            yield return new ExitPlayMode();
        }
        static Map02ObjectiveObject Target(Map02Work work)=>Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.work==work);
        static void Move(Map02Mission mission,Map02ObjectiveObject target)
        {
            var cc=mission.Player.GetComponent<CharacterController>();cc.enabled=false;mission.Player.position=target.standPoint.position;cc.enabled=true;Physics.SyncTransforms();
        }
    }
}
