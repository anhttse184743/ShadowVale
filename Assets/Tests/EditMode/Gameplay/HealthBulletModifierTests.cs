using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEngine;

namespace ShadowVale.Tests.EditMode.Gameplay
{
    public sealed class HealthBulletModifierTests
    {
        static void QueuedHit(Health health, WeaponKind weapon, bool lethal)
        {
            var method=typeof(Health).GetMethod("TakeBulletDamageFromWeapon",BindingFlags.Instance|BindingFlags.NonPublic);
            Assert.NotNull(method);
            method.Invoke(health,new object[]{100f,Vector3.zero,null,Vector3.forward,weapon,lethal});
        }

        [TestCase(WeaponKind.Rifle,false,75f)]
        [TestCase(WeaponKind.Pistol,false,75f)]
        [TestCase(WeaponKind.Sniper,true,0f)]
        public void QueuedBulletsApplyArmourOnceAndKeepTheirWeapon(WeaponKind weapon,bool lethal,float remaining)
        {
            var go=new GameObject("Queued bullet policy test");
            try
            {
                var health=go.AddComponent<Health>();health.SetMaxHealth(100);int calls=0;DamageNotice notice=default;
                health.BulletDamageModifier=(amount,point)=>{calls++;return amount*.25f;};
                health.Damaged+=damage=>notice=damage;
                QueuedHit(health,weapon,lethal);
                Assert.AreEqual(remaining,health.Current,.001f);Assert.AreEqual(1,calls);
                Assert.IsTrue(notice.IsBullet);Assert.AreEqual(weapon,notice.Weapon);
            }
            finally{Object.DestroyImmediate(go);}
        }

        [Test]public void CinematicProtectionStopsLethalAndLegacyBulletsBeforeModifiers()
        {
            var go=new GameObject("Protected bullet policy test");
            try
            {
                var health=go.AddComponent<Health>();health.SetMaxHealth(100);health.CinematicInvulnerable=true;int calls=0,events=0;
                health.BulletDamageModifier=(amount,point)=>{calls++;return amount;};health.Damaged+=damage=>events++;
                QueuedHit(health,WeaponKind.Sniper,true);health.TakeBulletDamage(1000,Vector3.zero,null,Vector3.forward);
                Assert.AreEqual(100,health.Current);Assert.AreEqual(0,calls);Assert.AreEqual(0,events);
            }
            finally{Object.DestroyImmediate(go);}
        }

        [Test]public void LegacyBulletsStillApplyTheSameArmourRule()
        {
            var go=new GameObject("Legacy bullet policy test");
            try
            {
                var health=go.AddComponent<Health>();health.SetMaxHealth(100);int calls=0;
                health.BulletDamageModifier=(amount,point)=>{calls++;return amount*.25f;};
                health.TakeBulletDamage(100,Vector3.zero,null,Vector3.forward);
                Assert.AreEqual(75,health.Current);Assert.AreEqual(1,calls);
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}
