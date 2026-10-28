using System.Collections;
using NUnit.Framework;
using ShadowVale.Gameplay.Enemies.Boss;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// Grenades as changed on 2026-10-22: Nam's F1 goes off as it lands (no 3.5 s on the ground), and the commander's
    /// are marked from the moment they leave his hand (Map01GrenadeWarning, Map01Hud.Grenade.cs) — the blast's ring on
    /// the ground with a second ring burning out to it, an F1 in place of the grey ball, the HUD's pointer red inside
    /// the blast and amber near it, nothing for one far off, and all of it gone with the blast.
    /// </summary>
    public sealed class GrenadeWarningTests : ForestSceneTestBase
    {
        private const string Out = "Docs/Previews/Map01Items/";

        [UnityTest]
        public IEnumerator NamsF1GoesOffAsItLands()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            mission.GetComponent<Map01Rescue>().MarkIntroSeen();
            foreach (var e in mission.Enemies) e.enabled = false;
            var thrower = mission.GetComponent<Map01StoneThrow>();
            var inventory = mission.GetComponent<Map01Inventory>();
            inventory.Add("grenade_f1", 1); inventory.SelectThrowable("grenade_f1");
            yield return null;
            float thrown = Time.time;
            Assert.IsTrue(thrower.ThrowAt(mission.player.position + mission.player.forward * 11f));
            while (Object.FindObjectsByType<Map01Blast>(FindObjectsSortMode.None).Length == 0 && Time.time - thrown < 8f) yield return null;
            Assert.IsNotEmpty(Object.FindObjectsByType<Map01Blast>(FindObjectsSortMode.None), "It went off.");
            // The hand opens at StoneReleaseDelay, 11 m of lob takes under a second; the old fuse alone was 3.5 s.
            Assert.Less(Time.time - thrown, 2f, "It goes off where it comes down, with no fuse left to burn on the ground.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator TheCommandersGrenadeIsMarkedFromTheThrow()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            mission.GetComponent<Map01Rescue>().MarkIntroSeen();
            foreach (var e in mission.Enemies) e.enabled = false;
            mission.Say(null, -1f);
            SetPreviewResolution(1920, 1080);
            yield return null;
            var warning = mission.GetComponent<Map01GrenadeWarning>();
            Assert.IsNotNull(warning, "Map 1 marks the commander's grenades.");
            var hud = mission.GetComponent<Map01Hud>();
            var profile = Object.FindFirstObjectByType<BossBehaviour>().Profile;   // the C3 commander's own: 7.5 m, 1.5 s
            Vector3 nam = mission.player.position, side = mission.player.right, from = nam + mission.player.forward * 14f + Vector3.up * 1.6f;

            // One far off is drawn on the ground but not pointed out; one just outside the blast is pointed out in amber.
            var far = Grenade.Launch(from, nam - side * (profile.GrenadeRadius + Map01Hud.GrenadeWarnBeyond + 4f), profile, "test");
            yield return null;
            Assert.IsTrue(warning.Marks(far, out _, out _));
            Assert.IsNull(hud.GrenadeThreat(out _), "Too far off to point out.");
            var near = Grenade.Launch(from, nam + side * (profile.GrenadeRadius + 3f), profile, "test");
            yield return null;
            Assert.AreSame(near, hud.GrenadeThreat(out bool insideNear));
            Assert.IsFalse(insideNear, "Near, not inside: amber.");
            far.Explode(); near.Explode();
            yield return null;
            Assert.IsEmpty(Grenade.Live);

            // One thrown at Nam's side: marked the frame it leaves the hand.
            mission.Say(null, -1f);
            var grenade = Grenade.Launch(from, nam + side * 2f, profile, "test");
            yield return null;
            Assert.IsFalse(grenade.Landed, "Still in the air.");
            Assert.IsTrue(warning.Marks(grenade, out var edge, out var fill), "Marked before it lands.");
            Assert.IsTrue(edge.enabled && edge.loop && fill.enabled);
            for (int i = 0; i < edge.positionCount; i++)
                Assert.AreEqual(grenade.Radius, Flat(edge.GetPosition(i) - grenade.Target), .05f, "The ring is the blast, point " + i);
            Assert.AreSame(grenade, hud.GrenadeThreat(out bool inside));
            Assert.IsTrue(inside, "Nam stands inside it: red, with the banner.");
            Assert.IsFalse(grenade.GetComponent<Renderer>().enabled, "No grey ball…");
            Assert.IsNotNull(GameObject.Find("Commander's grenade"), "…an F1 instead.");
            Assert.IsFalse((mission.Dialogue ?? "").Contains("lựu đạn"), "No caption box over the ground round Nam: the banner says it.");
            float early = Flat(fill.GetPosition(0) - grenade.Target);
            yield return Photo("game-grenade-01-vua-nem");

            float half = Time.time + grenade.TotalSeconds * .6f;
            while (Time.time < half) yield return null;
            Assert.IsTrue(grenade.Landed);
            Assert.Greater(Flat(fill.GetPosition(0) - grenade.Target), early + 1f, "The inner ring burns out towards the edge.");
            yield return Photo("game-grenade-02-sap-no");

            float until = Time.time + 5f;
            while (grenade != null && !grenade.Exploded && Time.time < until) yield return null;
            yield return null;
            Assert.IsTrue(edge == null && fill == null, "The rings go with the blast.");
            Assert.IsNull(GameObject.Find("Commander's grenade"));
            Assert.IsEmpty(Grenade.Live);
            Assert.IsNull(hud.GrenadeThreat(out _));
            yield return new ExitPlayMode();
        }

        private static float Flat(Vector3 v) { v.y = 0; return v.magnitude; }

        private static IEnumerator Photo(string name)
        {
            string path = Out + name + ".png";
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            float until = Time.realtimeSinceStartup + 5;
            while (!System.IO.File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
        }
    }
}
