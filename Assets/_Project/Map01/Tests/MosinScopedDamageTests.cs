using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
public sealed class MosinScopedDamageTests:ForestSceneTestBase {
 const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static Mouse mouse;static void HoldAim(){if(mouse!=null){mouse.MakeCurrent();InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));}}
 [UnityTest,Timeout(300000)]public IEnumerator ScopedClickKeepsItsAimRayWhenTheBoltLowersTheScope(){Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return Run();yield return new ExitPlayMode();}
 static IEnumerator Run(){
  float until=Time.realtimeSinceStartup+90;Map01Mission m=null;
  while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map01Mission>();if(m?.IsInitialized==true&&m.player.GetComponent<Map01Mosin>()!=null)break;yield return null;}
  Assert.NotNull(m);m.GetComponent<Map01Rescue>().MarkIntroSeen();m.ModernHealth.CinematicInvulnerable=true;
  foreach(var guard in m.Enemies){guard.enabled=false;var agent=guard.GetComponent<UnityEngine.AI.NavMeshAgent>();if(agent!=null)agent.enabled=false;}
  m.GetComponent<Map01C3Mission>().enabled=false;
  var target=m.Enemies.First(g=>!g.IsBoss);target.transform.position=new Vector3(0,80,35);target.GetComponent<Health>().CinematicInvulnerable=false;target.GetComponent<Health>().Revive();
  var cc=m.player.GetComponent<CharacterController>();cc.enabled=false;m.player.position=new Vector3(0,80,0);cc.enabled=true;
  var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,79.9f,0);floor.transform.localScale=new Vector3(5,.2f,5);
  var bag=m.GetComponent<Map01Inventory>();bag.Add("rifle_mosin",1);bag.Add("ammo_sniper",5);Assert.True(bag.EquipItem("rifle_mosin"));Assert.True(bag.Draw(Map01Inventory.Slot.Primary));m.ModernCombat.RestoreMagazine(WeaponKind.Sniper,5);
  var combat=m.ModernCombat;var mosin=m.player.GetComponent<Map01Mosin>();var rig=m.gameCamera.GetComponent<ThirdPersonCamera>();
  var routing=RouteInputToGame();mouse=InputSystem.AddDevice<Mouse>();if(Keyboard.current==null)InputSystem.AddDevice<Keyboard>();InputSystem.onBeforeUpdate+=HoldAim;
  try{
   yield return WaitGameSeconds(1.2f);
   rig.RestoreLook(Quaternion.LookRotation(target.transform.position+Vector3.up*1.1f-m.GetComponent<Map01Scouting>().Eye));yield return null;yield return null;
   Assert.IsTrue(mosin.Scoped);var before=new Ray(m.gameCamera.transform.position,m.gameCamera.transform.forward);Physics.SyncTransforms();
   Assert.IsTrue(Physics.Raycast(before,out var aimHit,200,~(1<<m.player.gameObject.layer),QueryTriggerInteraction.Ignore));Assert.AreSame(target,aimHit.collider.GetComponentInParent<Map01EnemyController>(),"The visible PU centre must be on the guard before clicking");
   float hp=target.GetComponent<Health>().Current;
   typeof(PlayerCombat).GetMethod("Attack",Private).Invoke(combat,null);
   // This is the real same-frame order: the bolt event lowers PU in Update,
   // the camera returns to third person, then GunfireFrame resolves at order 10000.
   mosin.SendMessage("Update");rig.SendMessage("LateUpdate");
   var after=new Ray(m.gameCamera.transform.position,m.gameCamera.transform.forward);
   typeof(PlayerCombat).GetMethod("ResolvePendingGunfire",Private).Invoke(combat,null);
   Directory.CreateDirectory("Logs/MosinScopedDamage");File.WriteAllText("Logs/MosinScopedDamage/evidence.txt",$"before={before.origin} forward={before.direction}\nafter={after.origin} forward={after.direction}\nhealth={hp}->{target.GetComponent<Health>().Current}\ndamage={mosin.Weapon.Damage} lethal={mosin.Weapon.LethalAgainstEnemies} magazine={combat.RoundsInMagazine}");
   Assert.AreEqual(4,combat.RoundsInMagazine);Assert.IsTrue(target.GetComponent<Health>().IsDead,"A scoped click on the torso must kill even when the bolt camera has moved before resolution");
  }finally{InputSystem.onBeforeUpdate-=HoldAim;if(mouse!=null)InputSystem.RemoveDevice(mouse);mouse=null;RestoreInputRouting(routing);}
 }
}
}
