using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class MosinBalanceTests:ForestSceneTestBase
    {
        const string Work="Docs/Reviews/MosinBalance_20261022";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
        static Map01EnemyController target;
        static Map01EnemyController[] freshTargets;static int freshIndex;
        static int damageEvents,kills;
        static DamageNotice lastDamage;
        static readonly List<string> evidence=new();
        static void Damaged(DamageNotice notice){damageEvents++;lastDamage=notice;}
        static void Killed(GameObject victim){if(target!=null&&victim==target.gameObject)kills++;}
        [SetUp]public void Isolate()=>SessionState.SetString(ForestSaveSlots.EditorTestRootKey,Path.GetFullPath(Work+"/TestSaves"));
        [TearDown]public void Cleanup(){SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);Directory.CreateDirectory(Work);File.WriteAllLines(Work+"/last-evidence.txt",evidence);}

        [Test]public void ProfilePrefabAndPrecisionSupportARealTwoHundredMetreHeadShot()
        {
            var bundle=JsonUtility.FromJson<ForestBundle>(File.ReadAllText("Assets/_Project/Map01/Generated/Map01Content.json"));
            var p=bundle.weapons.Single(w=>w.id=="rifle_mosin");Assert.AreEqual(200,p.damage);Assert.AreEqual(200,p.range);
            Assert.AreEqual(5,p.magazine_size);Assert.AreEqual(5,p.reload_seconds);Assert.AreEqual(.7f,p.fire_rate);Assert.AreEqual(30,p.noise_radius);
            var weapon=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Mosin_v13/Mosin_v13.prefab").GetComponent<Weapon>();
            Assert.AreEqual(p.damage,weapon.Damage);Assert.AreEqual(p.range,weapon.Range);Assert.IsTrue(weapon.LethalAgainstEnemies);
            var spread=new WeaponSpreadState();var random=new System.Random(2202026);
            foreach(var stance in new[]{ShooterStance.Standing,ShooterStance.Sneak})
            for(int i=0;i<2000;i++)
            {
                var ray=WeaponSpreadState.Scatter(Vector3.forward,spread.ConeHalfAngle(stance,true,WeaponKind.Sniper),random);
                var at=ray*(200/ray.z);Assert.Less(new Vector2(at.x,at.y).magnitude,.04f,"A settled shot stays inside a 4 cm head-centre margin at max range.");
            }
            Assert.Greater(spread.ConeHalfAngle(ShooterStance.Walking,true,WeaponKind.Sniper),spread.ConeHalfAngle(ShooterStance.Standing,true,WeaponKind.Sniper)*10);
            Assert.AreEqual(WeaponSpreadState.StanceCone(ShooterStance.Standing,true),spread.ConeHalfAngle(ShooterStance.Standing,true,WeaponKind.Rifle));
        }
        [Test]public void LethalityDoesNotLeakIntoNonEnemiesOrAReconfiguredPistol()
        {
            var go=new GameObject("Non-enemy damage fixture");
            try
            {
                var health=go.AddComponent<Health>();health.SetMaxHealth(2100);var weapon=go.AddComponent<Weapon>();
                weapon.Configure(WeaponKind.Sniper,200,200,.7f,false,go.transform,lethalHits:true);
                Assert.AreEqual(200,weapon.DamageAgainst(health),"Nam, Hùng and neutral Health components are not enemy agents.");
                weapon.Configure(WeaponKind.Pistol,22,25,3,false,go.transform);Assert.IsFalse(weapon.LethalAgainstEnemies);Assert.AreEqual(22,weapon.DamageAgainst(health));
            }
            finally{Object.DestroyImmediate(go);}
        }
        static void Prepare()
        {
            evidence.Clear();kills=damageEvents=0;
            if(Keyboard.current==null)InputSystem.AddDevice<Keyboard>();if(Mouse.current==null)InputSystem.AddDevice<Mouse>();
            M.GetComponent<Map01Rescue>().MarkIntroSeen();M.Cinematic=false;M.ModernHealth.CinematicInvulnerable=true;
            foreach(var g in M.Enemies){g.enabled=false;var nav=g.GetComponent<NavMeshAgent>();if(nav!=null)nav.enabled=false;}
            M.GetComponent<Map01C3Mission>().enabled=false;Map01BodyDiscovery.For(M).enabled=false;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Temporary 200 metre Mosin QA lane";
            floor.transform.position=new Vector3(0,79.8f,110);floor.transform.localScale=new Vector3(18,.2f,250);
            PlaceNam(M,new Vector3(0,80.04f,0));M.player.rotation=Quaternion.identity;
            var controller=M.player.GetComponent<CharacterController>();controller.Move(Vector3.down*.2f);Assert.IsTrue(controller.isGrounded);
            var bag=M.GetComponent<Map01Inventory>();bag.Add("rifle_mosin",1);bag.Add("ammo_sniper",20);
            int cell=Enumerable.Range(0,Map01Inventory.BagSize).First(i=>bag.Cell(i)?.id=="rifle_mosin");Assert.IsTrue(bag.EquipFromCell(cell));Assert.IsTrue(bag.Draw(Map01Inventory.Slot.Primary));
            M.ModernPlayer.enabled=false;M.ModernCombat.enabled=false;M.gameCamera.GetComponent<ThirdPersonCamera>().enabled=false;
            freshTargets=M.Enemies.Where(g=>!g.IsBoss&&g.GetComponent<Map01C3Sleeper>()==null&&g!=M.GetComponent<Map01C3Mission>().Commander).ToArray();freshIndex=0;
            Assert.GreaterOrEqual(freshTargets.Length,10);target=freshTargets[freshIndex++];M.ModernCombat.GunKilled+=Killed;
        }
        static void FreshTarget(Map01EnemyController next=null)
        {
            if(target!=null){target.GetComponent<Health>().Damaged-=Damaged;target.transform.position+=Vector3.left*20;}
            target=next!=null?next:freshTargets[freshIndex++];Physics.SyncTransforms();
        }
        static void Stage(float distance,float hp)
        {
            var health=target.GetComponent<Health>();health.Damaged-=Damaged;health.SetMaxHealth(hp);target.ReturnToPost();target.enabled=false;
            target.Repost(new Vector3(0,80,distance),180,stationary:true);health.CinematicInvulnerable=false;health.Damaged+=Damaged;
            damageEvents=0;lastDamage=default;Physics.SyncTransforms();
        }
        static void Aim(float height)
        {
            var eye=M.player.position+Vector3.up*1.65f;M.gameCamera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target.Position+Vector3.up*height-eye));
            M.gameCamera.GetComponent<ThirdPersonCamera>().ClearRecoil();Physics.SyncTransforms();
        }
        static void Fire(bool switchBeforeResolve=false)
        {
            var combat=M.ModernCombat;Assert.IsTrue(combat.EquippedKind==WeaponKind.Sniper);combat.RestoreMagazine(WeaponKind.Sniper,5);
            typeof(PlayerCombat).GetMethod("SetAiming",Private).Invoke(combat,new object[]{true});Assert.LessOrEqual(combat.CurrentSpreadDegrees,.01f);
            bool enabled=combat.enabled;combat.enabled=true;
            try
            {
                typeof(PlayerCombat).GetMethod("Attack",Private).Invoke(combat,null);
                if(switchBeforeResolve)Assert.IsTrue(combat.Equip(WeaponKind.Pistol));
                typeof(PlayerCombat).GetMethod("ResolvePendingGunfire",Private).Invoke(combat,null);
            }
            finally{combat.enabled=enabled;}
            Assert.AreEqual(4,combat.MagazineRounds(WeaponKind.Sniper));
        }
        static void AssertLethal(string label,float hp,int previousKills)
        {
            Assert.IsTrue(target.GetComponent<Health>().IsDead,label);Assert.AreEqual(1,damageEvents,label);Assert.AreEqual(previousKills+1,kills,label);
            Assert.AreEqual(WeaponKind.Sniper,lastDamage.Weapon);Assert.IsTrue(lastDamage.IsBullet);Assert.AreEqual(hp,lastDamage.Amount,.01f);
            evidence.Add(label+" one hit, one round, one kill; applied="+lastDamage.Amount);
            File.WriteAllLines(Work+"/one-shot-cases.txt",evidence);
        }
        [UnityTest]public IEnumerator HeadAndTorsoShotsKillAt35_80_120_199MetresIncludingHeavyAndCommanderHealth()
        {
            K54IntegrationTests.OpenSandbox();SetPreviewResolution(1920,1080);yield return new EnterPlayMode();yield return WaitForMap01Ready();Prepare();yield return WaitGameSeconds(.5f);
            foreach(float distance in new[]{35f,80f,120f,199f})
            foreach(float height in new[]{1.05f,1.65f})
            {
                FreshTarget();
                float hp=distance==35?80:distance==80?160:2100;Stage(distance,hp);Aim(height);int before=kills;
                Fire();AssertLethal($"distance={distance} hit="+(height>1.5f?"head":"torso"),hp,before);yield return WaitGameSeconds(1.55f);
            }
            FreshTarget();Stage(120,2100);Aim(1.65f);int queuedBefore=kills;Fire(switchBeforeResolve:true);AssertLethal("queued scoped shot survives weapon switch",2100,queuedBefore);
            Assert.IsTrue(M.GetComponent<Map01Inventory>().Draw(Map01Inventory.Slot.Primary));yield return WaitGameSeconds(1.55f);
            // This is the actual commander component, not a sphere with a large HP number.
            FreshTarget(M.GetComponent<Map01C3Mission>().Commander);float bossHp=target.GetComponent<Health>().Max;
            Stage(120,bossHp);Aim(1.05f);int bossBefore=kills;Fire();AssertLethal("actual commander torso",bossHp,bossBefore);
            File.WriteAllLines(Work+"/one-shot-cases.txt",evidence);yield return new ExitPlayMode();
        }
        [UnityTest]public IEnumerator MaxRangeCoverAndCinematicProtectionStillApplyAndScopeShowsTheActualTarget()
        {
            K54IntegrationTests.OpenSandbox();SetPreviewResolution(1920,1080);yield return new EnterPlayMode();yield return WaitForMap01Ready();Prepare();yield return WaitGameSeconds(.5f);
            Stage(201,160);Aim(1.05f);Fire();Assert.AreEqual(160,target.GetComponent<Health>().Current);Assert.AreEqual(0,damageEvents);yield return WaitGameSeconds(1.55f);
            Stage(80,160);Aim(1.05f);var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Mosin QA solid obstruction";wall.transform.position=new Vector3(0,81.5f,40);wall.transform.localScale=new Vector3(5,4,1);Physics.SyncTransforms();
            Fire();Assert.AreEqual(160,target.GetComponent<Health>().Current);Assert.AreEqual(0,damageEvents);Object.Destroy(wall);yield return WaitGameSeconds(1.55f);
            target.GetComponent<Health>().CinematicInvulnerable=true;Fire();Assert.AreEqual(160,target.GetComponent<Health>().Current);Assert.AreEqual(0,damageEvents);
            target.GetComponent<Health>().CinematicInvulnerable=false;yield return WaitGameSeconds(1.55f);int before=kills;Fire();AssertLethal("unprotected target after cover/protection removed",160,before);yield return WaitGameSeconds(1.55f);
            FreshTarget();Stage(120,160);var rig=M.gameCamera.GetComponent<ThirdPersonCamera>();rig.enabled=true;
            typeof(PlayerCombat).GetMethod("SetAiming",Private).Invoke(M.ModernCombat,new object[]{true});yield return WaitGameSeconds(.8f);
            rig.RestoreLook(Quaternion.LookRotation(target.Position+Vector3.up*1.65f-M.GetComponent<Map01Scouting>().Eye));yield return null;yield return null;
            Assert.IsTrue(M.player.GetComponent<Map01Mosin>().Scoped);Assert.IsTrue(rig.IsWeaponScope);Assert.AreEqual(rig.WeaponScopeFieldOfView,M.gameCamera.fieldOfView,.2f);
            Assert.IsTrue(Physics.Raycast(M.gameCamera.transform.position,M.gameCamera.transform.forward,out var centre,200,~(1<<M.player.gameObject.layer),QueryTriggerInteraction.Ignore));
            Assert.AreSame(target,centre.collider.GetComponentInParent<Map01EnemyController>(),"The visible scope centre must actually intersect this enemy.");
            var crosshair=Object.FindObjectsByType<CanvasRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(c=>c.name=="Crosshair");
            if(crosshair!=null)Assert.IsFalse(crosshair.gameObject.activeSelf,"The ordinary HUD dot must not cover the fine PU aiming mark.");
            Assert.Less(Vector3.Distance(M.gameCamera.transform.position,target.Position),200);Assert.Greater(Vector3.Distance(M.gameCamera.transform.position,target.Position),100);
            Directory.CreateDirectory(Work);ScreenCapture.CaptureScreenshot(Work+"/scope-120m.png");for(int i=0;i<4;i++)yield return null;
            evidence.Add("201m no hit; solid wall blocks; cinematic protection respected; 120m actual PU scope captured");File.WriteAllLines(Work+"/range-and-scope.txt",evidence);
            typeof(PlayerCombat).GetMethod("SetAiming",Private).Invoke(M.ModernCombat,new object[]{false});yield return null;yield return null;
            if(crosshair!=null)Assert.IsTrue(crosshair.gameObject.activeSelf,"Leaving the scope restores the normal reticle.");
            yield return new ExitPlayMode();
        }
    }
}
