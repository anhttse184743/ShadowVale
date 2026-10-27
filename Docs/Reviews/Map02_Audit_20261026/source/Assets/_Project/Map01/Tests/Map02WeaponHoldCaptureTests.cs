using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// Review captures of Nam holding the rifle through Map 2 (Day 1 to Night 3: free, carrying, after a carry, aiming),
    /// with the gun's parent, visibility and distance to the right hand written beside them. Not an automatic check.
    /// </summary>
    public sealed class Map02WeaponHoldCaptureTests:ForestSceneTestBase
    {
        const string Folder="Logs/Map02WeaponHold";
        static Map02Mission m;static Keyboard keyboard;static Mouse mouse;static bool rightDown;
        static void Pump(){keyboard.MakeCurrent();mouse.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState());var s=new MouseState();if(rightDown)s=s.WithButton(MouseButton.Right);InputSystem.QueueStateEvent(mouse,s);}

        [UnityTest,Explicit,Timeout(600000)]public IEnumerator CaptureNamHoldingTheRifleThroughMapTwo()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return InPlayMode();
            yield return new ExitPlayMode();
        }
        [UnityTest,Timeout(300000)]public IEnumerator TheRifleStaysInBothHandsThroughEveryDayAndAfterALoad()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");yield return new EnterPlayMode();
            yield return StanceInPlayMode();
            yield return new ExitPlayMode();
        }
        static IEnumerator StanceInPlayMode()
        {
            float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);m.Player.GetComponent<Health>().CinematicInvulnerable=true;
            var actor=m.Player.GetComponentsInChildren<Animator>().First(a=>a.isHuman);var combat=m.Player.GetComponent<PlayerCombat>();
            // The first load lands while the arrival's bank briefing may still be playing: releasing its pose must not drop the rifle stance.
            foreach(var (before,carry) in new[]{("D1_04",(string)null),("D1_04","D1_04"),("D1_04",null),("D2_01",null),("D3_01",null),("D3_01","D3_01"),("D3_01",null),("N3_04",null),("D1_04",null)})
            {
                Set(before,null,0,carry);yield return Wait(.5f);
                Assert.AreEqual(WeaponKind.Rifle,combat.EquippedKind,before);
                if(carry!=null){yield return Wait(1.3f);m.Capture();yield return null;Assert.IsTrue(combat.HandsOccupied,before+": both hands stay on the load, even after a save.");continue;}
                Assert.AreEqual((int)WeaponKind.Rifle,actor.GetInteger("Weapon"),$"{before}: the animator holds the equipped rifle (0 is the one-handed unarmed stance).");
            }
        }
        static void Set(string before,string trips=null,int count=0,string carry=null)
        {
            var snap=m.Capture();var s=new Map02ChapterState();
            foreach(var t in Map02ChapterDefinition.Load().tasks){if(t.id==before)break;s.Complete(t.id);}
            for(int k=0;k<count;k++)s.Increment(trips);s.carry=carry;snap.chapter=s;snap.equipped=(int)WeaponKind.Rifle;m.Restore(snap);
        }
        static IEnumerator Wait(float seconds){float end=Time.time+seconds;while(Time.time<end)yield return null;}
        static void Shoot(Camera camera,string file)
        {
            var old=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Path.Combine(Folder,file),image.EncodeToPNG());}
            finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);}
        }
        static string Describe(string label)
        {
            var combat=m.Player.GetComponent<PlayerCombat>();var actor=m.Player.GetComponentsInChildren<Animator>().First(a=>a.isHuman);var hand=actor.GetBoneTransform(HumanBodyBones.RightHand);
            var lines=new List<string>{$"[{label}] task {m.Chapter.State.activeTask} carry '{m.Chapter.State.carry}' equipped {combat.EquippedKind} aiming {combat.IsAiming} ready {combat.GunPresentationReady} busy {m.Chapter.Busy} gearLocked {m.CharacterGearLocked}"};
            foreach(var w in m.Player.GetComponentsInChildren<Weapon>(true))
            {
                var shown=w.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&!r.forceRenderingOff).ToArray();
                lines.Add($"   weapon {w.name} kind {w.Kind} active {w.gameObject.activeInHierarchy} shown {shown.Length}/{w.GetComponentsInChildren<Renderer>(true).Length} parent {w.transform.parent?.name} to right hand {Vector3.Distance(w.transform.position,hand.position):0.00} m");
            }
            foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.StartsWith("Work rifle")))lines.Add("   slung copy: "+t.name);
            lines.Add($"   animator {actor.runtimeAnimatorController?.name} graph {actor.playableGraph.IsValid()} outputs {(actor.playableGraph.IsValid()?actor.playableGraph.GetOutputCount():0)}");
            for(int i=0;i<actor.layerCount;i++)lines.Add($"   layer {i} {actor.GetLayerName(i)} w {actor.GetLayerWeight(i):0.00} clips {string.Join("+",actor.GetCurrentAnimatorClipInfo(i).Select(c=>c.clip.name+":"+c.weight.ToString("0.00")))}");
            foreach(var p in actor.parameters)lines.Add($"   param {p.name} = {(p.type==AnimatorControllerParameterType.Float?actor.GetFloat(p.name).ToString("0.00"):p.type==AnimatorControllerParameterType.Bool?actor.GetBool(p.name).ToString():p.type==AnimatorControllerParameterType.Int?actor.GetInteger(p.name).ToString():"trigger")}");
            foreach(var b in m.Player.GetComponentsInChildren<MonoBehaviour>(true))lines.Add($"   comp {b.GetType().Name} {(b.isActiveAndEnabled?"on":"off")}");
            object F(object o,string n)=>o.GetType().GetField(n,System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)?.GetValue(o);
            object P(object o,string n)=>o.GetType().GetProperty(n,System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)?.GetValue(o);
            lines.Add($"   occupied {F(combat,"_handsOccupied")} handsBusy {F(m.Chapter,"handsBusy")} escort {(F(m.Chapter,"escort") as Map02ChapterActor)?.role} dayActivity {P(m.Chapter,"DayActivityActive")} throwable {m.Bag?.ThrowableEquipped} scene {m.Chapter.CurrentScene?.id} introduced {string.Join(",",m.Chapter.State.introduced)}");
            var lhand=actor.GetBoneTransform(HumanBodyBones.LeftHand);lines.Add($"   hands apart {Vector3.Distance(lhand.position,hand.position):0.00} m");
            return string.Join("\n",lines);
        }
        static IEnumerator InPlayMode()
        {
            float deadline=Time.realtimeSinceStartup+100;
            while(Time.realtimeSinceStartup<deadline){m=Object.FindFirstObjectByType<Map02Mission>();if(m?.Chapter!=null&&m.Ready)break;Object.FindFirstObjectByType<Map02Arrival>()?.Skip();yield return null;}
            Assert.NotNull(m?.Chapter);Directory.CreateDirectory(Folder);m.Player.GetComponent<Health>().CinematicInvulnerable=true;
            var camera=m.GameCamera;var rig=camera.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>();
            var routing=RouteInputToGame();keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();rightDown=false;InputSystem.onBeforeUpdate+=Pump;
            var report=new List<string>();
            try
            {
                var states=new (string name,string before,string trips,int count,string carry)[]{
                    ("d1-free","D1_04",null,0,null),("d1-carry-crate","D1_04",null,0,"D1_04"),("d2-free","D2_01",null,0,null),
                    ("d3-free","D3_01",null,0,null),("d3-carry","D3_01",null,0,"D3_01"),("d3-after-carry","D3_01","D3_01",1,null),
                    ("n3-defend","N3_04",null,0,null),("d1-again","D1_04",null,0,null)};
                foreach(var st in states)
                {
                    Set(st.before,st.trips,st.count,st.carry);rig.ClearCinematicView();yield return Wait(.3f);
                    if(st.carry==null)m.Bag?.Draw(Map01Inventory.Slot.Primary);
                    yield return Wait(1.2f);
                    var nam=m.Player;var at=nam.position+Vector3.up*1.25f;
                    IEnumerator View(string file,Vector3 eye){for(int i=0;i<3;i++){rig.SetCinematicView(eye,Quaternion.LookRotation(at-eye),40,1);yield return null;}Shoot(camera,file);}
                    yield return View(st.name+"-side.png",at+nam.right*2.4f+nam.forward*.6f+Vector3.up*.2f);
                    yield return View(st.name+"-front.png",at+nam.forward*2.6f-nam.right*.8f+Vector3.up*.2f);
                    report.Add(Describe(st.name));
                    if(st.carry!=null)continue;
                    rig.ClearCinematicView();rightDown=true;yield return Wait(.8f);
                    yield return View(st.name+"-aim-side.png",at+nam.right*2.4f+nam.forward*.6f+Vector3.up*.2f);
                    report.Add(Describe(st.name+" aiming"));rightDown=false;rig.ClearCinematicView();yield return Wait(.4f);
                }
            }
            finally{rightDown=false;InputSystem.onBeforeUpdate-=Pump;InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);RestoreInputRouting(routing);rig.ClearCinematicView();File.WriteAllLines(Path.Combine(Folder,"report.txt"),report);}
        }
    }
}
