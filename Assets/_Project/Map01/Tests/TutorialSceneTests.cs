using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ShadowVale.Map01.Tests
{
    public sealed class TutorialSceneTests : ForestSceneTestBase
    {
        private const string Scene = "Assets/_Project/Scenes/Maps/Map00_Tutorial.unity";
        private static IEnumerator WaitForTutorial()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (Time.realtimeSinceStartupAsDouble < deadline) {
                var director = Object.FindFirstObjectByType<TutorialDirector>();
                var mission = director != null ? director.GetComponent<Map01Mission>() : null;
                if (mission != null && mission.IsInitialized && mission.ModernCombat != null && mission.RetryRequested != null) yield break;
                yield return null;
            }
            Assert.Fail($"Tutorial not ready: playing={Application.isPlaying}, scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}");
        }
        private static void StartLesson(TutorialDirector director, TutorialStep step)
        {
            var mission = director.GetComponent<Map01Mission>(); var body = mission.player.GetComponent<CharacterController>();
            body.enabled = false; mission.player.position = director.lessonStarts[(int)step].position; body.enabled = true;
            typeof(TutorialDirector).GetMethod("BeginStep", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, new object[] { step });
            // Gameplay fixtures start after the camera introduction. Presentation tests
            // exercise the complete pan, pause, retry and return to player control.
            typeof(TutorialDirector).GetMethod("CancelTargetReveal", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, null);
        }
        [UnityTest]
        public IEnumerator StartsAsHungWithLoadoutAndNoMapOneQuestOrSave()
        {
            EditorSceneManager.OpenScene(Scene);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode();
            UnityEngine.SceneManagement.SceneManager.LoadScene(TutorialDirector.SceneName); yield return WaitForTutorial(); yield return null; yield return null; yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            Assert.IsTrue(mission.IsInitialized); Assert.IsNull(mission.hung);
            Assert.IsNull(mission.GetComponent<Map01Quest>()); Assert.IsNull(mission.GetComponent<Map01SaveSystem>());
            Assert.IsInstanceOf<TutorialDirector>(mission.Objectives); Assert.IsInstanceOf<TutorialScouting>(mission.Binoculars);
            var actor = mission.player.GetComponentsInChildren<Animator>().First(a => a.name == "Model");
            Assert.IsTrue(actor.avatar.isValid && actor.avatar.isHuman); Assert.AreEqual("AC_Player", actor.runtimeAnimatorController.name);
            Assert.AreEqual("RightHand", mission.player.GetComponentsInChildren<Transform>().First(t => t.name == "WeaponAnchor").parent.name);
            Assert.AreEqual(2, mission.GetComponent<Map01Inventory>().Count("medkit_small"));
            Assert.Greater(mission.GetComponent<Map01Inventory>().Count("ammo_rifle"), 0);
            Assert.IsNotNull(Object.FindFirstObjectByType<TutorialPromptUI>());
            Assert.AreSame(mission.GetComponent<TutorialDirector>(), mission.GetComponent<TutorialDirector>().noiseTrap.director);
            var capture = mission.GetComponent<TutorialCaptureCutscene>();
            Assert.IsNotNull(capture.runClip); Assert.IsNotNull(capture.hitClip); Assert.IsNotNull(capture.fallClip); Assert.IsNotNull(capture.enemyRunClip);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator PauseResumesThePendingSlowMotionCue()
        {
            EditorSceneManager.OpenScene(Scene);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode();
            UnityEngine.SceneManagement.SceneManager.LoadScene(TutorialDirector.SceneName); yield return WaitForTutorial(); yield return null; yield return null; yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>(); var time = mission.GetComponent<TutorialTimeControl>();
            time.SetSlow(true); Assert.AreEqual(.2f, Time.timeScale, .001f);
            mission.SetPaused(true); Assert.AreEqual(0, Time.timeScale);
            mission.SetPaused(false); Assert.AreEqual(.2f, Time.timeScale, .001f);
            time.SetSlow(false); Assert.AreEqual(1, Time.timeScale);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator DeathRetriesOnlyTheCurrentStepAndRestoresSupplies()
        {
            EditorSceneManager.OpenScene(Scene);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode();
            var loading = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(TutorialDirector.SceneName);
            while (!loading.isDone) yield return null;
            yield return WaitForTutorial();
            var director = Object.FindFirstObjectByType<TutorialDirector>();
            Assert.IsNotNull(director);
            var mission = director.GetComponent<Map01Mission>();
            StartLesson(director, TutorialStep.Shoot);
            var inventory = mission.GetComponent<Map01Inventory>();
            int ammo = inventory.Count("ammo_rifle"), magazine = mission.ModernCombat.RoundsInMagazine;
            inventory.Spend("ammo_rifle", ammo); mission.ModernCombat.RestoreMagazine(0);
            mission.ModernHealth.TakeDamage(1000, mission.player.position, null); yield return null;
            Assert.IsTrue(director.Failed);
            var routing = RouteInputToGame();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return null;
                yield return null;
            } finally { InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing); }
            Assert.AreEqual(TutorialStep.Shoot, director.Step); Assert.IsFalse(director.Failed);
            Assert.Greater(mission.PlayerHealth, 0); Assert.AreEqual(ammo, inventory.Count("ammo_rifle"));
            Assert.AreEqual(magazine, mission.ModernCombat.RoundsInMagazine);
            director.RetryStep(); director.RetryStep();
            // Capturing director in a lambda creates a closure before EnterPlayMode;
            // that managed closure cannot survive the test runner's domain reload.
            int registrations = 0;
            foreach (var enemy in mission.Enemies) if (enemy == director.shootingGuard) registrations++;
            Assert.AreEqual(1, registrations);
            Assert.IsTrue(director.shootingGuard.Alive);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator LateSquadIsBoundAndHealingAlwaysHasUsableBandages()
        {
            EditorSceneManager.OpenScene(Scene);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode();
            UnityEngine.SceneManagement.SceneManager.LoadScene(TutorialDirector.SceneName); yield return WaitForTutorial(); yield return null; yield return null; yield return null;
            var director = Object.FindFirstObjectByType<TutorialDirector>(); var mission = director.GetComponent<Map01Mission>();
            StartLesson(director, TutorialStep.Squad);
            Assert.AreEqual(3, director.squad.Length);
            foreach (var guard in director.squad) {
                Assert.IsTrue(guard.gameObject.activeInHierarchy); Assert.Contains(guard, mission.Enemies);
                Assert.AreEqual("T3", guard.SquadId); Assert.AreEqual(mission.Weapon.damage * mission.Settings.guardDamageScale, guard.DamagePerShot, .001f);
            }
            mission.RestoreHealthFromSave(mission.Settings.playerHP);
            StartLesson(director, TutorialStep.Heal);
            Assert.Less(mission.PlayerHealth, mission.Settings.playerHP);
            Assert.Greater(mission.GetComponent<Map01Inventory>().Count("medkit_small"), 0);
            Assert.IsTrue(mission.GetComponent<Map01Inventory>().UseItem("medkit_small"));
            yield return null; Assert.AreEqual(1, director.Progress);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator SkippingFromPauseStartsFreshMapOneAtNormalSpeed()
        {
            EditorSceneManager.OpenScene(Scene);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode();
            UnityEngine.SceneManagement.SceneManager.LoadScene(TutorialDirector.SceneName); yield return WaitForTutorial(); yield return null; yield return null; yield return null;
            var director = Object.FindFirstObjectByType<TutorialDirector>();
            director.GetComponent<TutorialTimeControl>().SetSlow(true); director.GetComponent<Map01Mission>().SetPaused(true);
            director.SkipAll(); yield return null; yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            Assert.AreEqual("Map 1", mission.gameObject.scene.name); Assert.IsNotNull(mission.GetComponent<Map01Quest>());
            Assert.AreEqual(1, Time.timeScale); Assert.IsNull(Object.FindFirstObjectByType<TutorialDirector>());
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator NewGameChoiceCanStartTheTutorial()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/01_MainMenu.unity");
            yield return new EnterPlayMode();
            UnityEngine.SceneManagement.SceneManager.LoadScene("01_MainMenu"); yield return null;
            var menu = Object.FindFirstObjectByType<ForestMenu>();
            var action = typeof(ForestMenu).GetMethod("MainAction", BindingFlags.Instance | BindingFlags.NonPublic);
            action.Invoke(menu, new object[] { 1 });
            Assert.AreEqual("01_MainMenu", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            ScreenCapture.CaptureScreenshot("Temp/tutorial-menu.png");
            for (int i = 0; i < 8; i++) yield return null;
            action.Invoke(menu, new object[] { 0 }); yield return WaitForTutorial();
            Assert.IsNotNull(Object.FindFirstObjectByType<TutorialDirector>());
            Assert.IsNull(Object.FindFirstObjectByType<Map01SaveSystem>());
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator ReturnPatrolWaitsForTheBranchAndThenCaptureStarts()
        {
            EditorSceneManager.OpenScene(Scene); EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode(); UnityEngine.SceneManagement.SceneManager.LoadScene(TutorialDirector.SceneName); yield return WaitForTutorial();
            yield return null; yield return null; yield return null;
            var director = Object.FindFirstObjectByType<TutorialDirector>();
            StartLesson(director, TutorialStep.Return);
            Assert.IsTrue(director.noiseTrap.gameObject.activeSelf);
            Assert.IsTrue(director.patrol.All(e => !e.gameObject.activeInHierarchy));
            var mission = director.GetComponent<Map01Mission>();
            director.noiseTrap.SendMessage("OnTriggerEnter", mission.player.GetComponent<CharacterController>());
            Assert.AreEqual(TutorialStep.Capture, director.Step);
            Assert.IsTrue(mission.Cinematic); Assert.IsTrue(director.GetComponent<TutorialCaptureCutscene>().Playing);
            Assert.IsTrue(director.patrol.All(e => e.gameObject.activeInHierarchy));
            var cutscene = director.GetComponent<TutorialCaptureCutscene>();
            Vector3 start = mission.player.position;
            Vector3 home = Vector3.ProjectOnPlane(director.lessonStarts[0].position - start, Vector3.up).normalized;
            Assert.Greater(Vector3.Dot(cutscene.EscapeDirection, home), .8f, "Hùng follows the wooden ramp towards the original base.");
            double runningDeadline = Time.realtimeSinceStartupAsDouble + 5;
            while (!cutscene.Running && Time.realtimeSinceStartupAsDouble < runningDeadline) yield return null;
            yield return WaitGameSeconds(.3f);
            Assert.Greater(Vector3.Dot(mission.player.position - start, home), .1f, "The chase actually moves towards the base.");
            ScreenCapture.CaptureScreenshot("Temp/tutorial-straight-capture.png");
            double deadline = Time.realtimeSinceStartupAsDouble + 40;
            while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == TutorialDirector.SceneName && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.AreEqual("Map 1", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            Assert.AreEqual(1, Time.timeScale);
            yield return new ExitPlayMode();
        }
        [Test]
        public void TheKnifeGuardHasHisBackToTheApproachAndIsOutOfEarshot()
        {
            EditorSceneManager.OpenScene(Scene);
            var director = Object.FindFirstObjectByType<TutorialDirector>();
            var guard = director.knifeGuard.transform; var start = director.lessonStarts[(int)TutorialStep.StoneKnife].position;
            var toStart = Vector3.ProjectOnPlane(start - guard.position, Vector3.up);
            // A walking step carries 9 m (Map01Mission.FootstepNoiseRadius); arriving must not already be heard.
            Assert.Greater(toStart.magnitude, 9f, "The lesson starts out of earshot of a walking step.");
            Assert.Greater(Vector3.Angle(guard.forward, toStart), 110f, "He faces away from where Hùng comes in.");
            Assert.IsTrue(NavMesh.SamplePosition(guard.position, out _, .5f, NavMesh.AllAreas), "He stands on the NavMesh.");
            // The bush Hùng hides in is right behind him: one short step out of it reaches knife range (1.6 m).
            Assert.IsNotNull(director.knifeBush);
            var bush = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None).Where(p => p.kind == ForestPointKind.Hide)
                .OrderBy(p => (p.transform.position - director.knifeBush.position).sqrMagnitude).First();
            Assert.Less(Vector3.Distance(bush.transform.position, director.knifeBush.position), .5f, "The anchor marks a real hiding bush.");
            var toBush = Vector3.ProjectOnPlane(bush.transform.position - guard.position, Vector3.up);
            Assert.Greater(Vector3.Angle(guard.forward, toBush), 150f, "The bush is behind him.");
            float gap = toBush.magnitude - bush.radius;
            Assert.That(gap, Is.InRange(1.6f, 2.1f), "From the edge of the bush, knife range is a step away.");
        }
        [UnityTest]
        public IEnumerator TheKnifeLessonTellsThePlayerToSneakThenHide()
        {
            EditorSceneManager.OpenScene(Scene); EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode(); UnityEngine.SceneManagement.SceneManager.LoadScene(TutorialDirector.SceneName); yield return WaitForTutorial();
            yield return null; yield return null; yield return null;
            var director = Object.FindFirstObjectByType<TutorialDirector>(); var mission = director.GetComponent<Map01Mission>();
            var body = mission.player.GetComponent<CharacterController>();
            var start = director.lessonStarts[(int)TutorialStep.StoneKnife].position;
            // Sneaking is a keyboard toggle: without a keyboard the player always stands.
            var routing = RouteInputToGame();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try {
                yield return null;
                Assert.IsFalse(mission.ModernPlayer.IsSneaking);
                body.enabled = false; mission.player.position = start + Vector3.back * 20; body.enabled = true;
                typeof(TutorialDirector).GetMethod("BeginStep", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, new object[] { TutorialStep.StoneKnife });
                yield return null;
                Assert.IsTrue(director.AwaitingArrival);
                Assert.AreEqual("CTRL", director.KeyLabel, "On the way in, the hint is to sneak.");
                body.enabled = false; mission.player.position = start; body.enabled = true;
                yield return null; yield return null;
                Assert.IsFalse(director.AwaitingArrival);
                // The lesson first shows its target (TutorialDirector.TargetReveal); the hints follow.
                double revealEnds = Time.realtimeSinceStartupAsDouble + 15;
                while (director.RevealingTargets && Time.realtimeSinceStartupAsDouble < revealEnds) yield return null;
                Assert.IsFalse(director.RevealingTargets); yield return null;
                Assert.AreEqual("CTRL", director.KeyLabel, "Standing at the lesson, sneaking comes first.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                Assert.IsTrue(mission.ModernPlayer.IsSneaking, $"Ctrl toggles sneaking (input allowed={mission.ModernPlayer.InputAllowed?.Invoke()}, " +
                    $"camera input={mission.CameraInputEnabled}, stopped={mission.Stopped}, inventory={mission.InventoryOpen}, map={mission.MapOpen}, " +
                    $"menu={ForestMenu.Visible}, overlay={ShadowVale.Core.Services.InputFocus.OverlayOpen}, cinematic={mission.Cinematic})");
                Assert.AreEqual("BỤI CÂY", director.KeyLabel, "Sneaking, the next hint is the bush.");
                Assert.IsFalse(Time.timeScale < 1, "The knife lesson never slows time.");
                Assert.IsFalse(director.Failed);
            } finally { InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing); }
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator SpottedDuringTheKnifeLessonRetriesWithEnter()
        {
            EditorSceneManager.OpenScene(Scene); EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode(); UnityEngine.SceneManagement.SceneManager.LoadScene(TutorialDirector.SceneName); yield return WaitForTutorial();
            yield return null; yield return null; yield return null;
            var director = Object.FindFirstObjectByType<TutorialDirector>(); var mission = director.GetComponent<Map01Mission>();
            var body = mission.player.GetComponent<CharacterController>();
            // Arrive at the lesson as a player does: the step starts away from it, the guard appears on arrival.
            var start = director.lessonStarts[(int)TutorialStep.StoneKnife].position;
            body.enabled = false; mission.player.position = start + Vector3.back * 20; body.enabled = true;
            typeof(TutorialDirector).GetMethod("BeginStep", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, new object[] { TutorialStep.StoneKnife });
            Assert.IsTrue(director.AwaitingArrival);
            body.enabled = false; mission.player.position = start; body.enabled = true;
            yield return null; yield return null;
            Assert.IsFalse(director.AwaitingArrival); Assert.IsTrue(director.knifeGuard.gameObject.activeInHierarchy);
            // Stand in front of him until he spots Hùng.
            var guard = director.knifeGuard.transform; mission.Crouched = false;
            body.enabled = false; mission.player.position = guard.position + guard.forward * 4; body.enabled = true;
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!director.Failed && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsTrue(director.Failed, "The guard must spot Hùng standing in front of him.");
            // The player reads the failure panel for a while; the guard stands held meanwhile.
            yield return WaitGameSeconds(1.5f);
            var routing = RouteInputToGame();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
            } finally { InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing); }
            Assert.IsFalse(director.Failed, "Enter retries the lesson: " + director.FailureReason);
            Assert.AreEqual(TutorialStep.StoneKnife, director.Step);
            Assert.AreEqual(1, director.RetryCount);
            // Still retried once the guard resumes: the time he stood held must not leave him engaged.
            yield return WaitGameSeconds(.5f);
            Assert.IsFalse(director.Failed, "The retried lesson failed again: " + director.FailureReason);
            Assert.IsFalse(director.knifeGuard.Engaged, "The guard is back at his post, calm.");
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator HidingInTheBushThenStabbingFromBehindCompletesWithoutAStone()
        {
            EditorSceneManager.OpenScene(Scene); EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode(); UnityEngine.SceneManagement.SceneManager.LoadScene(TutorialDirector.SceneName); yield return WaitForTutorial();
            yield return null; yield return null; yield return null;
            var director = Object.FindFirstObjectByType<TutorialDirector>(); var mission = director.GetComponent<Map01Mission>();
            var body = mission.player.GetComponent<CharacterController>();
            var routing = RouteInputToGame();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try {
                StartLesson(director, TutorialStep.StoneKnife);
                yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl)); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
                Assert.IsTrue(mission.ModernPlayer.IsSneaking);
                // Into the bush, as the hint says.
                var guard = director.knifeGuard.transform;
                // A teleport is not a step: footsteps off while he is placed in it.
                var steps = mission.player.GetComponent<PlayerFootsteps>(); steps.enabled = false;
                body.enabled = false; mission.player.position = director.knifeBush.position; body.enabled = true;
                yield return null; yield return null; steps.enabled = true; yield return null;
                Assert.IsTrue(mission.Hidden, "Crouched in the bush, Hùng is hidden.");
                Assert.AreEqual("2", director.KeyLabel, "Hidden, the next hint is the knife.");
                mission.ModernCombat.Equip(WeaponKind.Knife); yield return null;
                // Sneak out towards his back at sneaking pace, footsteps and all.
                double deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (!director.knifeGuard.CanSilentTakedown() && !director.Failed && Time.realtimeSinceStartupAsDouble < deadline) {
                    var to = Vector3.ProjectOnPlane(guard.position - mission.player.position, Vector3.up);
                    body.Move(to.normalized * Mathf.Min(to.magnitude, 2.3f * Time.deltaTime)); yield return null;
                }
                Assert.IsFalse(director.Failed, "Sneaking out of the bush behind him goes unnoticed: " + director.FailureReason);
                Assert.IsTrue(director.knifeGuard.CanSilentTakedown(), "Behind him, close enough for the knife.");
                Assert.AreEqual("CHUỘT TRÁI", director.KeyLabel);
                StringAssert.Contains("Q", director.Tip, "The optional stone is offered on its own line.");
                Assert.IsTrue(mission.GetTakedownHint(3.2f, out var marked, out bool ready), "The knife badge shows over him, as in Map 1.");
                Assert.AreSame(director.knifeGuard, marked); Assert.IsTrue(ready);
                Assert.IsTrue(mission.ModernCombat.TrySpecialMelee(), "No stone needed.");
                Assert.IsTrue(director.knifeGuard.TakenDownSilently);
            } finally { InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing); }
            double until = Time.realtimeSinceStartupAsDouble + 8;
            while (director.Step == TutorialStep.StoneKnife && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Assert.AreEqual(TutorialStep.Shoot, director.Step);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator WalkingInLoudThenHidingInTheBushStillWorks()
        {
            EditorSceneManager.OpenScene(Scene); EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode(); UnityEngine.SceneManagement.SceneManager.LoadScene(TutorialDirector.SceneName); yield return WaitForTutorial();
            yield return null; yield return null; yield return null;
            var director = Object.FindFirstObjectByType<TutorialDirector>(); var mission = director.GetComponent<Map01Mission>();
            var body = mission.player.GetComponent<CharacterController>(); var guard = director.knifeGuard.transform;
            var routing = RouteInputToGame();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try {
                StartLesson(director, TutorialStep.StoneKnife);
                yield return null;
                var post = guard.position;
                // Standing up all the way to the bush: he hears it and turns to look.
                double deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (Vector3.ProjectOnPlane(director.knifeBush.position - mission.player.position, Vector3.up).magnitude > .3f && Time.realtimeSinceStartupAsDouble < deadline) {
                    var to = Vector3.ProjectOnPlane(director.knifeBush.position - mission.player.position, Vector3.up);
                    body.Move(to.normalized * Mathf.Min(to.magnitude, 4.2f * Time.deltaTime)); yield return null;
                }
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl)); yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
                Assert.IsTrue(mission.Hidden);
                Assert.IsTrue(director.knifeGuard.Alerted, "The noise made him look.");
                // Hidden in the bush he neither sees Hùng nor walks over into him, and he settles back.
                deadline = Time.realtimeSinceStartupAsDouble + 25;
                while (director.knifeGuard.Alerted && !director.Failed && Time.realtimeSinceStartupAsDouble < deadline) {
                    Assert.Less(Vector3.ProjectOnPlane(guard.position - post, Vector3.up).magnitude, .6f, "A sentry stays at his post.");
                    yield return null;
                }
                Assert.IsFalse(director.Failed, "Hidden in the bush, Hùng is not found: " + director.FailureReason);
                Assert.IsFalse(director.knifeGuard.Alerted);
                for (int i = 0; i < 30; i++) yield return null; // Back to facing his post.
                mission.ModernCombat.Equip(WeaponKind.Knife); yield return null;
                deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (!director.knifeGuard.CanSilentTakedown() && !director.Failed && Time.realtimeSinceStartupAsDouble < deadline) {
                    var to = Vector3.ProjectOnPlane(guard.position - mission.player.position, Vector3.up);
                    body.Move(to.normalized * Mathf.Min(to.magnitude, 2.3f * Time.deltaTime)); yield return null;
                }
                Assert.IsFalse(director.Failed, director.FailureReason);
                Assert.IsTrue(mission.ModernCombat.TrySpecialMelee());
                Assert.IsTrue(director.knifeGuard.TakenDownSilently);
            } finally { InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing); }
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator TheStoneIsOptionalButStillLuresBeforeTheKnife()
        {
            EditorSceneManager.OpenScene(Scene); EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode(); UnityEngine.SceneManagement.SceneManager.LoadScene(TutorialDirector.SceneName); yield return WaitForTutorial();
            yield return null; yield return null; yield return null;
            var director = Object.FindFirstObjectByType<TutorialDirector>(); var mission = director.GetComponent<Map01Mission>();
            StartLesson(director, TutorialStep.StoneKnife);
            float began = Time.time;
            Assert.IsTrue(mission.GetComponent<Map01StoneThrow>().ThrowAt(director.knifeGuard.transform.position + Vector3.forward * 4));
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (director.knifeGuard.LastStoneHeardAt < began && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.GreaterOrEqual(director.knifeGuard.LastStoneHeardAt, began);
            yield return null;
            ScreenCapture.CaptureScreenshot("Temp/tutorial-stealth.png");
            for (int i = 0; i < 8; i++) yield return null;
            var guard = director.knifeGuard.transform; var body = mission.player.GetComponent<CharacterController>();
            body.enabled = false; mission.player.position = guard.position - guard.forward; body.enabled = true;
            mission.ModernCombat.Equip(WeaponKind.Knife); yield return null;
            Assert.IsTrue(mission.ModernCombat.TrySpecialMelee());
            Assert.IsTrue(director.knifeGuard.TakenDownSilently);
            deadline = Time.realtimeSinceStartupAsDouble + 8;
            while (director.Step == TutorialStep.StoneKnife && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.AreEqual(TutorialStep.Shoot, director.Step);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator ScoutingGuideLeadsToTheBridgeThenItsCargo()
        {
            EditorSceneManager.OpenScene(Scene);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode();
            var loading = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(TutorialDirector.SceneName);
            while (!loading.isDone) yield return null;
            yield return WaitForTutorial();
            var director = Object.FindFirstObjectByType<TutorialDirector>();
            var mission = director.GetComponent<Map01Mission>();
            var body = mission.player.GetComponent<CharacterController>();
            body.enabled = false; mission.player.position = director.lessonStarts[(int)TutorialStep.Squad].position; body.enabled = true;
            typeof(TutorialDirector).GetMethod("BeginStep", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, new object[] { TutorialStep.Scout });
            Assert.IsTrue(director.AwaitingArrival);
            Assert.IsTrue(director.TryGetObjectiveTarget(out var target, out _));
            Assert.Less(Vector3.Distance(target, director.lessonStarts[(int)TutorialStep.Scout].position), .1f);
            body.enabled = false; mission.player.position = director.lessonStarts[(int)TutorialStep.Scout].position; body.enabled = true;
            yield return null; yield return null;
            Assert.IsFalse(director.AwaitingArrival);
            Assert.IsTrue(director.TryGetObjectiveTarget(out target, out _));
            Assert.Less(Vector3.Distance(target, director.GetComponent<TutorialScouting>().supplyCache.position), .1f);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator HoldingFRecordsTheActualVisibleSupplyCache()
        {
            EditorSceneManager.OpenScene(Scene); EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode(); UnityEngine.SceneManagement.SceneManager.LoadScene(TutorialDirector.SceneName); yield return WaitForTutorial();
            yield return null; yield return null; yield return null;
            var routing = RouteInputToGame(); var keyboard = InputSystem.AddDevice<Keyboard>();
            try {
                var director = Object.FindFirstObjectByType<TutorialDirector>(); var mission = director.GetComponent<Map01Mission>();
                StartLesson(director, TutorialStep.Scout);
                var scout = director.GetComponent<TutorialScouting>();
                Vector3 target = scout.ScanTarget;
                mission.gameCamera.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>().RestoreLook(Quaternion.LookRotation(target - (mission.player.position + Vector3.up * 1.6f)));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
                yield return WaitGameSeconds(.7f);
                ScreenCapture.CaptureScreenshot("Temp/tutorial-jetty-gameplay.png");
                for (int i = 0; i < 8; i++) yield return null;
                double deadline = Time.realtimeSinceStartupAsDouble + 8;
                while (director.Step == TutorialStep.Scout && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.AreEqual(TutorialStep.Return, director.Step, "The supply point must actually be visible from the scouting anchor.");
            } finally { InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing); }
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator ARealRifleKillAndCompletedReloadAdvanceToHealing()
        {
            EditorSceneManager.OpenScene(Scene);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode();
            var loading = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(TutorialDirector.SceneName);
            while (!loading.isDone) yield return null;
            yield return WaitForTutorial();
            var routing = RouteInputToGame();
            var mouse = InputSystem.AddDevice<Mouse>();
            try {
                var director = Object.FindFirstObjectByType<TutorialDirector>();
                var mission = director.GetComponent<Map01Mission>();
                StartLesson(director, TutorialStep.Shoot);
                var guard = director.shootingGuard;
                guard.enabled = false;
                guard.GetComponent<NavMeshAgent>().isStopped = true;
                mission.ModernHealth.CinematicInvulnerable = true;
                var combat = mission.ModernCombat;
                combat.Equip(WeaponKind.Rifle);
                var rig = mission.gameCamera.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>();
                var attack = typeof(PlayerCombat).GetMethod("Attack", BindingFlags.Instance | BindingFlags.NonPublic);
                InputSystem.QueueStateEvent(mouse, new MouseState().WithButton(MouseButton.Right));
                yield return null;
                double deadline = Time.realtimeSinceStartupAsDouble + 12;
                while (guard.Alive && Time.realtimeSinceStartupAsDouble < deadline) {
                    Vector3 eye = mission.player.position + Vector3.up * 1.5f;
                    Quaternion look = Quaternion.LookRotation(guard.transform.position + Vector3.up * 1.15f - eye);
                    rig.SetCinematicView(eye, look, 58, 1);
                    mission.gameCamera.transform.SetPositionAndRotation(eye, look);
                    combat.RestoreAttackCooldown(0);
                    attack.Invoke(combat, null);
                    yield return WaitGameSeconds(.2f);
                }
                Assert.IsFalse(guard.Alive, "The actual rifle rays must reach the lesson target.");
                Assert.IsFalse(director.Failed);
                Assert.AreEqual(TutorialStep.Shoot, director.Step, "A kill alone cannot replace reloading.");
                int reserve = mission.GetComponent<Map01Inventory>().Count("ammo_rifle");
                Assert.IsTrue(combat.TryReload());
                deadline = Time.realtimeSinceStartupAsDouble + 12;
                while (director.Step == TutorialStep.Shoot && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.AreEqual(TutorialStep.Heal, director.Step);
                Assert.Less(mission.GetComponent<Map01Inventory>().Count("ammo_rifle"), reserve);
                rig.ClearCinematicView();
            } finally { InputSystem.RemoveDevice(mouse); RestoreInputRouting(routing); }
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator KillingTheShootingTargetWithAKnifeFailsOnlyThatLesson()
        {
            EditorSceneManager.OpenScene(Scene); EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode(); UnityEngine.SceneManagement.SceneManager.LoadScene(TutorialDirector.SceneName); yield return WaitForTutorial();
            yield return null; yield return null; yield return null;
            var director = Object.FindFirstObjectByType<TutorialDirector>(); var mission = director.GetComponent<Map01Mission>();
            StartLesson(director, TutorialStep.Shoot); mission.ModernCombat.Equip(WeaponKind.Knife);
            director.shootingGuard.GetComponent<Health>().TakeDamage(1000, director.shootingGuard.transform.position, mission.player.gameObject);
            yield return null; Assert.IsTrue(director.Failed); director.RetryStep();
            Assert.AreEqual(TutorialStep.Shoot, director.Step); Assert.IsTrue(director.shootingGuard.Alive);
            yield return new ExitPlayMode();
        }
        [Test]
        public void GeneratedSceneIsEnabledOnceAndHungIsAVariant()
        {
            Assert.AreEqual(1, EditorBuildSettings.scenes.Count(s => s.path == Scene && s.enabled));
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PlayerHung.prefab");
            Assert.AreEqual(PrefabAssetType.Variant, PrefabUtility.GetPrefabAssetType(prefab));
        }
        [Test]
        public void CargoAndCaptureAreOnTheWoodenBridge()
        {
            EditorSceneManager.OpenScene(Scene);
            var jetty = GameObject.Find("B — captive jetty");
            Assert.IsNotNull(jetty, "The actual Map 1 wooden jetty must be retained.");
            var director = Object.FindFirstObjectByType<TutorialDirector>();
            var capture = director.GetComponent<TutorialCaptureCutscene>();
            var scout = director.GetComponent<TutorialScouting>();
            foreach (Vector3 point in new[] { capture.endPoint.position, scout.supplyCache.position }) {
                bool onBridge = Physics.RaycastAll(point + Vector3.up * 2, Vector3.down, 5).Any(hit => hit.transform.IsChildOf(jetty.transform));
                Assert.IsTrue(onBridge, "Cargo and capture must sit over the wooden bridge, not the far bank.");
            }
            Vector3 towardsHome = Vector3.ProjectOnPlane(director.lessonStarts[0].position - director.noiseTrap.transform.position, Vector3.up).normalized;
            Vector3 escape = Vector3.ProjectOnPlane(capture.endPoint.position - director.noiseTrap.transform.position, Vector3.up).normalized;
            Assert.Greater(Vector3.Dot(towardsHome, escape), .8f);
        }
        [Test]
        public void LessonsProgressDirectlyFromTheOriginalShelterToTheJetty()
        {
            EditorSceneManager.OpenScene(Scene);
            var director = Object.FindFirstObjectByType<TutorialDirector>();
            Vector3 start = director.lessonStarts[0].position;
            Vector3 forward = Vector3.ProjectOnPlane(director.lessonStarts[7].position - start, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            float last = -1;
            for (int i = 0; i <= 8; i++) {
                Vector3 delta = director.lessonStarts[i].position - start;
                Assert.Less(Mathf.Abs(Vector3.Dot(delta, right)), i < 8 ? .1f : 6, "Only the final wooden ramp may turn off the land route.");
                float along = Vector3.Dot(delta, forward);
                Assert.Greater(along, last); last = along;
            }
            Assert.Less(Vector3.Distance(start, GameObject.Find("A — underground friendly shelter").transform.position), 25, "Start near the original shelter.");
            Assert.IsNull(GameObject.Find("Straight tutorial trail"), "Changing the mission route must not create a new road.");
            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(start, director.lessonStarts[8].position, NavMesh.AllAreas, path));
            float distance = 0;
            for (int i = 1; i < path.corners.Length; i++) distance += Vector3.Distance(path.corners[i-1], path.corners[i]);
            Assert.Less(distance, last * 1.12f, "Navigation must not send the player round a detour.");
        }
        [Test]
        public void TutorialPreservesTheOriginalTerrainShelterAndScenery()
        {
            var tutorial = EditorSceneManager.OpenScene(Scene);
            var source = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity", OpenSceneMode.Additive);
            try {
                foreach (string prefix in new[] { "Detailed layout", "03 Surroundings" }) {
                    var actual = tutorial.GetRootGameObjects().Single(g => g.name.StartsWith(prefix));
                    var original = source.GetRootGameObjects().Single(g => g.name.StartsWith(prefix));
                    var placed = actual.GetComponentsInChildren<Transform>(true);
                    var expected = original.GetComponentsInChildren<Transform>(true);
                    Assert.AreEqual(expected.Length, placed.Length, "Do not remove scenery to change the tutorial route.");
                    for (int i = 0; i < expected.Length; i++) {
                        Assert.AreEqual(expected[i].name, placed[i].name);
                        Assert.Less(Vector3.Distance(expected[i].localPosition, placed[i].localPosition), .001f, expected[i].name);
                        Assert.Less(Quaternion.Angle(expected[i].localRotation, placed[i].localRotation), .01f, expected[i].name);
                        Assert.Less(Vector3.Distance(expected[i].localScale, placed[i].localScale), .001f, expected[i].name);
                    }
                    if (!prefix.StartsWith("Detailed")) continue;
                    var ground = actual.GetComponentsInChildren<MeshCollider>().Single(c => c.name.StartsWith("Continuous hills"));
                    var originalGround = original.GetComponentsInChildren<MeshCollider>().Single(c => c.name.StartsWith("Continuous hills"));
                    CollectionAssert.AreEqual(originalGround.sharedMesh.vertices, ground.sharedMesh.vertices, "Do not sculpt the terrain.");
                }
            } finally { EditorSceneManager.CloseScene(source, true); }
        }
        [Test]
        public void TheMainRouteHasACompleteNavMeshPath()
        {
            EditorSceneManager.OpenScene(Scene);
            var director = Object.FindFirstObjectByType<TutorialDirector>(); var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.SamplePosition(director.lessonStarts[0].position, out var start, 2, NavMesh.AllAreas));
            Assert.IsTrue(NavMesh.SamplePosition(director.lessonStarts[8].position, out var end, 2, NavMesh.AllAreas));
            Assert.IsTrue(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status);
            for (int i = 1; i <= (int)TutorialStep.Scout; i++) {
                Assert.IsTrue(NavMesh.SamplePosition(director.lessonStarts[i].position, out var lesson, 2, NavMesh.AllAreas));
                Assert.IsTrue(NavMesh.CalculatePath(start.position, lesson.position, NavMesh.AllAreas, path));
                Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status, "Lesson " + i + " must be connected to the base.");
            }
        }
        [UnityTest]
        public IEnumerator CaptureTutorialPreview()
        {
            EditorSceneManager.OpenScene(Scene); SetPreviewResolution(1600, 900);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode();
            UnityEngine.SceneManagement.SceneManager.LoadScene(TutorialDirector.SceneName); yield return WaitForTutorial();
            for (int i = 0; i < 12; i++) yield return null;
            ScreenCapture.CaptureScreenshot("Temp/tutorial-preview.png");
            for (int i = 0; i < 8; i++) yield return null;
            yield return new ExitPlayMode();
        }
    }
}
