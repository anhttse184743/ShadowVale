using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace ShadowVale.Map01.Tests {
public sealed class RescueDefianceTests:ForestSceneTestBase {
    static Map01Mission M=>Object.FindFirstObjectByType<Map01Mission>();
    static IEnumerator Warm(){
        float end=Time.realtimeSinceStartup+90;
        while(Time.realtimeSinceStartup<end&&(M==null||!M.IsInitialized||M.ModernPlayer==null||M.GetComponent<Map01Rescue>().Squad.Count!=4))yield return null;
        Assert.NotNull(M);Assert.IsTrue(M.IsInitialized);
        foreach(var g in M.Enemies)g.enabled=false;
        M.GetComponent<Map01Quest>().RestoreStoryStage(Map01Quest.RescueStage,RescueOrdered());
        var cc=M.player.GetComponent<CharacterController>();cc.enabled=false;M.player.position=M.GetComponent<Map01Rescue>().RetryPoint;cc.enabled=true;yield return null;
    }
    [UnityTest,Timeout(600000)]public IEnumerator EnemyFirstPanThenNamAndDefiantLastWordsBeforeDeath(){
        Type.GetType("ShadowVale.Map01.Editor.InterrogationMotionSetup, ShadowVale.Map01.Editor").GetMethod("Prepare").Invoke(null,null);
        Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return Warm();var m=M;var r=m.GetComponent<Map01Rescue>();var d=m.GetComponent<Map01RescueCinematic>()??m.gameObject.AddComponent<Map01RescueCinematic>();
        Assert.IsTrue(d.ShowIntro(r,r.Squad[0]));
        yield return Record("Approach",d,false);
        Assert.IsTrue(m.Crouched&&m.ModernPlayer.IsSneaking);Assert.IsFalse(r.Failed);
        yield return WaitGameSeconds(1.1f);r.ReportDetection(new Map01Detection(r.Squad[1],Map01DetectionCause.Sight,m.player.position));
        Assert.AreEqual(Map01Rescue.Phase.Alarm,r.CurrentPhase);
        yield return Record("Execution",d,true);
        Assert.IsTrue(r.Failed);Assert.AreEqual(10,d.ExecutionRounds);
        Assert.IsFalse(m.Cinematic);Assert.IsTrue(m.ModernCombat.enabled&&m.ModernPlayer.enabled);
        yield return new ExitPlayMode();
    }
    static IEnumerator Record(string name,Map01RescueCinematic d,bool execution){
        var m=M;var r=m.GetComponent<Map01Rescue>();var h=m.hung.GetComponentInChildren<Map01HungVisual>();
        var folder="Logs/InterrogationBodyCombo/"+name;Directory.CreateDirectory(folder);
        var pump=m.gameObject.AddComponent<ExtractionCapturePump>();
        var rows=new List<string>{"time,voice,rounds,contact,headY,leftContact,rightContact,hits"};var voices=new List<string>();string previous=null;int oldRounds=0;
        int frame=0,captured=0;float nextCapture=Time.time;bool contactCaptured=false,rightCaptured=false;float lowestLeftHead=99,lowestRightHead=99;bool lastWords=false,reaction=false;float start=Time.time,deadline=Time.realtimeSinceStartup+420;
        var original=m.gameCamera.transform.position;
        try {
            Time.captureDeltaTime=0;
            while(d.IsPlaying&&Time.realtimeSinceStartup<deadline){
                bool done=false;Exception error=null;
                pump.draw=()=>{try{
                    if(!d.IsPlaying)return;
                    var v=m.GetComponentsInChildren<DialogueVoice>().FirstOrDefault(x=>!string.IsNullOrEmpty(x.CurrentId));
                    if(v?.CurrentId!=null&&v.CurrentId!=previous){previous=v.CurrentId;voices.Add(previous);}
                    Assert.IsTrue(h.RopeVisible);
                    if(!execution&&v?.CurrentId?.StartsWith("m1_v2_rescue_approach_")==true){
                        Assert.IsFalse(d.IntroShowsCaptive);Assert.Less(Vector3.Distance(original,m.gameCamera.transform.position),.04f,"Nam speaks only back in his view.");
                    }
                    if(execution&&d.HungSpeakingLastWords){lastWords=true;Assert.IsFalse(r.Failed);Assert.IsTrue(d.ExecutionShowsCaptive);Assert.AreEqual(10,d.ExecutionRounds);Assert.Less(h.actor.GetBoneTransform(HumanBodyBones.Head).position.y-m.hung.position.y,.85f,"Hung must already have collapsed before speaking.");}
                    if(execution&&d.ExecutionShowsReaction){reaction=true;Assert.IsTrue(r.Failed);}
                    if(execution&&d.ExecutionShowsCaptive){
                        var p=m.gameCamera.WorldToViewportPoint(h.actor.GetBoneTransform(HumanBodyBones.Head).position);
                        Assert.Greater(p.z,0);Assert.That(p.x,Is.InRange(.05f,.95f));Assert.That(p.y,Is.InRange(.05f,.95f));
                    }
                    if(execution&&d.ExecutionRounds!=oldRounds){Assert.AreEqual(oldRounds+1,d.ExecutionRounds,"No catch-up burst in one frame.");oldRounds=d.ExecutionRounds;}
                    if(!execution&&(d.Elapsed<1.7f||d.Elapsed>=7.3f&&d.Elapsed<9.1f)){
                        var guard=r.Squad[0].GetComponentInChildren<Animator>();
                        Assert.Greater(guard.GetBoneTransform(HumanBodyBones.Hips).position.y-r.Squad[0].transform.position.y,.75f,"Stow is upper-body only: the boat's sitting legs must not enter the scene.");
                    }
                    if(!execution&&d.Elapsed>=2.6f&&d.Elapsed<9.1f){
                        if(d.Elapsed<7.3f){Assert.IsTrue(d.InterrogatorRifleSlung,"Both hands are free while punching.");Assert.Less(d.InterrogatorRifleBehindPack,-.12f,"The gun is secured beside the rear pack, not in front of the chest.");}
                        var guard=r.Squad[0].GetComponentInChildren<Animator>();var weapon=guard.GetComponent<Map01Rifle>().weapon;
                        Assert.IsTrue(weapon.transform.parent==guard.GetBoneTransform(HumanBodyBones.Chest)||weapon.transform.parent==guard.GetBoneTransform(HumanBodyBones.RightHand));
                        foreach(var bone in new[]{HumanBodyBones.Head,HumanBodyBones.Neck,HumanBodyBones.Chest}){
                            var point=h.actor.GetBoneTransform(bone).position;var a=weapon.transform.position;var delta=weapon.Muzzle.position-a;
                            var closest=a+delta*Mathf.Clamp01(Vector3.Dot(point-a,delta)/Mathf.Max(.00001f,delta.sqrMagnitude));
                            Assert.Greater(Vector3.Distance(point,closest),.17f,"The rifle must stay outside Hung's head and torso: "+bone);
                        }
                        Assert.Greater(weapon.Muzzle.position.y-m.hung.position.y,.15f,"Keep the muzzle out of the floor.");
                    }
                    if(!execution){
                        float height=h.actor.GetBoneTransform(HumanBodyBones.Head).position.y-m.hung.position.y;
                        if(d.Elapsed>4.05f&&d.Elapsed<4.55f)lowestLeftHead=Mathf.Min(lowestLeftHead,height);
                        if(d.Elapsed>5.35f&&d.Elapsed<5.85f)lowestRightHead=Mathf.Min(lowestRightHead,height);
                        bool left=!contactCaptured&&d.LeftBodyContactError>=0&&d.Elapsed<4.6f;
                        bool right=!rightCaptured&&d.RightBodyContactError>=0&&d.Elapsed<6.05f;
                        if(left||right){
                            if(left)contactCaptured=true;else rightCaptured=true;
                            var before=m.gameCamera.transform.position;var rotation=m.gameCamera.transform.rotation;float fov=m.gameCamera.fieldOfView;
                            var focus=Vector3.Lerp(h.actor.GetBoneTransform(HumanBodyBones.Hips).position,h.actor.GetBoneTransform(HumanBodyBones.Chest).position,.4f);
                            try{int view=0;foreach(var angle in new[]{new Vector3(1.65f,.65f,1.65f),new Vector3(-1.65f,.65f,-1.3f)}){
                                var lens=focus+m.hung.rotation*angle;m.gameCamera.transform.SetPositionAndRotation(lens,Quaternion.LookRotation(focus-lens));m.gameCamera.fieldOfView=43;CaptureFrame(m.gameCamera,folder+"/"+(left?"left":"right")+"-contact-"+(view++)+".png");
                            }}finally{m.gameCamera.transform.SetPositionAndRotation(before,rotation);m.gameCamera.fieldOfView=fov;}
                        }
                    }
                    if(Time.time>=nextCapture){
                        nextCapture=Time.time+1f/15;frame++;
                        CaptureFrame(m.gameCamera,folder+"/"+(captured++).ToString("D4")+".png");
                        rows.Add((Time.time-start).ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+","+(v?.CurrentId??"")+","+d.ExecutionRounds+","+d.InterrogationContactError+","+(h.actor.GetBoneTransform(HumanBodyBones.Head).position.y-m.hung.position.y)+","+d.LeftBodyContactError+","+d.RightBodyContactError+","+d.InterrogationHits);
                    }
                }catch(Exception e){error=e;}finally{done=true;}};
                while(!done&&Time.realtimeSinceStartup<deadline)yield return null;
                Assert.IsTrue(done);if(error!=null)throw error;
            }
            Assert.IsFalse(d.IsPlaying);
            if(execution){Assert.IsTrue(lastWords&&reaction);CollectionAssert.Contains(voices,"m1_rescue_hung_defiance");}
            else {
                CollectionAssert.AreEqual(new[]{"m1_rescue_interrogate","m1_story_11","m1_v2_rescue_approach_01","m1_v2_rescue_approach_02","m1_v2_rescue_approach_03"},voices);
                Assert.AreEqual(2,d.InterrogationHits);
                Assert.That(d.LeftBodyContactError,Is.InRange(0,.04f),"Left fist must contact the belly: "+d.LeftBodyDelta);
                Assert.That(d.RightBodyContactError,Is.InRange(0,.04f),"Right fist must contact the belly: "+d.RightBodyDelta);
                Assert.Less(lowestLeftHead,1.20f,"Hung folds after the left body shot.");Assert.Less(lowestRightHead,1.20f,"Hung folds again after the right body shot.");
            }
        }finally{Time.captureDeltaTime=0;Object.Destroy(pump);File.WriteAllLines(folder+"/frames.csv",rows);File.WriteAllLines(folder+"/voices.txt",voices);}
    }
    [UnityTest,Timeout(300000)]public IEnumerator NativeInterrogationCancelAndSkipRestoreWeaponAtEveryStage(){
        Type.GetType("ShadowVale.Map01.Editor.InterrogationMotionSetup, ShadowVale.Map01.Editor").GetMethod("Prepare").Invoke(null,null);
        Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return Warm();
        var m=M;var r=m.GetComponent<Map01Rescue>();var d=m.GetComponent<Map01RescueCinematic>()??m.gameObject.AddComponent<Map01RescueCinematic>();
        var actor=r.Squad[0].GetComponentInChildren<Animator>();var gun=actor.GetComponent<Map01Rifle>().weapon.transform;var parent=gun.parent;
        var controller=actor.runtimeAnimatorController;var avatar=actor.avatar;
        var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();var routing=RouteInputToGame();
        try{
        foreach(float at in new[]{.9f,4.1f,5.4f,8.1f}){
            Assert.IsTrue(d.ShowIntro(r,r.Squad[0]));while(d.Elapsed<at)yield return null;
            var camera=m.gameCamera;var before=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            try{
                var focus=actor.GetBoneTransform(HumanBodyBones.Chest).position;var lens=focus+r.Squad[0].transform.forward*2+r.Squad[0].transform.right*1.5f+Vector3.up*.2f;
                camera.transform.SetPositionAndRotation(lens,Quaternion.LookRotation(focus-lens));camera.fieldOfView=45;Directory.CreateDirectory("Logs/InterrogationBodyCombo/Stages");CaptureFrame(camera,"Logs/InterrogationBodyCombo/Stages/"+at.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png");
            }finally{camera.transform.SetPositionAndRotation(before,rotation);camera.fieldOfView=fov;}
            d.CancelForRestore();yield return null;
            Assert.AreSame(parent,gun.parent);Assert.AreSame(controller,actor.runtimeAnimatorController);Assert.AreSame(avatar,actor.avatar);
            Assert.IsFalse(m.Cinematic||d.InterrogatorRifleSlung);Assert.IsFalse(m.GetComponentsInChildren<LineRenderer>().Any(x=>x.name=="Interrogator canvas sling"));
        }
        Assert.IsTrue(d.ShowIntro(r,r.Squad[0]));yield return WaitGameSeconds(3.7f);d.Skip();yield return null;
        Assert.AreSame(parent,gun.parent);Assert.IsTrue(m.Crouched&&m.ModernPlayer.IsSneaking);Assert.IsFalse(r.Failed||m.Cinematic);
        }finally{UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);RestoreInputRouting(routing);}
        yield return new ExitPlayMode();
    }
    static void CaptureFrame(Camera camera,string path){
        var rt=RenderTexture.GetTemporary(960,540,24);var before=camera.targetTexture;var active=RenderTexture.active;var texture=new Texture2D(960,540,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally{camera.targetTexture=before;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.Destroy(texture);}
    }
    [UnityTest,Timeout(240000)]public IEnumerator SkipAfterFirstBurstStillCommitsFailureAndCancelDoesNot(){
        Type.GetType("ShadowVale.Map01.Editor.InterrogationMotionSetup, ShadowVale.Map01.Editor").GetMethod("Prepare").Invoke(null,null);
        Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");yield return new EnterPlayMode();yield return Warm();var m=M;var r=m.GetComponent<Map01Rescue>();var d=m.GetComponent<Map01RescueCinematic>()??m.gameObject.AddComponent<Map01RescueCinematic>();r.MarkIntroSeen();
        r.ReportDetection(new Map01Detection(r.Squad[1],Map01DetectionCause.Sight,m.player.position));
        while(d.ExecutionRounds<2)yield return null;
        Time.timeScale=0;yield return null;float elapsed=d.Elapsed;int rounds=d.ExecutionRounds;yield return new WaitForSecondsRealtime(.2f);Assert.AreEqual(elapsed,d.Elapsed);Assert.AreEqual(rounds,d.ExecutionRounds);Time.timeScale=1;
        d.CancelForRestore();Assert.IsFalse(r.Failed);Assert.IsFalse(m.Cinematic);
        d.ExecuteHostage(r,r.Squad[0]);while(d.ExecutionRounds<2)yield return null;
        d.Skip();Assert.IsTrue(r.Failed);Assert.IsFalse(m.Cinematic);Assert.IsFalse(m.GetComponentsInChildren<DialogueVoice>().Any(v=>v.HasSpeech));
        yield return new ExitPlayMode();
    }
}
}
