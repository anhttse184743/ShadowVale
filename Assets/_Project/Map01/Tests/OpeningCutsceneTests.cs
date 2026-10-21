using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object=UnityEngine.Object;
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
        string previousSaveRoot;
        static Keyboard keyboard;
        static bool interactionHeld;
        [SetUp] public void IsolateSaves() {
            previousSaveRoot=SessionState.GetString(ForestSaveSlots.EditorTestRootKey, "");
            SessionState.SetString(ForestSaveSlots.EditorTestRootKey,Path.GetFullPath("Docs/Reviews/CommanderOpening_20261018/TestSaves/"+Guid.NewGuid().ToString("N")));
        }
        [TearDown] public void RestoreSaves() {
            Time.timeScale=1;
            if(string.IsNullOrEmpty(previousSaveRoot))SessionState.EraseString(ForestSaveSlots.EditorTestRootKey);
            else SessionState.SetString(ForestSaveSlots.EditorTestRootKey,previousSaveRoot);
        }
        static void PumpInteraction() {
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard,interactionHeld?new KeyboardState(Key.E):new KeyboardState());
        }

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
        public IEnumerator CommanderConversationIsUnarmedUntilCancelAndRestoresLoadout() {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_MainMenu.unity");
            yield return new EnterPlayMode();
            yield return StartNewGame();
            yield return CheckConversationCancel();
            yield return new ExitPlayMode();
        }
        static IEnumerator StartNewGame() {
            Map01OpeningCutscene.RequestNewGame();Map01SaveSystem.BeginGame(-1);
            yield return WaitForMap01Ready();
            AssertWaitingForCommander(Object.FindFirstObjectByType<Map01Mission>());
        }
        static IEnumerator CheckConversationCancel() {
            var mission=Object.FindFirstObjectByType<Map01Mission>();
            var opening=mission.GetComponentInChildren<Map01OpeningCutscene>();
            var story=mission.GetComponent<Map01Story>();var dialogue=mission.GetComponent<Map01StoryDialogue>();
            var kind=mission.ModernCombat.EquippedKind;int rounds=mission.ModernCombat.RoundsInMagazine;
            PlaceNam(mission,story.Commander.position+story.Commander.forward*1.8f);
            Assert.IsTrue(story.TryInteract());Assert.IsTrue(dialogue.IsPlaying);
            Assert.IsTrue(mission.Cinematic&&mission.Stopped);Assert.IsFalse(mission.CameraInputEnabled);
            Assert.IsFalse(mission.GetComponent<Map01SaveSystem>().CanSave());
            AssertBriefingUnarmed(opening,mission);
            yield return WaitGameSeconds(.5f);AssertBriefingUnarmed(opening,mission);
            dialogue.Close(false);yield return WaitGameSeconds(.3f);
            AssertWaitingForCommander(mission);AssertGameplayWeaponRestored(mission);AssertCommanderUnarmed(opening);
            Assert.AreEqual(kind,mission.ModernCombat.EquippedKind);Assert.AreEqual(rounds,mission.ModernCombat.RoundsInMagazine);
            Assert.IsTrue(mission.GetComponent<Map01SaveSystem>().CanSave());
        }
        [UnityTest]
        public IEnumerator SeparateEPressesStartAndAcceptRescueOrder() {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_MainMenu.unity");
            yield return new EnterPlayMode();
            yield return StartNewGame();
            yield return CheckSeparateInteractionPresses();
            yield return new ExitPlayMode();
        }
        static IEnumerator CheckSeparateInteractionPresses() {
            var mission=Object.FindFirstObjectByType<Map01Mission>();
            var story=mission.GetComponent<Map01Story>();var dialogue=mission.GetComponent<Map01StoryDialogue>();
            var quest=mission.GetComponent<Map01Quest>();var opening=mission.GetComponentInChildren<Map01OpeningCutscene>();
            var voice=DialogueVoice.For(mission.gameObject);var heard=new System.Collections.Generic.List<string>();
            Action<DialogueVoice.Line> observe=line=>heard.Add(line.id);
            var routing=RouteInputToGame();keyboard=InputSystem.AddDevice<Keyboard>();interactionHeld=false;
            InputSystem.onBeforeUpdate+=PumpInteraction;
            var kind=mission.ModernCombat.EquippedKind;int rounds=mission.ModernCombat.RoundsInMagazine;
            try {
                PlaceNam(mission,story.Commander.position+story.Commander.forward*1.8f);
                yield return WaitGameSeconds(.4f);AssertWaitingForCommander(mission);
                var position=mission.player.position;
                interactionHeld=true;yield return WaitGameSeconds(.3f);
                Assert.IsTrue(dialogue.IsPlaying,"A real E press beside the commander must start the conversation.");
                Assert.AreEqual(0,dialogue.Index,"The opening E press cannot also skip the first line.");
                // Begin installs the mission's caption callback; observe after that handoff.
                heard.Add(voice.CurrentId);voice.LineStarted+=observe;
                Assert.IsFalse(quest.RescueOrderAccepted);
                float deadline=Time.realtimeSinceStartup+100;
                while(dialogue.IsPlaying&&(dialogue.Index<dialogue.Count-1||voice.HasSpeech)&&Time.realtimeSinceStartup<deadline) {
                    Assert.IsFalse(quest.RescueOrderAccepted,"Holding E cannot accept the order.");
                    AssertBriefingUnarmed(opening,mission);
                    Assert.Less(Vector3.Distance(position,mission.player.position),.02f,"Dialogue must prevent walking; allow up to 2 cm of ground-contact settling.");
                    yield return null;
                }
                Assert.IsTrue(dialogue.IsPlaying);Assert.AreEqual(dialogue.Count-1,dialogue.Index);Assert.IsFalse(voice.HasSpeech);
                yield return WaitGameSeconds(1.1f);
                Assert.IsTrue(dialogue.IsPlaying);Assert.IsFalse(quest.RescueOrderAccepted,"Listening to every line still requires a separate E press.");
                CollectionAssert.AreEqual(Enumerable.Range(1,5).Select(i=>"m1_story_rescue_order_"+i.ToString("00")).Concat(new[]{"m1_v2_rescue_approach_03"}),heard);
                interactionHeld=false;yield return WaitGameSeconds(.2f);
                interactionHeld=true;yield return WaitGameSeconds(.3f);
                Assert.IsTrue(quest.RescueOrderAccepted);Assert.IsFalse(dialogue.IsPlaying||mission.Cinematic||Map01OpeningCutscene.Active);
                Assert.AreEqual(Map01Quest.RescueStage,quest.Stage);Assert.IsFalse(voice.HasSpeech);
                Assert.IsTrue(string.IsNullOrEmpty(mission.Dialogue),"The final dialogue caption must not remain in gameplay.");
                Assert.IsTrue(mission.ModernPlayer.enabled&&mission.ModernCombat.enabled&&mission.CameraInputEnabled);
                Assert.AreEqual(kind,mission.ModernCombat.EquippedKind);Assert.AreEqual(rounds,mission.ModernCombat.RoundsInMagazine);
                AssertGameplayWeaponRestored(mission);AssertCommanderUnarmed(opening);
            } finally {
                interactionHeld=false;InputSystem.onBeforeUpdate-=PumpInteraction;InputSystem.RemoveDevice(keyboard);keyboard=null;
                voice.LineStarted-=observe;RestoreInputRouting(routing);
            }
        }
        [UnityTest]
        public IEnumerator GameplayCameraStaysAboveRiverWhenWading() {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();yield return WaitForMap01Ready();
            yield return CheckWadingCamera();
            yield return new ExitPlayMode();
        }
        static IEnumerator CheckWadingCamera() {
            var mission=Object.FindFirstObjectByType<Map01Mission>();
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

