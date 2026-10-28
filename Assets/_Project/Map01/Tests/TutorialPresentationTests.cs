using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class TutorialPresentationTests : ForestSceneTestBase
    {
        private const string Scene = "Assets/_Project/Scenes/Maps/Map00_Tutorial.unity";
        private static IEnumerator Load()
        {
            var loading = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(TutorialDirector.SceneName);
            while (!loading.isDone) yield return null;
            double until = Time.realtimeSinceStartupAsDouble + 12;
            while (Time.realtimeSinceStartupAsDouble < until) {
                var d = Object.FindFirstObjectByType<TutorialDirector>();
                if (d != null && d.GetComponent<Map01Mission>().RetryRequested != null && d.GetComponent<TutorialRouteGuide>() != null) {
                    for (int i = 0; i < 3; i++) yield return null; yield break;
                }
                yield return null;
            }
            Assert.Fail("Tutorial did not initialize.");
        }
        private static void Begin(TutorialDirector d, TutorialStep step) => typeof(TutorialDirector).GetMethod("BeginStep", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(d, new object[] { step });
        private static IEnumerator GoTo(TutorialDirector d, TutorialStep step)
        {
            var mission = d.GetComponent<Map01Mission>(); var body = mission.player.GetComponent<CharacterController>();
            body.enabled = false; mission.player.position = d.lessonStarts[(int)step].position; body.enabled = true;
            d.GetComponent<TutorialTimeControl>().SetSlow(false);
            float settle = Time.unscaledTime + .9f;
            while (Time.unscaledTime < settle) yield return null;
        }
        private static IEnumerator Photo(string name)
        {
            Directory.CreateDirectory("Temp/TutorialPresentation"); ScreenCapture.CaptureScreenshot("Temp/TutorialPresentation/" + name + ".png");
            for (int i = 0; i < 3; i++) yield return null;
        }
        [UnityTest]
        public IEnumerator CombatLessonsShowTargetsThenReturnToHintsWithoutNextLabel()
        {
            EditorSceneManager.OpenScene(Scene); SetPreviewResolution(1600, 900);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene); yield return new EnterPlayMode();
            yield return Load(); var d = Object.FindFirstObjectByType<TutorialDirector>(); var m = d.GetComponent<Map01Mission>();
            var ui = Object.FindFirstObjectByType<TutorialPromptUI>();
            Assert.IsNull(ui.transform.Find("Hint/Next state"));
            Assert.IsFalse(ui.GetComponentsInChildren<UnityEngine.UI.Text>(true).Any(t => t.text == "TIẾP THEO"));
            yield return Photo("01-hint-without-next");
            foreach (var step in new[] { TutorialStep.StoneKnife, TutorialStep.Shoot, TutorialStep.Squad }) {
                yield return GoTo(d, step);
                var position = m.player.position; var cameraPosition = m.gameCamera.transform.position; var cameraRotation = m.gameCamera.transform.rotation;
                float fov = m.gameCamera.fieldOfView;
                Begin(d, step); Assert.IsTrue(d.RevealingTargets); Assert.IsTrue(m.Cinematic); Assert.IsFalse(m.CameraInputEnabled);
                double until = Time.realtimeSinceStartupAsDouble + 6;
                while (d.RevealProgress < .43f && Time.realtimeSinceStartupAsDouble < until) yield return null;
                Assert.That(d.RevealProgress, Is.InRange(.43f, .65f));
                Assert.IsFalse(ui.transform.Find("Hint").gameObject.activeSelf, "Key instructions wait until the camera has returned.");
                var targets = step == TutorialStep.StoneKnife ? new[] { d.knifeGuard } : step == TutorialStep.Shoot ? new[] { d.shootingGuard } : d.squad;
                foreach (var enemy in targets) {
                    var view = m.gameCamera.WorldToViewportPoint(enemy.transform.position + Vector3.up * 1.15f);
                    Assert.Greater(view.z, 0); Assert.That(view.x, Is.InRange(.05f, .95f), enemy.name); Assert.That(view.y, Is.InRange(.1f, .9f), enemy.name);
                    Assert.IsFalse(enemy.Engaged, "The introduction cannot let AI attack Hùng.");
                    if (m.SightCover != null) Assert.IsFalse(m.SightCover.Blocks(enemy.transform.position + Vector3.up * 1.15f, m.gameCamera.transform.position), "Foliage must not hide the target introduction.");
                }
                Assert.Greater(Vector3.Distance(cameraPosition, m.gameCamera.transform.position), 1);
                yield return Photo("02-intro-" + step);
                while (d.RevealingTargets && Time.realtimeSinceStartupAsDouble < until) yield return null;
                Assert.IsFalse(d.RevealingTargets); Assert.IsFalse(m.Cinematic); Assert.Less(Vector3.Distance(position, m.player.position), .03f);
                Assert.Less(Vector3.Distance(cameraPosition, m.gameCamera.transform.position), .08f); Assert.Less(Quaternion.Angle(cameraRotation, m.gameCamera.transform.rotation), .5f);
                Assert.That(m.gameCamera.fieldOfView, Is.EqualTo(fov).Within(.5f));
                yield return null; Assert.IsTrue(ui.transform.Find("Hint").gameObject.activeSelf);
                Begin(d, TutorialStep.Move); // End this lesson before AI begins practice.
            }
            Assert.AreEqual(TutorialDirector.SceneName, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator TargetRevealPausesAndCancelsForRetryFailureAndChangingLessons()
        {
            EditorSceneManager.OpenScene(Scene); SetPreviewResolution(1600, 900);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene); yield return new EnterPlayMode();
            yield return Load(); var d = Object.FindFirstObjectByType<TutorialDirector>(); var m = d.GetComponent<Map01Mission>();
            yield return GoTo(d, TutorialStep.StoneKnife); Begin(d, TutorialStep.StoneKnife);
            for (int i = 0; i < 8; i++) yield return null;
            m.SetPaused(true); var at = m.gameCamera.transform.position; var rotation = m.gameCamera.transform.rotation; float progress = d.RevealProgress;
            for (int i = 0; i < 6; i++) yield return null;
            Assert.AreEqual(progress, d.RevealProgress); Assert.Less(Vector3.Distance(at, m.gameCamera.transform.position), .001f); Assert.Less(Quaternion.Angle(rotation, m.gameCamera.transform.rotation), .01f);
            m.SetPaused(false); d.RetryStep(); Assert.IsTrue(d.RevealingTargets); Assert.Less(d.RevealProgress, .1f);
            d.Fail("Reveal cleanup check"); Assert.IsFalse(d.RevealingTargets); Assert.IsFalse(m.Cinematic); Assert.IsTrue(m.gameCamera.GetComponent<ThirdPersonCamera>().enabled);
            d.RetryStep(); Assert.IsTrue(d.RevealingTargets);
            Begin(d, TutorialStep.Move); Assert.IsFalse(d.RevealingTargets); Assert.IsFalse(m.Cinematic);
            for (int i = 0; i < 6; i++) yield return null;
            Assert.IsFalse(m.Cinematic); Assert.IsTrue(m.gameCamera.GetComponent<ThirdPersonCamera>().enabled);
            Assert.AreEqual(TutorialDirector.SceneName, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator PursuerStaysVisibleAndMovesContinuouslyAcrossChaseCuts()
        {
            EditorSceneManager.OpenScene(Scene); SetPreviewResolution(1600, 900);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene); yield return new EnterPlayMode();
            yield return Load(); var d = Object.FindFirstObjectByType<TutorialDirector>(); var m = d.GetComponent<Map01Mission>();
            var body = m.player.GetComponent<CharacterController>(); body.enabled = false; m.player.position = d.noiseTrap.transform.position; body.enabled = true;
            Begin(d, TutorialStep.Return);
            var original = d.patrol.SelectMany(e => e.GetComponentsInChildren<SkinnedMeshRenderer>(true)).ToDictionary(s => s, s => s.updateWhenOffscreen);
            bool occlusion = m.gameCamera.useOcclusionCulling;
            d.BeginCapture(); var film = d.GetComponent<TutorialCaptureCutscene>();
            int oldRate = Time.captureFramerate, frame = 0; Vector3? previous = null; float sampledAt = 0;
            float deckHeight = d.noiseTrap.transform.position.y;
            var positions = new System.Text.StringBuilder("shot,seconds,x,y,z,speed,verticalSpeed\n");
            Directory.CreateDirectory("Temp/TutorialChaseReview/Frames");
            try {
                Time.captureFramerate = 24; double until = Time.realtimeSinceStartupAsDouble + 35;
                while (film.CurrentShot < TutorialCaptureCutscene.Shot.SeatedFall) {
                    Assert.Less(Time.realtimeSinceStartupAsDouble, until);
                    yield return new WaitForEndOfFrame();
                    if (film.CurrentShot >= TutorialCaptureCutscene.Shot.Discovery) {
                        var position = film.Pursuer.position;
                        float speed = previous.HasValue ? Vector3.Distance(position, previous.Value) / (Time.time - sampledAt) : 0;
                        float verticalSpeed = previous.HasValue ? Mathf.Abs(position.y - previous.Value.y) / (Time.time - sampledAt) : 0;
                        positions.AppendLine($"{film.CurrentShot},{film.ShotElapsed:F3},{position.x:F3},{position.y:F3},{position.z:F3},{speed:F3},{verticalSpeed:F3}");
                        if (speed >= 6) ScreenCapture.CaptureScreenshot("Temp/TutorialChaseReview/pursuer-discontinuity.png");
                        Assert.Less(speed, 6, "Pursuer teleports in 3D at " + film.CurrentShot + " " + film.ShotElapsed);
                        if (film.CurrentShot == TutorialCaptureCutscene.Shot.Discovery || film.CurrentShot == TutorialCaptureCutscene.Shot.ChaseWide)
                            Assert.Greater(position.y, deckHeight - .08f, "The pursuer must stay on the elevated deck and access ramp, above the riverbed.");
                        previous = position; sampledAt = Time.time;
                        Assert.IsTrue(film.Pursuer.gameObject.activeInHierarchy); Assert.IsFalse(m.gameCamera.useOcclusionCulling);
                        foreach (var skin in film.Pursuer.GetComponentsInChildren<SkinnedMeshRenderer>()) { Assert.IsTrue(skin.enabled); Assert.IsFalse(skin.forceRenderingOff); Assert.IsTrue(skin.updateWhenOffscreen); }
                        if ((film.CurrentShot == TutorialCaptureCutscene.Shot.ChaseWide || film.CurrentShot == TutorialCaptureCutscene.Shot.ChaseFront) && film.ShotElapsed > .1f) {
                            var view = m.gameCamera.WorldToViewportPoint(film.Pursuer.position + Vector3.up * 1.2f);
                            Assert.Greater(view.z, 0); Assert.That(view.x, Is.InRange(.03f, film.CurrentShot == TutorialCaptureCutscene.Shot.ChaseFront ? .5f : .97f)); Assert.That(view.y, Is.InRange(.09f, .91f));
                            var hung = m.gameCamera.WorldToViewportPoint(m.player.position + Vector3.up * 1.2f);
                            Assert.Greater(hung.z, 0); Assert.That(hung.x, Is.InRange(.03f, .97f)); Assert.That(hung.y, Is.InRange(.09f, .91f));
                        }
                        ScreenCapture.CaptureScreenshot($"Temp/TutorialChaseReview/Frames/{frame++:D4}.png");
                    }
                    yield return null;
                }
                for (int i = 0; i < 3; i++) yield return null;
            } finally {
                Time.captureFramerate = oldRate; film.Cancel();
                File.WriteAllText("Temp/TutorialChaseReview/frame-count.txt", frame.ToString()); File.WriteAllText("Temp/TutorialChaseReview/positions.csv", positions.ToString());
                File.WriteAllText("Temp/TutorialChaseReview/fps.txt", "12");
            }
            foreach (var pair in original) Assert.AreEqual(pair.Value, pair.Key.updateWhenOffscreen);
            Assert.AreEqual(occlusion, m.gameCamera.useOcclusionCulling); Assert.IsFalse(m.Cinematic);
            Assert.AreEqual(TutorialDirector.SceneName, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            yield return new ExitPlayMode();
        }
    }
}
