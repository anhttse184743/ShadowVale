using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class M16BalanceTests : ForestSceneTestBase
    {
        const string Work = "Docs/Reviews/M16_C3_Balance_20261022";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        [SetUp] public void Isolate() => SessionState.SetString(ForestSaveSlots.EditorTestRootKey, Path.GetFullPath(Work + "/TestSaves"));

        [Test] public void RuntimePrefabAndContentAgreeAndDoNotInheritAkDamage()
        {
            var content = JsonUtility.FromJson<ForestBundle>(File.ReadAllText("Assets/_Project/Map01/Generated/Map01Content.json"));
            var profile = content.weapons.Single(w => w.id == Map01M16.Id);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Items/W_M16.prefab").GetComponent<Weapon>();
            Assert.AreEqual(Map01M16.Damage, profile.damage); Assert.AreEqual(profile.damage, prefab.Damage);
            Assert.AreEqual(Map01M16.Range, profile.range); Assert.AreEqual(profile.range, prefab.Range);
            Assert.AreEqual(Map01M16.ShotSeconds, prefab.Cooldown, .00001f);
            Assert.AreEqual(1f / profile.fire_rate, prefab.Cooldown, .00001f);
            Assert.IsTrue(prefab.UsesM16Handling); Assert.IsFalse(prefab.LethalAgainstEnemies);
            Assert.AreEqual(30, profile.magazine_size); Assert.AreEqual(2.4f, profile.reload_seconds);
            Assert.AreEqual(18, profile.noise_radius); Assert.IsTrue(prefab.Automatic);
            var actor = Object.Instantiate(Map01ActorAssets.Load().rifle.gameObject);
            try
            {
                var gun = actor.GetComponent<Weapon>();
                gun.ConfigureRifle(999, .5f, gun.Muzzle);
                Map01RifleVisual.Apply(gun, Map01M16.Id);
                Assert.AreEqual(22, gun.Damage, "M16 owns its damage even if the AK baseline changes.");
                Assert.AreEqual(80, gun.Range); Assert.IsTrue(gun.UsesM16Handling);
                Map01RifleVisual.Apply(gun, "rifle_standard");
                Assert.AreEqual(999, gun.Damage); Assert.IsFalse(gun.UsesM16Handling);
            }
            finally { Object.DestroyImmediate(actor); }
        }

        [Test] public void SustainedM16FireBloomsEarlierKicksMoreAndRecoversForATap()
        {
            var m16 = new WeaponSpreadState(); var ak = new WeaponSpreadState();
            for (int i = 0; i < 12; i++)
            {
                if (i > 0) { m16.Tick(.1f, true); ak.Tick(.1f); }
                var m16Kick = m16.Fire(i * .1f, true); var akKick = ak.Fire(i * .1f);
                Assert.AreEqual(akKick.x * 1.35f, m16Kick.x, .00001f);
                if (i == 1) Assert.AreEqual(0, m16.Bloom);
                if (i == 2) { Assert.Greater(m16.Bloom, 0); Assert.AreEqual(0, ak.Bloom); }
            }
            Assert.Greater(m16.Bloom, ak.Bloom + .4f);
            Assert.Greater(m16.ConeHalfAngle(ShooterStance.Standing, true, WeaponKind.Rifle, true),
                ak.ConeHalfAngle(ShooterStance.Standing, true, WeaponKind.Rifle) * 2);
            m16.Fire(1.5f, true);
            Assert.AreEqual(1, m16.ShotIndex); Assert.AreEqual(0, m16.Bloom);
            Assert.AreEqual(.12f, m16.ConeHalfAngle(ShooterStance.Standing, true, WeaponKind.Rifle, true));
            Assert.Greater(m16.ConeHalfAngle(ShooterStance.Walking, true, WeaponKind.Rifle, true), .5f);
            Assert.AreEqual(.01f, m16.ConeHalfAngle(ShooterStance.Standing, true, WeaponKind.Sniper, true),
                "A sniper never inherits M16 handling.");
        }

        [UnityTest] public IEnumerator ActualM16NeedsFourHitsForAGuardAndCannotReachPastEightyMetres()
        {
            K54IntegrationTests.OpenSandbox(); yield return new EnterPlayMode(); yield return WaitForMap01Ready();
            yield return ReviewActualHits();
            yield return new ExitPlayMode();
        }

        // Keep the captured fixture state inside Play Mode, after Unity's domain reload.
        static IEnumerator ReviewActualHits()
        {
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            Assert.NotNull(mission);
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            if (Mouse.current == null) InputSystem.AddDevice<Mouse>();
            mission.GetComponent<Map01Rescue>().MarkIntroSeen(); mission.Cinematic = false;
            mission.ModernHealth.CinematicInvulnerable = true;
            foreach (var g in mission.Enemies) { g.enabled = false; var nav = g.GetComponent<NavMeshAgent>(); if (nav != null) nav.enabled = false; }
            mission.GetComponent<Map01C3Mission>().enabled = false; Map01BodyDiscovery.For(mission).enabled = false;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0, 79.8f, 45); floor.transform.localScale = new Vector3(18, .2f, 100);
            PlaceNam(mission, new Vector3(0, 80.04f, 0)); mission.player.rotation = Quaternion.identity;
            mission.player.GetComponent<CharacterController>().Move(Vector3.down * .2f);
            Assert.IsTrue(mission.player.GetComponent<CharacterController>().isGrounded);
            var bag = mission.GetComponent<Map01Inventory>(); bag.Add(Map01M16.Id, 1); bag.Add(Map01M16.Ammo, 60);
            int cell = Enumerable.Range(0, Map01Inventory.BagSize).First(i => bag.Cell(i)?.id == Map01M16.Id);
            Assert.IsTrue(bag.EquipFromCell(cell)); Assert.IsTrue(bag.Draw(Map01Inventory.Slot.Primary));
            var combat = mission.ModernCombat; var gun = combat.WeaponFor(WeaponKind.Rifle);
            mission.ModernPlayer.enabled = false; combat.enabled = false;
            mission.gameCamera.GetComponent<ThirdPersonCamera>().enabled = false;
            var target = mission.Enemies.First(g => !g.IsBoss && g.GetComponent<Map01C3Sleeper>() == null);
            var health = target.GetComponent<Health>(); health.SetMaxHealth(80); health.CinematicInvulnerable = false;
            int events = 0; health.Damaged += _ => events++;
            void Stage(float distance)
            {
                target.Repost(new Vector3(0, 80, distance), 180, stationary: true);
                var eye = mission.player.position + Vector3.up * 1.65f;
                mission.gameCamera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target.Position + Vector3.up * 1.05f - eye));
                Physics.SyncTransforms();
            }
            void Fire()
            {
                combat.enabled = true;
                typeof(PlayerCombat).GetMethod("SetAiming", Private).Invoke(combat, new object[] { true });
                try
                {
                    typeof(PlayerCombat).GetMethod("Attack", Private).Invoke(combat, null);
                    typeof(PlayerCombat).GetMethod("ResolvePendingGunfire", Private).Invoke(combat, null);
                }
                finally { combat.enabled = false; }
            }
            yield return WaitGameSeconds(.5f);
            combat.RestoreMagazine(WeaponKind.Rifle, 30);
            Stage(81); Fire(); Assert.AreEqual(80, health.Current); Assert.AreEqual(0, events);
            yield return WaitGameSeconds(.4f);
            Stage(79); Fire(); Assert.AreEqual(58, health.Current); Assert.AreEqual(1, events);
            yield return WaitGameSeconds(.4f);
            Stage(20);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = new Vector3(0, 81.5f, 10); wall.transform.localScale = new Vector3(5, 4, 1); Physics.SyncTransforms();
            Fire(); Assert.AreEqual(58, health.Current); Assert.AreEqual(1, events); Object.Destroy(wall);
            yield return WaitGameSeconds(.4f);
            Fire(); Assert.AreEqual(36, health.Current);
            yield return WaitGameSeconds(.4f);
            Fire(); Assert.AreEqual(14, health.Current);
            yield return WaitGameSeconds(.4f);
            Fire(); Assert.IsTrue(health.IsDead); Assert.AreEqual(4, events); Assert.AreEqual(24, combat.RoundsInMagazine);
            Directory.CreateDirectory(Work);
            File.WriteAllText(Work + "/m16-live-hit-evidence.txt", "Actual Map 1 enemy: 80 -> 58 (79m) -> 36 -> 14 -> 0; four hits. 81m miss and solid-wall block. Six shots spend six rounds, 30 -> 24.\n");
            Assert.AreEqual(Map01M16.Damage, gun.Damage);
        }

        [Serializable] public sealed class LayoutAudit { public PointAudit[] points; public EnemyAudit[] enemies; }
        [Serializable] public sealed class PointAudit { public string id, name, kind; public Vector3 position; public bool active; public float restock; public ForestIngredient[] items; }
        [Serializable] public sealed class EnemyAudit { public string id, name, rifle; public Vector3 position; public float health, vision, attackRange; public int operation; public bool active, boss; }
        [UnityTest] public IEnumerator CaptureCurrentRuntimeC3AndResourceLayoutForTheProposal()
        {
            K54IntegrationTests.OpenSandbox(); yield return new EnterPlayMode(); yield return WaitForMap01Ready();
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            Assert.NotNull(mission.GetComponent<Map01C3Mission>().CachePoint);
            var audit = new LayoutAudit
            {
                points = Object.FindObjectsByType<ForestPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(p => p.kind != ForestPointKind.Hide && p.kind != ForestPointKind.Cover)
                    .OrderBy(p => p.id).Select(p => new PointAudit { id = p.id, name = p.name, kind = p.kind.ToString(), position = p.transform.position, active = p.gameObject.activeInHierarchy, restock = p.restockSeconds, items = p.items }).ToArray(),
                enemies = Object.FindObjectsByType<Map01EnemyController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .OrderBy(g => g.SaveId).Select(g => new EnemyAudit { id = g.SaveId, name = g.name, rifle = g.RifleItemId, position = g.Position, health = g.GetComponent<Health>().Max, vision = g.VisionRange, attackRange = g.AttackRange, operation = g.operationIndex, active = g.gameObject.activeInHierarchy, boss = g.IsBoss }).ToArray()
            };
            Directory.CreateDirectory(Work); File.WriteAllText(Work + "/current-runtime-layout.json", JsonUtility.ToJson(audit, true));
            Assert.IsTrue(audit.points.Any(p => p.id == Map01C3Mission.CacheId));
            yield return new ExitPlayMode();
        }
    }
}
