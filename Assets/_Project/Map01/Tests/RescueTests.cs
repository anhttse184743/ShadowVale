using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class RescueTests:ForestSceneTestBase
    {
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static readonly FieldInfo Storage=typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic);
        private static Map01Mission Mission=>Object.FindFirstObjectByType<Map01Mission>();
        private static Map01Rescue Rescue=>Mission.GetComponent<Map01Rescue>();
        private static Map01Quest Quest=>Mission.GetComponent<Map01Quest>();
        private static Map01EnemyController GuardById(string id)=>Mission.Enemies.FirstOrDefault(e=>e.SaveId==id);
        private IEnumerator UnusedOpen()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            Storage.SetValue(null,Path.GetFullPath("Logs/Rescue/saves/"+Guid.NewGuid().ToString("N")));
            Assert.NotNull(Rescue.Layout);Assert.AreEqual(4,Rescue.Squad.Count);
        }
        private static void PlayerAt(Vector3 position,Quaternion rotation)
        {
            var body=Mission.player.GetComponent<CharacterController>();bool enabled=body.enabled;
            body.enabled=false;Mission.player.SetPositionAndRotation(position,rotation);body.enabled=enabled;
            Mission.ModernPlayer.RestoreMotion(true);Mission.Crouched=true;
            Physics.SyncTransforms();
        }
        private static void Behind(Map01EnemyController guard)
        {
            var spot=guard.transform.position-guard.transform.forward*1.25f;
            if(NavMesh.SamplePosition(spot,out var hit,1,NavMesh.AllAreas))spot=hit.position;
            PlayerAt(spot,guard.transform.rotation);Mission.ModernCombat.Equip(WeaponKind.Knife);
        }
        private IEnumerator WaitFree()
        {
            float until=Time.time+18;
            while(Quest.Stage==Map01Quest.RescueStage&&Time.time<until)yield return null;
            Assert.AreEqual(Map01Quest.EscortStage,Quest.Stage,"Untie and stand-up must finish.");
        }
        private IEnumerator ClearAndFree()
        {
            foreach(var enemy in Mission.Enemies)enemy.enabled=false;
            foreach(var guard in Rescue.Squad)guard.GetComponent<Health>().TakeDamage(9999,guard.transform.position,null);
            PlayerAt(Mission.hung.position-Mission.hung.forward*.7f,Mission.hung.rotation);
            Quest.TryRescueHung();yield return WaitFree();
        }
        private static float RouteLength(Vector3[] points)
        {float d=0;for(int i=1;i<points.Length;i++)d+=Vector3.Distance(points[i-1],points[i]);return d;}
        private static Vector3 Along(Vector3[] points,float fraction)
        {
            float d=RouteLength(points)*fraction;
            for(int i=1;i<points.Length;i++){
                float length=Vector3.Distance(points[i-1],points[i]);if(d<=length)return Vector3.Lerp(points[i-1],points[i],d/length);d-=length;
            }return points.Last();
        }
        private static void EscortAt(Vector3 position)
        {
            Mission.hung.GetComponent<NavMeshAgent>().Warp(position);
            PlayerAt(position+Vector3.left*.7f,Quaternion.identity);
            // Look away from the forest banks so the test still exercises the offscreen check.
            var rig=Mission.gameCamera.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>();
            rig.SetCinematicView(position+Vector3.up*3-Vector3.forward*5,Quaternion.LookRotation(Vector3.forward+Vector3.down*.4f),45,1);
        }
        [UnityTest]public IEnumerator FourGuardsHaveThreeSeparatedConnectedRoutesAndApproaches()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
            Storage.SetValue(null,Path.GetFullPath("Logs/Rescue/saves/"+Guid.NewGuid().ToString("N")));Assert.NotNull(Rescue.Layout);Assert.AreEqual(4,Rescue.Squad.Count);
            Assert.AreEqual(0,Rescue.Squad[0].PatrolPoints.Count);
            for(int i=1;i<4;i++){
                var guard=Rescue.Squad[i];float distance=Vector3.Distance(Rescue.Layout.guardPosts[i].position,Rescue.CaptivePost);
                Assert.That(distance,Is.InRange(18f,30f));Assert.AreEqual(4,guard.PatrolPoints.Count);
                foreach(var p in guard.PatrolPoints)Assert.Greater(Map01Rescue.Path(p,Rescue.CaptivePost).Length,1);
            }
            var approaches=Rescue.Layout.transform.Find("Three approach directions");Assert.AreEqual(3,approaches.childCount);
            foreach(Transform marker in approaches)Assert.Greater(Map01Rescue.Path(Mission.player.position,marker.position).Length,1);
            Assert.GreaterOrEqual(Rescue.Layout.pursuitSites.Length,8);
            yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator FootstepsAndStonesInvestigateWithoutExecutingHung()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
            Storage.SetValue(null,Path.GetFullPath("Logs/Rescue/saves/"+Guid.NewGuid().ToString("N")));Assert.NotNull(Rescue.Layout);Assert.AreEqual(4,Rescue.Squad.Count);var guard=Rescue.Squad[1];var overseer=Rescue.Squad[0];
            Assert.IsTrue(guard.Hear(guard.transform.position+Vector3.left*3,9,Map01NoiseKind.Footstep));
            Assert.IsFalse(guard.Alerted,"A footstep has a short reaction; rocks remain immediate.");Assert.IsTrue(overseer.Hear(overseer.transform.position+Vector3.left*9,14,Map01NoiseKind.Stone));
            Assert.AreEqual(Map01Rescue.Phase.Captive,Rescue.CurrentPhase);
            yield return WaitGameSeconds(1.15f);Assert.IsTrue(guard.Alerted);yield return WaitGameSeconds(.85f);
            Assert.LessOrEqual(Vector3.Distance(overseer.transform.position,Rescue.Layout.guardPosts[0].position),6.5f);
            Assert.IsFalse(Rescue.Failed);yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator RealGunshotCommitsExecutionBeforeLethalHitscanAndRetryRestoresItems()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
            Storage.SetValue(null,Path.GetFullPath("Logs/Rescue/saves/"+Guid.NewGuid().ToString("N")));Assert.NotNull(Rescue.Layout);Assert.AreEqual(4,Rescue.Squad.Count);var overseer=Rescue.Squad[0];var inventory=Mission.GetComponent<Map01Inventory>();
            int stones=inventory.Count("stone");Behind(overseer);Mission.ModernCombat.Equip(WeaponKind.Rifle);
            inventory.Spend("stone",2);
            typeof(PlayerCombat).GetMethod("Attack",Private).Invoke(Mission.ModernCombat,null);
            Assert.AreEqual(Map01Rescue.Phase.Alarm,Rescue.CurrentPhase,"Shot event must precede hitscan.");
            overseer.GetComponent<Health>().TakeDamage(9999,overseer.transform.position,Mission.player.gameObject);
            Assert.IsTrue(overseer.Alive,"The alarm's execution cannot be cancelled by the same shot.");
            Assert.IsFalse(Mission.GetComponent<Map01SaveSystem>().SaveSlot(1,out _,true));
            yield return WaitGameSeconds(1.8f);Assert.IsTrue(Rescue.Failed);
            Rescue.Retry();yield return WaitGameSeconds(.8f);
            Assert.AreEqual(Map01Quest.RescueStage,Quest.Stage);Assert.IsFalse(Rescue.Failed);Assert.AreEqual(4,Rescue.Remaining);
            Assert.AreEqual(stones,Mission.GetComponent<Map01Inventory>().Count("stone"));
            Assert.IsFalse(Rescue.Squad[0].GetComponent<Health>().CinematicInvulnerable);
            yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator ConfirmedSightExecutesButPartialSuspicionDoesNot()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
            Storage.SetValue(null,Path.GetFullPath("Logs/Rescue/saves/"+Guid.NewGuid().ToString("N")));Assert.NotNull(Rescue.Layout);Assert.AreEqual(4,Rescue.Squad.Count);var guard=Rescue.Squad[2];
            foreach(var g in Mission.Enemies)g.enabled=g==guard;
            PlayerAt(guard.transform.position+guard.transform.forward*5,Quaternion.LookRotation(-guard.transform.forward));
            var until=Time.time+6;while(!Rescue.Failed&&Time.time<until)yield return null;
            Assert.IsTrue(Rescue.Failed);StringAssert.Contains("phát hiện",Rescue.FailureReason);
            yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator KnifeUsesPreDamageValidationAndPausesOtherGuardsThenRestoresInput()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
            Storage.SetValue(null,Path.GetFullPath("Logs/Rescue/saves/"+Guid.NewGuid().ToString("N")));Assert.NotNull(Rescue.Layout);Assert.AreEqual(4,Rescue.Squad.Count);var guard=Rescue.Squad[0];
            PlayerAt(guard.transform.position+guard.transform.forward*1.1f,guard.transform.rotation);Mission.ModernCombat.Equip(WeaponKind.Knife);
            Assert.IsFalse(guard.CanSilentTakedown());
            Behind(guard);Assert.IsTrue(guard.CanSilentTakedown());
            var others=Rescue.Squad.Skip(1).Select(g=>g.transform.position).ToArray();
            int rounds=Mission.ModernCombat.RoundsInMagazine;
            typeof(PlayerCombat).GetMethod("Attack",Private).Invoke(Mission.ModernCombat,null);
            Assert.IsFalse(guard.Alive);Assert.IsTrue(guard.TakenDownSilently);Assert.IsTrue(Mission.Cinematic);
            Assert.IsFalse(guard.TrySilentTakedown());Assert.IsFalse(Mission.GetComponent<Map01SaveSystem>().SaveSlot(1,out _,true));
            yield return WaitGameSeconds(2);
            for(int i=0;i<3;i++)Assert.Less(Vector3.Distance(others[i],Rescue.Squad[i+1].transform.position),.03f);
            yield return WaitGameSeconds(3);
            Assert.IsFalse(Mission.Cinematic);Assert.IsTrue(Mission.ModernPlayer.enabled);Assert.IsTrue(Mission.ModernCombat.enabled);
            Assert.AreEqual(rounds,Mission.ModernCombat.RoundsInMagazine);Assert.IsFalse(Rescue.OverseerAlive);
            Rescue.Squad[1].Hear(Mission.player.position,100,Map01NoiseKind.Gunshot);
            Rescue.ReportDetection(new Map01Detection(Rescue.Squad[1],Map01DetectionCause.Sight,Mission.player.position));
            Assert.IsFalse(Rescue.Failed);Assert.AreEqual(Map01Rescue.Phase.Captive,Rescue.CurrentPhase);
            yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator FreeRequiresFourKillsButNoHealingItemAndWaitsForStandingUp()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
            Storage.SetValue(null,Path.GetFullPath("Logs/Rescue/saves/"+Guid.NewGuid().ToString("N")));Assert.NotNull(Rescue.Layout);Assert.AreEqual(4,Rescue.Squad.Count);var inventory=Mission.GetComponent<Map01Inventory>();
            inventory.Spend("herb",inventory.Count("herb"));inventory.Spend("medkit_small",inventory.Count("medkit_small"));
            PlayerAt(Mission.hung.position,Mission.hung.rotation);Quest.TryRescueHung();Assert.AreEqual(Map01Rescue.Phase.Captive,Rescue.CurrentPhase);
            foreach(var guard in Mission.Enemies)guard.enabled=false;
            foreach(var guard in Rescue.Squad)guard.GetComponent<Health>().TakeDamage(9999,guard.transform.position,null);
            Quest.TryRescueHung();Assert.AreEqual(Map01Quest.RescueStage,Quest.Stage);Assert.AreEqual(Map01Rescue.Phase.Untying,Rescue.CurrentPhase);
            yield return WaitGameSeconds(2);Assert.AreEqual(Map01Quest.RescueStage,Quest.Stage);
            yield return WaitFree();Assert.AreEqual(Map01Rescue.Phase.Escorting,Rescue.CurrentPhase);
            var visual=Mission.hung.GetComponentInChildren<Map01HungVisual>();Assert.IsFalse(visual.RopeVisible);Assert.IsFalse(visual.IsRising);
            Assert.Greater(visual.actor.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position.y-Mission.hung.position.y,.3f);
            Assert.Greater(visual.actor.GetBoneTransform(HumanBodyBones.RightLowerLeg).position.y-Mission.hung.position.y,.3f);
            Assert.AreEqual(0,inventory.Count("herb"));yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator ThreeWavesSpawnOnlyOnceOffscreenAndReloadKeepsDefeatedEnemies()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
            Storage.SetValue(null,Path.GetFullPath("Logs/Rescue/saves/"+Guid.NewGuid().ToString("N")));Assert.NotNull(Rescue.Layout);Assert.AreEqual(4,Rescue.Squad.Count);yield return ClearAndFree();
            var path=Map01Rescue.Path(Rescue.CaptivePost,Rescue.Layout.shelterDoor.position);
            foreach(float fraction in new[]{.23f,.5f,.76f}){
                EscortAt(Along(path,fraction));yield return WaitGameSeconds(.8f);
                foreach(var guard in Rescue.Pursuers)guard.enabled=false;
            }
            Assert.AreEqual(7,Rescue.WaveMask);Assert.AreEqual(6,Rescue.Pursuers.Count);
            var dead=Rescue.Pursuers[0];dead.GetComponent<Health>().TakeDamage(9999,dead.transform.position,Mission.player.gameObject);string id=dead.SaveId;
            Rescue.HitHung(25);float hp=Rescue.HungHealth;
            Assert.IsTrue(Mission.GetComponent<Map01SaveSystem>().SaveSlot(2,out var error,true),error);
            Map01SaveSystem.BeginGame(2);yield return WaitGameSeconds(.8f);
            Assert.AreEqual(6,Rescue.Pursuers.Count);Assert.AreEqual(7,Rescue.WaveMask);Assert.AreEqual(hp,Rescue.HungHealth);
            Assert.IsFalse(GuardById(id).Alive);
            EscortAt(Along(path,.85f));yield return WaitGameSeconds(.8f);Assert.AreEqual(6,Rescue.Pursuers.Count);
            yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator SafeZoneNeedsBothPassengersAndShelterSkipRewardsExactlyOnce()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
            Storage.SetValue(null,Path.GetFullPath("Logs/Rescue/saves/"+Guid.NewGuid().ToString("N")));Assert.NotNull(Rescue.Layout);Assert.AreEqual(4,Rescue.Squad.Count);yield return ClearAndFree();
            var layout=Rescue.Layout;
            PlayerAt(layout.safeEntry.position,Quaternion.identity);yield return WaitGameSeconds(.6f);Assert.IsFalse(Rescue.SafeReached);
            EscortAt(layout.shelterDoor.position);yield return WaitGameSeconds(.6f);Assert.IsTrue(Rescue.SafeReached);
            var cinematic=Mission.GetComponent<Map01RescueCinematic>();Assert.IsTrue(cinematic.IsPlaying);
            int before=Mission.GetComponent<Map01Inventory>().Count("supplies");
            cinematic.Skip();Assert.AreEqual(Map01Quest.BriefingStage,Quest.Stage);Assert.IsTrue(Rescue.RewardDelivered);
            Assert.AreEqual(before+1,Mission.GetComponent<Map01Inventory>().Count("supplies"));Assert.IsFalse(Rescue.CompleteDelivery());
            Assert.Less(Vector3.Distance(Mission.player.position,layout.reportPoint.position),.15f);
            Assert.IsTrue(Mission.ModernPlayer.enabled);Assert.IsTrue(Mission.ModernCombat.enabled);
            yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator EscortFailureRetriesFromFreedHungWithOriginalInventory()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
            Storage.SetValue(null,Path.GetFullPath("Logs/Rescue/saves/"+Guid.NewGuid().ToString("N")));Assert.NotNull(Rescue.Layout);Assert.AreEqual(4,Rescue.Squad.Count);yield return ClearAndFree();int stones=Mission.GetComponent<Map01Inventory>().Count("stone");
            Mission.GetComponent<Map01Inventory>().Spend("stone",2);Rescue.HitHung(9999);Assert.IsTrue(Rescue.Failed);
            Rescue.Retry();yield return WaitGameSeconds(.8f);
            Assert.AreEqual(Map01Quest.EscortStage,Quest.Stage);Assert.AreEqual(Map01Rescue.Phase.Escorting,Rescue.CurrentPhase);
            Assert.IsFalse(Rescue.Failed);Assert.AreEqual(stones,Mission.GetComponent<Map01Inventory>().Count("stone"));Assert.AreEqual(0,Rescue.WaveMask);
            Assert.AreEqual(0,Rescue.Remaining);yield return new ExitPlayMode();
        }
        [TearDown]public void ResetStorage()=>Storage.SetValue(null,null);
    }
}
