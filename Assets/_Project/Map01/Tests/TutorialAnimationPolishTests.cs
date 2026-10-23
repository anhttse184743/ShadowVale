using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class TutorialAnimationPolishTests : ForestSceneTestBase
    {
        private const string Scene = "Assets/_Project/Scenes/Maps/Map00_Tutorial.unity";
        private static Animator Actor(GameObject root) => root.GetComponentsInChildren<Animator>(true).First(a => a.name == "Model");
        private static (float stature, float sole) Measure(GameObject root)
        {
            var actor = Actor(root);
            using (var pose = new Map01Extraction.PosePlayer(actor)) {
                pose.Play(TutorialCaptureAssets.Load().idle); pose.Settle(); pose.Rate(1); pose.Tick(.2f);
                float feet = (actor.GetBoneTransform(HumanBodyBones.LeftFoot).position.y + actor.GetBoneTransform(HumanBodyBones.RightFoot).position.y) * .5f;
                float sole = float.PositiveInfinity;
                foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                    if (!skin.enabled || !skin.gameObject.activeInHierarchy) continue;
                    var baked = new Mesh(); skin.BakeMesh(baked, true);
                    foreach (var v in baked.vertices) sole = Mathf.Min(sole, skin.transform.TransformPoint(v).y);
                    Object.DestroyImmediate(baked);
                }
                return (actor.GetBoneTransform(HumanBodyBones.Head).position.y - feet, sole);
            }
        }
        [Test]
        public void HungMatchesNamsStandingSizeAndGroundContact()
        {
            var nam = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab"));
            var hung = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PlayerHung.prefab"));
            try {
                nam.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); hung.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var a = Measure(nam); var b = Measure(hung);
                Assert.That(b.stature / a.stature, Is.InRange(.98f, 1.02f), "Measure the same standing pose, not the captive NPC bounds.");
                Assert.That(Mathf.Abs(a.sole - b.sole), Is.LessThan(.02f), "Matching size must also keep the feet at the floor.");
                Assert.AreEqual(nam.GetComponent<CharacterController>().height, hung.GetComponent<CharacterController>().height);
                Directory.CreateDirectory("Temp/TutorialAnimationPolish");
                File.WriteAllText("Temp/TutorialAnimationPolish/standing-size.txt", $"Nam stature {a.stature:F4}; Hung stature {b.stature:F4}; soles {a.sole:F4}/{b.sole:F4}; Hung model scale {Actor(hung).transform.localScale}.");
            } finally { Object.DestroyImmediate(nam); Object.DestroyImmediate(hung); }
        }
        private static IEnumerator Ready()
        {
            double until = Time.realtimeSinceStartupAsDouble + 12;
            while (Time.realtimeSinceStartupAsDouble < until) {
                var d = Object.FindFirstObjectByType<TutorialDirector>();
                if (d != null && d.GetComponent<Map01Mission>().RetryRequested != null) yield break;
                yield return null;
            }
            Assert.Fail("Tutorial not ready.");
        }
        private static void Frame(Map01Mission mission, Vector3 towardCamera)
        {
            var at = mission.player.position; var eye = at + towardCamera * 3.5f + Vector3.Cross(Vector3.up, towardCamera) * .8f + Vector3.up * 1.35f;
            mission.gameCamera.GetComponent<ThirdPersonCamera>().SetCinematicView(eye, Quaternion.LookRotation(at + Vector3.up - eye), 40, 1);
        }
        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) yield return null;
        }
        private static IEnumerator Photo(string name)
        {
            Directory.CreateDirectory("Temp/TutorialAnimationPolish");
            ScreenCapture.CaptureScreenshot("Temp/TutorialAnimationPolish/" + name + ".png");
            for (int i = 0; i < 4; i++) yield return null;
        }
        private static void Reset(Map01Mission mission, Vector3 position, Quaternion rotation)
        {
            var body = mission.player.GetComponent<CharacterController>(); body.enabled = false;
            mission.player.SetPositionAndRotation(position, rotation); body.enabled = true; mission.ModernPlayer.RestoreMotion(false);
        }
        private static void Elbows(Animator actor)
        {
            foreach (bool right in new[] { false, true }) {
                var upper = actor.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
                var lower = actor.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
                var hand = actor.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                Assert.Greater(Vector3.Angle(upper.position - lower.position, hand.position - lower.position), 15, "The forearm must not fold into the upper arm.");
            }
        }
        [UnityTest]
        public IEnumerator GameplayPosesAndSideBySideSizeReview()
        {
            EditorSceneManager.OpenScene(Scene); SetPreviewResolution(1600, 900);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode();
            UnityEngine.SceneManagement.SceneManager.LoadScene(TutorialDirector.SceneName); yield return Ready(); yield return null; yield return null;
            var d = Object.FindFirstObjectByType<TutorialDirector>(); var mission = d.GetComponent<Map01Mission>();
            d.enabled = false; d.GetComponent<TutorialTimeControl>().SetSlow(false);
            var ui = Object.FindFirstObjectByType<TutorialPromptUI>(); ui.enabled = false; ui.GetComponent<Canvas>().enabled = false;
            var hud = mission.GetComponent<Map01Hud>(); if (hud != null) hud.enabled = false;
            var routing = RouteInputToGame(); var keyboard = InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>();
            // A disposable review stage avoids foliage/fences hiding the joints in QA photos.
            // It exists only in Play Mode; neither tutorial scene nor map geometry is saved.
            var position = new Vector3(500, 50, 500); var rotation = mission.player.rotation; var towardCamera = -mission.player.forward;
            var stage = GameObject.CreatePrimitive(PrimitiveType.Cube); stage.name = "Temporary animation review floor";
            stage.transform.position = position - Vector3.up * .13f; stage.transform.localScale = new Vector3(18, .2f, 18);
            Reset(mission, position, rotation);
            GameObject nam = null;
            try {
                mission.ModernCombat.Equip(WeaponKind.Unarmed); Frame(mission, towardCamera);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S)); yield return Wait(.4f); Frame(mission, towardCamera);
                Assert.IsTrue(mission.ModernPlayer.IsMoving); Elbows(Actor(mission.player.gameObject)); yield return Photo("01-walk");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return Wait(.1f); Reset(mission, position, rotation);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S, Key.LeftShift)); yield return Wait(.4f); Frame(mission, towardCamera);
                Assert.IsTrue(mission.ModernPlayer.IsSprinting); Elbows(Actor(mission.player.gameObject)); yield return Photo("02-run");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return Wait(.1f); Reset(mission, position, rotation);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S, Key.LeftCtrl)); yield return Wait(.4f); Frame(mission, towardCamera);
                Assert.IsTrue(mission.ModernPlayer.IsSneaking); yield return Photo("03-crouch");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return Wait(.1f); Reset(mission, position, rotation);
                mission.ModernCombat.Equip(WeaponKind.Knife); yield return Wait(.35f); Frame(mission, towardCamera); yield return Photo("04-knife");
                mission.ModernCombat.Equip(WeaponKind.Rifle); yield return Wait(.35f); Frame(mission, towardCamera); yield return Photo("05-rifle-carry");
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right)); yield return Wait(.4f);
                Assert.IsTrue(mission.ModernCombat.IsAiming); mission.ModernPlayer.enabled = false;
                var rifle = mission.player.GetComponentsInChildren<Weapon>().First(w => w.Kind == WeaponKind.Rifle);
                Assert.Less(Vector3.Angle(rifle.Muzzle.forward, mission.gameCamera.transform.forward), 3, "The visible rifle must point along the aiming view.");
                var grip = mission.player.GetComponent<HungPlayerGrip>(); grip.enabled = false;
                var posedActor = Actor(mission.player.gameObject); posedActor.enabled = false;
                Frame(mission, mission.player.forward); Elbows(posedActor); yield return Photo("06-aim"); posedActor.enabled = true; grip.enabled = true;
                InputSystem.QueueStateEvent(mouse, new MouseState()); yield return Wait(.15f);
                mission.ModernCombat.Equip(WeaponKind.Unarmed); mission.ModernPlayer.enabled = true;
                var actions = Map01NamActions.For(mission); actions.Hold(Map01NamActions.Binoculars, true); yield return Wait(.4f);
                Frame(mission, mission.player.forward); yield return Photo("07-binoculars"); actions.Hold(Map01NamActions.Binoculars, false); yield return Wait(.3f);
                actions.Hold(Map01NamActions.ThrowAim, true); yield return Wait(.4f); Frame(mission, mission.player.forward); yield return Photo("08-stone-aim"); actions.CancelAction();
                actions.enabled = false; mission.ModernPlayer.enabled = false; mission.ModernCombat.enabled = false;
                Reset(mission, position, Quaternion.LookRotation(towardCamera));
                nam = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/Player.prefab"));
                foreach (var behaviour in nam.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                foreach (var collider in nam.GetComponentsInChildren<Collider>()) collider.enabled = false;
                var right = Vector3.Cross(towardCamera, Vector3.up);
                mission.player.position = position - right * .6f; nam.transform.SetPositionAndRotation(position + right * .6f, Quaternion.LookRotation(towardCamera));
                using (var hungPose = new Map01Extraction.PosePlayer(Actor(mission.player.gameObject)))
                using (var namPose = new Map01Extraction.PosePlayer(Actor(nam))) {
                    hungPose.Play(TutorialCaptureAssets.Load().idle); hungPose.Settle(); namPose.Play(TutorialCaptureAssets.Load().idle); namPose.Settle();
                    var eye = position + towardCamera * 4.4f + Vector3.up * 1.25f;
                    mission.gameCamera.GetComponent<ThirdPersonCamera>().SetCinematicView(eye, Quaternion.LookRotation(position + Vector3.up - eye), 39, 1);
                    yield return Photo("09-hung-left-nam-right");
                }
            } finally {
                if (nam != null) Object.Destroy(nam);
                Object.Destroy(stage);
                InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); RestoreInputRouting(routing);
            }
            yield return new ExitPlayMode();
        }
    }
}
