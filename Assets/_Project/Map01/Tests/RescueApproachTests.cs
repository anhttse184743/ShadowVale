using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests {
public sealed class RescueApproachTests:ForestSceneTestBase {
    static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
    static Map01Rescue R=>M.GetComponent<Map01Rescue>();
    static void Isolate(){
        typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,Path.GetFullPath("Logs/RescueApproach/saves/"+Guid.NewGuid().ToString("N")));
        M.GetComponent<Map01Quest>().RestoreStoryStage(Map01Quest.RescueStage,RescueOrdered());
        foreach(var g in M.Enemies)g.enabled=false;
    }
    static void At(Vector3 point){var cc=M.player.GetComponent<CharacterController>();cc.enabled=false;M.player.position=point;cc.enabled=true;Physics.SyncTransforms();}
    [UnityTest]public IEnumerator FortyMetreIntroPreemptsGuardAndStonesAreFiniteSavedLoot(){
        Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
        Isolate();var mission=M;var rescue=R;Assert.AreEqual(40,rescue.Layout.introRadius);
        var points=mission.GetComponent<Map01RescueStones>().Pickups;
        Assert.GreaterOrEqual(points.Count,6,"Provide plentiful dry-ground stone pickups on the approaches.");
        Assert.AreEqual(points.Count,points.Select(p=>p.id).Distinct().Count());
        foreach(var p in points){Assert.AreEqual("stone",p.items.Single().item_id);Assert.AreEqual(3,p.items.Single().count);Assert.IsTrue(NavMesh.SamplePosition(p.transform.position,out _,.5f,NavMesh.AllAreas));}
        int count=mission.Points.Count;Map01RescueStones.Ensure(mission,rescue.Layout,rescue.CaptivePost);Assert.AreEqual(count,mission.Points.Count);
        const string pickedId="rescue_stones_00";
        var direction=(rescue.RetryPoint-rescue.CaptivePost).normalized;
        Assert.IsTrue(NavMesh.SamplePosition(rescue.CaptivePost+direction*43,out var outside,2,NavMesh.AllAreas));At(outside.position);
        Assert.Greater(Vector3.Distance(mission.player.position,rescue.CaptivePost),40);rescue.TryApproachIntro();Assert.IsFalse(mission.Cinematic);
        Assert.IsTrue(NavMesh.SamplePosition(rescue.CaptivePost+direction*39,out var inside,2,NavMesh.AllAreas));At(inside.position);
        typeof(Map01EnemyController).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(rescue.Squad[1],null);
        Assert.IsTrue(mission.Cinematic&&rescue.IntroSeen,"The guard must trigger introduction before evaluating player sight.");Assert.IsFalse(rescue.Failed);
        yield return null;
        mission.GetComponent<Map01RescueCinematic>().Skip();foreach(var g in mission.Enemies)g.enabled=false;
        var pickup=mission.Points.First(p=>p.id==pickedId);Assert.NotNull(pickup);At(pickup.transform.position);
        Directory.CreateDirectory("Logs/RescueApproach");File.WriteAllText("Logs/RescueApproach/stone-pickups.txt",mission.GetComponent<Map01RescueStones>().Pickups.Count+" piles, three stones each");
        var bag=mission.GetComponent<Map01Inventory>();Assert.NotNull(bag);int stones=bag.Stones;
        mission.GetComponent<Map01PlayerInteraction>().Interact(pickup);yield return WaitGameSeconds(4.3f);
        Assert.AreEqual(stones+3,mission.GetComponent<Map01Inventory>().Stones);Assert.IsTrue(pickup.used);
        Assert.IsFalse(pickup.transform.Find("Pickable stones").gameObject.activeSelf);
        mission.GetComponent<Map01PlayerInteraction>().Interact(pickup);Assert.AreEqual(stones+3,mission.GetComponent<Map01Inventory>().Stones);
        Assert.IsTrue(mission.GetComponent<Map01SaveSystem>().SaveSlot(3,out var error,true),error);
        Map01SaveSystem.BeginGame(3);
        yield return null;
        float deadline=Time.realtimeSinceStartup+30;
        while((M==null||!M.IsInitialized||Map01SaveSystem.IsRestoring)&&Time.realtimeSinceStartup<deadline)yield return null;
        Assert.NotNull(M,"Reloaded Map 1 must be initialized before querying pickups.");
        yield return null;
        var restored=M.Points.Where(p=>p.id==pickedId).ToArray();Assert.AreEqual(1,restored.Length);Assert.IsTrue(restored[0].used);Assert.IsFalse(restored[0].transform.Find("Pickable stones").gameObject.activeSelf);
        yield return new ExitPlayMode();
    }
    [UnityTest]public IEnumerator SideBushHintIsReadyAndUsesActualPullAction(){
        Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
        Isolate();R.MarkIntroSeen();M.Crouched=true;M.ModernPlayer.RestoreMotion(true);M.ModernCombat.Equip(WeaponKind.Knife);M.ModernCombat.RestoreAttackCooldown(0);M.GetComponent<Map01Inventory>().SuppressFire=false;
        var bush=M.GetComponent<Map01BushTakedown>();var target=R.Squad[1];bool found=false;
        foreach(var spot in M.Points.Where(p=>p.kind==ForestPointKind.Hide).OrderBy(p=>Vector3.Distance(p.transform.position,R.CaptivePost))){
            if(spot.radius<.72f||!NavMesh.SamplePosition(spot.transform.position,out var center,.5f,NavMesh.AllAreas))continue;
            foreach(var forward in new[]{Vector3.forward,Vector3.right,Vector3.back,Vector3.left}){
                var right=Vector3.Cross(Vector3.up,forward);
                if(!NavMesh.SamplePosition(center.position+forward*.6f,out var guard,.2f,NavMesh.AllAreas)||!NavMesh.SamplePosition(guard.position+right*.85f-forward*.3f,out var nam,.2f,NavMesh.AllAreas))continue;
                target.GetComponent<NavMeshAgent>().Warp(guard.position);target.transform.rotation=Quaternion.LookRotation(forward);At(nam.position);
                float angle=Vector3.Angle(forward,Vector3.ProjectOnPlane(M.player.position-target.transform.position,Vector3.up));
                if(angle<95||angle>125||!bush.Qualifies(target,out _))continue;found=true;break;
            }if(found)break;
        }
        Assert.IsTrue(found,"A real spacious bush must support the side approach.");Assert.IsFalse(target.CanSilentTakedown(),"This case specifically cannot use the ordinary rear knife action.");
        Assert.IsTrue(R.GetTakedownHint(out var hinted,out var ready));Assert.AreSame(target,hinted);Assert.IsTrue(ready,"The leaf icon must light up for a valid side pull, not only for rear stabs.");
        SetPreviewResolution(960,540);var camera=M.gameCamera.GetComponent<ThirdPersonCamera>();var focus=target.transform.position+Vector3.up;
        var eye=focus+target.transform.right*3+Vector3.up*1.6f-target.transform.forward*3;camera.SetCinematicView(eye,Quaternion.LookRotation(focus-eye),50,1);
        Directory.CreateDirectory("Logs/RescueApproach");yield return WaitGameSeconds(.2f);ScreenCapture.CaptureScreenshot("Logs/RescueApproach/Bush-Ready.png");yield return WaitGameSeconds(.2f);camera.ClearCinematicView();
        float readyUntil=Time.time+5;
        while((Map01NamActions.For(M).Busy||!Map01InteractionWarmup.For(M).BushReady)&&Time.time<readyUntil)yield return null;
        Assert.IsFalse(Map01NamActions.For(M).Busy,"Wait for the knife draw before pressing attack.");
        Assert.IsTrue(Map01InteractionWarmup.For(M).BushReady);
        Assert.IsTrue(M.ModernCombat.TrySpecialMelee());Assert.IsTrue(bush.Active);Assert.AreSame(target,bush.Victim);
        bush.enabled=false;Assert.IsFalse(M.Cinematic);Assert.IsTrue(target.Alive);
        yield return new ExitPlayMode();
    }
    [TearDown]public void ClearStorage()=>typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,null);
}}
