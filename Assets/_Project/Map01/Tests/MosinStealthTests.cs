using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.AI.Fsm;
using ShadowVale.AI.Perception;
using ShadowVale.Core.Events;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using ShadowVale.Gameplay.Skills;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class MosinStealthTests:ForestSceneTestBase
    {
        const string Work="Docs/Reviews/Mosin_Stealth_20261020";
        const string Sandbox="Assets/_Project/Map01/Tests/Generated/Mosin Stealth Sandbox.unity";
        static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
        static Map01C3Mission C=>M.GetComponent<Map01C3Mission>();
        static Map01EnemyController G,Ally;
        static int sights,gunshots,kills;
        static Camera proofCamera;
        static string savedGuardId;
        static readonly List<string> evidence=new();
        static readonly List<PlayerContactEvent> contacts=new();
        static readonly List<string> measurements=new();
        static readonly Vector3 Lane=new(0,55,0);
        static void Contact(PlayerContactEvent e){contacts.Add(e);evidence.Add($"contact {e.AgentId} seen={e.Seen} at={e.PlayerPosition}");}
        static void Kill(EnemyKilledEvent e){if(e.WeaponId=="sniper")kills++;}
        static void Detection(Map01Detection e){if(e.cause==Map01DetectionCause.Sight)sights++;else if(e.cause==Map01DetectionCause.Gunshot)gunshots++;evidence.Add($"detection {e.observer.name} {e.cause} at={e.position}");}
        static object Field(Map01EnemyController g,string name)=>typeof(Map01EnemyController).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(g);
        static void Damage(DamageNotice d)=>evidence.Add($"damage bullet={d.IsBullet} weapon={d.Weapon} source={d.Source?.name} isNam={d.Source==M.player.gameObject} origin={d.SourcePosition} pending={Field(G,"_pendingDamage")}/{Field(G,"_pendingOnlyMosin")} bound={Field(G,"_mission")!=null}");
        [SetUp] public void Isolate()=>SessionState.SetString(ForestSaveSlots.EditorTestRootKey,Path.GetFullPath(Work+"/TestSaves"));
        [TearDown] public void Cleanup()
        {
            Time.captureFramerate=0;SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);
            EventBus.Unsubscribe<PlayerContactEvent>(Contact);EventBus.Unsubscribe<EnemyKilledEvent>(Kill);
            if(G!=null)G.Detected-=Detection;if(Ally!=null)Ally.Detected-=Detection;
            File.WriteAllLines(Work+"/last_evidence.txt",evidence);
        }
        static void Open()
        {
            Directory.CreateDirectory(Work);Directory.CreateDirectory(Path.GetDirectoryName(Sandbox));
            if(!File.Exists(Sandbox))AssetDatabase.CopyAsset("Assets/_Project/Scenes/Maps/Map 1.unity",Sandbox);
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene(Sandbox);
        }
        static void Quiet()
        {
            M.GetComponent<Map01Rescue>().MarkIntroSeen();M.ModernHealth.CinematicInvulnerable=true;
            foreach(var g in M.Enemies){g.enabled=false;g.GetComponent<NavMeshAgent>().enabled=false;}
            C.enabled=false;Map01BodyDiscovery.For(M).enabled=false;
            M.ModernPlayer.enabled=false;M.ModernCombat.enabled=false;M.player.GetComponent<CharacterController>().enabled=false;
            M.gameCamera.GetComponent<ThirdPersonCamera>().enabled=false;
            sights=gunshots=kills=0;contacts.Clear();measurements.Clear();evidence.Clear();proofCamera=null;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="QA perception lane floor";
            floor.transform.position=Lane-Vector3.up*.1f;floor.transform.localScale=new Vector3(10,.2f,10);
            EventBus.Unsubscribe<PlayerContactEvent>(Contact);EventBus.Unsubscribe<EnemyKilledEvent>(Kill);
            EventBus.Subscribe<PlayerContactEvent>(Contact);EventBus.Subscribe<EnemyKilledEvent>(Kill);
        }
        static void Equip(bool legacySurvivorProbe=false)
        {
            var bag=M.GetComponent<Map01Inventory>();bag.Add("rifle_mosin",1);bag.Add("ammo_sniper",10);
            int cell=Enumerable.Range(0,Map01Inventory.BagSize).First(i=>bag.Cell(i)?.id=="rifle_mosin");
            Assert.IsTrue(bag.EquipFromCell(cell));Assert.IsTrue(bag.Draw(Map01Inventory.Slot.Primary));
            M.ModernCombat.RestoreMagazine(WeaponKind.Sniper,5);
            // Surviving-hit memory is still an AI contract. Use an explicitly weakened fixture
            // for those probes; production Mosin lethality is checked by MosinBalanceTests.
            if(legacySurvivorProbe){var w=M.ModernCombat.WeaponFor(WeaponKind.Sniper);w.Configure(WeaponKind.Sniper,95,w.Range,.7f,false,w.Muzzle);}
            typeof(PlayerCombat).GetMethod("SetAiming",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(M.ModernCombat,new object[]{true});
        }
        static void Targets(string squad="C1")
        {
            var guards=M.Enemies.Where(g=>g.SquadId==squad&&!g.IsBoss).ToArray();Assert.GreaterOrEqual(guards.Length,2);
            G=guards[0];Ally=guards[1];G.Detected+=Detection;Ally.Detected+=Detection;G.GetComponent<Health>().Damaged+=Damage;
        }
        static void Stage(Map01EnemyController g,Vector3 at,float hp)
        {
            // Set the fixture's health before resetting the guard's damage baseline.
            // Lowering Max after ReturnToPost looks like an untyped hit to the polling adapter.
            g.GetComponent<Health>().SetMaxHealth(hp);g.ReturnToPost();g.Repost(at,Quaternion.LookRotation(Lane-at).eulerAngles.y,stationary:true);
            g.HoldsPost=true;g.enabled=true;g.Brain.Memory.CalmUntil=0;
            // These tests isolate perception from random weapon spread, not hit-box accuracy.
            // The actual tower proof below keeps its original colliders.
            var capsule=g.GetComponent<CapsuleCollider>();if(capsule!=null)capsule.radius=1.1f;
            Physics.SyncTransforms();
        }
        static void Aim(Vector3 at)
        {
            M.gameCamera.transform.position=M.player.position+Vector3.up*1.35f;
            M.gameCamera.transform.LookAt(at);Physics.SyncTransforms();
        }
        static void Fire()
        {
            if(Mathf.Abs(M.player.position.y-Lane.y)<1)
            {
                var floor=GameObject.Find("QA perception lane floor");
                floor.transform.position=new Vector3(M.player.position.x,Lane.y-.1f,M.player.position.z);Physics.SyncTransforms();
            }
            var cc=M.player.GetComponent<CharacterController>();cc.enabled=true;cc.Move(Vector3.down*.04f);
            Assert.IsTrue(cc.isGrounded,"A steady shot needs a grounded shooter, not the disabled controller's airborne spread.");
            typeof(PlayerCombat).GetMethod("SetAiming",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(M.ModernCombat,new object[]{true});
            int before=M.ModernCombat.MagazineRounds(WeaponKind.Sniper);
            // Dev defers hitscan until the final bone/camera pass. This perception fixture
            // freezes normal input, so explicitly flush that same production queue while enabled.
            bool wasEnabled=M.ModernCombat.enabled;M.ModernCombat.enabled=true;
            try {
                typeof(PlayerCombat).GetMethod("Attack",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(M.ModernCombat,null);
                typeof(PlayerCombat).GetMethod("ResolvePendingGunfire",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(M.ModernCombat,null);
            } finally { M.ModernCombat.enabled=wasEnabled; }
            Assert.AreEqual(before-1,M.ModernCombat.MagazineRounds(WeaponKind.Sniper),"Use the real shot, ammo and hitscan path.");
        }
        static void NoInventedContact()
        {
            string detail=$"Nam={M.player.position} G={G.Position} ally={Ally.Position} hp={G.GetComponent<Health>().Current} engaged={G.Engaged}/{Ally.Engaged} known={G.TryGetKnownThreat(out var at)} at={at}\n"+string.Join("\n",evidence);
            Assert.AreEqual(0,sights,detail);Assert.AreEqual(0,contacts.Count,detail);
            Assert.IsFalse(G.Engaged||Ally.Engaged,detail);Assert.IsFalse(G.TryGetKnownThreat(out _),detail);
        }
        [UnityTest] public IEnumerator QueuedMosinKeepsItsIdentityWhenSwitchedBeforeTheFinalShotPass()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();Quiet();Equip(true);Targets();PlaceNam(M,Lane);
            Stage(G,Lane+Vector3.forward*35,200);Stage(Ally,Lane+new Vector3(6,0,40),80);
            yield return WaitGameSeconds(.5f);Aim(G.Position+Vector3.up);
            VerifyQueuedSwitchIdentity();yield return null;yield return null;
            Assert.AreEqual(105,G.GetComponent<Health>().Current,.01f);NoInventedContact();
            yield return new ExitPlayMode();
        }
        static void VerifyQueuedSwitchIdentity()
        {
            var cc=M.player.GetComponent<CharacterController>();cc.enabled=true;cc.Move(Vector3.down*.04f);
            Assert.IsTrue(cc.isGrounded,"Keep the queued-shot probe independent of airborne spread.");
            var combat=M.ModernCombat;var health=G.GetComponent<Health>();DamageNotice observed=default;
            Action<DamageNotice> capture=d=>observed=d;health.Damaged+=capture;
            bool enabled=combat.enabled;combat.enabled=true;
            try {
                typeof(PlayerCombat).GetMethod("SetAiming",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(combat,new object[]{true});
                typeof(PlayerCombat).GetMethod("Attack",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(combat,null);
                Assert.AreEqual(200,health.Current,"The shot is still waiting for its final animation/camera pass.");
                Assert.IsTrue(combat.Equip(WeaponKind.Rifle));
                typeof(PlayerCombat).GetMethod("ResolvePendingGunfire",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(combat,null);
                Assert.AreEqual(WeaponKind.Sniper,observed.Weapon,"Damage must identify the weapon that fired, even though Nam now holds AK.");
                Assert.IsTrue(observed.IsBullet);Assert.AreEqual(95,observed.Amount,.01f);
            } finally {health.Damaged-=capture;combat.enabled=enabled;}
        }

        [UnityTest] public IEnumerator RealMosinShotsAt35_50_70MetresNeverInventSightOrSquadContact()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();Quiet();Equip();Targets();PlaceNam(M,Lane);
            yield return WaitGameSeconds(.5f);
            int initialShootingXp=M.Skills.Xp(SkillKind.Shooting);
            foreach(float distance in new[]{35f,50f,70f})
            {
                foreach(float hp in new[]{200f,80f})
                {
                    Stage(G,Lane+Vector3.forward*distance,hp);Stage(Ally,Lane+new Vector3(6,0,distance+2),80);
                    yield return null;yield return null;sights=gunshots=0;contacts.Clear();
                    Aim(G.transform.position+Vector3.up);M.ModernCombat.RestoreMagazine(WeaponKind.Sniper,5);
                    float heardDistance=Vector3.Distance(M.ModernCombat.WeaponFor(WeaponKind.Sniper).Muzzle.position,G.Position);
                    Assert.Greater(heardDistance,30);Fire();yield return null;yield return null;
                    Assert.AreEqual(0,G.GetComponent<Health>().Current,.01f,"The production Mosin kills on the first real hit.");
                    NoInventedContact();Assert.AreEqual(0,gunshots);
                    if(G.Alive)Assert.IsTrue(G.Wary&&G.Alerted);else Assert.NotNull(G.GetComponent<ForestPoint>(),"A sniper kill still drops loot.");
                    measurements.Add($"distance={distance} hp={hp} muzzle_distance={heardDistance:F4} sight={sights} contact={contacts.Count} heard={gunshots} alive={G.Alive}");
                    yield return WaitGameSeconds(2f);
                }
            }
            Assert.AreEqual(6,kills);Assert.AreEqual(initialShootingXp+6,M.Skills.Xp(SkillKind.Shooting),"Every real lethal hitscan still awards shooting XP once.");
            File.WriteAllLines(Work+"/distance_cases.txt",measurements);
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator CloseShotIsHeardByEveryListenerAndRemembersTheShotRatherThanNamMoving()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();Quiet();Equip(true);Targets();PlaceNam(M,Lane);
            // Both within Map01Mission.ShotPinpointMeters (12 m): further off, a Mosin shot is only searched for (MosinSniperShotTests).
            Stage(G,Lane+Vector3.forward*10,200);Stage(Ally,Lane+Vector3.back*5,80);
            G.transform.rotation=Quaternion.LookRotation(Vector3.forward);
            Ally.transform.rotation=Quaternion.LookRotation(Vector3.back); // Hearing behind the shooter, without a real visual contact.
            yield return WaitGameSeconds(.5f);Aim(G.Position+Vector3.up);
            Vector3 origin=M.ModernCombat.WeaponFor(WeaponKind.Sniper).Muzzle.position;
            Fire();yield return null;yield return null;Assert.AreEqual(105,G.GetComponent<Health>().Current,.01f);
            Assert.GreaterOrEqual(gunshots,2,"The listener behind Nam also hears the shot.");Assert.AreEqual(0,sights);
            Assert.IsTrue(G.TryGetKnownThreat(out var at));Assert.Less(Vector3.Distance(origin,at),.05f);
            PlaceNam(M,Lane+Vector3.right*80);yield return null;yield return null;
            Assert.IsTrue(G.TryGetKnownThreat(out at));Assert.Less(Vector3.Distance(origin,at),.05f);Assert.Greater(Vector3.Distance(M.player.position,at),50);
            var saved=JsonUtility.FromJson<Map01EnemyController.Snapshot>(JsonUtility.ToJson(G.Capture()));G.ReturnToPost();G.RestoreSnapshot(saved);
            Assert.IsTrue(G.TryGetKnownThreat(out at));Assert.Less(Vector3.Distance(origin,at),.05f);
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator ActualSightStillFindsNamAndHiddenSniperDamageKeepsThePreviousContact()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();Quiet();Equip(true);Targets();PlaceNam(M,Lane);
            Stage(G,Lane+Vector3.forward*8,200);Stage(Ally,Lane+new Vector3(30,0,40),80);
            yield return WaitGameSeconds(2.3f);Assert.IsTrue(G.Engaged);Assert.Greater(sights,0);
            var known=G.Brain.Memory.LastKnownPlayerPosition;PlaceNam(M,Lane+Vector3.back*50);
            yield return WaitGameSeconds(.4f);Aim(G.Position+Vector3.up);Fire();yield return null;yield return null;
            Assert.AreEqual(105,G.GetComponent<Health>().Current,.01f);Assert.IsTrue(G.Engaged);
            Assert.Less(Vector3.Distance(known,G.Brain.Memory.LastKnownPlayerPosition),.05f);Assert.Greater(Vector3.Distance(M.player.position,known),45);
            var snapshot=JsonUtility.FromJson<Map01EnemyController.Snapshot>(JsonUtility.ToJson(G.Capture()));G.ReturnToPost();G.RestoreSnapshot(snapshot);
            Assert.IsTrue(G.TryGetKnownThreat(out var restored));Assert.Less(Vector3.Distance(known,restored),.05f);
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator HitEvidenceSurvivesWeaponSwitchAndOtherDamageIsNotMisclassifiedAsMosin()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();Quiet();Equip(true);Targets();PlaceNam(M,Lane);
            Stage(G,Lane+Vector3.forward*40,200);Stage(Ally,Lane+new Vector3(6,0,42),80);
            yield return WaitGameSeconds(.5f);G.enabled=false;Aim(G.Position+Vector3.up);Fire();
            Assert.IsTrue(M.GetComponent<Map01Inventory>().Draw(Map01Inventory.Slot.Pistol));G.enabled=true;
            yield return null;yield return null;NoInventedContact();Assert.IsTrue(G.Wary);
            Assert.IsTrue(M.GetComponent<Map01Inventory>().Draw(Map01Inventory.Slot.Primary));yield return WaitGameSeconds(.3f);
            G.GetComponent<Health>().TakeDamage(1,G.Position+Vector3.up,M.player.gameObject);yield return null;yield return null;
            Assert.IsTrue(G.Engaged);Assert.Greater(sights,0);Assert.Greater(contacts.Count,0,"Generic damage keeps its existing rule, even while holding Mosin.");
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator SaveFileReloadKeepsAnUnknownSniperAttackUnknown()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();Quiet();Equip(true);Targets();PlaceNam(M,Lane);
            Stage(G,Lane+Vector3.forward*40,200);Stage(Ally,Lane+new Vector3(6,0,42),80);
            yield return WaitGameSeconds(.5f);Aim(G.Position+Vector3.up);Fire();yield return null;yield return null;NoInventedContact();
            Assert.NotNull(M,"Mission still exists after the shot.");savedGuardId=G.SaveId;
            var save=M.GetComponent<Map01SaveSystem>();Assert.NotNull(save,"The copied map must carry its real save system: "+M.name);
            Assert.IsTrue(save.IsSandbox);bool saved=save.SaveSlot(0,out var error,true);Assert.IsTrue(saved,error);
            G.Alert(M.player.position);save.Load();yield return null;yield return null;yield return WaitForMap01Ready();yield return WaitGameSeconds(.5f);
            Quiet();G=M.Enemies.Single(g=>g.SaveId==savedGuardId);PlaceNam(M,Lane+Vector3.right*80);
            Assert.IsTrue(G.Wary);Assert.IsFalse(G.Engaged);Assert.IsFalse(G.TryGetKnownThreat(out _));
            Assert.Greater(Vector3.Distance(G.Brain.Memory.InvestigatePoint,M.player.position),35);
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator C3ReservesAndRunnersReceiveOnlyRecordedInformation()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();Quiet();PlaceNam(M,Lane);
            C.enabled=true;Assert.Greater(C.CallReinforcements(),0);
            yield return WaitGameSeconds(Map01BedAnimationData.Load().wake.length+.4f);
            foreach(var guard in C.Sleepers){Assert.IsFalse(guard.Engaged);Assert.IsFalse(guard.TryGetKnownThreat(out _));}
            yield return WaitGameSeconds(C.Data.runners.delaySeconds+1);
            Assert.IsTrue(C.Current.runnersSent);
            var runners=M.Enemies.Where(g=>g.name.StartsWith("C3 runner",StringComparison.Ordinal)).ToArray();Assert.AreEqual(2,runners.Length);
            foreach(var guard in runners){Assert.IsFalse(guard.Engaged);Assert.IsFalse(guard.TryGetKnownThreat(out _));}
            C.enabled=false;C.Restore(null);foreach(var guard in M.Enemies)guard.ReturnToPost();
            var old=C.Guard("gate").Position+Vector3.forward*5;C.Guard("gate").Alert(old);C.CallReinforcements();
            PlaceNam(M,Lane+Vector3.right*70);yield return WaitGameSeconds(Map01BedAnimationData.Load().wake.length+.4f);
            foreach(var guard in C.Sleepers){Assert.IsTrue(guard.TryGetKnownThreat(out var at));Assert.Less(Vector3.Distance(old,at),.05f);}
            yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator RepeatedDistantMissesDoNotCreateDamageOrContact()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();Quiet();Equip();Targets();PlaceNam(M,Lane);
            Stage(G,Lane+Vector3.forward*50,200);Stage(Ally,Lane+new Vector3(6,0,52),80);
            yield return WaitGameSeconds(.3f);
            for(int i=0;i<3;i++)
            {
                Aim(Lane+Vector3.right*70);Fire();yield return WaitGameSeconds(1.6f);
                Assert.AreEqual(200,G.GetComponent<Health>().Current);NoInventedContact();Assert.AreEqual(0,gunshots);
            }
            Assert.AreEqual(2,M.ModernCombat.MagazineRounds(WeaponKind.Sniper));yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator LethalMosinDamageToASleepingReserveCreditsExactlyOneKillAndLootWithoutSight()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();Quiet();Equip();PlaceNam(M,Lane);
            var guard=C.Sleepers[0];var bed=guard.GetComponent<Map01C3Sleeper>();Assert.IsTrue(bed.Sleeping&&!guard.enabled);
            guard.Detected+=Detection;var hp=guard.GetComponent<Health>();Assert.LessOrEqual(hp.Current,190);
            hp.TakeBulletDamage(95,bed.BodyPoint,M.player.gameObject,Vector3.forward);
            if(!hp.IsDead)hp.TakeBulletDamage(95,bed.BodyPoint,M.player.gameObject,Vector3.forward);
            yield return null;yield return null;
            Assert.IsFalse(guard.Alive);Assert.AreEqual(Map01C3Sleeper.Mode.Dead,bed.CurrentMode);Assert.IsTrue(bed.Visual.activeInHierarchy);
            Assert.IsTrue(C.Current.sleepersKilled[0]);Assert.IsFalse(guard.TakenDownSilently);
            Assert.NotNull(guard.GetComponent<ForestPoint>());Assert.AreEqual(1,kills);Assert.AreEqual(0,sights);Assert.AreEqual(0,contacts.Count);
            yield return WaitGameSeconds(.3f);Assert.AreEqual(1,kills,"The disabled controller and bed proxy must not credit the same kill twice.");
            guard.Detected-=Detection;yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator DistantMosinKillAtRadioTriggersBodySearchAndResponseWithoutKnowingNam()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();Quiet();Equip();Targets("C2");PlaceNam(M,Lane);
            Stage(G,Lane+Vector3.forward*50,80);Stage(Ally,Lane+new Vector3(6,0,52),100);
            Ally.transform.LookAt(G.Position);Map01BodyDiscovery.For(M).enabled=true;
            yield return WaitGameSeconds(.3f);Aim(G.Position+Vector3.up);Fire();yield return null;yield return null;
            Assert.IsFalse(G.Alive);NoInventedContact();Assert.IsTrue(Map01BodyDiscovery.CanSee(M,Ally,G,out _),"The witness really has a view of the casualty.");
            // Response spawning already requires off-screen sites. Turn the shot camera away
            // after firing rather than changing spawn rules or the map's geometry.
            M.gameCamera.transform.rotation=Quaternion.Euler(-90,0,0);
            yield return WaitGameSeconds(2f);
            Assert.IsTrue(G.BodyFound);Assert.IsTrue(Ally.Wary&&Ally.Alerted);NoInventedContact();
            var story=M.GetComponent<Map01Story>();Assert.IsTrue(story.Progress.radioResponseCalled);
            float deadline=Time.time+8;while(!story.Progress.radioResponseSpawned&&Time.time<deadline)yield return null;
            Assert.IsTrue(story.Progress.radioResponseSpawned);
            var response=M.Enemies.Where(g=>g.name.StartsWith("operation_radio_response_",StringComparison.Ordinal)).ToArray();Assert.AreEqual(3,response.Length);
            foreach(var guard in response)Assert.IsFalse(guard.TryGetKnownThreat(out _),"Radio responders search at the station, not at Nam's unknown position.");
            Assert.AreEqual(0,contacts.Count);yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator TheCommanderAlsoGetsNoContactFromAnUnseenDistantMosinHit()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();Quiet();Equip(true);PlaceNam(M,Lane);
            G=C.Commander;Ally=C.Guard("gate");G.Detected+=Detection;Ally.Detected+=Detection;
            float hp=G.GetComponent<Health>().Max;Assert.Greater(hp,95);
            Stage(G,Lane+Vector3.forward*50,hp);Stage(Ally,Lane+new Vector3(6,0,52),100);
            yield return WaitGameSeconds(.3f);Aim(G.Position+Vector3.up);Fire();yield return null;yield return null;
            Assert.AreEqual(hp-95,G.GetComponent<Health>().Current,.01f);Assert.IsTrue(G.Wary);NoInventedContact();
            yield return new ExitPlayMode();
        }
        static void Picture(string path)
        {
            var camera=proofCamera!=null?proofCamera:M.gameCamera;var rt=RenderTexture.GetTemporary(1920,1080,24);var previous=camera.targetTexture;var active=RenderTexture.active;
            var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,path.EndsWith(".jpg")?pixels.EncodeToJPG(88):pixels.EncodeToPNG());}
            finally{camera.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(pixels);}
        }
        [UnityTest] public IEnumerator CaptureActualTowerShotFromNestAWithoutInventingNamPosition()
        {
            Open();yield return new EnterPlayMode();yield return WaitForMap01Ready();Quiet();Equip();SetPreviewResolution(1920,1080);
            var nest=C.At(C.Data.nestA[0],C.Data.nestA[1]);PlaceNam(M,nest);
            foreach(var guard in M.Enemies.Where(g=>g.SquadId=="C3"&&g.Alive))
            {guard.enabled=true;var agent=guard.GetComponent<NavMeshAgent>();if(!guard.HoldsPost)agent.enabled=true;}
            C.enabled=true;Map01BodyDiscovery.For(M).enabled=true;
            yield return WaitGameSeconds(.7f);var tower=C.Guard("tower");Aim(tower.Position+Vector3.up*1.3f);M.gameCamera.transform.position=nest+Vector3.up*1.5f;M.gameCamera.transform.LookAt(tower.Position+Vector3.up*1.3f);
            Physics.SyncTransforms();
            bool hit=Physics.Raycast(M.gameCamera.transform.position,M.gameCamera.transform.forward,out var centre,80,~0,QueryTriggerInteraction.Ignore);
            Assert.IsTrue(hit&&centre.collider.GetComponentInParent<Map01EnemyController>()==tower,"Centre ray blocked by "+(hit?centre.collider.name:"nothing"));
            proofCamera=new GameObject("Temporary tower QA observation camera").AddComponent<Camera>();proofCamera.CopyFrom(M.gameCamera);proofCamera.tag="Untagged";proofCamera.enabled=false;
            proofCamera.nearClipPlane=.03f;proofCamera.fieldOfView=45;proofCamera.transform.position=tower.Position+new Vector3(5,1.1f,-6);proofCamera.transform.LookAt(tower.Position+Vector3.up*.8f);
            proofCamera.ResetProjectionMatrix();proofCamera.ResetWorldToCameraMatrix();proofCamera.ResetCullingMatrix();
            float nearest=M.Enemies.Where(g=>g.Alive&&g.SquadId=="C3").Min(g=>Vector3.Distance(M.ModernCombat.WeaponFor(WeaponKind.Sniper).Muzzle.position,g.Position));
            Assert.Greater(nearest,30);contacts.Clear();Time.captureFramerate=60;
            float initialHP=tower.GetComponent<Health>().Current;int shots=0;
            var rows=new List<string>{"frame,tower_alive,tower_hp,contacts,c3_alarm"};
            for(int frame=1;frame<=240;frame++)
            {
                if(frame==31||frame==151&&tower.Alive){Fire();shots++;}yield return null;
                Picture(Work+"/UnityFrames/frame_"+frame.ToString("D4")+".jpg");
                if(frame==25||frame==42||frame==180)Picture(Work+"/Unity_Sniper_"+frame+".png");
                rows.Add(FormattableString.Invariant($"{frame},{tower.Alive},{tower.GetComponent<Health>().Current},{contacts.Count},{C.Alarmed}"));
            }
            Time.captureFramerate=0;Assert.IsFalse(tower.Alive,"Real shots must damage the original collider without changing the guard's HP.");Assert.AreEqual(5-shots,M.ModernCombat.MagazineRounds(WeaponKind.Sniper));
            yield return WaitGameSeconds(4f);Assert.AreEqual(0,contacts.Count);
            Assert.IsFalse(M.Enemies.Any(g=>g.SquadId=="C3"&&g.Alive&&g.TryGetKnownThreat(out _)),"Body alarms may happen, but they cannot invent Nam's position.");
            File.WriteAllLines(Work+"/tower_film.csv",rows);File.WriteAllText(Work+"/tower_proof.txt",FormattableString.Invariant($"nearest_C3_listener_m={nearest}\ninitial_tower_hp={initialHP}\nshots={shots}\ncontacts={contacts.Count}\ntower_alive={tower.Alive}\nbody_found={tower.BodyFound}\n"));
            yield return new ExitPlayMode();
        }
    }
}
