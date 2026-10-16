using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// The shot is traced from the crosshair and its streak is drawn from the muzzle: unless the rifle
    /// itself points at what the crosshair is on, the streak leaves the barrel at an angle.
    /// </summary>
    public sealed class AimAtCrosshairTests : ForestSceneTestBase
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        // Set once Play has started, so they survive the domain reload EnterPlayMode triggers. Lambdas
        // stay in static helpers for the same reason (see the note at the top of ForestFlowTests).
        private static Map01Mission mission;
        private static Mouse mouse;
        private static float ahead;

        private static void Look(float yaw, float pitch)
        {
            var rig = mission.gameCamera.GetComponent<ThirdPersonCamera>();
            typeof(ThirdPersonCamera).GetField("_yaw", Private).SetValue(rig, yaw);
            typeof(ThirdPersonCamera).GetField("_pitch", Private).SetValue(rig, pitch);
        }

        private static void Hold(bool aim, bool fire)
        {
            var state = new MouseState();
            if (aim) state = state.WithButton(MouseButton.Right);
            if (fire) state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(mouse, state);
        }

        private static Weapon Rifle => mission.player.GetComponentsInChildren<Weapon>().First(w => w.IsGun);
        private static BarrelAim Aim => mission.player.GetComponent<BarrelAim>();
        private static Transform Chest => mission.player.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.UpperChest);

        /// <summary>Degrees between the barrel and the line from the muzzle to what the crosshair is on.</summary>
        private static float BarrelError()
        {
            var gun = Rifle;
            var view = mission.gameCamera.transform;
            int mask = ((LayerMask)typeof(PlayerCombat).GetField("hitMask", Private).GetValue(mission.ModernCombat)).value;
            Vector3 point = Physics.Raycast(view.position, view.forward, out var hit, gun.Range, mask, QueryTriggerInteraction.Ignore)
                ? hit.point : view.position + view.forward * gun.Range;
            return Vector3.Angle(gun.Muzzle.forward, point - gun.Muzzle.position);
        }

        /// <summary>How far round from the view the body stands, in degrees.</summary>
        private static float BodyOffView() =>
            Mathf.DeltaAngle(mission.gameCamera.transform.eulerAngles.y, mission.player.eulerAngles.y);

        /// <summary>Somewhere to stand with nothing within 14 m of the view, level, up or down, a quarter turn either side of <paramref name="yaw"/>.</summary>
        private static bool OpenGround(out Vector3 spot, out float yaw)
        {
            for (float radius = 0; radius <= 80; radius += 8)
                for (int around = 0; around < 360; around += 30)
                {
                    var near = mission.player.position + Quaternion.Euler(0, around, 0) * Vector3.forward * radius;
                    if (!NavMesh.SamplePosition(near, out var ground, 4, NavMesh.AllAreas)) continue;
                    for (yaw = 0; yaw < 360; yaw += 45)
                    {
                        bool clear = true;
                        var eye = ground.position + Vector3.up * 1.7f - Quaternion.Euler(0, yaw, 0) * Vector3.forward * 2.5f;
                        foreach (float turn in new[] { -45f, -20f, 0f, 20f, 45f })
                            foreach (float pitch in new[] { -30f, -10f, 0f, 8f })
                                clear &= !Physics.Raycast(eye, Quaternion.Euler(pitch, yaw + turn, 0) * Vector3.forward, 14, ~0, QueryTriggerInteraction.Ignore);
                        if (clear) { spot = ground.position; return true; }
                    }
                }
            spot = default; yaw = 0; return false;
        }

        /// <summary>Map 1 running, the guards switched off, Nam on open ground with the rifle and a mouse to hold.</summary>
        private static IEnumerator Arrive()
        {
            mission = Object.FindFirstObjectByType<Map01Mission>();
            foreach (var enemy in mission.Enemies) { enemy.enabled = false; enemy.GetComponent<NavMeshAgent>().enabled = false; }
            mission.ModernHealth.CinematicInvulnerable = true;
            Assert.IsTrue(OpenGround(out var spot, out ahead), "No open ground to aim across.");
            var controller = mission.player.GetComponent<CharacterController>();
            controller.enabled = false; mission.player.SetPositionAndRotation(spot, Quaternion.Euler(0, ahead, 0)); controller.enabled = true;
            Look(ahead, 0);
            mouse = InputSystem.AddDevice<Mouse>();
            mission.ModernCombat.Equip(WeaponKind.Rifle);
            yield return WaitGameSeconds(.5f);
        }

        [UnityTest]
        public IEnumerator BarrelStaysOnTheCrosshairWhileAimingSweepingAndFiring()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode(); yield return null; yield return null;
            var routing = RouteInputToGame();
            yield return Arrive();
            Hold(true, false);
            yield return WaitGameSeconds(.8f);
            Assert.IsTrue(mission.ModernCombat.IsAiming, "The right button raises the rifle.");
            float stance = BodyOffView();

            // Held on a point: up at the sky, level, down at the ground ahead, and off to either side.
            float[] pitches = { -30, -12, 0, 10, -20, 6 }, turns = { 0, 0, 0, 0, 35, -35 };
            for (int i = 0; i < pitches.Length; i++)
            {
                Look(ahead + turns[i], pitches[i]);
                yield return WaitGameSeconds(.7f);
                float error = BarrelError();
                Debug.Log($"[Aim] held pitch {pitches[i]} turn {turns[i]}: barrel {error:0.0} degrees off the crosshair, body {BodyOffView():0.0} off the view.");
                Assert.Less(error, 3f, $"Aiming {pitches[i]} degrees of pitch, {turns[i]} of turn: the barrel must lie on the crosshair.");
                // The feet still turn him to the view, as the take stands him: the spine only makes up the tilt.
                Assert.AreEqual(stance, BodyOffView(), 4f, "The body turns with the view; it is not left behind for the spine to twist.");
            }

            // Swept across the view the way a player drags the mouse.
            float worst = 0, start = Time.time;
            while (Time.time - start < 2.5f)
            {
                float t = Time.time - start;
                Look(ahead + 35 * Mathf.Sin(t * 2.2f), -10 + 20 * Mathf.Sin(t * 3.1f));
                yield return null;
                if (t > .3f) worst = Mathf.Max(worst, BarrelError());
            }
            Debug.Log($"[Aim] sweeping: at worst {worst:0.0} degrees off the crosshair.");
            Assert.Less(worst, 6f, "The barrel must keep up with a moving crosshair.");

            // And while the trigger is held: each streak has to leave along the barrel.
            Look(ahead, -8);
            yield return WaitGameSeconds(.5f);
            Hold(true, true);
            float sum = 0; int frames = 0; worst = 0; start = Time.time; int rounds = mission.ModernCombat.RoundsInMagazine;
            while (Time.time - start < 1.2f)
            {
                float t = Time.time - start;
                Look(ahead + 25 * Mathf.Sin(t * 2.5f), -8 + 10 * Mathf.Sin(t * 3.3f));
                yield return null;
                float error = BarrelError(); sum += error; frames++; worst = Mathf.Max(worst, error);
            }
            Hold(true, false);
            Debug.Log($"[Aim] firing while sweeping: {sum / frames:0.0} degrees off on average, {worst:0.0} at worst; {rounds - mission.ModernCombat.RoundsInMagazine} rounds.");
            Assert.Less(mission.ModernCombat.RoundsInMagazine, rounds, "The burst was really fired.");
            Assert.Less(sum / frames, 4f, "Firing, the barrel stays on the crosshair between kicks.");

            InputSystem.RemoveDevice(mouse); RestoreInputRouting(routing);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator TheBendComesAndGoesWithTheShotAndSurvivesAPauseAndAReload()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode(); yield return null; yield return null;
            var routing = RouteInputToGame();
            yield return Arrive();
            Assert.IsNotNull(Aim, "PlayerCombat adds the aim correction itself.");
            Assert.AreEqual(0f, Aim.Weight, "Carrying the rifle, the spine is the animation's alone.");

            // From the hip: no sights, the view well above where the carry points.
            Look(ahead, -18);
            yield return WaitGameSeconds(.3f);
            int rounds = mission.ModernCombat.RoundsInMagazine;
            Hold(false, true);
            yield return WaitGameSeconds(.5f);
            float sum = 0; int frames = 0; float start = Time.time;
            while (Time.time - start < .6f) { yield return null; sum += BarrelError(); frames++; }
            Hold(false, false);
            Debug.Log($"[Aim] from the hip: {sum / frames:0.0} degrees off on average; {rounds - mission.ModernCombat.RoundsInMagazine} rounds.");
            Assert.Less(mission.ModernCombat.RoundsInMagazine, rounds, "The burst was really fired.");
            Assert.Less(sum / frames, 5f, "Fired from the hip, the rifle comes up onto the crosshair too.");
            yield return WaitGameSeconds(1.2f);
            Assert.AreEqual(0f, Aim.Weight, "With the rifle lowered again, the spine is handed back.");

            // Paused with the sights up, the animator stops posing him: the bend must not pile up frame on frame.
            Hold(true, false);
            Look(ahead, -22);
            yield return WaitGameSeconds(.8f);
            Assert.AreEqual(1f, Aim.Weight, "Sights up, the whole bend is in.");
            mission.SetPaused(true);
            yield return null; yield return null;
            Vector3 barrel = Rifle.Muzzle.forward;
            for (int i = 0; i < 12; i++) yield return null;
            float drift = Vector3.Angle(barrel, Rifle.Muzzle.forward);
            mission.SetPaused(false);
            Assert.Less(drift, .5f, "Twelve paused frames must leave the rifle where it was.");
            // A pause takes the input away, so the animator lowers the rifle and raises it again afterwards.
            yield return WaitGameSeconds(1f);
            Assert.Less(BarrelError(), 3f, "Back from the pause, still on the crosshair.");

            // A reload works the rifle in the hands; the back keeps the lean of the aim and does not chase the barrel.
            Vector3 chest = Chest.forward;
            mission.GetComponent<Map01Inventory>().Add("ammo_rifle", 30);
            Assert.IsTrue(mission.ModernCombat.TryReload(), "A part-used magazine can be changed.");
            float lean = 0;
            while (mission.ModernCombat.IsReloading) { yield return null; lean = Mathf.Max(lean, Vector3.Angle(chest, Chest.forward)); }
            Debug.Log($"[Aim] reloading with the sights up: the chest moved {lean:0.0} degrees at most.");
            Assert.Less(lean, 30f, "Reloading must not wrench the back round after the barrel.");
            yield return WaitGameSeconds(.8f);
            Assert.Less(BarrelError(), 3f, "The reload done, the barrel is back on the crosshair.");

            Hold(false, false);
            InputSystem.RemoveDevice(mouse); RestoreInputRouting(routing);
            yield return new ExitPlayMode();
        }
    }
}
