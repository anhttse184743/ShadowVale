using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
public sealed class RescueRadioIntroTests:ForestSceneTestBase {
    [UnityTest]public IEnumerator RadioReportHoldsNamStillAndRestoresGameplay(){
        Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
        yield return new EnterPlayMode();yield return null;yield return null;
        yield return Review(false);yield return new ExitPlayMode();
    }
    [UnityTest]public IEnumerator SkipDuringRadioRestoresKnifeCrouchAndAudio(){
        Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
        yield return new EnterPlayMode();yield return null;yield return null;
        yield return Review(true);yield return new ExitPlayMode();
    }
    static IEnumerator Review(bool skip){
        var m=Object.FindFirstObjectByType<Map01Mission>();var rescue=m.GetComponent<Map01Rescue>();
        typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,Path.GetFullPath("Logs/RescueRadio/saves/"+Guid.NewGuid().ToString("N")));
        foreach(var enemy in m.Enemies)enemy.enabled=false;
        m.ModernCombat.Equip(skip?WeaponKind.Knife:WeaponKind.Rifle);m.Crouched=skip;m.ModernPlayer.RestoreMotion(skip);
        var actor=m.player.GetComponentInChildren<Animator>();var controller=actor.runtimeAnimatorController;var avatar=actor.avatar;
        actor.SetFloat(PlayerCombat.AnimatorParams.Speed,5);actor.Update(.1f);
        var position=m.player.position;var rotation=m.player.rotation;
        int ammo=m.ModernCombat.RoundsInMagazine;float hp=m.ModernHealth.Current;
        var weapons=m.player.GetComponentsInChildren<Weapon>(true).ToDictionary(w=>w,w=>w.gameObject.activeSelf);
        var d=m.GetComponent<Map01RescueCinematic>();if(d==null)d=m.gameObject.AddComponent<Map01RescueCinematic>();
        Assert.IsTrue(d.ShowIntro(rescue,rescue.Squad[0]));
        var input=RouteInputToGame();var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
        var heard=new List<string>();string previous=null;Vector3? left=null,right=null;float deadline=Time.realtimeSinceStartup+120;
        Directory.CreateDirectory("Logs/RescueRadio");
        var pump=m.gameObject.AddComponent<ExtractionCapturePump>();
        try{
            while(d.IsPlaying&&Time.realtimeSinceStartup<deadline){
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W,Key.LeftShift));
                InputSystem.QueueStateEvent(mouse,new MouseState().WithButton(MouseButton.Right));
                Assert.Less(Vector3.Distance(position,m.player.position),.001f,"Held movement cannot move Nam during the radio scene.");
                Assert.Less(Quaternion.Angle(rotation,m.player.rotation),.01f);
                Assert.IsFalse(m.ModernPlayer.enabled||m.ModernCombat.enabled);
                var voice=m.GetComponentsInChildren<DialogueVoice>().FirstOrDefault(v=>!string.IsNullOrEmpty(v.CurrentId));
                var id=voice?.CurrentId;
                if(id!=null&&id!=previous){
                    previous=id;heard.Add(id);
                    if(!skip){string capture=id;pump.draw=()=>{Capture(m.gameCamera,"Logs/RescueRadio/"+capture+".png");};}
                }
                if(d.Elapsed>1){
                    var l=actor.GetBoneTransform(HumanBodyBones.LeftFoot).position;var r=actor.GetBoneTransform(HumanBodyBones.RightFoot).position;
                    if(!left.HasValue){left=l;right=r;}
                    Assert.Less(Vector3.ProjectOnPlane(l-left.Value,Vector3.up).magnitude,.03f,"Left foot remains planted, not running in place.");
                    Assert.Less(Vector3.ProjectOnPlane(r-right.Value,Vector3.up).magnitude,.03f,"Right foot remains planted, not running in place.");
                }
                if(skip&&id=="m1_v2_rescue_approach_02"){d.Skip();break;}
                yield return null;
            }
            Assert.IsFalse(d.IsPlaying);Assert.IsTrue(rescue.IntroSeen);Assert.IsFalse(rescue.Failed||m.Cinematic);
            if(!skip){CollectionAssert.AreEqual(new[]{"m1_v2_rescue_approach_01","m1_v2_rescue_approach_02","m1_v2_rescue_approach_03"},heard);Assert.Greater(d.Elapsed,8);Assert.Less(d.Elapsed,13);}
            Assert.IsFalse(heard.Contains("m1_rescue_intro_nam"));
            Assert.AreSame(controller,actor.runtimeAnimatorController);Assert.AreSame(avatar,actor.avatar);
            Assert.IsTrue(m.ModernPlayer.enabled&&m.ModernCombat.enabled&&m.player.GetComponent<CharacterController>().enabled);
            Assert.IsTrue(m.ModernPlayer.IsSneaking&&m.Crouched,"Natural completion and skip both return Nam to stealth.");Assert.AreEqual(ammo,m.ModernCombat.RoundsInMagazine);Assert.AreEqual(hp,m.ModernHealth.Current);
            foreach(var w in weapons)Assert.AreEqual(w.Value,w.Key.gameObject.activeSelf,"Restore equipped props without changing the inventory.");
            Assert.IsFalse(m.GetComponentsInChildren<DialogueVoice>().Any(v=>v.HasSpeech));
            File.WriteAllLines("Logs/RescueRadio/"+(skip?"skip":"natural")+".txt",heard);
        }finally{Object.Destroy(pump);InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);RestoreInputRouting(input);}
    }
    static void Capture(Camera camera,string path){
        var rt=RenderTexture.GetTemporary(960,540,24);var old=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(960,540,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
        finally{camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(image);}
    }
    [TearDown]public void ResetStorage()=>typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,null);
}
}
