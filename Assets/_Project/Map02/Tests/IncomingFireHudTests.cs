using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class IncomingFireHudTests : ForestSceneTestBase
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly FieldInfo ShotTime = typeof(Map01Hud).GetField("incomingFireAt", Private);

        [TestCase(0f)]
        [TestCase(45f)]
        [TestCase(90f)]
        [TestCase(135f)]
        [TestCase(180f)]
        [TestCase(-135f)]
        [TestCase(-90f)]
        [TestCase(-45f)]
        public void ArcUsesCameraHeadingOnAllEightSides(float expected)
        {
            var method = typeof(Map01Hud).GetMethod("TryIncomingFireAngle", BindingFlags.NonPublic | BindingFlags.Static);
            var camera = Quaternion.Euler(90, 67, 0);
            var attacker = Quaternion.Euler(0, 67 + expected, 0) * Vector3.forward + Vector3.up;
            var args = new object[] { camera * Vector3.right, attacker, 0f };
            Assert.IsTrue((bool)method.Invoke(null, args));
            Assert.AreEqual(0f, Mathf.DeltaAngle(expected, (float)args[2]), .01f);
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator TutorialUsesTheIncomingFireHud() => CheckMapOneHud("Map00_Tutorial");

        [UnityTest, Timeout(180000)]
        public IEnumerator MapOneGuardReportsShotsToTheHud() => CheckMapOneHud("Map 1");

        private static IEnumerator CheckMapOneHud(string scene)
        {
            EditorSceneManager.OpenScene($"Assets/_Project/Scenes/Maps/{scene}.unity");
            yield return new EnterPlayMode();
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            float deadline = Time.realtimeSinceStartup + 40f;
            while ((mission?.ModernHealth == null || mission.Stopped) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.NotNull(mission?.ModernHealth);
            Assert.IsFalse(mission.Stopped);
            var hud = mission.GetComponent<Map01Hud>();
            yield return null;
            hud.ClearIncomingFire();
            if (scene == "Map 1")
            {
                var guard = mission.Enemies.First(e => e.Alive);
                typeof(Map01EnemyController).GetField("_nextShot", Private).SetValue(guard, 0f);
                typeof(Map01EnemyController).GetField("_shots", Private).SetValue(guard, 0);
                guard.FireAtPlayer();
            }
            else mission.ModernHealth.NotifyIncomingFire(mission.player.position + Vector3.right * 12);
            AssertCueStarted(hud);
            yield return new ExitPlayMode();
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator MapTwoGunfireShowsTheCueAndRestoreClearsIt()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity");
            SetPreviewResolution(1280, 720);
            yield return new EnterPlayMode();
            Map02Mission mission = null;
            float deadline = Time.realtimeSinceStartup + 100f;
            while (Time.realtimeSinceStartup < deadline)
            {
                mission = Object.FindFirstObjectByType<Map02Mission>();
                if (mission?.Ready == true && mission.Chapter != null) break;
                Object.FindFirstObjectByType<Map02Arrival>()?.Skip();
                yield return null;
            }
            Assert.NotNull(mission?.CharacterHud);
            var preview = Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor", true);
            preview.GetMethod("ApplyToMission").Invoke(null, new object[] { mission, 2 });
            yield return null;
            var save = Map02Mission.Parse(JsonUtility.ToJson(mission.Capture()));
            var shooter = new GameObject("Incoming fire test guard");
            var right = Vector3.ProjectOnPlane(mission.GameCamera.transform.right, Vector3.up).normalized;
            shooter.transform.position = mission.Player.position + (right + Vector3.Cross(right, Vector3.up)).normalized * 12;
            var guard = shooter.AddComponent<Map02ChapterEnemy>();
            guard.Initialize(mission.Chapter, "incoming_fire_test", "hud_test", 100, false);
            guard.enabled = false;
            guard.FireAtPlayer();
            AssertCueStarted(mission.CharacterHud);
            Directory.CreateDirectory("Logs/HudDirectionPreview");
            ScreenCapture.CaptureScreenshot("Logs/HudDirectionPreview/incoming-fire-gameplay.png");
            yield return null;
            mission.Restore(save);
            Assert.IsTrue(float.IsNegativeInfinity((float)ShotTime.GetValue(mission.CharacterHud)));
            Object.Destroy(shooter);
            yield return new ExitPlayMode();
        }

        [Test]
        public void CueHoldsThenFadesWithinTheApprovedDuration()
        {
            var opacity = typeof(Map01Hud).GetMethod("IncomingFireOpacity", BindingFlags.NonPublic | BindingFlags.Static);
            float At(float time) => (float)opacity.Invoke(null, new object[] { time });
            Assert.AreEqual(1f, At(.375f));
            Assert.That(At(.8f), Is.InRange(.01f, .99f));
            Assert.AreEqual(0f, At(1.25f));
        }

        private static void AssertCueStarted(Map01Hud hud)
        {
            Assert.NotNull(hud);
            Assert.AreEqual(Time.unscaledTime, (float)ShotTime.GetValue(hud));
        }
    }
}
