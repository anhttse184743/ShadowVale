using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    public sealed class ShotTracerLifecycleTests : ForestSceneTestBase
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void DestroyedTracerWrapperDoesNotAccessNativeRenderer()
        {
            var host = new GameObject("Destroyed tracer regression");
            var tracer = host.AddComponent<ShotTracer>();
            Object.DestroyImmediate(host);
            Assert.IsFalse(tracer.IsUsable);
            Assert.DoesNotThrow(() => tracer.Play(Vector3.zero, Vector3.forward));
            Assert.IsFalse(tracer.TryPlay(Vector3.zero, Vector3.forward));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator PoolRepairsLostObjectsWithoutGrowingOrDraggingShots()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var combat = Object.FindFirstObjectByType<Map01Mission>().ModernCombat;
            var pool = Pool(combat);
            int capacity = pool.Length;
            var root = pool[0].transform.parent;
            Assert.AreSame(combat.transform, root.parent);

            Vector3 from = new Vector3(10, 20, 30), to = new Vector3(11, 22, 33);
            Show(combat, from, to);
            var line = pool[0].GetComponent<LineRenderer>();
            Assert.IsTrue(line.useWorldSpace);
            var position = combat.transform.position;
            combat.transform.position += Vector3.right * 3;
            Assert.AreEqual(from, line.GetPosition(0));
            Assert.AreEqual(to, line.GetPosition(1));
            combat.transform.position = position;

            // Reproduce native scene objects disappearing while managed pool references remain.
            Object.Destroy(root.gameObject);
            yield return null;
            Assert.IsTrue(root == null);
            Show(combat, from, to);
            pool = Pool(combat);
            Assert.AreEqual(capacity, pool.Length);
            Assert.IsTrue(pool.All(t => t != null && t.IsUsable));
            Assert.AreSame(combat.transform, pool[0].transform.parent.parent);

            var lost = pool[0];
            Object.Destroy(lost.gameObject);
            yield return null;
            typeof(PlayerCombat).GetField("_tracerCursor", Private).SetValue(combat, 0);
            Show(combat, from, to);
            Assert.IsTrue(lost == null);
            Assert.IsTrue(Pool(combat)[0] != null && Pool(combat)[0].IsUsable);

            // Rebind a cached renderer reference that was invalidated independently.
            var staleHost = new GameObject("Stale renderer");
            var staleLine = staleHost.AddComponent<LineRenderer>();
            var repaired = Pool(combat)[0];
            typeof(ShotTracer).GetField("_line", Private).SetValue(repaired, staleLine);
            Object.Destroy(staleHost);
            yield return null;
            Assert.IsTrue(repaired.TryPlay(from, to));
            Assert.AreSame(repaired.GetComponent<LineRenderer>(),
                typeof(ShotTracer).GetField("_line", Private).GetValue(repaired));

            var stablePool = Pool(combat).ToArray();
            for (int i = 0; i < 500; i++) Show(combat, from, to);
            CollectionAssert.AreEqual(stablePool, Pool(combat));
            Assert.AreEqual(capacity, combat.GetComponentsInChildren<ShotTracer>(true).Length);
            Assert.AreEqual(1, combat.transform.Cast<Transform>().Count(t => t.name == "ShotTracers"));
            yield return WaitGameSeconds(.2f);
            Assert.IsTrue(Pool(combat).All(t => t.IsFree && !t.GetComponent<LineRenderer>().enabled));
            LogAssert.NoUnexpectedReceived();

            root = Pool(combat)[0].transform.parent;
            // Remove the declared dependent component before removing its required combat owner.
            Object.Destroy(combat.GetComponent<ShadowVale.Gameplay.Audio.CombatAudio>());
            yield return null;
            Object.Destroy(combat);
            yield return null;
            yield return null;
            Assert.IsTrue(root == null, "Removing the combat owner must release its pooled renderers.");
            yield return new ExitPlayMode();
        }

        public static ShotTracer[] Pool(PlayerCombat combat) =>
            (ShotTracer[])typeof(PlayerCombat).GetField("_tracers", Private).GetValue(combat);

        private static void Show(PlayerCombat combat, Vector3 from, Vector3 to) =>
            typeof(PlayerCombat).GetMethod("ShowTracer", Private).Invoke(combat, new object[] { from, to });

        public static void FireAfterArrival(PlayerCombat combat)
        {
            var pool = Pool(combat).ToArray();
            Assert.IsTrue(pool.All(t => t != null && t.IsUsable), "Scene unload must not destroy Nam's tracer pool.");
            Assert.AreSame(combat.transform, pool[0].transform.parent.parent);
            Assert.AreEqual(combat.gameObject.scene, pool[0].gameObject.scene);
            Assert.NotNull(Camera.main);
            int ammo = combat.RoundsInMagazine;
            Assert.GreaterOrEqual(ammo, 3);
            for (int i = 0; i < 3; i++)
            {
                typeof(PlayerCombat).GetField("_nextAttackTime", Private).SetValue(combat, 0f);
                typeof(PlayerCombat).GetMethod("Attack", Private).Invoke(combat, null);
            }
            Assert.AreEqual(ammo - 3, combat.RoundsInMagazine);
            Assert.IsTrue(pool.Any(t => t.GetComponent<LineRenderer>().enabled), "Actual gameplay gunfire must show a tracer.");
            for (int i = 0; i < 500; i++) Show(combat, Vector3.one, Vector3.one * 2);
            CollectionAssert.AreEqual(pool, Pool(combat));
            Assert.AreEqual(pool.Length, combat.GetComponentsInChildren<ShotTracer>(true).Length);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
