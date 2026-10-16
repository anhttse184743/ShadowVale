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
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class CrouchStoneTests:ForestSceneTestBase
    {
        [UnityTest]public IEnumerator CrouchedThrowKeepsLegsPlantedAndRestoresKnifeAndRifle()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return null;yield return null;
            // Capturing lambdas must be created after EnterPlayMode's domain reload.
            yield return Review();yield return new ExitPlayMode();
        }
        static IEnumerator Review()
        {
            var m=Object.FindFirstObjectByType<Map01Mission>();Assert.NotNull(m);
            yield return WaitGameSeconds(.5f);
            foreach(var enemy in m.Enemies)if(enemy!=null)enemy.enabled=false;
            var cc=m.player.GetComponent<CharacterController>();cc.enabled=false;
            m.player.SetPositionAndRotation(m.GetComponent<Map01Rescue>().Layout.safeEntry.position,Quaternion.identity);cc.enabled=true;
            var input=RouteInputToGame();var keyboard=InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
            try {
            m.ModernPlayer.RestoreMotion(true);m.Crouched=true;
            EditorSettings.asyncShaderCompilation=false;ShaderUtil.allowAsyncCompilation=false;
            var actor=m.player.GetComponentInChildren<Animator>();var actions=Map01NamActions.For(m);
            var stones=m.GetComponent<Map01StoneThrow>();var inv=m.GetComponent<Map01Inventory>();
            var camera=m.gameCamera;var rig=camera.GetComponent<ThirdPersonCamera>();
            var pump=m.player.gameObject.AddComponent<ExtractionCapturePump>();
            var metrics=new List<string>{"weapon,time,hips_height,head_height,hand_reach"};
            int frame=0;
            foreach(var kind in new[]{WeaponKind.Knife,WeaponKind.Rifle}) {
                m.ModernCombat.Equip(kind);yield return WaitGameSeconds(.5f);
                Assert.IsTrue(m.ModernPlayer.IsSneaking);
                float pelvis=actor.GetBoneTransform(HumanBodyBones.Hips).position.y-m.player.position.y;
                int before=inv.Count("stone");InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Q));InputSystem.Update();stones.BeginAim();Assert.IsTrue(stones.Aiming);
                float start=Time.time;bool released=false;
                Directory.CreateDirectory("Logs/CrouchStone/"+kind);
                while(Time.time-start<3.3f) {
                    float t=Time.time-start;
                    if(t>=1.1f&&!released) {InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();stones.Release();released=true;Assert.AreEqual(before-1,inv.Count("stone"));}
                    var target=m.player.position+Vector3.up*.80f;
                    var eye=target+new Vector3(2.4f,1.0f,2.2f);
                    rig.SetCinematicView(eye,Quaternion.LookRotation(target-eye),44,1);
                    bool drawn=false;Exception failure=null;
                    pump.draw=()=>{try {
                        float height=actor.GetBoneTransform(HumanBodyBones.Hips).position.y-m.player.position.y;
                        Assert.Less(Mathf.Abs(height-pelvis),.04f,"The upper-body throw must preserve the crouch pelvis height.");
                        var upper=actor.GetBoneTransform(HumanBodyBones.RightUpperArm);var lower=actor.GetBoneTransform(HumanBodyBones.RightLowerArm);var hand=actor.GetBoneTransform(HumanBodyBones.RightHand);
                        float reach=Vector3.Distance(upper.position,hand.position)/(Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,hand.position));
                        if(actions.StoneActionPlaying&&t>.3f)Assert.Less(reach,.98f,"Do not lock the throwing elbow straight.");
                        if(!released&&t>.35f)Assert.Greater(hand.position.y,(actor.GetBoneTransform(HumanBodyBones.Chest)??actor.GetBoneTransform(HumanBodyBones.Spine)).position.y,"Hold Q must keep the stone drawn back, not in Nam's lap.");
                        if(actions.StoneActionPlaying)Assert.IsTrue(m.player.GetComponentsInChildren<Weapon>().SelectMany(w=>w.GetComponentsInChildren<Renderer>()).All(r=>r.forceRenderingOff),"Free the throwing hand from the equipped weapon.");
                        Capture(camera,"Logs/CrouchStone/"+kind+"/"+(frame++).ToString("D4")+".png");
                        metrics.Add(kind+","+t+","+height+","+(actor.GetBoneTransform(HumanBodyBones.Head).position.y-m.player.position.y)+","+reach);
                    }catch(Exception error){failure=error;}finally{drawn=true;}};
                    yield return new WaitUntil(()=>drawn);pump.draw=null;if(failure!=null)throw failure;
                }
                Assert.IsFalse(actions.StoneActionPlaying);Assert.AreEqual(kind,m.ModernCombat.EquippedKind);
                Assert.IsTrue(m.player.GetComponentsInChildren<Weapon>().SelectMany(w=>w.GetComponentsInChildren<Renderer>()).All(r=>!r.forceRenderingOff));
                Assert.Less(actor.GetLayerWeight(actor.GetLayerIndex("ActionsUpper")),.01f);
                Assert.IsTrue(actor.GetCurrentAnimatorStateInfo(0).IsName(kind==WeaponKind.Knife?"Sneak_Knife":"Sneak_Rifle"));
                int retained=inv.Count("stone");InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Q));InputSystem.Update();stones.BeginAim();yield return WaitGameSeconds(.3f);stones.Cancel();InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();yield return WaitGameSeconds(.35f);
                Assert.AreEqual(retained,inv.Count("stone"));Assert.IsFalse(actions.StoneActionPlaying);
            }
            File.WriteAllLines("Logs/CrouchStone/metrics.csv",metrics);rig.ClearCinematicView();Object.Destroy(pump);
            }finally{InputSystem.RemoveDevice(keyboard);RestoreInputRouting(input);}
        }
        static void Capture(Camera camera,string path) {
            var old=camera.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(960,540,24);var image=new Texture2D(960,540,TextureFormat.RGB24,false);
            try {camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally {camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);}
        }
    }
}
