using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02FreeMovementTests:ForestSceneTestBase
    {
        static Keyboard keyboard;static Mouse mouse;static Key[] keys=System.Array.Empty<Key>();static bool rightDown;
        static void Pump()
        {
            keyboard.MakeCurrent();mouse.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
            var state=new MouseState();if(rightDown)state=state.WithButton(MouseButton.Right);InputSystem.QueueStateEvent(mouse,state);
        }
        [UnityTest,Timeout(240000)]public IEnumerator CargoEscortAndHeldOpticsKeepRealPlayerMovementAndAnimation()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            Map02Mission m=null;float until=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<until){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Ready==true&&m.Chapter!=null)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);m.Player.GetComponent<Health>().CinematicInvulnerable=true;
            var routing=RouteInputToGame();keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();keys=System.Array.Empty<Key>();rightDown=false;InputSystem.onBeforeUpdate+=Pump;
            try
            {
                var open=OpenPoint();
                LoadThrough(m,"D1_03");yield return WaitGameSeconds(1.5f);Interact(m,"D1_04");yield return WaitGameSeconds(.2f);
                // A load in both hands walks at CarryPace without sprinting (doc Day 1: "Nam chạy chậm lại khi đang khiêng").
                Assert.AreEqual("D1_04",m.Chapter.State.carry);Place(m,open);yield return CheckMovement(m,Map02ChapterDirector.CarryPace,false);
                LoadThrough(m,"D2_04");yield return WaitGameSeconds(1.5f);Interact(m,"D2_05");yield return WaitGameSeconds(.2f);
                Assert.AreEqual("D2_05",m.Chapter.State.carry);Place(m,open);yield return CheckMovement(m,Map02ChapterDirector.CarryPace,false);
                LoadThrough(m,"D2_03");yield return WaitGameSeconds(1.5f);Interact(m,"D2_04");yield return WaitGameSeconds(.2f);
                var restored=Map02Mission.Parse(JsonUtility.ToJson(m.Capture()));m.Restore(restored);yield return null;
                Assert.IsNotNull(m.Chapter.Companion);Assert.IsTrue(m.CharacterGearLocked);Assert.IsTrue(m.CharacterCanHeal);
                m.Chapter.State.scoutAmbushPassedBridge=true; // Isolate movement from the separately tested ambush.
                Place(m,open);yield return WaitGameSeconds(.2f);
                Assert.Less(Vector3.Distance(m.Player.position,m.Chapter.Actor("Tâm").transform.position),2,"The pair must stay together.");
                Assert.AreEqual(1.8f,m.Player.GetComponent<PlayerController>().AssistedWalkSpeed);
                yield return CheckMovement(m,1,false,true);
                Assert.IsFalse(m.Bag.Draw(Map01Inventory.Slot.Primary));Assert.AreEqual(WeaponKind.Pistol,m.Player.GetComponent<PlayerCombat>().EquippedKind);rightDown=true;keys=new[]{Key.W};yield return WaitGameSeconds(.6f);
                Assert.IsTrue(m.Player.GetComponent<PlayerCombat>().IsAiming);Assert.Greater(m.Player.GetComponent<PlayerController>().PlanarSpeed,.5f);
                rightDown=false;yield return WaitGameSeconds(.2f);Assert.IsFalse(m.Player.GetComponent<PlayerCombat>().IsAiming);
                keys=new[]{Key.F};yield return WaitGameSeconds(.2f);Assert.IsFalse(m.Chapter.Binoculars,"The escort keeps Nam's free hand on the K54.");
                LoadThrough(m,"D2_04");Place(m,open);yield return WaitGameSeconds(1.5f);
                keys=new[]{Key.F,Key.W};yield return WaitGameSeconds(.4f);Assert.IsTrue(m.Chapter.Binoculars);Assert.Greater(m.Player.GetComponent<PlayerController>().PlanarSpeed,.5f);
                keys=new[]{Key.W};yield return WaitGameSeconds(.2f);Assert.IsFalse(m.Chapter.Binoculars,"Releasing F must immediately lower the binoculars.");
                keys=new[]{Key.F};yield return WaitGameSeconds(.2f);Assert.IsTrue(m.Chapter.Binoculars);
                m.SetCharacterPanel(1);yield return WaitGameSeconds(.2f);Assert.IsFalse(m.Chapter.Binoculars);m.SetCharacterPanel(0);
            }
            finally{keys=System.Array.Empty<Key>();rightDown=false;InputSystem.onBeforeUpdate-=Pump;InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);RestoreInputRouting(routing);}
            yield return new ExitPlayMode();
        }
        static IEnumerator CheckMovement(Map02Mission m,float pace,bool sprint,bool assisted=false)
        {
            var player=m.Player.GetComponent<PlayerController>();var actor=m.Player.GetComponentInChildren<Animator>();
            Assert.IsFalse(m.InputBlocked);Assert.AreEqual(pace,player.SurfaceSpeedMultiplier,.001f);Assert.IsTrue(m.CharacterCanSprint);
            player.RestoreMotion(false);
            foreach(var key in new[]{Key.W,Key.S,Key.A,Key.D})
            {
                var before=m.Player.position;keys=new[]{key};yield return WaitGameSeconds(.35f);
                var foot=actor.GetBoneTransform(HumanBodyBones.LeftFoot);var pose=actor.transform.InverseTransformPoint(foot.position);
                yield return WaitGameSeconds(.17f);
                Assert.Greater(Vector3.Distance(before,m.Player.position),.3f,key+" must move Nam.");if(!assisted)Assert.Greater(actor.GetFloat("Speed"),.3f);
                Assert.Greater(Vector3.Distance(pose,actor.transform.InverseTransformPoint(foot.position)),.003f,"Leg animation must advance rather than hold a frozen pose.");
                keys=System.Array.Empty<Key>();yield return WaitGameSeconds(.3f);
            }
            keys=new[]{Key.W,Key.LeftShift};yield return WaitGameSeconds(.5f);
            if(sprint){Assert.IsTrue(player.IsSprinting);Assert.Greater(player.PlanarSpeed,3);}else Assert.IsFalse(player.IsSprinting,"No sprinting with a load or a wounded man to lead.");
            keys=System.Array.Empty<Key>();player.RestoreMotion(false);yield return WaitGameSeconds(.3f);
        }
        static void LoadThrough(Map02Mission m,string id)
        {
            var data=m.Capture();data.chapter=new Map02ChapterState();foreach(var task in Map02ChapterDefinition.Load().tasks){Assert.IsTrue(data.chapter.Complete(task.id),task.id);if(task.id==id)break;}
            data.patrolCheckpoint=null;data.stamina=m.CharacterSettings.stamina;m.Restore(data);
        }
        static void Interact(Map02Mission m,string id){var marker=FindMarker(id);Place(m,marker.transform.position);Assert.IsTrue(m.Interact(marker),id);}
        static Map02ObjectiveObject FindMarker(string id)=>Object.FindObjectsByType<Map02ObjectiveObject>(FindObjectsSortMode.None).Single(t=>t.chapterId==id);
        static void Place(Map02Mission m,Vector3 at){var cc=m.Player.GetComponent<CharacterController>();cc.enabled=false;m.Player.position=at;cc.enabled=true;m.Player.GetComponent<PlayerController>().RestoreMotion(false);Physics.SyncTransforms();}
        static Vector3 OpenPoint()
        {
            for(int x=-20;x<=60;x+=10)for(int z=20;z<=60;z+=10)
            {
                if(!NavMesh.SamplePosition(new Vector3(x,1,z),out var hit,3,NavMesh.AllAreas))continue;
                if(NavMesh.FindClosestEdge(hit.position,out var edge,NavMesh.AllAreas)&&edge.distance<4)continue;
                var from=hit.position+Vector3.up;bool clear=true;
                foreach(var direction in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right})if(Physics.SphereCast(from,.35f,direction,out _,3,~0,QueryTriggerInteraction.Ignore))clear=false;
                if(clear)return hit.position;
            }
            Assert.Fail("Need an unobstructed navigation patch for real WASD checks.");return Vector3.zero;
        }
    }
}
