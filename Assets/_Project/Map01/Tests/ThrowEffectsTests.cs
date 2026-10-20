using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// How the throwables look (Map01Blast, Map01Smoke, Map01CameraShake, Map01StoneThrow): the F1's flash, dirt, dust
    /// ring, smoke column and scorch; the smoke bomb's sparks, jet and cloud; the camera shake near a blast; and that it
    /// all clears away.
    /// </summary>
    public sealed class ThrowEffectsTests : ForestSceneTestBase
    {
        [UnityTest]
        public IEnumerator BlastAndSmokeBuildTheirEffectsAndClearAway()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            mission.GetComponent<Map01Rescue>().MarkIntroSeen();
            Vector3 at = mission.player.position + mission.player.forward * 12f;

            var blast = Map01Blast.Spawn(at, Vector3.up, mission.gameCamera);
            var parts = blast.GetComponentsInChildren<ParticleSystem>().Select(p => p.name).ToArray();
            CollectionAssert.IsSubsetOf(new[] { "Core", "Flame", "Dirt", "Dust", "Ground ring", "Smoke" }, parts);
            Assert.IsNotNull(blast.GetComponentInChildren<Light>(), "A flash lights the ground.");
            Assert.IsNotNull(blast.transform.Find("Scorch"), "A scorch is left.");
            var bang = blast.Sound;
            Assert.IsNotNull(bang, "It is heard (Resources/Audio/Explosion_Medium_Blast).");
            Assert.AreEqual(1f, bang.spatialBlend, "In 3D, where it went off.");
            Assert.AreEqual(1, bang.clip.channels, "Imported mono, so Unity can place it.");
            Assert.IsNotNull(bang.GetComponent<AudioLowPassFilter>());
            Assert.IsNotNull(bang.GetComponent<AudioReverbFilter>(), "A light echo.");
            Assert.Greater(Map01Blast.MuffleCutoff(2f), Map01Blast.MuffleCutoff(60f) * 2f, "Sharp up close, a dull rumble far off.");
            Assert.IsTrue(mission.gameCamera.TryGetComponent<Map01CameraShake>(out var shake), "The camera 12 m off shakes.");
            Assert.Greater(shake.Strength(), 0f);
            yield return WaitGameSeconds(.5f);
            Assert.IsNull(blast.GetComponentInChildren<Light>(), "The flash is over in a fraction of a second.");
            yield return WaitGameSeconds(1f);
            Assert.AreEqual(0f, shake.Strength(), "The shake has died away.");

            // A blast far off does not shake the camera.
            Map01Blast.Spawn(at + mission.player.forward * 80f, Vector3.up, mission.gameCamera);
            Assert.AreEqual(0f, shake.Strength());

            var cloud = Map01Smoke.Spawn(at + mission.player.right * 6f, Map01StoneThrow.SmokeRadius, Map01StoneThrow.SmokeSeconds);
            var smokeParts = cloud.GetComponentsInChildren<ParticleSystem>().Select(p => p.name).ToArray();
            CollectionAssert.IsSubsetOf(new[] { "Cloud", "Jet", "Ignition" }, smokeParts);
            var dust = Map01Smoke.Spawn(at, 1.6f, 4f, visible: false);
            Assert.IsEmpty(dust.GetComponentsInChildren<ParticleSystem>(), "The blast's dust hides without drawing a second cloud.");
            Assert.IsTrue(Map01Smoke.Blocks(dust.Centre - Vector3.right * 3f, dust.Centre + Vector3.right * 3f));

            Map01Blast.ClearAll(); Map01Smoke.ClearAll();
            yield return null;
            Assert.IsEmpty(Object.FindObjectsByType<Map01Blast>(FindObjectsSortMode.None));
            Assert.IsEmpty(Object.FindObjectsByType<Map01Smoke>(FindObjectsSortMode.None));
            yield return new ExitPlayMode();
        }

        // ---- a look at it in the game, at 1920x1080 ------------------------------------------------------------

        private const string Out = "Docs/Previews/Map01Items/";
        private const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;

        private static IEnumerator Photo(string name)
        {
            string path = Out + name + ".png";
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            float until = Time.realtimeSinceStartup + 5;
            while (!System.IO.File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
        }

        private static IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }

        [UnityTest, Explicit("Photographs for review; writes Docs/Previews/Map01Items/game-fx-*.png.")]
        public IEnumerator PhotographTheThrowEffects()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            mission.GetComponent<Map01Rescue>().MarkIntroSeen();
            foreach (var e in mission.Enemies) { e.enabled = false; var agent = e.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (agent != null) agent.enabled = false; }
            var data = Resources.Load<Map01StoneSiteData>(Map01StoneSiteData.ResourcePath);
            var site = data.sites.First(s => s.source == Map01StoneSiteData.Source.Trail);
            var controller = mission.player.GetComponent<CharacterController>();
            controller.enabled = false; mission.player.position = site.position + Vector3.up * .1f; controller.enabled = true;
            var rig = mission.gameCamera.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>();
            typeof(ShadowVale.Gameplay.Player.ThirdPersonCamera).GetField("_pitch", Private).SetValue(rig, 14f);
            SetPreviewResolution(1920, 1080);
            mission.Say(null, -1f);
            yield return Frames(10);
            Time.captureDeltaTime = 1f / 60f;                     // a steady clock: each frame is 1/60 s of game time
            try
            {
                var thrower = mission.GetComponent<Map01StoneThrow>();
                var inventory = mission.GetComponent<Map01Inventory>();
                Vector3 ahead = mission.player.position + mission.player.forward * 11f;

                // smoke bomb: sparks and the first jet, the cloud building, standing, thinning
                inventory.Add("smoke_bomb", 1); inventory.SelectThrowable("smoke_bomb");
                Assert.IsTrue(thrower.ThrowAt(ahead));
                while (Map01Smoke.Active.Count == 0) yield return null;
                mission.Say(null, -1f);
                yield return Frames(12); yield return Photo("game-fx-01-khoi-moi-chay");
                yield return Frames(60); yield return Photo("game-fx-02-khoi-day-len");
                yield return Frames(240); yield return Photo("game-fx-03-may-khoi");
                yield return Frames(600); yield return Photo("game-fx-04-khoi-tan");
                Map01Smoke.ClearAll();
                yield return Frames(30);

                // F1: thrown, its fuse smoking where it lies, then the blast frame by frame
                inventory.Add("grenade_f1", 1); inventory.SelectThrowable("grenade_f1");
                Assert.IsTrue(thrower.ThrowAt(ahead));
                yield return Frames(70); mission.Say(null, -1f); yield return Photo("game-fx-05-f1-ngoi-chay");
                while (Object.FindObjectsByType<Map01Blast>(FindObjectsSortMode.None).Length == 0) yield return null;
                mission.Say(null, -1f);
                yield return Photo("game-fx-06-f1-chop");
                yield return Frames(8); yield return Photo("game-fx-07-f1-dat-bung");
                yield return Frames(25); yield return Photo("game-fx-08-f1-bui");
                yield return Frames(90); yield return Photo("game-fx-09-f1-cot-khoi");
                yield return Frames(300); yield return Photo("game-fx-10-f1-vet-chay");
            }
            finally { Time.captureDeltaTime = 0f; }
            yield return new ExitPlayMode();
        }
    }
}
