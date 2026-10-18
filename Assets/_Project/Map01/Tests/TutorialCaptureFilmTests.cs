using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class TutorialCaptureFilmTests : ForestSceneTestBase
    {
        private const string Scene = "Assets/_Project/Scenes/Maps/Map00_Tutorial.unity";
        private static IEnumerator LoadFreshTutorial()
        {
            var loading = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(TutorialDirector.SceneName);
            while (!loading.isDone) yield return null;
            yield return null; yield return null;
            yield return Ready();
        }
        private static IEnumerator Ready()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 12;
            while (Time.realtimeSinceStartupAsDouble < deadline) {
                var director = Object.FindFirstObjectByType<TutorialDirector>();
                if (director != null && director.GetComponent<Map01Mission>().IsInitialized && director.GetComponent<Map01Mission>().RetryRequested != null) yield break;
                yield return null;
            }
            Assert.Fail("Tutorial did not initialize.");
        }
        private static TutorialCaptureCutscene StartCapture()
        {
            var director = Object.FindFirstObjectByType<TutorialDirector>(); var mission = director.GetComponent<Map01Mission>();
            var body = mission.player.GetComponent<CharacterController>(); body.enabled = false;
            mission.player.position = director.noiseTrap.transform.position; body.enabled = true;
            typeof(TutorialDirector).GetMethod("BeginStep", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(director, new object[] { TutorialStep.Return });
            director.BeginCapture(); return director.GetComponent<TutorialCaptureCutscene>();
        }
        private static IEnumerator At(TutorialCaptureCutscene film, TutorialCaptureCutscene.Shot shot, float elapsed = .15f)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 40;
            var walls = GameObject.Find("Tutorial Root").GetComponentsInChildren<BoxCollider>(true);
            while (film != null && (film.CurrentShot != shot || film.ShotElapsed < elapsed) && Time.realtimeSinceStartupAsDouble < deadline) {
                foreach (var wall in walls) {
                    if (wall.name != "Tutorial boundary") continue;
                    foreach (var actor in new[] { film.GetComponent<Map01Mission>().player, film.Pursuer, film.BlockingSoldier }) {
                        var center = actor.position + Vector3.up * .8f;
                        Assert.Greater(Vector3.Distance(center, wall.ClosestPoint(center)), .22f, actor.name + " must not cross the existing fence.");
                    }
                }
                yield return null;
            }
            Assert.IsNotNull(film); Assert.AreEqual(shot, film.CurrentShot, "The film must reach each requested shot in order.");
            yield return null;
        }
        private static IEnumerator Photograph(string name)
        {
            Directory.CreateDirectory("Temp/TutorialFilm");
            string path = "Temp/TutorialFilm/" + name + ".png"; if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            for (int i = 0; i < 3; i++) yield return null;
        }
        private static Transform Rifle(TutorialCaptureCutscene film) => Actor(film.BlockingSoldier).GetComponent<Map01Rifle>().weapon.transform;
        private static Vector3 Stock(Transform rifle) => rifle.TransformPoint(new Vector3(-.038f, .007f, -.34f));
        private static void OnScreen(Camera camera, Vector3 point)
        {
            var view = camera.WorldToViewportPoint(point);
            Assert.Greater(view.z, .15f, "The wind-up must stay in front of the camera.");
            Assert.That(view.x, Is.InRange(.04f, .96f), "The raised rifle must be visible across the frame.");
            Assert.That(view.y, Is.InRange(.09f, .91f), "The raised rifle must clear the letterbox bars.");
        }
        private static Animator Actor(Transform root)
        {
            foreach (var actor in root.GetComponentsInChildren<Animator>()) if (actor.isHuman) return actor;
            return null;
        }
        private static void Elbows(Animator actor)
        {
            foreach (bool right in new[] { false, true }) {
                var upper = actor.GetBoneTransform(right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
                var lower = actor.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
                var hand = actor.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                Assert.Greater(Vector3.Angle(upper.position - lower.position, hand.position - lower.position), 30, actor.name + " elbow folds into the upper arm.");
            }
        }
        private static void StrikeArms(Animator actor, Transform rifle, Camera camera, float seconds, bool framing = true)
        {
            var chest = actor.GetBoneTransform(HumanBodyBones.Chest).position;
            var facing = Vector3.ProjectOnPlane(actor.transform.forward, Vector3.up).normalized;
            foreach (bool right in new[] { false, true }) {
                var hand = actor.GetBoneTransform(right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand).position;
                var elbow = actor.GetBoneTransform(right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm).position;
                string label = $"{(right ? "Right" : "Left")} arm at {seconds:F2}s";
                Assert.Greater(Vector3.Dot(hand - chest, facing), .05f, label + ": wrist must stay in front of the torso.");
                Assert.Greater(Vector3.Dot(elbow - chest, facing), -.025f, label + ": elbow must not bend behind the torso.");
                if (framing) OnScreen(camera, hand);
                var grip = rifle.TransformPoint(right ? new Vector3(.011f, .007f, -.004f) : new Vector3(-.04f, .007f, .14f));
                Assert.Less(Vector3.Distance(hand, grip), .13f, label + ": hand must remain on its rifle grip.");
            }
            Elbows(actor);
        }
        [Test]
        public void FilmClipsAreIncludedWithoutChangingTheMap()
        {
            var assets = TutorialCaptureAssets.Load(); Assert.IsNotNull(assets);
            foreach (var clip in new[] { assets.run, assets.hit, assets.idle, assets.startled, assets.enemyRun, assets.enemyAim, assets.enemyFire, assets.enemyWalk, assets.unarmedWalk }) {
                Assert.IsNotNull(clip); Assert.IsTrue(clip.humanMotion, clip.name);
            }
            Assert.IsNotNull(assets.concussionProfile);
            Assert.IsNotEmpty(assets.footMeshes);
            foreach (var data in assets.footMeshes) { Assert.IsNotEmpty(data.left); Assert.IsNotEmpty(data.right); }
        }
        [UnityTest]
        public IEnumerator CaptureFilmUsesTheRequestedFramingContactsAndEnding()
        {
            EditorSceneManager.OpenScene(Scene); SetPreviewResolution(1600, 900);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode(); yield return LoadFreshTutorial();
            var film = StartCapture(); var mission = film.GetComponent<Map01Mission>(); var actor = Actor(mission.player); var camera = mission.gameCamera;
            yield return At(film, TutorialCaptureCutscene.Shot.Stumble, .2f); Assert.Less(camera.fieldOfView, 40);
            yield return Photograph("01-stumble");
            yield return At(film, TutorialCaptureCutscene.Shot.Discovery); yield return Photograph("02-discovery");
            yield return At(film, TutorialCaptureCutscene.Shot.ChaseWide, .6f); yield return Photograph("03-chase-orbit");
            yield return At(film, TutorialCaptureCutscene.Shot.ChaseFront, .5f);
            var hungView = camera.WorldToViewportPoint(actor.GetBoneTransform(HumanBodyBones.Chest).position);
            var enemyView = camera.WorldToViewportPoint(Actor(film.Pursuer).GetBoneTransform(HumanBodyBones.Chest).position);
            Assert.That(hungView.x, Is.InRange(.5f, .95f), "Hùng stays on the right.");
            Assert.That(enemyView.x, Is.InRange(.05f, .5f), "Pursuer stays on the left.");
            Assert.Greater(enemyView.z, hungView.z + 1, "Pursuer is farther from the camera.");
            yield return Photograph("04-front-diagonal");
            yield return At(film, TutorialCaptureCutscene.Shot.Gunman, .4f); Elbows(Actor(film.Pursuer)); yield return Photograph("05-gunman");
            yield return At(film, TutorialCaptureCutscene.Shot.ShoulderHit, .08f); Assert.IsTrue(film.ShotFired);
            Assert.Less(Vector3.Distance(film.BloodPosition, actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position), .5f);
            Assert.IsNotNull(GameObject.Find("Blood impacts")); yield return Photograph("06-arm-hit");
            yield return At(film, TutorialCaptureCutscene.Shot.WoundedRun, .5f);
            Assert.Less(Vector3.Distance(actor.GetBoneTransform(HumanBodyBones.LeftHand).position, actor.GetBoneTransform(HumanBodyBones.RightUpperArm).position), .3f, "Left hand holds the wounded right arm.");
            Elbows(actor);
            yield return Photograph("07-wounded-run");
            yield return At(film, TutorialCaptureCutscene.Shot.Collision, .5f); yield return Photograph("08-collision");
            yield return At(film, TutorialCaptureCutscene.Shot.SeatedFall, 1f);
            Assert.Less(actor.GetBoneTransform(HumanBodyBones.Hips).position.y - mission.player.position.y, .55f, "Hùng sits down instead of playing a standing death.");
            Elbows(actor);
            yield return Photograph("09-seated-fall");
            yield return At(film, TutorialCaptureCutscene.Shot.VictimView, .7f);
            Assert.Less(Vector3.Distance(camera.transform.position, actor.GetBoneTransform(HumanBodyBones.Head).position), .35f);
            yield return Photograph("10-victim-view");
            yield return At(film, TutorialCaptureCutscene.Shot.VictimView, 1.3f);
            var rifle = Rifle(film); OnScreen(camera, Stock(rifle)); OnScreen(camera, rifle.GetComponent<ShadowVale.Gameplay.Combat.Weapon>().Muzzle.position);
            StrikeArms(Actor(film.BlockingSoldier), rifle, camera, film.ShotElapsed);
            Assert.AreEqual(0, film.ButtImpacts); Assert.AreEqual(0, film.Fade, "The wind-up must be readable before the hit.");
            yield return Photograph("10a-rifle-raised");
            yield return At(film, TutorialCaptureCutscene.Shot.VictimView, 1.90f);
            Assert.AreEqual(1, film.ButtImpacts); Assert.AreEqual(0, film.Fade, "Blackout begins after the stock makes contact.");
            Assert.Less(Vector3.Distance(Stock(rifle), camera.transform.position), .25f, "The rifle stock must reach Hùng's face at impact.");
            StrikeArms(Actor(film.BlockingSoldier), rifle, camera, film.ShotElapsed, false);
            Elbows(Actor(film.BlockingSoldier)); yield return Photograph("10b-rifle-stock-impact");
            yield return At(film, TutorialCaptureCutscene.Shot.VictimView, 2.12f);
            Assert.Greater(film.VisionBlur, .1f, "Vision loses focus before the fade to black.");
            Assert.AreEqual(0, film.Fade);
            yield return Photograph("10c-impact-shake");
            yield return At(film, TutorialCaptureCutscene.Shot.VictimView, 2.65f);
            Assert.Greater(film.VisionBlur, .9f); Assert.Greater(film.CameraShakeEuler.y, 20, "Hùng's view falls to one side.");
            yield return Photograph("10d-head-drops-and-blurs");
            yield return At(film, TutorialCaptureCutscene.Shot.VictimView, 2.95f);
            Assert.That(film.Fade, Is.InRange(.1f, .95f)); yield return Photograph("10e-losing-consciousness");
            yield return At(film, TutorialCaptureCutscene.Shot.Blackout); Assert.AreEqual(1, film.ButtImpacts); Assert.AreEqual(1, film.Fade);
            yield return Photograph("11-blackout");
            var image = new Texture2D(2, 2);
            try {
                Assert.IsTrue(image.LoadImage(File.ReadAllBytes("Temp/TutorialFilm/11-blackout.png")));
                for (int y = 1; y < 8; y++) for (int x = 1; x < 12; x++)
                    Assert.Less(image.GetPixel(image.width * x / 12, image.height * y / 8).maxColorComponent, .03f, "The rendered screen must actually be black, including subtitles.");
            } finally { Object.Destroy(image); }
            yield return At(film, TutorialCaptureCutscene.Shot.Dragging, .8f);
            Assert.Less(Vector3.Distance(Actor(film.BlockingSoldier).GetBoneTransform(HumanBodyBones.LeftHand).position, actor.GetBoneTransform(HumanBodyBones.LeftHand).position), .18f);
            Assert.Less(Vector3.Distance(Actor(film.Pursuer).GetBoneTransform(HumanBodyBones.RightHand).position, actor.GetBoneTransform(HumanBodyBones.RightHand).position), .18f);
            Elbows(actor); Elbows(Actor(film.Pursuer)); Elbows(Actor(film.BlockingSoldier));
            yield return Photograph("12-drag-to-bridge");
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == TutorialDirector.SceneName && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.AreEqual("Map 1", UnityEngine.SceneManagement.SceneManager.GetActiveScene().name); Assert.AreEqual(1, Time.timeScale);
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator RecordStockStrikeBlackoutAndDragReveal()
        {
            EditorSceneManager.OpenScene(Scene); SetPreviewResolution(1600, 900);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode(); yield return LoadFreshTutorial();
            var film = StartCapture(); yield return At(film, TutorialCaptureCutscene.Shot.VictimView, .05f);
            int previousCaptureRate = Time.captureFramerate, frame = 0, blackFrames = 0;
            Directory.CreateDirectory("Temp/TutorialStrikeImpactReview/Frames");
            var samples = new System.Text.StringBuilder("seconds,right_wrist_front,left_wrist_front,right_elbow_front,left_elbow_front\n");
            try {
                Time.captureFramerate = 24;
                double deadline = Time.realtimeSinceStartupAsDouble + 55;
                while (film.CurrentShot != TutorialCaptureCutscene.Shot.Dragging || film.ShotElapsed < 1.1f) {
                    Assert.Less(Time.realtimeSinceStartupAsDouble, deadline);
                    if (film.CurrentShot == TutorialCaptureCutscene.Shot.VictimView && film.ShotElapsed > .2f) {
                        var actor = Actor(film.BlockingSoldier); var chest = actor.GetBoneTransform(HumanBodyBones.Chest).position;
                        samples.Append(film.ShotElapsed.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                        foreach (var id in new[] { HumanBodyBones.RightHand, HumanBodyBones.LeftHand, HumanBodyBones.RightLowerArm, HumanBodyBones.LeftLowerArm })
                            samples.Append(',').Append(Vector3.Dot(actor.GetBoneTransform(id).position - chest, actor.transform.forward).ToString("F4", System.Globalization.CultureInfo.InvariantCulture));
                        samples.AppendLine();
                        StrikeArms(actor, Rifle(film), film.GetComponent<Map01Mission>().gameCamera, film.ShotElapsed, film.ButtImpacts == 0);
                    }
                    if (film.CurrentShot == TutorialCaptureCutscene.Shot.Blackout) { Assert.AreEqual(1, film.Fade); blackFrames++; }
                    if (film.CurrentShot == TutorialCaptureCutscene.Shot.DragReveal) Assert.GreaterOrEqual(blackFrames, 20, "A sustained black screen separates impact from the drag setup.");
                    int rendered = Time.frameCount;
                    ScreenCapture.CaptureScreenshot($"Temp/TutorialStrikeImpactReview/Frames/{frame++:D4}.png");
                    do { yield return null; } while (Time.frameCount <= rendered);
                }
                File.WriteAllText("Temp/TutorialStrikeImpactReview/frame-count.txt", frame.ToString());
                Assert.Greater(blackFrames, 20);
                for (int i = 0; i < 4; i++) yield return null;
            } finally { Time.captureFramerate = previousCaptureRate; film.Cancel(); File.WriteAllText("Temp/TutorialStrikeImpactReview/arm-positions.csv", samples.ToString()); }
            yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator StumblePinsTheToeThenStepsForwardToRecover()
        {
            EditorSceneManager.OpenScene(Scene); SetPreviewResolution(1600, 900);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode(); yield return LoadFreshTutorial();
            var film = StartCapture(); var actor = Actor(film.GetComponent<Map01Mission>().player);
            var stick = film.GetComponent<TutorialDirector>().noiseTrap.transform.Find("Dry branch");
            var branch = stick.position - stick.up * (Mathf.Sign(Vector3.Dot(stick.up, film.EscapeDirection)) * stick.lossyScale.y);
            int oldRate = Time.captureFramerate, frame = 0; Vector3? pinned = null;
            Directory.CreateDirectory("Temp/TutorialStumbleReview/Frames");
            try {
                Time.captureFramerate = 24;
                while (film.CurrentShot == TutorialCaptureCutscene.Shot.Stumble || (film.CurrentShot == TutorialCaptureCutscene.Shot.Discovery && film.ShotElapsed < .3f)) {
                    if (film.CurrentShot == TutorialCaptureCutscene.Shot.Stumble && film.ShotElapsed > .4f && film.ShotElapsed < .75f) {
                        var foot = actor.GetBoneTransform(HumanBodyBones.RightFoot).position;
                        if (pinned.HasValue) Assert.Less(Vector3.Distance(foot, pinned.Value), .03f, "The caught foot stays at the branch while the body tips forward.");
                        else pinned = foot;
                        var toe = foot + film.EscapeDirection * .15f;
                        Assert.That(Vector3.Dot(toe - branch, film.EscapeDirection), Is.InRange(-.20f, -.025f), "The sandal stops against the near side of the branch instead of penetrating it.");
                        Assert.Less(Mathf.Abs(Vector3.Dot(toe - branch, actor.transform.right)), .08f);
                        AssertFootOutsideBranch(actor, stick);
                    }
                    int rendered = Time.frameCount;
                    ScreenCapture.CaptureScreenshot($"Temp/TutorialStumbleReview/Frames/{frame++:D4}.png");
                    do { yield return null; } while (Time.frameCount <= rendered);
                }
                Assert.IsTrue(pinned.HasValue);
                Assert.Greater(Vector3.Dot(actor.GetBoneTransform(HumanBodyBones.LeftFoot).position - branch, film.EscapeDirection), .15f, "The other foot steps past the branch to catch Hùng's weight.");
                File.WriteAllText("Temp/TutorialStumbleReview/frame-count.txt", frame.ToString());
                for (int i = 0; i < 4; i++) yield return null;
            } finally { Time.captureFramerate = oldRate; film.Cancel(); }
            yield return new ExitPlayMode();
        }
        private static void AssertFootOutsideBranch(Animator actor, Transform branch)
        {
            foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                var mesh = new Mesh();
                try {
                    renderer.BakeMesh(mesh); var vertices = mesh.vertices;
                    foreach (var sample in FootSamples(actor, renderer, true)) {
                        var local = branch.InverseTransformPoint(renderer.transform.TransformPoint(vertices[sample.vertexIndex]));
                        float radial = Mathf.Sqrt(local.x * local.x + local.z * local.z);
                        Assert.IsFalse(Mathf.Abs(local.y) < .97f && radial < .47f, "The rendered sandal must not penetrate the branch mesh.");
                    }
                } finally { Object.Destroy(mesh); }
            }
        }
        private static TutorialCaptureAssets.FootVertex[] FootSamples(Animator actor, SkinnedMeshRenderer skin, bool right)
        {
            foreach (var data in TutorialCaptureAssets.Load().footMeshes) {
                var node = string.IsNullOrEmpty(data.rendererPath) ? actor.transform : actor.transform.Find(data.rendererPath);
                if (node == skin.transform && skin.sharedMesh.vertexCount == data.vertexCount) return right ? data.right : data.left;
            }
            return System.Array.Empty<TutorialCaptureAssets.FootVertex>();
        }
        private static bool Floor(Vector3 at, out RaycastHit floor)
        {
            floor = default; float nearest = float.PositiveInfinity;
            foreach (var hit in Physics.RaycastAll(at + Vector3.up * 3, Vector3.down, 6, ~0, QueryTriggerInteraction.Ignore)) {
                if (hit.distance >= nearest || hit.normal.y < .55f || !hit.transform.root.name.StartsWith("Detailed layout")) continue;
                string name = hit.transform.name;
                if (!name.StartsWith("Continuous hills") && name.IndexOf("ramp", System.StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("deck", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                nearest = hit.distance; floor = hit;
            }
            return nearest < float.PositiveInfinity;
        }
        private static float SoleClearance(Animator actor, bool right)
        {
            float clearance = float.PositiveInfinity; int count = 0;
            foreach (var renderer in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                var mesh = new Mesh();
                try {
                    renderer.BakeMesh(mesh); var vertices = mesh.vertices;
                    foreach (var sample in FootSamples(actor, renderer, right)) {
                        count++; var at = renderer.transform.TransformPoint(vertices[sample.vertexIndex]);
                        if (Floor(at, out var floor)) clearance = Mathf.Min(clearance, at.y - floor.point.y);
                    }
                } finally { Object.Destroy(mesh); }
            }
            Assert.Greater(count, 0, "Foot mesh samples must be included for the non-readable model.");
            return clearance;
        }
        private static IEnumerator PhotographFeet(TutorialCaptureCutscene film, string name)
        {
            var mission = film.GetComponent<Map01Mission>(); mission.SetPaused(true);
            var go = new GameObject("Temporary foot contact review camera");
            var camera = go.AddComponent<Camera>(); camera.CopyFrom(mission.gameCamera); camera.depth = mission.gameCamera.depth + 10;
            var at = mission.player.position;
            camera.transform.SetPositionAndRotation(at + mission.player.right * 2.5f + mission.player.forward * .55f + Vector3.up * .65f,
                Quaternion.LookRotation(at + mission.player.forward * .25f + Vector3.up * .35f - (at + mission.player.right * 2.5f + mission.player.forward * .55f + Vector3.up * .65f)));
            camera.fieldOfView = 43;
            try { for (int i = 0; i < 3; i++) yield return null; yield return Photograph(name); }
            finally { Object.Destroy(go); mission.SetPaused(false); }
        }
        [UnityTest]
        public IEnumerator FeetStayAboveDeckAndSlopingGroundThroughoutCapture()
        {
            EditorSceneManager.OpenScene(Scene); SetPreviewResolution(1600, 900);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode(); yield return LoadFreshTutorial();
            var actor = Actor(Object.FindFirstObjectByType<TutorialDirector>().GetComponent<Map01Mission>().player);
            TutorialCaptureCutscene film = null;
            var originals = new System.Collections.Generic.Dictionary<SkinnedMeshRenderer, Mesh>();
            var copies = new System.Collections.Generic.List<Mesh>();
            float minimum = float.PositiveInfinity, highestDrag = 0, next = 0; string worst = ""; bool photographed = false;
            var rows = new System.Text.StringBuilder("shot,seconds,left_sole_clearance,right_sole_clearance\n");
            double deadline = Time.realtimeSinceStartupAsDouble + 70;
            try {
                // Unit tests in the Editor can otherwise retain CPU mesh data that a
                // player discards. Reproduce the user's import/runtime restriction.
                foreach (var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                    originals[skin] = skin.sharedMesh; var copy = Object.Instantiate(skin.sharedMesh); copies.Add(copy);
                    copy.UploadMeshData(true); Assert.IsFalse(copy.isReadable); skin.sharedMesh = copy;
                }
                film = StartCapture();
                while (film != null && film.Playing) {
                    Assert.Less(Time.realtimeSinceStartupAsDouble, deadline);
                    if (Time.time >= next) {
                        next = Time.time + .12f; float left = SoleClearance(actor, false), right = SoleClearance(actor, true);
                        rows.AppendLine($"{film.CurrentShot},{film.ShotElapsed:F3},{left:F4},{right:F4}");
                        if (film.CurrentShot == TutorialCaptureCutscene.Shot.DragReveal || film.CurrentShot == TutorialCaptureCutscene.Shot.Dragging || film.CurrentShot == TutorialCaptureCutscene.Shot.FadeOut)
                            highestDrag = Mathf.Max(highestDrag, Mathf.Max(left, right));
                        if (Mathf.Min(left, right) < minimum) { minimum = Mathf.Min(left, right); worst = $"{film.CurrentShot} at {film.ShotElapsed:F2}s (left {left:F3}m, right {right:F3}m)"; }
                    }
                    if (!photographed && film.CurrentShot == TutorialCaptureCutscene.Shot.Dragging && film.ShotElapsed > 2) { photographed = true; yield return PhotographFeet(film, "17-drag-feet-side"); }
                    yield return null;
                }
                Assert.IsTrue(photographed);
                Assert.GreaterOrEqual(minimum, -.01f, "Rendered sandal penetrates the walkable map: " + worst);
                Assert.Less(highestDrag, .03f, "Dragged heels must touch the ground rather than hovering above it.");
            } finally {
                Directory.CreateDirectory("Temp/TutorialFootContact"); File.WriteAllText("Temp/TutorialFootContact/clearance.csv", rows.ToString());
                File.WriteAllText("Temp/TutorialFootContact/minimum.txt", $"{minimum:F4}m: {worst}; highest dragged heel {highestDrag:F4}m; source meshes forced non-readable"); if (film != null) film.Cancel();
                foreach (var pair in originals) if (pair.Key != null) pair.Key.sharedMesh = pair.Value;
                foreach (var copy in copies) Object.Destroy(copy);
            }
            yield return new ExitPlayMode();
        }
        private static float Detail(string path)
        {
            var texture = new Texture2D(2, 2);
            try {
                texture.LoadImage(File.ReadAllBytes(path)); var p = texture.GetPixels32(); float detail = 0, light = 0; int count = 0;
                float L(int index) => p[index].r * .2126f + p[index].g * .7152f + p[index].b * .0722f;
                for (int y = texture.height / 5; y < texture.height * 4 / 5; y += 2)
                for (int x = texture.width / 5; x < texture.width * 4 / 5; x += 2) {
                    int at = y * texture.width + x; float c = L(at);
                    detail += Mathf.Abs(L(at - 1) - 2 * c + L(at + 1)) + Mathf.Abs(L(at - texture.width) - 2 * c + L(at + texture.width)); light += c; count++;
                }
                return detail / Mathf.Max(1, light);
            } finally { Object.Destroy(texture); }
        }
        [UnityTest]
        public IEnumerator ConcussionActuallyBlursTheImageAndRestoresCameraOnCancel()
        {
            EditorSceneManager.OpenScene(Scene); SetPreviewResolution(1600, 900);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode(); yield return LoadFreshTutorial();
            var d = Object.FindFirstObjectByType<TutorialDirector>(); var mission = d.GetComponent<Map01Mission>();
            var data = mission.gameCamera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            bool post = data.renderPostProcessing; var mask = data.volumeLayerMask; var depth = data.requiresDepthOption;
            var film = StartCapture(); yield return At(film, TutorialCaptureCutscene.Shot.VictimView, 1.4f);
            var before = mission.gameCamera.transform.rotation;
            yield return At(film, TutorialCaptureCutscene.Shot.VictimView, 2.65f);
            Assert.Greater(Quaternion.Angle(before, mission.gameCamera.transform.rotation), 25);
            mission.SetPaused(true); var held = mission.gameCamera.transform.rotation; float blur = film.VisionBlur;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.Less(Quaternion.Angle(held, mission.gameCamera.transform.rotation), .01f); Assert.AreEqual(blur, film.VisionBlur);
            var volume = GameObject.Find("Tutorial concussion vision").GetComponent<UnityEngine.Rendering.Volume>();
            Assert.IsTrue(data.renderPostProcessing); Assert.IsTrue(volume.enabled);
            yield return Photograph("15-concussion-blurred");
            volume.enabled = false; for (int i = 0; i < 4; i++) yield return null;
            yield return Photograph("15-concussion-sharp-reference");
            Assert.Less(Detail("Temp/TutorialFilm/15-concussion-blurred.png"), Detail("Temp/TutorialFilm/15-concussion-sharp-reference.png") * .75f, "Rendered vision must lose detail, not merely darken.");
            film.Cancel(); yield return null;
            Assert.AreEqual(post, data.renderPostProcessing); Assert.AreEqual(mask, data.volumeLayerMask); Assert.AreEqual(depth, data.requiresDepthOption);
            Assert.IsNull(GameObject.Find("Tutorial concussion vision")); Assert.AreEqual(0, film.VisionBlur);
            mission.SetPaused(false); yield return new ExitPlayMode();
        }
        [UnityTest]
        public IEnumerator PauseAndCancelRestoreTheActorsEvenFromHungView()
        {
            EditorSceneManager.OpenScene(Scene); EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Scene);
            yield return new EnterPlayMode(); yield return LoadFreshTutorial();
            var film = StartCapture(); var mission = film.GetComponent<Map01Mission>();
            yield return At(film, TutorialCaptureCutscene.Shot.ChaseFront, .3f);
            mission.SetPaused(true); float elapsed = film.ShotElapsed; var position = mission.player.position;
            double deadline = Time.realtimeSinceStartupAsDouble + .3f;
            while (Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.AreEqual(elapsed, film.ShotElapsed); Assert.Less(Vector3.Distance(position, mission.player.position), .001f);
            mission.SetPaused(false); yield return At(film, TutorialCaptureCutscene.Shot.VictimView, .2f);
            film.Cancel(); yield return null;
            Assert.IsFalse(film.Playing); Assert.IsFalse(mission.Cinematic); Assert.AreEqual(0, film.Fade);
            Assert.IsTrue(mission.ModernPlayer.enabled); Assert.IsTrue(mission.ModernCombat.enabled); Assert.IsTrue(mission.player.GetComponent<CharacterController>().enabled);
            Assert.IsTrue(Map01NamActions.For(mission).enabled);
            Assert.IsNotNull(Actor(mission.player).runtimeAnimatorController);
            foreach (var renderer in mission.player.GetComponentsInChildren<Renderer>()) Assert.IsFalse(renderer.forceRenderingOff);
            yield return new ExitPlayMode();
        }
    }
}
