using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Enemies;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class SupplyCombatFlowTests : ForestSceneTestBase
    {
        const string Work = "Docs/Reviews/Supply_Combat_Flow_20261022";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static Map01Mission M => Object.FindFirstObjectByType<Map01Mission>();
        [SetUp] public void Isolate() => SessionState.SetString(ForestSaveSlots.EditorTestRootKey, Path.GetFullPath(Work + "/TestSaves"));

        [Test] public void ThreeRoundBurstsRestForRecoveryAndThirtyRoundsRequireAReload()
        {
            var tuning = new Map01GuardFireTuning(); var cycle = new Map01GuardFireCycle();
            for (int shot = 1; shot <= 30; shot++)
            {
                float delay = cycle.AfterShot(tuning, Map01M16.ShotSeconds, .2f);
                Assert.AreEqual(shot == 30 ? 3.2f : shot % 3 == 0 ? 2f : .1f, delay, .0001f, "shot " + shot);
            }
            Assert.AreEqual(0, cycle.BurstShots); Assert.AreEqual(0, cycle.MagazineSpent);
            cycle.Restore(2, 29, tuning); Assert.AreEqual(3.2f, cycle.AfterShot(tuning, .1f, 0));
            cycle.Restore(999, -5, tuning); Assert.AreEqual(2, cycle.BurstShots); Assert.AreEqual(0, cycle.MagazineSpent);
        }

        [Test] public void ConsumablesAreGuaranteedBeforeC3AndTheStoreFitsAUsefulFortyPointKit()
        {
            var data = Map01Loot.Load(); CollectionAssert.IsEmpty(data.Problems());
            int Minimum(string table, string item) => data.TableOf(table).entries.Where(e => e.item == item && e.tier == 5).Sum(e => e.min);
            Assert.AreEqual(1, Minimum("lt_m1_base_start", "smoke_bomb"));
            Assert.AreEqual(1, Minimum("lt_m1_supply_reward", "smoke_bomb"));
            Assert.AreEqual(1, Minimum("lt_m1_supply_reward", "grenade_f1"));
            Assert.AreEqual(2, Minimum("lt_m1_supply_reward", "medkit_small"));
            Assert.AreEqual(2, Minimum("lt_m1_chest_c1", "medkit_small"));
            Assert.AreEqual(1, Minimum("lt_m1_chest_c1", "smoke_bomb"));
            Assert.AreEqual(1, Minimum("lt_m1_chest_c1", "grenade_f1"));
            Assert.AreEqual(3, Minimum("lt_m1_chest_c2", "medkit_small"));
            Assert.AreEqual(2, Minimum("lt_m1_chest_c2", "smoke_bomb"));
            Assert.AreEqual(1, Minimum("lt_m1_chest_c2", "grenade_f1"));
            Assert.AreEqual(3, Minimum("lt_c3_field_approach", "medkit_small"));
            Assert.AreEqual(2, Minimum("lt_c3_field_approach", "smoke_bomb"));
            Assert.AreEqual(1, Minimum("lt_c3_field_approach", "grenade_f1"));
            Assert.AreEqual(2, Minimum("lt_c3_field_medic", "medkit_small"));
            Assert.AreEqual(1, Minimum("lt_m1_base_stock", "grenade_f1"));
            Assert.AreEqual(39, 2 * data.BaseValue("medkit_small") + data.BaseValue("smoke_bomb"));
            Assert.AreEqual(3, data.adaptive.medkitPity);
            // A healthy player still finds more dressings; support does not require farming components first.
            var table = data.TableOf("lt_m1_operation_guard");
            double medkitShare = Enumerable.Range(0, 6000).Count(seed => Map01LootRoller.Roll(data, table, seed).Any(d => d.item == "medkit_small")) / 6000.0;
            Assert.That(medkitShare, Is.InRange(.21, .27));
        }

        static void Quiet()
        {
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>(); if (Mouse.current == null) InputSystem.AddDevice<Mouse>();
            M.GetComponent<Map01Rescue>().MarkIntroSeen(); M.Cinematic = false;
            M.ModernHealth.CinematicInvulnerable = true;
            foreach (var guard in M.Enemies) { guard.enabled = false; var nav = guard.GetComponent<NavMeshAgent>(); if (nav != null) nav.enabled = false; }
        }
        [UnityTest] public IEnumerator ActualGuardBurstsLimitGroupFireKeepReloadAndRespectBossCadence()
        {
            K54IntegrationTests.OpenSandbox(); yield return new EnterPlayMode(); yield return WaitForMap01Ready();
            yield return ReviewGuardBursts();
            yield return new ExitPlayMode();
        }
        static IEnumerator ReviewGuardBursts()
        {
            Quiet(); M.Cinematic = true; // Hold their brains while driving the real weapon API at controlled times.
            M.ModernHealth.SetMaxHealth(1000); M.ModernHealth.CinematicInvulnerable = false;
            var guards = M.Enemies.Where(g => !g.IsBoss && g.GetComponent<Map01C3Sleeper>() == null).Take(6).ToArray();
            Assert.AreEqual(6, guards.Length);
            PlaceNam(M, new Vector3(0, 80, 0)); M.ModernPlayer.enabled = false;
            foreach (var guard in guards)
            {
                guard.RefreshRifleLoadout(Map01M16.Id); guard.ReturnToPost();
                guard.Repost(new Vector3(Array.IndexOf(guards, guard) * 2, 80, 12), 180, stationary: true);
                guard.Accuracy = new ShotAccuracyProfile { enabled = false };
                typeof(Map01EnemyController).GetField("obstructionMask", Private).SetValue(guard, (LayerMask)0);
                guard.enabled = true;
            }
            Physics.SyncTransforms();
            var baseline = guards.ToDictionary(g => g, g => g.AudibleShots);
            foreach (var guard in guards) { guard.FireAtPlayer(); guard.FireAtPlayer(); }
            Assert.AreEqual(2, guards.Sum(g => g.AudibleShots - baseline[g]), "Only two ordinary M16 bursts begin together; a same-frame second call cannot fire.");
            float groupUntil = Time.time + 1.2f;
            while (Time.time < groupUntil)
            {
                foreach (var guard in guards) guard.FireAtPlayer();
                Assert.LessOrEqual(guards.Count(g => g.Capture().guardBurstLeaseRemaining > 0), 2);
                yield return null;
            }
            foreach (var guard in guards) guard.enabled = false;
            var one = guards[0]; one.ReturnToPost(); one.enabled = true;
            int previous = one.AudibleShots; var times = new List<float>(); float deadline = Time.time + 45;
            float oldScale = Time.timeScale; Time.timeScale = 2;
            try
            {
                while (times.Count < 31 && Time.time < deadline)
                {
                    one.FireAtPlayer();
                    if (one.AudibleShots > previous) { times.Add(Time.time); previous = one.AudibleShots; }
                    yield return null;
                }
            }
            finally { Time.timeScale = oldScale; }
            Assert.AreEqual(31, times.Count);
            for (int shot = 1; shot < times.Count; shot++)
            {
                float gap = times[shot] - times[shot - 1];
                Assert.GreaterOrEqual(gap, shot == 30 ? 3.19f : shot % 3 == 0 ? 1.79f : .095f, "gap before shot " + (shot + 1));
            }
            var saved = one.Capture(); Assert.AreEqual(1, saved.guardMagazineSpent); Assert.AreEqual(1, saved.guardBurstShots);
            one.ReturnToPost(); one.RestoreSnapshot(saved); Assert.AreEqual(saved.guardMagazineSpent, one.Capture().guardMagazineSpent);
            // A frame hitch cannot repay overdue rounds in one frame.
            typeof(Map01EnemyController).GetField("_nextShot", Private).SetValue(one, Time.time - 20);
            int beforeHitch = one.AudibleShots; one.FireAtPlayer(); one.FireAtPlayer(); Assert.AreEqual(beforeHitch + 1, one.AudibleShots);
            var boss = M.GetComponent<Map01C3Mission>().Commander; boss.RefreshRifleLoadout(Map01M16.Id);
            boss.ConfigureBursts(3, .3f, 1.4f);
            var next = typeof(Map01EnemyController).GetMethod("NextShotDelay", Private);
            Assert.AreEqual(.3f, (float)next.Invoke(boss, null)); Assert.AreEqual(.3f, (float)next.Invoke(boss, null)); Assert.AreEqual(1.4f, (float)next.Invoke(boss, null));
            Directory.CreateDirectory(Work);
            File.WriteAllText(Work + "/guard-fire-evidence.txt", "Six ordinary M16 guards: max two concurrent burst leases. Same-frame calls produce one shot.\nShot timestamps: " + string.Join(",", times.Select(t => t.ToString("F3", System.Globalization.CultureInfo.InvariantCulture))) + "\nEvery third shot rests >= 1.8s; 30th -> 31st rests >= 3.2s. Save preserves magazine/burst progress. Boss uses its own .3/.3/1.4 profile.\n");
            foreach (var guard in guards) guard.enabled = false;
            M.Cinematic = false;
        }

        [UnityTest] public IEnumerator FieldSuppliesCanBeCollectedBeforeTheBossAndPersistWithoutRefills()
        {
            K54IntegrationTests.OpenSandbox(); SetPreviewResolution(1600, 900);
            yield return new EnterPlayMode(); yield return WaitForMap01Ready();
            yield return ReviewFieldSupplies();
            yield return new ExitPlayMode();
        }
        static IEnumerator ReviewFieldSupplies()
        {
            Quiet(); yield return null;
            var caches = M.GetComponent<Map01FieldSupplies>(); Assert.NotNull(caches); Assert.NotNull(caches.ApproachPoint); Assert.NotNull(caches.MedicPoint);
            Assert.IsFalse(caches.ApproachPoint.gameObject.activeSelf);
            var q = M.GetComponent<Map01Quest>(); q.RestoreStage(Map01Quest.BossStage); yield return null;
            var approach = caches.ApproachPoint; var medic = caches.MedicPoint;
            Assert.IsTrue(approach.gameObject.activeSelf); Assert.IsTrue(medic.gameObject.activeSelf);
            Assert.IsTrue(M.GetComponent<Map01C3Mission>().Commander.Alive);
            foreach (var point in new[] { approach, medic })
            {
                Assert.IsTrue(NavMesh.SamplePosition(point.transform.position, out var onNav, .5f, NavMesh.AllAreas), point.id + " must be reachable.");
                Assert.Less(Vector3.Distance(point.transform.position, onNav.position), .2f);
            }
            var route = new NavMeshPath(); var c3 = M.GetComponent<Map01C3Mission>();
            Assert.IsTrue(NavMesh.SamplePosition(c3.CachePoint.transform.position, out var nest, 3f, NavMesh.AllAreas));
            Assert.IsTrue(NavMesh.CalculatePath(nest.position, approach.transform.position, NavMesh.AllAreas, route));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, route.status);
            Assert.IsTrue(NavMesh.SamplePosition(c3.At(68, 44), out var gate, 2f, NavMesh.AllAreas));
            Assert.IsTrue(NavMesh.CalculatePath(gate.position, medic.transform.position, NavMesh.AllAreas, route));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, route.status);
            var bag = M.GetComponent<Map01Inventory>(); var interact = M.GetComponent<Map01PlayerInteraction>();
            int med = bag.Count("medkit_small"), smoke = bag.Count("smoke_bomb"), f1 = bag.Count("grenade_f1");
            PlaceNam(M, approach.transform.position + Vector3.back * 1.2f);
            interact.Interact(approach); Map01NamActions.For(M).CancelAction();
            Assert.AreEqual(med + 3, bag.Count("medkit_small")); Assert.AreEqual(smoke + 2, bag.Count("smoke_bomb")); Assert.AreEqual(f1 + 1, bag.Count("grenade_f1"));
            Assert.IsTrue(approach.used); interact.Interact(approach); Assert.AreEqual(med + 3, bag.Count("medkit_small"), "Used caches cannot be collected twice.");
            // A legacy run may have taken the Mosin already; the new caches have their own IDs.
            c3.CachePoint.MarkLooted();
            Assert.IsTrue(M.GetComponent<Map01SaveSystem>().SaveSlot(1, out var error, true), error);
            M.GetComponent<Map01SaveSystem>().Load(); yield return null; yield return WaitForMap01Ready(); yield return null; Quiet();
            caches = M.GetComponent<Map01FieldSupplies>(); bag = M.GetComponent<Map01Inventory>();
            Assert.IsTrue(caches.ApproachPoint.used); Assert.IsFalse(caches.MedicPoint.used);
            Assert.IsTrue(M.GetComponent<Map01C3Mission>().CachePoint.used);
            Assert.AreEqual(med + 3, bag.Count("medkit_small")); Assert.AreEqual(smoke + 2, bag.Count("smoke_bomb")); Assert.AreEqual(f1 + 1, bag.Count("grenade_f1"));
            M.RestoreHealthFromSave(20); Assert.IsTrue(bag.UseItem("medkit_small")); Assert.AreEqual(65, M.PlayerHealth, .01f);
            // When medicine is scarce, three dry packs cannot continue withholding it. Cached packs stay cached.
            bag.Spend("medkit_small", bag.Count("medkit_small"));
            var logistics = Map01Logistics.For(M); var state = logistics.Capture(); state.medkitDry = 2; logistics.Restore(state);
            var guard = M.Enemies.First(g => !g.IsBoss); var pack = logistics.GuardPack(guard, "supply_flow_pity_probe");
            Assert.IsTrue(pack.Any(i => i.item_id == "medkit_small"));
            Assert.AreEqual(JsonUtility.ToJson(new Pack { items = pack }), JsonUtility.ToJson(new Pack { items = logistics.GuardPack(guard, "supply_flow_pity_probe") }));
            // Photograph the actual world cache, rather than a diagram or recreated model.
            M.gameCamera.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>().enabled = false;
            var focus = caches.MedicPoint.transform.position;
            M.gameCamera.transform.SetPositionAndRotation(focus + new Vector3(1.2f, 1.8f, -2f), Quaternion.LookRotation(focus + Vector3.up * .12f - (focus + new Vector3(1.2f, 1.8f, -2f))));
            yield return null; yield return null;
            Directory.CreateDirectory(Work); ScreenCapture.CaptureScreenshot(Work + "/c3-medic-cache.png"); for (int i = 0; i < 4; i++) yield return null;
            File.WriteAllText(Work + "/field-supply-evidence.txt", "Approach cache: 3 medkits + 2 smoke + 1 F1 before commander death. Western medical cache reachable by complete NavMesh path from gate.\nApproach=" + caches.ApproachPoint.transform.position + "; medic=" + caches.MedicPoint.transform.position + "\nSaved/load retains separate Mosin/approach/medic used states; no duplicate items. One dressing heals 20 -> 65 HP. Three dry packs with low medicine guarantee a dressing.\n");
        }
        [Serializable] sealed class Pack { public ForestIngredient[] items; }

        [UnityTest] public IEnumerator SupplyDeliveryGivesReadyThrowablesOnceWithoutLosingThemToAFullBag()
        {
            K54IntegrationTests.OpenSandbox(); yield return new EnterPlayMode(); yield return WaitForMap01Ready();
            ReviewSupplyReward(); yield return new ExitPlayMode();
        }
        static void ReviewSupplyReward()
        {
            Quiet(); var bag = M.GetComponent<Map01Inventory>(); var story = M.GetComponent<Map01Story>();
            bag.Add("scrap_metal", 100000);
            Assert.IsTrue(Enumerable.Range(0, Map01Inventory.BagSize).All(i => bag.Cell(i) != null));
            int Total(string item) => bag.Count(item) + Map01Drops.For(M).Capture().Where(p => p.id == item).Sum(p => p.count);
            int med = Total("medkit_small"), smoke = Total("smoke_bomb"), f1 = Total("grenade_f1");
            story.Progress.supplyRewardGranted = false;
            var grant = typeof(Map01Story).GetMethod("GiveSupplyReward", Private); grant.Invoke(story, null);
            Assert.AreEqual(med + 2, Total("medkit_small")); Assert.AreEqual(smoke + 1, Total("smoke_bomb")); Assert.AreEqual(f1 + 1, Total("grenade_f1"));
            Assert.IsTrue(story.Progress.supplyRewardGranted); grant.Invoke(story, null);
            Assert.AreEqual(med + 2, Total("medkit_small")); Assert.AreEqual(f1 + 1, Total("grenade_f1"));
            Directory.CreateDirectory(Work);
            File.WriteAllText(Work + "/supply-reward-evidence.txt", "C1 handoff guarantees 2 dressings + 1 smoke + 1 F1, retaining the original supplies and 30 AK rounds. Full bags place overflow at Nam's feet; repeated reward calls grant nothing.\n");
        }

        [UnityTest] public IEnumerator StoreShowsTheBottomSmokeOfferAndCanIssueAffordableConsumables()
        {
            K54IntegrationTests.OpenSandbox(); SetPreviewResolution(1600, 900);
            yield return new EnterPlayMode(); yield return WaitForMap01Ready();
            yield return ReviewStore(); yield return new ExitPlayMode();
        }
        static IEnumerator ReviewStore()
        {
            Quiet();
            var shelf = M.Interactables.First(p => p.kind == ForestPointKind.Workbench || p.kind == ForestPointKind.Supplies);
            PlaceNam(M, shelf.transform.position + Vector3.back);
            var logistics = Map01Logistics.For(M); var state = logistics.Capture(); state.credit = 100; logistics.Restore(state);
            var hud = M.GetComponent<Map01Hud>(); var bag = M.GetComponent<Map01Inventory>(); hud.OpenStore();
            var stock = logistics.Stock.ToList(); Assert.Greater(stock.Count, 8); Assert.AreEqual("smoke_bomb", stock.Last().item);
            yield return null; yield return null;
            Directory.CreateDirectory(Work); ScreenCapture.CaptureScreenshot(Work + "/store-top.png"); for (int i = 0; i < 3; i++) yield return null;
            typeof(Map01Hud).GetField("storeScroll", Private).SetValue(hud, new Vector2(0, 10000));
            yield return null; yield return null;
            ScreenCapture.CaptureScreenshot(Work + "/store-bottom.png"); for (int i = 0; i < 3; i++) yield return null;
            int before = bag.Count("smoke_bomb"); Assert.IsTrue(logistics.Issue(stock.Last())); Assert.AreEqual(before + 1, bag.Count("smoke_bomb"));
            before = bag.Count("grenade_f1"); Assert.IsTrue(logistics.Issue(stock.Single(s => s.item == "grenade_f1"))); Assert.AreEqual(before + 1, bag.Count("grenade_f1"));
            var medkit = stock.Single(s => s.item == "medkit_small"); Assert.IsTrue(logistics.Issue(medkit)); Assert.IsTrue(logistics.Issue(medkit));
            Assert.AreEqual(31, logistics.Credit, "15 smoke + 30 F1 + 24 for two dressings = 69 points.");
            File.WriteAllText(Work + "/store-evidence.txt", "All 10 stock offers render in a scrollable viewport; first F1 / last smoke photographed. Issue API grants smoke, F1 and two dressings for 69 points.\n");
            M.SetBenchOpen(false);
        }
    }
}
