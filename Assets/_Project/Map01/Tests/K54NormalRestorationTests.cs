using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.AI.Perception;
using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class K54NormalRestorationTests : ForestSceneTestBase
    {
        const string Work="Docs/Reviews/K54_Normal_Mixamo_20261020";
        static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
        static NoiseKind heard; static float radius; static int reports;
        static void Record(Vector3 at,float metres,NoiseKind kind){if(kind==NoiseKind.Gunshot){heard=kind;radius=metres;reports++;}}
        [SetUp] public void Isolate()=>SessionState.SetString(ForestSaveSlots.EditorTestRootKey,Path.GetFullPath(Work+"/TestSaves"));
        [TearDown] public void Release()=>SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);
        [Test] public void OrdinaryK54ProfileRestoresSixteenMetresWithoutChangingBalance()
        {
            var b=JsonUtility.FromJson<ForestBundle>(File.ReadAllText("Assets/_Project/Map01/Generated/Map01Content.json"));
            var p=b.weapons.Single(w=>w.id=="pistol_k54");
            Assert.IsFalse(p.suppressed);Assert.AreEqual(16,p.noise_radius);
            Assert.AreEqual(22,p.damage);Assert.AreEqual(3,p.fire_rate);Assert.AreEqual(25,p.range);
            Assert.AreEqual(8,p.magazine_size);Assert.AreEqual(1.8f,p.reload_seconds);
            Assert.AreEqual(80,p.durability_max);Assert.AreEqual(.4f,p.durability_per_shot);
            Assert.AreEqual(18,b.weapons.Single(w=>w.id=="rifle_standard").noise_radius);
            Assert.AreEqual(30,b.weapons.Single(w=>w.id=="rifle_mosin").noise_radius);
        }
        [Test] public void HeldLootAndIconsUseTheOriginalApprovedPistol()
        {
            var d=Resources.Load<Map01PistolAnimationData>("Weapons/K54V12");Assert.NotNull(d);
            Assert.AreEqual("55192bd490b5a4a7c6e9cbe7e077e709f4ddbfc718531ea5bd9d669a21223133",d.sourceHash);
            Assert.AreEqual("Assets/_Project/Art/K54_v12/K54_v12.prefab",AssetDatabase.GetAssetPath(d.prefab));
            Assert.AreEqual("K54_Muzzle",d.prefab.GetComponent<Weapon>().Muzzle.name);
            Assert.IsFalse(d.prefab.GetComponentsInChildren<Transform>(true).Any(t=>t.name.Contains("Suppressor")||t.name.Contains("Suppressed")||t.name=="K54_Prop_Exterior_Can"));
            Assert.AreEqual("Assets/_Project/Art/Environment/Map01Items/Meshes/Item_pistol_k54.fbx",AssetDatabase.GetAssetPath(Map01ItemCatalog.Load().Model("pistol_k54")));
            Assert.NotNull(Resources.Load<Texture2D>("Hud/ItemIcons/pistol_k54"));Assert.NotNull(Resources.Load<Texture2D>("Hud/HotbarIcons/pistol_k54"));
            Assert.IsNull(Resources.Load<AudioClip>("Weapons/K54_Suppressed"));
        }
        [UnityTest] public IEnumerator ActualShotUsesNormalReportAudioAndStillWakesWithinTenMetres()
        {
            K54IntegrationTests.OpenSandbox();SetPreviewResolution(1920,1080);
            yield return new EnterPlayMode();yield return WaitForMap01Ready();
            M.GetComponent<Map01Rescue>().MarkIntroSeen();M.ModernHealth.CinematicInvulnerable=true;
            foreach(var g in M.Enemies){g.enabled=false;var a=g.GetComponent<NavMeshAgent>();if(a!=null)a.enabled=false;}
            var c=M.GetComponent<Map01C3Mission>();c.enabled=false;
            var bag=M.GetComponent<Map01Inventory>();Assert.IsTrue(bag.Draw(Map01Inventory.Slot.Pistol));
            M.ModernCombat.RestoreMagazine(WeaponKind.Pistol,2);yield return WaitGameSeconds(.5f);
            reports=0;M.NoiseEmitted+=Record;
            try{typeof(PlayerCombat).GetMethod("Attack",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(M.ModernCombat,null);}
            finally{M.NoiseEmitted-=Record;}
            Assert.AreEqual(1,reports);Assert.AreEqual(NoiseKind.Gunshot,heard);Assert.AreEqual(16,radius);
            Assert.AreEqual(1,M.ModernCombat.MagazineRounds(WeaponKind.Pistol));
            var pistol=M.player.GetComponent<Map01Pistol>();Assert.IsFalse(pistol.Weapon.Automatic);Assert.AreEqual("K54_Muzzle",pistol.Weapon.Muzzle.name);
            var audio=M.player.GetComponent<CombatAudio>();Assert.IsTrue(audio.Gunshot.IsValid);
            Assert.AreEqual(audio.Gunshot.clip,audio.SliceFor(WeaponKind.Pistol).clip);
            Assert.AreEqual(audio.Gunshot.clip,audio.SliceFor(WeaponKind.Rifle).clip);Assert.AreEqual(audio.Gunshot.clip,audio.SliceFor(WeaponKind.Sniper).clip);
            var inside=c.Data.sleepers[0].inside;var origin=c.At(inside[0],inside[1]);
            M.EmitNoise(origin+Vector3.left*11,16,NoiseKind.Gunshot);yield return null;
            Assert.IsTrue(c.Sleepers[0].GetComponent<Map01C3Sleeper>().Sleeping);
            M.EmitNoise(origin+Vector3.left*6,16,NoiseKind.Gunshot);yield return null;
            Assert.IsTrue(c.Sleepers[0].GetComponent<Map01C3Sleeper>().Waking);
            typeof(PlayerCombat).GetMethod("SetAiming",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(M.ModernCombat,new object[]{true});
            yield return WaitGameSeconds(.9f);Assert.Greater(pistol.PoseWeight,.99f);
            var camera=new GameObject("K54 ordinary restoration review").AddComponent<Camera>();camera.CopyFrom(M.gameCamera);
            camera.transform.position=M.player.position+M.player.forward*1.8f+M.player.right*.8f+Vector3.up*1.55f;
            camera.transform.LookAt(M.player.position+Vector3.up*1.28f);camera.fieldOfView=38;camera.nearClipPlane=.03f;
            var rt=RenderTexture.GetTemporary(1920,1080,24);var previous=RenderTexture.active;var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();File.WriteAllBytes(Work+"/Unity_K54_Normal.png",tex.EncodeToPNG());}
            finally{RenderTexture.active=previous;camera.targetTexture=null;RenderTexture.ReleaseTemporary(rt);Object.Destroy(tex);Object.Destroy(camera.gameObject);}
            yield return new ExitPlayMode();
        }
    }
}
