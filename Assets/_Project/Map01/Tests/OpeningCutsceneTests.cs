using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace ShadowVale.Map01.Tests
{
    public sealed class OpeningCutsceneTests : ForestSceneTestBase
    {
        [Test]
        public void AssetsUseExistingIdleAndHumanoidClips()
        {
            var asset = Resources.Load<Map01OpeningCutscene>("Cutscenes/Map01Opening");
            Assert.IsNotNull(asset);
            Assert.IsTrue(asset.salute.humanMotion);
            Assert.IsTrue(asset.talking.humanMotion);
            Assert.IsTrue(asset.pointing.humanMotion);
            Assert.AreEqual("Assets/_Project/Art/Characters/Animations/Generated/Idle_Tuned.anim",
                UnityEditor.AssetDatabase.GetAssetPath(asset.idle));
            Assert.IsNotNull(asset.commanderVoice); Assert.IsNotNull(asset.soldierVoice);
            Assert.IsFalse(File.Exists("Assets/_Project/Map01/Resources/Cutscenes/Intro.mp4"));
        }
        [UnityTest]
        public IEnumerator AuthoredSaluteSweepsEveryBakedFrame() {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            Map01OpeningCutscene.CancelPending();yield return new EnterPlayMode();yield return null;
            var mission=Object.FindFirstObjectByType<Map01Mission>();
            mission.Cinematic=true;
            mission.ModernPlayer.enabled=false;mission.ModernCombat.enabled=false;
            var actor=mission.player.GetComponentInChildren<Animator>();actor.runtimeAnimatorController=null;actor.applyRootMotion=false;actor.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())skin.updateWhenOffscreen=true;
            foreach(var gun in mission.player.GetComponentsInChildren<ShadowVale.Gameplay.Combat.Weapon>(true))
                foreach(var renderer in gun.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            var asset=Resources.Load<Map01OpeningCutscene>("Cutscenes/Map01Opening");
            var type=typeof(Map01OpeningCutscene).GetNestedType("ClipPlayer",System.Reflection.BindingFlags.NonPublic);
            var pose=System.Activator.CreateInstance(type,new object[]{actor,false,new[]{asset.soldierIdle,asset.salute}});
            var sample=type.GetMethod("SampleSelected");
            type.GetMethod("Select").Invoke(pose,new object[]{1});type.GetMethod("Tick").Invoke(pose,new object[]{1f});
            var view=new GameObject("Salute frame review").AddComponent<Camera>();view.CopyFrom(mission.gameCamera);view.enabled=false;view.fieldOfView=48;
            var originalHand=actor.GetBoneTransform(HumanBodyBones.RightHand).position;
            Directory.CreateDirectory("Logs/Cutscene/sweep-front");Directory.CreateDirectory("Logs/Cutscene/sweep-side");
            float previousElbowHeight=float.MaxValue,previousHandHeight=float.MaxValue;
            float previousBend=-1,previousRaiseHeight=float.MinValue;
            try {
                for(int frame=0;frame<=Mathf.RoundToInt(asset.salute.length*30);frame++) {
                    sample.Invoke(pose,new object[]{frame/30f});yield return null;sample.Invoke(pose,new object[]{frame/30f});VerifyWrist(actor,40);
                    if(frame==0 || frame==Mathf.RoundToInt(asset.salute.length*30))VerifyStraightArm(actor);
                    if(frame<=21) {
                        float bend=ElbowBend(actor),height=actor.GetBoneTransform(HumanBodyBones.RightHand).position.y;
                        Assert.GreaterOrEqual(bend,previousBend-3f,"The elbow must flex progressively as the arm rises.");
                        Assert.GreaterOrEqual(height,previousRaiseHeight-.01f,"The hand must rise continuously toward the temple.");
                        previousBend=bend;previousRaiseHeight=height;
                    }
                    if(frame>=27) {
                        float elbow=actor.GetBoneTransform(HumanBodyBones.RightLowerArm).position.y;
                        float hand=actor.GetBoneTransform(HumanBodyBones.RightHand).position.y;
                        Assert.LessOrEqual(elbow,previousElbowHeight+.01f,"The elbow must descend continuously when lowering the salute.");
                        Assert.LessOrEqual(hand,previousHandHeight+.02f,"The hand must descend with the elbow, without rising again.");
                        previousElbowHeight=elbow;previousHandHeight=hand;
                    }
                    if(frame==21)Assert.Greater(actor.GetBoneTransform(HumanBodyBones.RightHand).position.y-originalHand.y,.35f,"The rendered sweep must actually raise the hand.");
                    var focus=actor.GetBoneTransform(HumanBodyBones.Head).position-Vector3.up*.28f;
                    foreach(var side in new[]{"front","side"}) {
                        var eye=focus+(side=="front"?mission.player.forward*1.6f+mission.player.right*.25f:mission.player.right*1.6f+mission.player.forward*.3f)+Vector3.up*.15f;
                        view.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(focus-eye));
                        Capture(view,"Logs/Cutscene/sweep-"+side+"/"+frame.ToString("D4")+".png");
                    }
                }
            } finally {type.GetMethod("Dispose").Invoke(pose,null);Object.Destroy(view.gameObject);}
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator BriefingIsUnarmedThroughSkipAndRestoresGameplayWeapons()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_MainMenu.unity");
            yield return new EnterPlayMode();
            Map01OpeningCutscene.RequestNewGame();Map01SaveSystem.BeginGame(-1);
            yield return null;yield return null;
            var opening=Object.FindFirstObjectByType<Map01OpeningCutscene>();
            var mission=Object.FindFirstObjectByType<Map01Mission>();
            Assert.IsTrue(opening.IsPlaying);
            AssertBriefingUnarmed(opening,mission);
            opening.Skip();
            var deadline=Time.realtimeSinceStartupAsDouble+15;
            while(opening.IsPlaying && Time.realtimeSinceStartupAsDouble<deadline) {
                AssertBriefingUnarmed(opening,mission);yield return null;
            }
            Assert.IsFalse(opening.IsPlaying);
            AssertGameplayWeaponRestored(mission);
            AssertCommanderUnarmed(opening);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator FullBriefingSalutesAndReturnsControlWithoutChangingQuest()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_MainMenu.unity");
            yield return new EnterPlayMode();
            Map01OpeningCutscene.RequestNewGame();
            Map01SaveSystem.BeginGame(-1);
            yield return null; yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var opening = Object.FindFirstObjectByType<Map01OpeningCutscene>();
            Assert.IsTrue(opening.IsPlaying);
            Assert.IsFalse(mission.CameraInputEnabled);
            Assert.IsTrue(mission.Stopped);
            Assert.IsFalse(mission.GetComponent<Map01SaveSystem>().CanSave());
            var start = mission.player.position;
            double poseDeadline = Time.realtimeSinceStartupAsDouble + 20;
            while (opening.BriefingTime < 2.5f && Time.realtimeSinceStartupAsDouble < poseDeadline) {
                AssertBriefingUnarmed(opening,mission);yield return null;
            }
            Assert.IsFalse(opening.IsIntroducing);
            var actor = opening.GetComponentsInChildren<Animator>()[0];
            var hand = actor.GetBoneTransform(HumanBodyBones.RightHand);
            var shoulder = actor.GetBoneTransform(HumanBodyBones.RightUpperArm);
            Assert.Greater(hand.position.y - shoulder.position.y, -.25f, "Pointing must raise the hand, not be hidden by idle.");
            var nam = mission.player.GetComponentInChildren<Animator>();
            Debug.Log("SALUTE_RUNTIME_BONES "+nam.GetComponentsInChildren<Transform>().Length+" middle="+nam.GetBoneTransform(HumanBodyBones.RightMiddleDistal));
            Assert.IsNull(nam.runtimeAnimatorController, "Gameplay stance must not override the briefing.");
            VerifyStraightArm(nam);
            Assert.Less(nam.GetBoneTransform(HumanBodyBones.RightHand).position.y,
                nam.GetBoneTransform(HumanBodyBones.RightUpperArm).position.y - .3f, "Nam must rest his right arm during briefing.");
            Assert.Less(nam.GetBoneTransform(HumanBodyBones.LeftHand).position.y,
                nam.GetBoneTransform(HumanBodyBones.LeftUpperArm).position.y - .3f, "Nam must rest his left arm during briefing.");
            Capture(mission.gameCamera, "Logs/Cutscene/wide.png");
            yield return RecordSalute(opening,mission.gameCamera);
            while (opening.BriefingTime < opening.SaluteBeginsAt + .85f) {
                AssertBriefingUnarmed(opening,mission);yield return null;
            }
            Assert.Less(Vector3.Distance(nam.GetBoneTransform(HumanBodyBones.RightHand).position,
                nam.GetBoneTransform(HumanBodyBones.Head).position), .27f, "Salute must bring the right hand to the forehead.");
            Assert.Less(nam.GetBoneTransform(HumanBodyBones.LeftHand).position.y,
                nam.GetBoneTransform(HumanBodyBones.LeftUpperArm).position.y - .3f, "The other arm must remain down during salute.");
            Capture(mission.gameCamera, "Logs/Cutscene/salute.png");
            VerifyWrist(nam);
            using(var poseHandler=new HumanPoseHandler(nam.avatar,nam.transform)) {
                var pose=new HumanPose();poseHandler.GetHumanPose(ref pose);
                foreach(var name in new[]{"Right Index 1 Stretched","Right Index 2 Stretched","Right Middle 1 Stretched","Right Middle 2 Stretched"})
                    Assert.Greater(pose.muscles[System.Array.IndexOf(HumanTrait.MuscleName,name)],.85f,"The salute must extend fingers: "+name);
            }
            yield return RecordSaluteEnding(opening,mission.gameCamera);
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while (opening.IsPlaying && Time.realtimeSinceStartupAsDouble < deadline) {
                AssertBriefingUnarmed(opening,mission);yield return null;
            }
            Assert.IsFalse(opening.IsPlaying);
            Assert.IsFalse(Map01OpeningCutscene.Active);
            Assert.IsNotNull(nam.runtimeAnimatorController);
            Assert.IsTrue(mission.ModernPlayer.enabled);
            Assert.IsTrue(mission.ModernCombat.enabled);
            AssertGameplayWeaponRestored(mission);
            AssertCommanderUnarmed(opening);
            Assert.IsTrue(mission.CameraInputEnabled);
            Assert.AreEqual(Map01Quest.RescueStage, mission.Stage);
            Assert.Less(Vector3.Distance(start, mission.player.position), .3f);
            Assert.IsTrue(mission.GetComponent<Map01SaveSystem>().CanSave());
            Capture(mission.gameCamera, "Logs/Cutscene/gameplay.png");
            // Exercise the river from a low camera angle: body must remain tinted, not sliced.
            mission.ModernPlayer.enabled = false;
            var controller = mission.player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            float riverZ = 35;
            float riverX = mission.riverOffset + mission.riverAmplitude1 * Mathf.Sin(riverZ * mission.riverFrequency1)
                + mission.riverAmplitude2 * Mathf.Sin(riverZ * mission.riverFrequency2);
            mission.player.position = new Vector3(riverX, -.7f, riverZ);
            yield return null;
            Assert.IsTrue(mission.IsWading);
            var rig = mission.gameCamera.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>();
            Assert.IsTrue(rig.SurfaceCameraFloor.Invoke().HasValue);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            typeof(ShadowVale.Gameplay.Player.ThirdPersonCamera).GetField("_pitch", flags).SetValue(rig, -25f);
            yield return null; yield return null;
            Assert.GreaterOrEqual(mission.gameCamera.transform.position.y, rig.SurfaceCameraFloor.Invoke().Value - .01f);
            typeof(ShadowVale.Gameplay.Player.ThirdPersonCamera).GetField("_pitch", flags).SetValue(rig, 12f);
            typeof(ShadowVale.Gameplay.Player.ThirdPersonCamera).GetField("_yaw", flags).SetValue(rig, 90f);
            yield return WaitGameSeconds(.5f);
            Capture(mission.gameCamera, "Logs/Cutscene/wading.png");
            if (controller != null) controller.enabled = true;
            mission.ModernPlayer.enabled = true;
            yield return new ExitPlayMode();
        }
        private static IEnumerator RecordSalute(Map01OpeningCutscene opening,Camera camera) {
            Directory.CreateDirectory("Logs/Cutscene/salute-frames");
            var events=new System.Collections.Generic.List<string>();int frame=0;
            while(opening.BriefingTime<opening.SaluteBeginsAt-.15f) {
                AssertBriefingUnarmed(opening,Object.FindFirstObjectByType<Map01Mission>());yield return null;
            }
            var pump=opening.gameObject.AddComponent<ExtractionCapturePump>();
            while(opening.BriefingTime<opening.SaluteBeginsAt+.85f) {
                bool rendered=false;float at=opening.BriefingTime;
                pump.draw=()=>{CaptureSaluteViews(opening,camera,frame);rendered=true;};
                yield return new WaitUntil(()=>rendered);
                events.Add(frame+","+at);frame++;yield return WaitGameSeconds(.05f);
            }
            Object.Destroy(pump);File.WriteAllLines("Logs/Cutscene/salute-frames.csv",events);
        }
        private static IEnumerator RecordSaluteEnding(Map01OpeningCutscene opening,Camera camera) {
            var events=new System.Collections.Generic.List<string>();int frame=1000;
            var pump=opening.gameObject.AddComponent<ExtractionCapturePump>();
            while(opening.IsPlaying) {
                bool rendered=false;float at=opening.BriefingTime;
                pump.draw=()=>{CaptureSaluteViews(opening,camera,frame);rendered=true;};
                yield return new WaitUntil(()=>rendered);
                events.Add(frame+","+at);frame++;yield return WaitGameSeconds(.08f);
            }
            Object.Destroy(pump);File.AppendAllLines("Logs/Cutscene/salute-frames.csv",events);
        }
        private static void VerifyWrist(Animator actor,float maximum=25) {
            var hand=actor.GetBoneTransform(HumanBodyBones.RightHand);
            var elbow=actor.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var end=System.Array.Find(hand.GetComponentsInChildren<Transform>(),t=>t.name=="RightHand_end");
            var finger=actor.GetBoneTransform(HumanBodyBones.RightMiddleDistal);
            var direction=end!=null?end.position-hand.position:finger!=null?finger.position-hand.position:hand.up;
            Assert.Less(Vector3.Angle(hand.position-elbow.position,direction),maximum,"Palm must continue the forearm without a bent wrist.");
        }
        private static float ElbowBend(Animator actor) {
            var upper=actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
            var elbow=actor.GetBoneTransform(HumanBodyBones.RightLowerArm).position;
            var hand=actor.GetBoneTransform(HumanBodyBones.RightHand).position;
            return Vector3.Angle(elbow-upper,hand-elbow);
        }
        private static void VerifyStraightArm(Animator actor) {
            Assert.Less(ElbowBend(actor),8,"Nam must begin and finish the salute with a straight arm, including the listening idle.");
        }
        private static void CaptureSaluteViews(Map01OpeningCutscene opening,Camera camera,int frame) {
            if(opening.IsPlaying)AssertBriefingUnarmed(opening,Object.FindFirstObjectByType<Map01Mission>());
            if(opening.IsPlaying && opening.BriefingTime>opening.SaluteBeginsAt+.15f)
                VerifyWrist(Object.FindFirstObjectByType<Map01Mission>().player.GetComponentInChildren<Animator>(),40);
            Capture(camera,"Logs/Cutscene/salute-frames/"+frame.ToString("D4")+".png");
            if(frame%3!=0)return;
            var root=Object.FindFirstObjectByType<Map01Mission>().player;
            var pos=camera.transform.position;var rot=camera.transform.rotation;float fov=camera.fieldOfView;
            var focus=root.position+Vector3.up*1.55f;
            foreach(var side in new[]{"front","side"}) {
                var eye=root.position+(side=="front"?root.forward*1.7f+root.right*.2f:root.right*1.7f+root.forward*.3f)+Vector3.up*1.6f;
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(focus-eye));camera.fieldOfView=48;
                Capture(camera,"Logs/Cutscene/"+side+"-"+frame.ToString("D4")+".png");
            }
            camera.transform.SetPositionAndRotation(pos,rot);camera.fieldOfView=fov;
        }
        private static void AssertBriefingUnarmed(Map01OpeningCutscene opening,Map01Mission mission) {
            AssertCommanderUnarmed(opening);
            foreach(var weapon in mission.player.GetComponentsInChildren<ShadowVale.Gameplay.Combat.Weapon>(true))
                Assert.IsFalse(weapon.gameObject.activeInHierarchy,"Nam must have no weapon during the entire opening: "+weapon.name);
        }
        private static void AssertCommanderUnarmed(Map01OpeningCutscene opening) {
            foreach(var weapon in opening.GetComponentsInChildren<ShadowVale.Gameplay.Combat.Weapon>(true))
                Assert.IsFalse(weapon.gameObject.activeInHierarchy,"The briefing commander must remain unarmed: "+weapon.name);
        }
        private static void AssertGameplayWeaponRestored(Map01Mission mission) {
            bool visible=false;
            foreach(var weapon in mission.player.GetComponentsInChildren<ShadowVale.Gameplay.Combat.Weapon>(true))
                foreach(var renderer in weapon.GetComponentsInChildren<Renderer>(true))
                    visible|=renderer.enabled && renderer.gameObject.activeInHierarchy;
            Assert.IsTrue(visible,"The gameplay weapon must be restored after natural playback or skip.");
        }
        private static void Capture(Camera camera, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var previous = camera.targetTexture; var active = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1280, 720, 24);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            } finally {
                camera.targetTexture = previous; RenderTexture.active = active;
                RenderTexture.ReleaseTemporary(target); Object.DestroyImmediate(image);
            }
        }
    }
}

