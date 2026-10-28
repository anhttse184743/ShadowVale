using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
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
    /// <summary>
    /// Nam's stealth crouch and his reloads as the game plays them: Map 1 in Play Mode, the real Animator with its
    /// layers and blends, real keyboard movement. Measures what the eye judges — the hips when he starts to move out
    /// of the crouch, the magazine's path through each reload — and photographs it into Logs/NamReview.
    /// </summary>
    public sealed class NamCrouchReloadReviewTests : ForestSceneTestBase
    {
        private const string Out = "Logs/NamReview/";

        private static Map01Mission M => Object.FindFirstObjectByType<Map01Mission>();
        private static Map01Rescue R => M.GetComponent<Map01Rescue>();

        [UnityTest]
        public IEnumerator CrouchAndReloadsInTheRealGame()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            yield return null;
            yield return Review();
            yield return new ExitPlayMode();
        }

        private static string F(float value, string format = "F1") => value.ToString(format, CultureInfo.InvariantCulture);

        private static IEnumerator Review()
        {
            foreach (var guard in M.Enemies) guard.enabled = false;
            var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            PropertyInfo mode = pipeline.GetType().GetProperty("gpuResidentDrawerMode");
            object previousMode = mode.GetValue(pipeline);
            mode.SetValue(pipeline, Enum.ToObject(mode.PropertyType, 0));
            var input = RouteInputToGame();
            // a keyboard of the test's own (as KnifeWristLureTests walks him): events queued on the machine's keyboard are not his to read
            Keyboard previousKeyboard = Keyboard.current;
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("Nam review keyboard");
            keyboard.MakeCurrent();
            var body = M.player.GetComponent<CharacterController>();
            // open ground: from the safe entry, the way he can walk furthest in a straight line
            Vector3 start = R.Layout.safeEntry.position;
            if (UnityEngine.AI.NavMesh.SamplePosition(start, out UnityEngine.AI.NavMeshHit ground, 4f, UnityEngine.AI.NavMesh.AllAreas)) start = ground.position;
            Vector3 heading = Vector3.forward; float reach = -1f;
            for (int i = 0; i < 16; i++)
            {
                Vector3 way = Quaternion.Euler(0, i * 22.5f, 0) * Vector3.forward;
                float clear = UnityEngine.AI.NavMesh.Raycast(start, start + way * 9f, out UnityEngine.AI.NavMeshHit wall, UnityEngine.AI.NavMesh.AllAreas) ? wall.distance : 9f;
                if (clear > reach) { reach = clear; heading = way; }
            }
            body.enabled = false; M.player.SetPositionAndRotation(start, Quaternion.LookRotation(heading)); body.enabled = true;
            Transform basis = new GameObject("Nam review input basis").transform;
            basis.rotation = Quaternion.LookRotation(heading);
            // a steady 60 frames a second, whatever a screenshot costs: "from one frame to the next" then means what a player sees
            Time.captureDeltaTime = 1f / 60f;
            FieldInfo cameraField = typeof(PlayerController).GetField("cameraTransform", BindingFlags.Instance | BindingFlags.NonPublic);
            object oldBasis = cameraField.GetValue(M.ModernPlayer);
            cameraField.SetValue(M.ModernPlayer, basis);
            M.ModernPlayer.RestoreMotion(false);
            M.ModernCombat.Equip(WeaponKind.Rifle);
            M.ModernCombat.ReserveRounds = () => 300;
            M.ModernCombat.DrawRounds = wanted => wanted;
            Animator actor = M.player.GetComponentInChildren<Animator>();
            foreach (var mesh in actor.GetComponentsInChildren<SkinnedMeshRenderer>()) mesh.forceMatrixRecalculationPerRender = true;
            var carrier = actor.GetComponent<ReloadMagazineCarrier>();
            Assert.IsNotNull(carrier, "The player in the scene carries the reload magazine carrier.");
            EditorSettings.asyncShaderCompilation = false; ShaderUtil.allowAsyncCompilation = false;
            Directory.CreateDirectory(Out);
            var log = new List<string>();
            var view = M.gameCamera.GetComponent<ThirdPersonCamera>();
            Transform hips = actor.GetBoneTransform(HumanBodyBones.Hips);
            try
            {
                // ---- 1. the crouch: still, then moving off, then stopping again
                M.ModernPlayer.RestoreMotion(true);
                M.Crouched = true;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return Seconds(1.2f);
                Assert.IsTrue(actor.GetCurrentAnimatorStateInfo(0).IsName("Sneak_Rifle"), "Crouched with the rifle.");
                // standing still he breathes: how much the hips and the chest move over one breath of the idle
                Transform chest = actor.GetBoneTransform(HumanBodyBones.UpperChest) ?? actor.GetBoneTransform(HumanBodyBones.Chest);
                float hipLow = float.MaxValue, hipHigh = float.MinValue, chestLow = float.MaxValue, chestHigh = float.MinValue, idleUntil = Time.time + 3.4f;
                while (Time.time < idleUntil)
                {
                    yield return null;
                    float height = hips.position.y - M.player.position.y, pitch = Vector3.Angle(chest.up, Vector3.up);
                    hipLow = Mathf.Min(hipLow, height); hipHigh = Mathf.Max(hipHigh, height); chestLow = Mathf.Min(chestLow, pitch); chestHigh = Mathf.Max(chestHigh, pitch);
                }
                log.Add($"crouch idle over one breath: hips move {F((hipHigh - hipLow) * 1000)} mm, the chest tilts {F(chestHigh - chestLow, "F2")} deg");
                float still = hips.position.y - M.player.position.y;
                yield return Side(view, heading, .8f); yield return Draw(Out + "crouch_0_still.png");
                float low = still, high = still, step = 0, last = still, lift = 0;
                Vector3 from = M.player.position;
                float[] shots = { .1f, .3f, .6f, 1.2f }; int shot = 0; float started = Time.time;
                while (Time.time < started + 1.6f)
                {
                    // W held, pumped before every game frame (as KnifeWristLureTests walks him): a queued event alone is not read in time
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W)); InputSystem.Update(); keyboard.MakeCurrent();
                    Assert.IsTrue(keyboard.wKey.isPressed, "W reaches the game before its Update.");
                    yield return null;
                    float height = hips.position.y - M.player.position.y;
                    low = Mathf.Min(low, height); high = Mathf.Max(high, height); step = Mathf.Max(step, Mathf.Abs(height - last)); last = height;
                    if (Time.time < started + .5f) lift = Mathf.Max(lift, Mathf.Abs(height - still));
                    if (shot < shots.Length && Time.time >= started + shots[shot])
                    {
                        yield return Side(view, heading, .8f); yield return Draw(Out + "crouch_" + (shot + 1) + "_moving.png"); shot++;
                    }
                }
                float travelled = Vector3.Distance(from, M.player.position);
                float stopped = Time.time;
                while (Time.time < stopped + 1f)
                {
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); keyboard.MakeCurrent();
                    yield return null;
                    float height = hips.position.y - M.player.position.y;
                    low = Mathf.Min(low, height); high = Mathf.Max(high, height); step = Mathf.Max(step, Mathf.Abs(height - last)); last = height;
                }
                yield return Side(view, heading, .8f); yield return Draw(Out + "crouch_5_stopped.png");
                log.Add($"crouch: hips {F(still * 100)} cm above the ground standing still; walked {F(travelled)} m; in the first half second of moving off they change by at most {F(lift * 100)} cm; " +
                        $"through start, walk and stop they stay within {F(low * 100)}..{F(high * 100)} cm (the walk's own bob included), largest change in one frame at 60 fps {F(step * 1000)} mm; back to {F(last * 100)} cm");
                Assert.Greater(travelled, .6f, $"He really walked: the measurement covers the move from idle into the walk. (input allowed {M.ModernPlayer.InputAllowed?.Invoke() ?? true}, stopped {M.Stopped})");
                Assert.Less(high - low, .12f, "Moving off from the crouch no longer lifts the hips (the kneeling idle was 25 cm below the walk).");

                // ---- 2. the three reloads
                foreach (var (label, crouched, rounds, state, marks) in new[] {
                    ("tactical", false, 10, "Reload_Rifle", new[] { .15f, .30f, .41f, .50f, .62f, .75f }),
                    ("empty", false, 0, "ReloadEmpty_Rifle", new[] { .10f, .24f, .32f, .40f, .48f, .60f }),
                    ("crouched", true, 10, "ReloadCrouch_Rifle", new[] { .10f, .22f, .30f, .35f, .37f, .45f }) })
                {
                    M.ModernPlayer.RestoreMotion(crouched);
                    yield return Seconds(.8f);
                    M.ModernCombat.RestoreMagazine(rounds);
                    Assert.IsTrue(M.ModernCombat.TryReload(), label + ": the reload starts");
                    float farthest = 0, jump = 0; int hidden = 0, frames = 0, mark = 0; bool inState = false;
                    Vector3 lastPlace = Vector3.zero; bool hasLast = false;
                    float limit = Time.time + 10f;
                    while (M.ModernCombat.IsReloading && Time.time < limit)
                    {
                        yield return null;
                        frames++;
                        inState |= actor.GetCurrentAnimatorStateInfo(actor.GetLayerIndex(PlayerCombat.UpperBodyLayer)).IsName(state);
                        farthest = Mathf.Max(farthest, carrier.OutOfWell);
                        if (!carrier.Shown) hidden++;
                        // the magazine as his chest sees it: how far it moves from one frame to the next
                        Vector3 place = actor.GetBoneTransform(HumanBodyBones.Chest).InverseTransformPoint(carrier.Magazine.position);
                        if (hasLast && carrier.Shown) jump = Mathf.Max(jump, Vector3.Distance(place, lastPlace));
                        lastPlace = place; hasLast = carrier.Shown;
                        if (mark < marks.Length && M.ModernCombat.ReloadProgress >= marks[mark])
                        {
                            yield return Front(view, crouched ? .85f : 1.3f); yield return Draw(Out + "reload_" + label + "_" + mark + ".png"); mark++;
                            hasLast = false;      // the picture took frames this loop did not see: not a move "between two frames"
                        }
                    }
                    yield return Seconds(.5f);
                    log.Add($"reload {label} ({state}): {F(frames / 60f, "F2")} s; magazine up to {F(farthest * 100)} cm out of the rifle, hidden for {F(hidden / 60f, "F2")} s, " +
                            $"largest move between two frames at 60 fps {F(jump * 100)} cm; afterwards {F(carrier.OutOfWell * 1000)} mm from its place, shown {carrier.Shown}");
                    Assert.IsTrue(inState, label + ": the reload state played");
                    Assert.Greater(farthest, .15f, label + ": the magazine leaves the rifle");
                    Assert.Greater(hidden, 0, label + ": the old magazine is put away before the fresh one appears");
                    Assert.Less(carrier.OutOfWell, 1e-3f, label + ": the magazine ends in the rifle");
                    Assert.IsTrue(carrier.Shown, label + ": the magazine ends visible");
                }

                // ---- 3. a reload cut short by drawing the knife
                M.ModernPlayer.RestoreMotion(false);
                yield return Seconds(.5f);
                M.ModernCombat.RestoreMagazine(10);
                Assert.IsTrue(M.ModernCombat.TryReload());
                float cut = Time.time + 6f;
                while (M.ModernCombat.ReloadProgress < .32f && Time.time < cut) yield return null;
                float carried = carrier.OutOfWell;
                M.ModernCombat.Equip(WeaponKind.Knife);
                yield return Seconds(.4f);
                M.ModernCombat.Equip(WeaponKind.Rifle);
                yield return Seconds(.6f);
                log.Add($"interrupted: magazine {F(carried * 100)} cm out of the rifle when the knife was drawn; back with the rifle it is {F(carrier.OutOfWell * 1000)} mm from its place, shown {carrier.Shown}");
                Assert.Less(carrier.OutOfWell, 1e-3f, "An interrupted reload leaves the magazine in the rifle.");
                Assert.IsTrue(carrier.Shown);
            }
            finally
            {
                File.WriteAllLines(Out + "review.txt", log);
                Time.captureDeltaTime = 0f;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update();
                InputSystem.RemoveDevice(keyboard); previousKeyboard?.MakeCurrent();
                RestoreInputRouting(input);
                mode.SetValue(pipeline, previousMode);
                cameraField.SetValue(M.ModernPlayer, oldBasis);
                Object.Destroy(basis.gameObject);
            }
        }

        private static IEnumerator Seconds(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until) yield return null;
        }

        /// <summary>True when something of the level stands between the camera and him.</summary>
        private static bool Hidden(Vector3 eye, Vector3 focus) =>
            Physics.Linecast(eye, focus, out RaycastHit hit) && !hit.transform.IsChildOf(M.player);

        /// <summary>Side-on to the way he walks, whole body: the hips against the ground.</summary>
        private static IEnumerator Side(ThirdPersonCamera view, Vector3 heading, float focusHeight)
        {
            Vector3 position = M.player.position, side = Vector3.Cross(Vector3.up, heading).normalized;
            Vector3 focus = position + Vector3.up * focusHeight;
            Vector3 eye = position + side * 3.2f + Vector3.up * 1.0f;
            if (Hidden(eye, focus)) eye = position - side * 3.2f + Vector3.up * 1.0f;
            view.SetCinematicView(eye, Quaternion.LookRotation(focus - eye), 40, 1);
            yield return null;
        }

        /// <summary>From his front-left, chest and hands: the magazine.</summary>
        private static IEnumerator Front(ThirdPersonCamera view, float focusHeight)
        {
            Vector3 position = M.player.position;
            Vector3 focus = position + Vector3.up * focusHeight;
            Vector3 eye = position + M.player.forward * 1.7f - M.player.right * 1.1f + Vector3.up * (focusHeight + .15f);
            if (Hidden(eye, focus)) eye = position + M.player.forward * 1.7f + M.player.right * 1.1f + Vector3.up * (focusHeight + .15f);
            view.SetCinematicView(eye, Quaternion.LookRotation(focus - eye), 34, 1);
            yield return null;
        }

        private static IEnumerator Draw(string path)
        {
            var pump = M.gameObject.AddComponent<ExtractionCapturePump>();
            bool ready = false;
            pump.draw = () =>
            {
                Camera camera = M.gameCamera;
                RenderTexture target = RenderTexture.GetTemporary(960, 720, 24);
                RenderTexture previous = camera.targetTexture, active = RenderTexture.active;
                var pixels = new Texture2D(960, 720, TextureFormat.RGB24, false);
                try
                {
                    camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); pixels.Apply();
                    File.WriteAllBytes(path, pixels.EncodeToPNG());
                }
                finally
                {
                    camera.targetTexture = previous; RenderTexture.active = active;
                    RenderTexture.ReleaseTemporary(target); Object.Destroy(pixels);
                }
                ready = true;
            };
            yield return new WaitUntil(() => ready);
            Object.Destroy(pump);
        }
    }
}
