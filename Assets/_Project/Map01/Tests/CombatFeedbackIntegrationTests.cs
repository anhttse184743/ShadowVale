using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Audio;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Enemies;
using ShadowVale.Gameplay.Player;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace ShadowVale.Map01.Tests
{
    public sealed class CombatFeedbackIntegrationTests : ForestSceneTestBase
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [UnityTest]
        public IEnumerator SprintToggleAndWallCollisionUseActualMovementForAccuracy()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            yield return VerifyMovement();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyMovement()
        {
            var routing = RouteInputToGame();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0, -.5f, 0);
            floor.transform.localScale = new Vector3(100, 1, 100);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 2, 3);
            wall.transform.localScale = new Vector3(10, 4, 1);
            var actor = new GameObject("Movement regression");
            actor.transform.position = new Vector3(0, 1.1f, 0);
            var player = actor.AddComponent<PlayerController>();
            try
            {
                yield return WaitGameSeconds(.3f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null; yield return null;
                Assert.IsTrue(player.IsSprinting, "Shift stays toggled on.");
                Assert.IsFalse(player.IsMoving);
                Assert.AreEqual(ShooterStance.Standing,
                    WeaponSpreadState.ResolveStance(true, false, player.IsSprinting, player.PlanarSpeed));
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
                yield return WaitGameSeconds(.25f);
                Assert.Greater(player.PlanarSpeed, 2.7f);
                yield return WaitGameSeconds(.8f);
                Assert.Less(player.PlanarSpeed, .2f, "Holding W+Sprint against a wall is not running.");
                Assert.IsFalse(player.IsMoving);
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing);
                Object.Destroy(actor); Object.Destroy(floor); Object.Destroy(wall);
            }
        }

        [UnityTest]
        public IEnumerator GuardsUsePlayerAssetsAndHitsProducePooledFeedback()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return VerifyInPlay();
            yield return new ExitPlayMode();
        }

        private static IEnumerator VerifyInPlay()
        {
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            Assert.AreEqual(300, mission.Settings.stamina);
            mission.SetStamina(300);
            Assert.AreEqual(300, mission.Stamina);
            mission.SetStamina(75); // An old save keeps its absolute value and can now recover to 300.
            Assert.AreEqual(75, mission.Stamina);
            foreach (var enemy in mission.Enemies)
            {
                enemy.enabled = false;
                enemy.GetComponent<EnemyFeedback>().CanStep = () => false;
            }
            var guard = mission.Enemies.First(e => e.Alive);
            var feedback = guard.GetComponent<EnemyFeedback>();
            Assert.IsNotNull(feedback);
            Assert.AreSame(mission.player.GetComponent<PlayerFootsteps>().Bank,
                typeof(EnemyFeedback).GetField("bank", Private).GetValue(feedback));
            Assert.IsNotNull(mission.ModernCombat.TracerPrefab);
            Assert.IsTrue(mission.player.GetComponent<CombatAudio>().Gunshot.IsValid);

            mission.player.GetComponent<CharacterController>().enabled = false;
            mission.ModernPlayer.enabled = false;
            mission.player.position = guard.transform.position + Vector3.right * 3;
            mission.ModernHealth.CinematicInvulnerable = false;
            typeof(Map01EnemyController).GetField("obstructionMask", Private).SetValue(guard, (LayerMask)0);
            typeof(Map01EnemyController).GetField("_shots", Private).SetValue(guard, 0);
            typeof(Map01EnemyController).GetField("_nextShot", Private).SetValue(guard, 0f);
            guard.Accuracy = new ShotAccuracyProfile { enabled = false }; // A sure hit; the hit chance has its own tests.
            float hp = mission.ModernHealth.Current;
            guard.FireAtPlayer();
            Assert.Less(mission.ModernHealth.Current, hp);
            Assert.IsTrue(guard.GetComponentsInChildren<ShotTracer>().Any(t => !t.IsFree));
            var gun = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)
                .First(a => a.clip == mission.player.GetComponent<CombatAudio>().Gunshot.clip && a.isPlaying);
            Assert.AreEqual(1f, gun.spatialBlend);
            Assert.AreEqual(AudioRolloffMode.Linear, gun.rolloffMode);
            Assert.AreEqual(65f, gun.maxDistance);
            var blood = Object.FindFirstObjectByType<BloodImpact>();
            Assert.IsNotNull(blood);
            Assert.IsTrue(blood.GetComponentsInChildren<ParticleSystem>().Any(p => p.particleCount > 0));

            feedback.CanStep = () => true;
            var previous = typeof(EnemyFeedback).GetField("previous", Private);
            var update = typeof(EnemyFeedback).GetMethod("LateUpdate", Private);
            for (int i = 0; i < 3; i++)
            {
                previous.SetValue(feedback, feedback.transform.position - Vector3.right * .55f);
                update.Invoke(feedback, null);
            }
            var step = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None)
                .First(a => a.isPlaying && a.maxDistance == 20f);
            Assert.AreEqual(1f, step.spatialBlend);
            Assert.AreEqual(AudioRolloffMode.Linear, step.rolloffMode);
            feedback.CanStep = () => false;

            int activeSources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(a => a.isPlaying);
            mission.player.position += Vector3.right * 100;
            feedback.Fire(mission.player.position + Vector3.up, mission.player, 0, out _);
            Assert.AreEqual(activeSources,
                Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(a => a.isPlaying),
                "Shots outside hearing range do not occupy another audio source.");
            yield return WaitGameSeconds(.8f);
            Assert.IsTrue(guard.GetComponentsInChildren<ShotTracer>().All(t => t.IsFree));
            Assert.IsTrue(blood.GetComponentsInChildren<ParticleSystem>().All(p => p.particleCount == 0));
            // The runner still fails on errors/exceptions; normal offline telemetry logs are expected.
        }
    }
}
