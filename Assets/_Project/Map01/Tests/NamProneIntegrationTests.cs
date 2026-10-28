using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using ShadowVale.Gameplay.Skills;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    public sealed class NamProneIntegrationTests : ForestSceneTestBase
    {
        static Mouse heldMouse;static bool rightHeld;
        static void FeedMouse(){if(heldMouse!=null&&InputState.currentUpdateType==InputUpdateType.Dynamic){heldMouse.MakeCurrent();InputSystem.QueueStateEvent(heldMouse,rightHeld?new MouseState().WithButton(MouseButton.Right):new MouseState());}}
        [UnityTest, Timeout(240000)] public IEnumerator MapOneZCrawlsAndSpaceStandsWithoutJumping() => Exercise(1);
        [UnityTest, Timeout(240000)] public IEnumerator MapTwoZCrawlsAndSpaceStandsWithoutJumping() => Exercise(2);
        static IEnumerator AssertProneBlend(Animator actor, PlayerController player, float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) {
                yield return new WaitForEndOfFrame();
                Assert.IsTrue(player.IsProne);
                Assert.Less(actor.GetBoneTransform(HumanBodyBones.Hips).position.y-player.transform.position.y,.65f,
                    "Reload transitions must keep the visible hips prone, including the first blend frames.");
            }
        }
        IEnumerator Exercise(int map)
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map "+map+".unity");
            yield return new EnterPlayMode();
            CharacterHudSession session=null;
            float until=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<until) {
                Object.FindFirstObjectByType<Map02Arrival>()?.Skip();
                var one=Object.FindFirstObjectByType<Map01Mission>();var two=Object.FindFirstObjectByType<Map02Mission>();
                if(map==1 && one?.ModernPlayer!=null)session=CharacterHudSession.For(one.gameObject);
                if(map==2 && two?.Ready==true && two.Bag!=null)session=CharacterHudSession.For(two.gameObject);
                if(session!=null && !session.Cinematic)break;
                yield return null;
            }
            Assert.NotNull(session);
            if(map==2) {
                var preview=System.Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor",true).GetMethod("ApplyToMission");
                preview.Invoke(null,new object[]{session.MapTwo,2});
            }
            foreach(var enemy in Object.FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None))enemy.enabled=false;
            foreach(var enemy in Object.FindObjectsByType<Map02ChapterEnemy>(FindObjectsSortMode.None))enemy.enabled=false;
            var player=session.player.GetComponent<PlayerController>();var body=player.GetComponent<CharacterController>();
            var motion=player.GetComponent<NamProneMotion>();Assert.NotNull(motion,"Both map startup paths must install the shared motion.");
            yield return WaitGameSeconds(.3f);
            var routing=RouteInputToGame();var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
            heldMouse=mouse;rightHeld=false;InputSystem.onBeforeUpdate+=FeedMouse;
            float height=body.height,ground=player.transform.position.y;
            try {
                yield return Press(keyboard,Key.Z);
                Assert.IsTrue(player.IsProne,"Z must enter prone.");
                yield return WaitGameSeconds(.5f);Assert.Greater(motion.PoseWeight,.99f);Assert.Less(body.height,height);
                var actor=player.GetComponentInChildren<Animator>();
                Assert.Less(actor.GetBoneTransform(HumanBodyBones.Head).position.y-player.transform.position.y,.8f);
                Vector3 before=player.transform.position;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));
                yield return WaitGameSeconds(.8f);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                Assert.Greater(Vector3.ProjectOnPlane(player.transform.position-before,Vector3.up).magnitude,.15f,"Prone must allow crawling.");
                Assert.IsFalse(player.IsSprinting);
                yield return Press(keyboard,Key.Space);
                Assert.IsFalse(player.IsProne);Assert.AreEqual(height,body.height,.001f);
                float peak=player.transform.position.y;
                for(int i=0;i<12;i++){yield return null;peak=Mathf.Max(peak,player.transform.position.y);}
                Assert.Less(peak-ground,.2f,"Space must stand up without also jumping.");
                yield return Press(keyboard,Key.Z);Assert.IsTrue(player.IsProne);
                yield return Press(keyboard,Key.Z);Assert.IsFalse(player.IsProne,"Z must also return to standing.");
                yield return Press(keyboard,Key.Z);Assert.IsTrue(player.IsProne);
                var roof=GameObject.CreatePrimitive(PrimitiveType.Cube);
                roof.transform.position=player.transform.position+Vector3.up*1.25f;roof.transform.localScale=new Vector3(3,.12f,3);Physics.SyncTransforms();
                yield return Press(keyboard,Key.Space);Assert.IsTrue(player.IsProne,"Do not stand through an overhead obstacle.");
                Object.Destroy(roof);yield return null;Physics.SyncTransforms();
                yield return Press(keyboard,Key.Z);Assert.IsFalse(player.IsProne);
                Assert.IsTrue(session.ModernCombat.Equip(WeaponKind.Rifle));
                yield return Press(keyboard,Key.Z);Assert.IsTrue(player.IsProne);
                yield return WaitGameSeconds(.3f);Assert.AreEqual("rifle",motion.SelectedPose);
                var carryGun=session.ModernCombat.WeaponFor(WeaponKind.Rifle);
                var carryReceiver=carryGun.GetComponentsInChildren<Renderer>(true).First(r=>r.name.StartsWith("ul_dustcover"));
                var carryMagazine=carryGun.GetComponentsInChildren<Renderer>(true).First(r=>r.name.StartsWith("bm_magazine_low"));
                Assert.Greater(Vector3.Dot(Vector3.ProjectOnPlane(carryReceiver.bounds.center-carryMagazine.bounds.center,carryGun.Muzzle.forward).normalized,Vector3.up),.85f,"Carried rifle must also keep its magazine down.");
                Capture(session, map, "carry");
                rightHeld=true;
                yield return WaitGameSeconds(.6f);Assert.IsTrue(session.ModernCombat.IsAiming);
                var gun=session.ModernCombat.WeaponFor(WeaponKind.Rifle);
                Assert.Greater(Vector3.Dot(gun.Muzzle.forward.normalized,player.transform.forward),.85f,"Prone aim must point the actual gun ahead.");
                var receiver=gun.GetComponentsInChildren<Renderer>(true).First(r=>r.name.StartsWith("ul_dustcover"));
                var magazine=gun.GetComponentsInChildren<Renderer>(true).First(r=>r.name.StartsWith("bm_magazine_low"));
                Assert.Greater(Vector3.Dot(Vector3.ProjectOnPlane(receiver.bounds.center-magazine.bounds.center,gun.Muzzle.forward).normalized,Vector3.up),.85f,"Aimed rifle must keep its magazine below the receiver.");
                Capture(session,map,"aim");
                rightHeld=false;yield return WaitGameSeconds(.4f);
                Assert.IsFalse(session.ModernCombat.IsAiming,"Release mouse to stop aiming.");
                var combat=session.ModernCombat;var skills=player.GetComponent<PlayerSkills>();
                var savedSkills=skills.Capture();
                session.GetComponent<Map01Inventory>().Add("ammo_rifle",180);
                try {
                    foreach(int level in new[]{0,skills.MaxLevel(SkillKind.Shooting)}) {
                        skills.Restore(new SkillProgress{shooting=skills.Threshold(SkillKind.Shooting,level)});
                        combat.RestoreMagazine(5);
                        yield return Press(keyboard,Key.R);
                        Assert.IsTrue(combat.IsReloading);Assert.IsTrue(player.IsProne,"R must reload without standing up.");
                        yield return AssertProneBlend(actor,player,.25f);
                        yield return WaitGameSeconds(Mathf.Max(0,combat.BaseReloadSeconds*skills.ReloadMultiplier*.45f-.25f));
                        Assert.AreEqual("reload",motion.SelectedPose);
                        Assert.AreEqual(combat.ReloadProgress,motion.ReloadPoseProgress,.06f,"Animation must sample the real skill-adjusted timer.");
                        Assert.IsFalse(magazine.enabled,"Only the authored detachable magazine should be visible during reload.");
                        Capture(session,map,"reload-level"+level);
                        float deadline=Time.realtimeSinceStartup+8;
                        while(combat.IsReloading && Time.realtimeSinceStartup<deadline)yield return null;
                        Assert.IsFalse(combat.IsReloading);
                        yield return AssertProneBlend(actor,player,.25f);
                        Assert.IsTrue(player.IsProne);Assert.IsTrue(magazine.enabled,"Magazine must be restored on completion.");
                        Assert.Greater(combat.RoundsInMagazine,5);
                    }
                    var testBag=session.GetComponent<Map01Inventory>();
                    testBag.Add("ammo_556",120);testBag.Add("ammo_pistol",80);testBag.Add("ammo_sniper",20);
                    foreach(var weaponCase in new[]{("rifle_m16",WeaponKind.Rifle),("pistol_k54",WeaponKind.Pistol),("rifle_mosin",WeaponKind.Sniper)}) {
                        var slot=Map01Inventory.SlotOf(weaponCase.Item1).Value;
                        if(testBag.SlotItem(slot)!=weaponCase.Item1) {
                            testBag.Add(weaponCase.Item1,1);
                            if(testBag.SlotItem(slot)!=weaponCase.Item1) {
                                int cell=Enumerable.Range(0,Map01Inventory.BagSize).First(i=>testBag.Cell(i)?.id==weaponCase.Item1);
                                Assert.IsTrue(testBag.EquipFromCell(cell));
                            }
                        }
                        Assert.IsTrue(combat.Equip(weaponCase.Item2));yield return WaitGameSeconds(.35f);
                        Assert.IsTrue(player.IsProne);Assert.IsTrue(motion.CanReloadProne);
                        int initial=combat.MagazineCapacity-2;combat.RestoreMagazine(initial);
                        Assert.IsTrue(combat.TryReload());yield return WaitGameSeconds(combat.BaseReloadSeconds*skills.ReloadMultiplier*.35f);
                        Assert.IsTrue(player.IsProne,weaponCase.Item1+" must not stand up to reload.");
                        Assert.IsTrue(combat.IsReloading,weaponCase.Item1+" standing pose controller must not cancel the shared reload.");
                        Assert.AreEqual("reload",motion.SelectedPose);
                        Assert.AreEqual(combat.ReloadProgress,motion.ReloadPoseProgress,.06f);
                        Capture(session,map,"reload-"+weaponCase.Item1);
                        float end=Time.realtimeSinceStartup+12;
                        while(combat.IsReloading && Time.realtimeSinceStartup<end)yield return null;
                        Assert.IsFalse(combat.IsReloading);yield return null;yield return null;
                        Assert.Greater(combat.RoundsInMagazine,initial);Assert.IsTrue(player.IsProne);
                    }
                    int akCell=Enumerable.Range(0,Map01Inventory.BagSize).First(i=>testBag.Cell(i)?.id=="rifle_standard");
                    Assert.IsTrue(testBag.EquipFromCell(akCell));Assert.IsTrue(combat.Equip(WeaponKind.Rifle));
                    yield return WaitGameSeconds(.3f);
                    combat.RestoreMagazine(5);Assert.IsTrue(combat.TryReload());
                    yield return WaitGameSeconds(.3f);
                    yield return Press(keyboard,Key.Z);
                    Assert.IsFalse(combat.IsReloading,"Standing up must cancel the prone reload transaction.");
                    Assert.IsTrue(magazine.enabled);Assert.AreEqual(5,combat.RoundsInMagazine);
                    yield return WaitGameSeconds(.3f);yield return Press(keyboard,Key.Z);
                    combat.RestoreMagazine(5);Assert.IsTrue(combat.TryReload());yield return WaitGameSeconds(.3f);
                    Assert.IsTrue(combat.Equip(WeaponKind.Knife));yield return null;yield return null;
                    Assert.IsFalse(combat.IsReloading);Assert.IsTrue(magazine.enabled,"Switching weapons must restore the rifle magazine.");
                } finally {skills.Restore(savedSkills);combat.CancelReload();}
                Assert.IsTrue(session.ModernCombat.Equip(WeaponKind.Knife));
                yield return WaitGameSeconds(.3f);Assert.AreEqual("knife",motion.SelectedPose);
                Assert.IsTrue(player.IsProne,"Changing equipment must preserve prone.");
                var bag=session.GetComponent<Map01Inventory>();bag.Add("stone",2);bag.Add("grenade_f1",2);
                bag.SelectThrowable("stone");yield return WaitGameSeconds(.3f);Assert.AreEqual("stone",motion.SelectedPose);Assert.IsTrue(player.IsProne);
                bag.SelectThrowable("grenade_f1");yield return WaitGameSeconds(.3f);Assert.AreEqual("grenade",motion.SelectedPose);Assert.IsTrue(player.IsProne);
                bag.OnSlotKey(0);yield return null;
                var actions=player.GetComponent<Map01NamActions>();
                Assert.IsTrue(actions.Play(Map01NamActions.Bandage,.5f,true));
                Assert.IsFalse(player.IsProne,"A standing full-body story action must leave prone cleanly.");
                yield return WaitGameSeconds(.8f);Assert.IsFalse(actions.Busy);
                player.RestoreMotion(false);Assert.IsFalse(player.IsProne);Assert.AreEqual(height,body.height,.001f);
            } finally {InputSystem.onBeforeUpdate-=FeedMouse;heldMouse=null;rightHeld=false;InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);RestoreInputRouting(routing);}
            yield return new ExitPlayMode();
        }
        static IEnumerator Press(Keyboard keyboard,Key key)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
        }
        static void Capture(CharacterHudSession session,int map,string pose)
        {
            var go=new GameObject("Disposable prone validation camera");var cam=go.AddComponent<Camera>();
            cam.CopyFrom(session.gameCamera);cam.enabled=false;
            Vector3 target=session.player.position+Vector3.up*.35f;
            cam.transform.SetPositionAndRotation(target+session.player.right*2.2f+session.player.forward*1.7f+Vector3.up*.8f,
                Quaternion.LookRotation(-session.player.right*2.2f-session.player.forward*1.7f-Vector3.up*.8f));
            var rt=new RenderTexture(1000,700,24);var texture=new Texture2D(1000,700,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            try {cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1000,700),0,0);texture.Apply();
                System.IO.Directory.CreateDirectory("Logs/AnimationPreview/NamCrawl/Unity");
                System.IO.File.WriteAllBytes("Logs/AnimationPreview/NamCrawl/Unity/map"+map+"-prone-"+pose+".png",texture.EncodeToPNG());
            } finally {RenderTexture.active=previous;cam.targetTexture=null;Object.Destroy(rt);Object.Destroy(texture);Object.Destroy(go);}
        }
    }
}
