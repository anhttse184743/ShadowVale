using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Enemies;
using UnityEngine;

namespace ShadowVale.Tests.EditMode.Gameplay
{
    public sealed class EnemyFeedbackTests
    {
        [Test]
        public void ShotStopsAtCoverAndReachesVictimWhenCoverIsRemoved()
        {
            var shooter = new GameObject("shooter");
            var victim = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                victim.transform.position = new Vector3(0, 1, 10);
                cover.transform.position = new Vector3(0, 1, 5);
                cover.transform.localScale = new Vector3(3, 3, 1);
                var feedback = shooter.AddComponent<EnemyFeedback>();
                Physics.SyncTransforms();
                Assert.IsFalse(feedback.Fire(victim.transform.position, victim.transform, ~0, out var blocked));
                Assert.Less(blocked.z, 6f);
                Object.DestroyImmediate(cover); cover = null;
                Physics.SyncTransforms();
                Assert.IsTrue(feedback.Fire(victim.transform.position, victim.transform, ~0, out var hit));
                Assert.That(hit.z, Is.InRange(9f, 10f));
            }
            finally
            {
                Object.DestroyImmediate(shooter); Object.DestroyImmediate(victim);
                if (cover != null) Object.DestroyImmediate(cover);
            }
        }

        [Test]
        public void BulletDamageHonoursInvulnerabilityAndDeath()
        {
            var actor = new GameObject("victim");
            try
            {
                var health = actor.AddComponent<Health>(); health.SetMaxHealth(100);
                health.CinematicInvulnerable = true;
                health.TakeBulletDamage(20, Vector3.zero, null, Vector3.forward);
                Assert.AreEqual(100, health.Current);
                health.CinematicInvulnerable = false;
                health.TakeBulletDamage(20, Vector3.zero, null, Vector3.forward);
                Assert.AreEqual(80, health.Current);
                health.TakeBulletDamage(100, Vector3.zero, null, Vector3.forward);
                Assert.IsTrue(health.IsDead);
                health.TakeBulletDamage(20, Vector3.zero, null, Vector3.forward);
                Assert.AreEqual(0, health.Current);
            }
            finally { Object.DestroyImmediate(actor); }
        }
    }
}
