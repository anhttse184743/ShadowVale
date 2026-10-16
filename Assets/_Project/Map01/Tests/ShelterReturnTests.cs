using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class ShelterReturnTests : ForestSceneTestBase
    {
        static readonly FieldInfo Storage=typeof(ForestSaveSlots).GetField("storageRoot",BindingFlags.Static|BindingFlags.NonPublic);
        [UnityTest] public IEnumerator RifleReturnKeepsSteppingThroughTheShelterAndRestoresGameplay()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            yield return Review(WeaponKind.Rifle);yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator KnifeReturnKeepsSteppingThroughTheShelterAndRestoresGameplay()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            yield return Review(WeaponKind.Knife);yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator HungAlreadyBehindWalksAtASafeDistanceAndSettles()
        {
            Map01OpeningCutscene.CancelPending();EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return null;yield return null;
            yield return Review(WeaponKind.Rifle,true);yield return new ExitPlayMode();
        }
        IEnumerator Review(WeaponKind weapon,bool behind=false)
        {
            var mission=Object.FindFirstObjectByType<Map01Mission>();
            var rescue=mission.GetComponent<Map01Rescue>();var quest=mission.GetComponent<Map01Quest>();
            Storage.SetValue(null,Path.GetFullPath("Logs/ShelterWalk/saves/"+Guid.NewGuid().ToString("N")));
            foreach(var guard in mission.Enemies)guard.enabled=false;
            mission.ModernCombat.Equip(weapon);mission.Crouched=false;
            quest.RestoreStage(Map01Quest.EscortStage);
            var character=mission.player.GetComponent<CharacterController>();character.enabled=false;
            var forward=Vector3.ProjectOnPlane(rescue.Layout.shelterRun[1].position-rescue.Layout.shelterDoor.position,Vector3.up).normalized;
            var namStart=behind?rescue.Layout.shelterDoor.position-forward*.3f:rescue.Layout.shelterDoor.position+Vector3.left*.7f;
            mission.player.SetPositionAndRotation(namStart,Quaternion.identity);
            character.enabled=true;mission.ModernPlayer.RestoreMotion(true);
            var hungStart=behind?rescue.Layout.shelterDoor.position-forward*2.1f:rescue.Layout.shelterDoor.position;
            Assert.IsTrue(NavMesh.SamplePosition(hungStart,out var startHit,.7f,NavMesh.AllAreas));
            mission.hung.GetComponent<NavMeshAgent>().Warp(startHit.position);
            Physics.SyncTransforms();
            yield return WaitGameSeconds(.55f);
            var cinematic=mission.GetComponent<Map01RescueCinematic>();
            Assert.NotNull(cinematic);Assert.AreEqual(Map01RescueCinematic.Mode.Shelter,cinematic.CurrentMode);
            var actor=mission.player.GetComponentInChildren<Animator>();
            var left=actor.GetBoneTransform(HumanBodyBones.LeftFoot);var right=actor.GetBoneTransform(HumanBodyBones.RightFoot);
            var hungActor=mission.hung.GetComponentInChildren<Animator>();
            var hungLeft=hungActor.GetBoneTransform(HumanBodyBones.LeftFoot);var hungRight=hungActor.GetBoneTransform(HumanBodyBones.RightFoot);
            var controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Art/Characters/Animations/Controllers/AC_Player.controller");
            int ammo=mission.ModernCombat.RoundsInMagazine,supplies=mission.GetComponent<Map01Inventory>().Count("supplies");
            string folder="Logs/ShelterWalk/"+(behind?"Following":weapon.ToString());Directory.CreateDirectory(folder+"/frames");Directory.CreateDirectory(folder+"/close");
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;var mode=pipeline.GetType().GetProperty("gpuResidentDrawerMode");var previous=mode.GetValue(pipeline);
            mode.SetValue(pipeline,Enum.ToObject(mode.PropertyType,0));ShaderUtil.allowAsyncCompilation=false;
            var rows=new List<string>{"frame,time,distance,footSeparation,hungFootSeparation,hungHeadHeight,bodySpacing,speed"};
            var cameraReview=new List<string>();
            float nearMin=float.PositiveInfinity,nearMax=float.NegativeInfinity,insideMin=nearMin,insideMax=nearMax,walked=0;
            float hungMin=nearMin,hungMax=nearMax,hungHeight=nearMin;
            float spacing=nearMin,initialSpacing=nearMin,cameraClearance=nearMin,maxSpeed=0;int interiorSamples=0;
            int frame=0;var lastPosition=mission.player.position;
            var pump=mission.gameObject.AddComponent<ExtractionCapturePump>();
            try {
                Time.captureDeltaTime=1f/30;
                float deadline=Time.time+35;
                while(cinematic.IsPlaying&&Time.time<deadline){
                    Assert.IsFalse(mission.hung.GetComponentInChildren<Map01HungVisual>().RopeVisible,"A freed escort must not carry the captive rope into the shelter.");
                    bool done=false;pump.draw=()=>{
                        float step=Vector3.Distance(lastPosition,mission.player.position);lastPosition=mission.player.position;walked+=step;
                        float separation=mission.player.InverseTransformPoint(left.position).z-mission.player.InverseTransformPoint(right.position).z;
                        float hungSeparation=mission.hung.InverseTransformPoint(hungLeft.position).z-mission.hung.InverseTransformPoint(hungRight.position).z;
                        float headHeight=hungActor.GetBoneTransform(HumanBodyBones.Head).position.y-mission.hung.position.y;
                        float bodySpacing=Vector3.ProjectOnPlane(actor.GetBoneTransform(HumanBodyBones.Hips).position-hungActor.GetBoneTransform(HumanBodyBones.Hips).position,Vector3.up).magnitude;
                        float speed=step/Mathf.Max(.001f,Time.deltaTime);maxSpeed=Mathf.Max(maxSpeed,speed);
                        initialSpacing=Mathf.Min(initialSpacing,bodySpacing);
                        if(mission.player.position.y<.6f&&Vector3.Distance(mission.player.position,rescue.Layout.reportPoint.position)<8){
                            float namLens=Vector3.Distance(mission.gameCamera.transform.position,actor.GetBoneTransform(HumanBodyBones.Hips).position);
                            float hungLens=Vector3.Distance(mission.gameCamera.transform.position,hungActor.GetBoneTransform(HumanBodyBones.Hips).position);
                            cameraClearance=Mathf.Min(cameraClearance,Mathf.Min(namLens,hungLens));
                        }
                        if(speed>.1f||mission.player.position.y<.6f)spacing=Mathf.Min(spacing,bodySpacing);
                        rows.Add($"{frame},{cinematic.Elapsed:F4},{walked:F4},{separation:F4},{hungSeparation:F4},{headHeight:F4},{bodySpacing:F4},{speed:F4}");
                        if(frame%15==0){
                            var focus=mission.player.position+Vector3.up;var eye=rescue.Layout.reportPoint.position+rescue.Layout.interiorCameraOffset;var delta=eye-focus;
                            cameraReview.Add($"{frame}: Nam={mission.player.position} Hung={mission.hung.position} camera={mission.gameCamera.transform.position} roomEye={eye}; "+string.Join(";",Array.ConvertAll(Physics.SphereCastAll(focus,.18f,delta.normalized,delta.magnitude,mission.ObstructionMask,QueryTriggerInteraction.Ignore),h=>$"{h.transform.name}@{h.distance:F2}")));
                        }
                        if(step>.005f&&mission.player.position.y>=.6f){nearMin=Mathf.Min(nearMin,separation);nearMax=Mathf.Max(nearMax,separation);}
                        if(step>.005f&&mission.player.position.y<.6f){insideMin=Mathf.Min(insideMin,separation);insideMax=Mathf.Max(insideMax,separation);interiorSamples++;}
                        if(cinematic.Elapsed>1f&&cinematic.IsPlaying){hungMin=Mathf.Min(hungMin,hungSeparation);hungMax=Mathf.Max(hungMax,hungSeparation);hungHeight=Mathf.Min(hungHeight,headHeight);}
                        if(frame%3==0)Capture(mission.gameCamera,folder+"/frames/"+frame.ToString("D4")+".png");
                        if(frame%15==0&&cinematic.Elapsed>3.5f)CaptureClose(mission,folder+"/close/"+frame.ToString("D4")+".png");
                        frame++;done=true;
                    };yield return new WaitUntil(()=>done);
                }
                File.WriteAllLines(folder+"/motion.csv",rows);
                File.WriteAllLines(folder+"/camera.txt",cameraReview);
                File.WriteAllText(folder+"/metrics.txt",$"Travel {walked:F3}m; approach foot excursion {nearMax-nearMin:F3}m; interior excursion {insideMax-insideMin:F3}m; Hung foot excursion {hungMax-hungMin:F3}m; minimum Hung head height {hungHeight:F3}m; minimum rendered body separation while Nam moves {spacing:F3}m; minimum including yielding {initialSpacing:F3}m; camera body clearance {cameraClearance:F3}m; maximum speed {maxSpeed:F3}m/s; duration {cinematic.Elapsed:F3}s.");
                Assert.IsFalse(cinematic.IsPlaying,"Both walkers must reach their stopping points without blocking one another.");
                Assert.LessOrEqual(maxSpeed,1.5f,"This is a walking return, not running.");
                Assert.GreaterOrEqual(spacing,1.05f,"Rendered bodies must stay separated, not merely the transform roots.");
                Assert.GreaterOrEqual(initialSpacing,.75f,"The opening yield must not move either model through the other.");
                Assert.GreaterOrEqual(cameraClearance,1.5f,"The room camera must stand aside from the walking route, not be crossed by a body.");
                Assert.Greater(interiorSamples,20);
                Assert.Greater(walked,4,"The actual shelter route must be traversed.");
                Assert.Greater(nearMax-nearMin,.18f,"Feet must keep alternating on the approach, rather than a fixed pose sliding.");
                Assert.Greater(insideMax-insideMin,.18f,"The walk cycle must continue after entering the shelter.");
                Assert.Greater(hungMax-hungMin,.18f,"Hung must walk rather than slide in the captive pose.");
                Assert.Greater(hungHeight,1.05f,"Hung must be upright throughout the shelter walk, not kneeling.");
                Assert.IsFalse(hungActor.GetCurrentAnimatorStateInfo(0).IsName(Map01HungVisual.CaptiveState),"Handoff must not flash the default captive state.");
                Assert.IsFalse(hungActor.GetBool("Captive"));Assert.IsFalse(mission.hung.GetComponentInChildren<Map01HungVisual>().RopeVisible);
                CaptureClose(mission,folder+"/settled.png");
                Assert.AreEqual(Map01Quest.BriefingStage,quest.Stage);Assert.IsFalse(mission.Cinematic);
                Assert.IsTrue(rescue.RewardDelivered);Assert.AreEqual(supplies+1,mission.GetComponent<Map01Inventory>().Count("supplies"));
                Assert.IsFalse(rescue.CompleteDelivery());Assert.AreEqual(ammo,mission.ModernCombat.RoundsInMagazine);
                Assert.AreEqual(weapon,mission.ModernCombat.EquippedKind);
                Assert.AreSame(controller,actor.runtimeAnimatorController);Assert.IsFalse(actor.applyRootMotion);
                Assert.IsTrue(mission.ModernPlayer.enabled&&mission.ModernCombat.enabled&&character.enabled);
                Assert.Less(Vector3.Distance(mission.player.position,rescue.Layout.reportPoint.position),.15f);
            } finally {Time.captureDeltaTime=0;mode.SetValue(pipeline,previous);Object.Destroy(pump);Storage.SetValue(null,null);}
        }
        static void Capture(Camera camera,string path)
        {
            var texture=RenderTexture.GetTemporary(960,540,24);var old=camera.targetTexture;var active=RenderTexture.active;var image=new Texture2D(960,540,TextureFormat.RGB24,false);
            try {camera.targetTexture=texture;camera.Render();RenderTexture.active=texture;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally {camera.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(texture);Object.Destroy(image);}
        }
        static void CaptureClose(Map01Mission mission,string path)
        {
            var camera=mission.gameCamera;var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
            var focus=Vector3.Lerp(mission.player.position,mission.hung.position,.5f)+Vector3.up*.8f;
            var eye=focus-mission.player.forward*2.8f-mission.player.right*1.4f+Vector3.up*.35f;
            try {camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(focus-eye));camera.fieldOfView=58;Capture(camera,path);}
            finally {camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;}
        }
        [TearDown]public void ResetReview(){Time.captureDeltaTime=0;Storage.SetValue(null,null);}
    }
}
