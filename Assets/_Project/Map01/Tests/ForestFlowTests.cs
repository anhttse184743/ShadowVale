using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using ShadowVale.UI.HUD;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class ForestFlowTests : ForestSceneTestBase
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        /// <summary>Kept out of the test iterators on purpose: a lambda capturing one of their
        /// locals moves it into a closure created before EnterPlayMode, and the runner's resume
        /// after the domain reload skips that creation — the closure is null and the test NREs.</summary>
        private static bool NearestZoneIs(Map01Scouting scouting, Vector3 target) =>
            scouting.Camps.Any(c => !c.Found && Vector3.Distance(c.ZoneCenter, target) < .1f && Vector3.Distance(c.Center, target) > 5f);

        private static Map01EnemyController NearestOutpostGuard(Map01Mission mission) =>
            mission.Enemies.Where(e => e.Alive && e.name.StartsWith("Outpost guard "))
                .OrderBy(e => Vector3.Distance(e.transform.position, mission.player.position)).First();

        [TestCase(false)]
        [TestCase(true)]
        public void MissingOrDestroyedContentStopsInitializationWithoutGuardCascade(bool destroyed)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Mission initialization regression");
            var balance = new TextAsset("{}");
            // Edit Mode does not invoke these runtime lifecycle methods automatically.
            var mission = root.AddComponent<Map01Mission>();
            try
            {
                mission.balanceJson = balance;
                if (destroyed)
                {
                    mission.contentBundle = new TextAsset("{}");
                    Object.DestroyImmediate(mission.contentBundle);
                }
                LogAssert.Expect(LogType.Error, "Map01Mission could not initialize. Assign valid balanceJson and contentBundle TextAssets in the Inspector.");
                typeof(Map01Mission).GetMethod("Awake", Private).Invoke(mission, null);
                Assert.IsFalse(mission.IsInitialized);
                Assert.IsFalse(mission.enabled);
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(balance);
            }
        }

        [UnityTest]
        public IEnumerator BriefingRouteRescuesHungClearsOutpostsAndDefeatsCommander()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var inventory = mission.GetComponent<Map01Inventory>();
            var interaction = mission.GetComponent<Map01PlayerInteraction>();
            Assert.IsTrue(mission.IsInitialized, "The serialized mission content must resolve before guards start.");
            Assert.AreEqual(0, quest.Stage);
            var points = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None);
            var controller = mission.player.GetComponent<CharacterController>();

            interaction.Interact(points.Single(p => p.id == "supplies"));
            Assert.AreEqual(0, quest.Stage, "Reporting to base is gated until Hùng is treated.");

            inventory.Add("herb", 1);
            controller.enabled = false; mission.player.position = mission.hung.position; controller.enabled = true;
            yield return null;
            quest.TryRescueHung();
            Assert.AreEqual(Map01Quest.EscortStage, quest.Stage, "Treating the wounded Hùng with herb must send Nam back to base.");
            Assert.AreEqual(0, inventory.Count("herb"), "The herb must be spent on the treatment.");

            interaction.Interact(points.Single(p => p.id == "supplies"));
            Assert.AreEqual(Map01Quest.BriefingStage, quest.Stage, "Bringing Hùng home ends the escort.");
            Assert.AreEqual(1, inventory.Count("supplies"));

            // Every order comes from Hùng in person, and every finished task is reported back to him.
            quest.TalkToHung();
            Assert.AreEqual(Map01Quest.ScoutStage, quest.Stage, "Hùng's first order is to scout.");
            quest.CompleteScouting(); // The binocular run itself is covered by ScoutingTests.
            Assert.AreEqual(Map01Quest.ReportScoutStage, quest.Stage, "Logging all three camps finishes the scouting.");
            quest.TalkToHung();
            Assert.AreEqual(Map01Quest.CampsStage, quest.Stage);

            var outposts = Object.FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None)
                .Where(e => e.name.StartsWith("Outpost guard ")).ToArray();
            Assert.GreaterOrEqual(outposts.Length, 1, "Map 1's three built-in enemy outposts must still be present.");
            foreach (var guard in outposts) guard.GetComponent<Health>().TakeDamage(9999, guard.transform.position, null);
            yield return null;
            Assert.AreEqual(Map01Quest.ReportCampsStage, quest.Stage, "Clearing every outpost sends Nam back to report.");
            Assert.IsFalse(quest.BossSpawned, "The commander only shows up once Hùng has the report.");
            quest.TalkToHung();
            Assert.AreEqual(Map01Quest.BossStage, quest.Stage);

            var boss = Object.FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None).Single(e => e.name == "Chỉ huy địch");
            Assert.IsTrue(boss.IsBoss);
            boss.GetComponent<Health>().TakeDamage(999999, boss.transform.position, null);
            Assert.AreEqual(Map01Quest.ReportBossStage, quest.Stage, "The commander's death still has to be reported.");
            quest.TalkToHung();
            Assert.AreEqual(Map01Quest.CompleteStage, quest.Stage, "Reporting the victory to Hùng completes Map 1.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator InteractKeyTreatsHungWhenInRangeElseFallsBackToNearbyPoint()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var inventory = mission.GetComponent<Map01Inventory>();
            var interaction = mission.GetComponent<Map01PlayerInteraction>();
            var loot = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None).Single(p => p.id == "tutorial_loot");
            var controller = mission.player.GetComponent<CharacterController>();

            // Map01PlayerInteraction.Update() (which computes Nearby) bails out with no Keyboard
            // device present, and batchmode Play Mode tests do not attach a real one.
            var testKeyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                // Stage 0, standing at a point that is not Hùng: [E] must still trigger it — before
                // this fix, stage 0 routed every [E] press into the rescue check alone, so the herb
                // the rescue itself needs could never be picked up.
                Assert.AreEqual(0, quest.Stage);
                controller.enabled = false; mission.player.position = loot.transform.position; controller.enabled = true;
                yield return null;
                typeof(Map01PlayerInteraction).GetMethod("HandleInteractKey", Private).Invoke(interaction, null);
                Assert.Greater(inventory.Count("herb"), 0, "Standing at a point other than Hùng must still interact with it.");

                // Standing at Hùng instead: [E] treats him rather than doing nothing.
                controller.enabled = false; mission.player.position = mission.hung.position; controller.enabled = true;
                yield return null;
                typeof(Map01PlayerInteraction).GetMethod("HandleInteractKey", Private).Invoke(interaction, null);
                Assert.AreEqual(1, quest.Stage, "[E] near Hùng must treat him, not silently do nothing.");
            }
            finally { InputSystem.RemoveDevice(testKeyboard); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator StartsWithWeaponsOnlyThenLootSuppliesAmmoAndHerb()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var inventory = mission.GetComponent<Map01Inventory>();
            var interaction = mission.GetComponent<Map01PlayerInteraction>();

            // Nam starts equipped but empty-handed — the loadout is weapons only, no free ammo
            // or herb, so the field pickup is the only way to arm up.
            Assert.AreEqual(1, inventory.Count("rifle_standard"));
            Assert.AreEqual(1, inventory.Count("knife"));
            Assert.AreEqual(0, inventory.Count("ammo_rifle"));
            Assert.AreEqual(0, inventory.Count("medkit_small"));
            Assert.AreEqual(0, inventory.Count("herb"));
            Assert.AreEqual(0, inventory.Count("cloth"));

            var loot = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None).Single(p => p.id == "tutorial_loot");
            interaction.Interact(loot);
            Assert.AreEqual(60, inventory.Count("ammo_rifle"), "The starting crate gives two rifle magazines.");
            Assert.AreEqual(2, inventory.Count("medkit_small"), "The starting crate gives bandages so Nam can heal before the first fight.");
            Assert.AreEqual(2, inventory.Count("herb"), "The starting loot crate must supply the herb the rescue needs.");
            Assert.AreEqual(4, inventory.Count("cloth"));
            Assert.AreEqual(1, inventory.Count("rifle_standard"), "Looting must not touch the weapons already carried.");
            Assert.AreEqual(1, inventory.Count("knife"));

            // Healing: a real [H] press uses a bandage straight from what was just picked up.
            mission.Damage(50);
            float wounded = mission.PlayerHealth;
            var routing = RouteInputToGame();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.H));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
            }
            finally { InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing); }
            Assert.AreEqual(1, inventory.Count("medkit_small"), "[H] must spend one looted bandage.");
            // Guards nearby may land a hit in these frames, so compare against the wound, not an exact value.
            Assert.Greater(mission.PlayerHealth, wounded, "[H] must heal Nam.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator KillingAnEnemyDropsLootableAmmoOnce()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var inventory = mission.GetComponent<Map01Inventory>();
            var interaction = mission.GetComponent<Map01PlayerInteraction>();
            var guard = mission.Enemies.First(e => e.Alive);
            Assert.IsNull(guard.GetComponent<ForestPoint>(), "A living guard must not already carry a loot point.");

            guard.GetComponent<Health>().TakeDamage(9999, guard.transform.position, null);
            yield return null; // Update() drops the loot the first frame it observes the guard dead.

            var drop = guard.GetComponent<ForestPoint>();
            Assert.IsNotNull(drop, "A dead guard must drop a lootable ammo point.");
            Assert.AreEqual(ForestPointKind.Loot, drop.kind);

            int ammo = inventory.Count("ammo_rifle"), medkits = inventory.Count("medkit_small");
            interaction.Interact(drop);
            Assert.AreEqual(ammo + mission.Settings.guardDropAmmo, inventory.Count("ammo_rifle"), "Looting a dead guard must grant a magazine.");
            Assert.AreEqual(30, mission.Settings.guardDropAmmo, "Map01Balance.json: one AK magazine per guard.");
            Assert.AreEqual(medkits + 1, inventory.Count("medkit_small"), "Looting a dead guard must grant a bandage.");

            interaction.Interact(drop);
            yield return WaitGameSeconds(.2f);
            interaction.Interact(drop);
            Assert.AreEqual(ammo + 30, inventory.Count("ammo_rifle"), "A corpse is searched once — it never restocks.");
            Assert.AreEqual(medkits + 1, inventory.Count("medkit_small"));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SupplyCrateAndBaseRestockAmmoAndBandages()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var inventory = mission.GetComponent<Map01Inventory>();
            var interaction = mission.GetComponent<Map01PlayerInteraction>();
            var points = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None);
            var crate = points.Single(p => p.id == "tutorial_loot");
            var supplies = points.Single(p => p.id == "supplies");
            Assert.AreEqual(60, crate.restockSeconds, "The field crate refills every minute.");
            Assert.AreEqual(90, supplies.restockSeconds, "The base refills every minute and a half.");

            // The crate is empty right after looting, then refills with the same items.
            interaction.Interact(crate);
            interaction.Interact(crate);
            Assert.AreEqual(60, inventory.Count("ammo_rifle"), "An emptied crate must not pay out again before it restocks.");
            crate.restockSeconds = .1f; crate.MarkLooted(); // Shorten the wait for the test.
            yield return WaitGameSeconds(.3f);
            Assert.IsFalse(crate.used, "The crate must restock once its timer runs out.");
            interaction.Interact(crate);
            Assert.AreEqual(120, inventory.Count("ammo_rifle"));
            Assert.AreEqual(4, inventory.Count("medkit_small"));

            // The base only hands out supplies once Hùng is treated; delivering him opens the
            // resupply, which then keeps working after the quest has moved on.
            interaction.Interact(supplies);
            Assert.AreEqual(120, inventory.Count("ammo_rifle"), "No resupply before Hùng is brought home.");
            quest.RestoreStage(Map01Quest.EscortStage);
            interaction.Interact(supplies);
            Assert.AreEqual(Map01Quest.EscortStage, quest.Stage, "Hùng is still out where he was treated — he has not been brought home.");
            Assert.AreEqual(120, inventory.Count("ammo_rifle"));
            mission.hung.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(mission.player.position + Vector3.right);
            interaction.Interact(supplies);
            Assert.AreEqual(Map01Quest.BriefingStage, quest.Stage);
            Assert.AreEqual(180, inventory.Count("ammo_rifle"), "Bringing Hùng home comes with the first resupply.");
            supplies.restockSeconds = .1f; supplies.MarkLooted();
            yield return WaitGameSeconds(.3f);
            quest.RestoreStage(Map01Quest.CampsStage);
            interaction.Interact(supplies);
            Assert.AreEqual(240, inventory.Count("ammo_rifle"), "The base keeps resupplying after the quest moves on.");
            Assert.AreEqual(8, inventory.Count("medkit_small"));
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator FiringWorksAfterBagMapAndWeaponSwitches()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var inventory = mission.GetComponent<Map01Inventory>();
            var interaction = mission.GetComponent<Map01PlayerInteraction>();
            var combat = mission.player.GetComponent<PlayerCombat>();
            var routing = RouteInputToGame();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                interaction.Interact(Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None).Single(p => p.id == "tutorial_loot"));
                Assert.AreEqual(60, inventory.Count("ammo_rifle"));

                // Everything that raises SuppressFire in play, the way Map01PlayerInteraction does
                // it: Tab open/close, M open/close, and switching weapons from the bag.
                mission.SetInventoryOpen(true); inventory.SuppressFire = true;
                mission.SetInventoryOpen(false); inventory.SuppressFire = true;
                mission.SetMapOpen(true); inventory.SuppressFire = true;
                mission.SetMapOpen(false); inventory.SuppressFire = true;
                Assert.IsTrue(inventory.EquipItem("knife"));
                Assert.IsTrue(inventory.EquipItem("rifle_standard"));
                yield return null;
                Assert.IsTrue(combat.InputAllowed(), "Combat input must come back once the click that toggled a panel or weapon is released.");

                // A real trigger pull through PlayerCombat's own input path (the rifle is automatic,
                // so it reads the held button rather than a this-frame edge).
                var centre = new Vector2(Screen.width / 2f, Screen.height / 2f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = centre }.WithButton(MouseButton.Left)); InputSystem.Update();
                for (int i = 0; i < 5; i++) yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = centre }); InputSystem.Update();
                Assert.Less(inventory.Count("ammo_rifle"), 60, "Holding fire with the rifle equipped and ammo in the bag must shoot.");
            }
            finally { InputSystem.RemoveDevice(mouse); RestoreInputRouting(routing); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator GuardDamageFollowsMap01Balance()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var playerHealth = mission.player.GetComponent<Health>();
            float expected = mission.Weapon.damage * mission.Settings.guardDamageScale;
            foreach (var enemy in mission.Enemies)
            {
                Assert.AreEqual(expected, enemy.DamagePerShot, 1e-4f, enemy.name);
                Assert.AreEqual(mission.Settings.guardShotInterval, enemy.FireInterval, 1e-4f, enemy.name);
            }

            // One real hit through the guard's own Shoot(): exactly the balance damage, and no
            // second hit before the interval has passed.
            var guard = mission.Enemies.First(e => e.Alive);
            typeof(Map01EnemyController).GetField("_nextShot", Private).SetValue(guard, 0f);
            var shoot = typeof(Map01EnemyController).GetMethod("Shoot", Private);
            float before = playerHealth.Current;
            shoot.Invoke(guard, null);
            Assert.AreEqual(before - expected, playerHealth.Current, 1e-3f);
            shoot.Invoke(guard, null);
            Assert.AreEqual(before - expected, playerHealth.Current, 1e-3f, "A guard must not fire again before guardShotInterval.");

            // The commander keeps his multiplier on top of the baseline rather than being reset by it.
            quest.RestoreStage(Map01Quest.BossStage); // Spawns him, as reporting the outposts would.
            yield return null;
            var boss = mission.Enemies.Single(e => e.IsBoss);
            Assert.AreEqual(expected * 1.6f, boss.DamagePerShot, 1e-3f);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator CommanderReturnsAfterLoadingACheckpointMidFight()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var rootField = typeof(ForestSaveSlots).GetField("storageRoot", BindingFlags.NonPublic | BindingFlags.Static);
            string root = Path.Combine(Application.temporaryCachePath, "boss-test-" + Guid.NewGuid().ToString("N"));
            rootField.SetValue(null, root);
            try
            {
                var mission = Object.FindFirstObjectByType<Map01Mission>();
                var quest = mission.GetComponent<Map01Quest>();
                quest.RestoreStage(Map01Quest.CampsStage);
                foreach (var g in mission.Enemies.Where(e => e.name.StartsWith("Outpost guard ")))
                    g.GetComponent<Health>().TakeDamage(9999, g.transform.position, null);
                yield return null;
                Assert.AreEqual(Map01Quest.ReportCampsStage, quest.Stage);
                quest.RestoreStage(Map01Quest.BossStage); // What reporting the outposts to Hùng leads to.
                Assert.AreEqual(Map01Quest.BossStage, quest.Stage);
                var bossHealth = mission.Enemies.Single(e => e.IsBoss).GetComponent<Health>();
                bossHealth.TakeDamage(100, bossHealth.transform.position, null);
                float woundedHp = bossHealth.Current;

                // Quitting to the menu mid-fight autosaves at the boss stage (only manual saves are blocked there).
                Assert.IsTrue(mission.GetComponent<Map01SaveSystem>().AutoSaveOnExit(out var error), error);
                Map01SaveSystem.BeginGame(ForestSaveSlots.AutoSlot);
                yield return null;
                var pending = typeof(Map01SaveSystem).GetField("pendingCheckpoint", BindingFlags.NonPublic | BindingFlags.Static);
                double deadline = Time.realtimeSinceStartupAsDouble + 10;
                while (pending.GetValue(null) != null && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.IsNull(pending.GetValue(null));

                mission = Object.FindFirstObjectByType<Map01Mission>();
                quest = mission.GetComponent<Map01Quest>();
                Assert.AreEqual(Map01Quest.BossStage, quest.Stage);
                var boss = mission.Enemies.SingleOrDefault(e => e.IsBoss);
                Assert.IsNotNull(boss, "Loading a checkpoint taken mid-fight must bring the commander back, or the objective is stuck.");
                Assert.IsTrue(boss.Alive);
                Assert.AreEqual(woundedHp, boss.GetComponent<Health>().Current, 1e-3f, "The commander keeps the damage he had taken.");
                boss.GetComponent<Health>().TakeDamage(999999, boss.transform.position, null);
                Assert.AreEqual(Map01Quest.ReportBossStage, quest.Stage);
            }
            finally
            {
                rootField.SetValue(null, null);
                Time.timeScale = 1;
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator ObjectiveGuideFollowsEachQuestStep()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var interaction = mission.GetComponent<Map01PlayerInteraction>();
            var guide = mission.GetComponent<Map01ObjectiveGuide>();
            Assert.IsNotNull(guide, "The HUD must bring the objective guide along.");
            var points = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None);
            // Frozen guards cannot wound Nam mid-test (a dead Nam stops the mission, hiding the guide).
            foreach (var e in mission.Enemies) e.enabled = false;

            yield return WaitGameSeconds(.6f);
            Assert.IsTrue(guide.HasTarget);
            Assert.AreEqual("THÙNG VẬT TƯ", guide.Label, "Without a herb, the first stop is the supply crate.");

            interaction.Interact(points.Single(p => p.id == "tutorial_loot"));
            yield return WaitGameSeconds(.6f);
            Assert.AreEqual("HÙNG", guide.Label, "With the herb in the bag, the guide turns to Hùng.");
            Assert.Less(Vector3.Distance(guide.Target, mission.hung.position), .5f);

            // Nam starts at the base, so head for the base from where the rescue leaves him: by Hùng.
            var controller = mission.player.GetComponent<CharacterController>();
            controller.enabled = false; mission.player.position = mission.hung.position + Vector3.right; controller.enabled = true;
            quest.RestoreStage(Map01Quest.EscortStage);
            yield return WaitGameSeconds(.6f);
            var supplies = points.Single(p => p.kind == ForestPointKind.Supplies);
            Assert.AreEqual("CĂN CỨ CHỈ HUY", guide.Label);
            Assert.Less(Vector3.Distance(guide.Target, supplies.transform.position), .5f);
            Assert.Greater(guide.Route.Count, 2, "The base must be reachable on the NavMesh.");
            Assert.Less(Vector3.Distance(guide.Route[0], mission.player.position), 3f, "The route starts at Nam.");
            Assert.GreaterOrEqual(guide.Distance + .5f, Vector3.Distance(mission.player.position, supplies.transform.position),
                "A walking route is never shorter than the straight line.");
            Assert.Less(Vector3.Distance(guide.Route[guide.Route.Count - 1], supplies.transform.position), 4f,
                "The route must end at the base's floor, not on the roof above its supply point.");

            // Every report step points back at Hùng, wherever he stands at the base.
            foreach (int report in new[] { Map01Quest.BriefingStage, Map01Quest.ReportScoutStage, Map01Quest.ReportCampsStage })
            {
                quest.RestoreStage(report);
                yield return WaitGameSeconds(.6f);
                Assert.AreEqual("BÁO CÁO HÙNG", guide.Label, $"Stage {report} is a report to Hùng.");
                Assert.Less(Vector3.Distance(guide.Target, mission.hung.position), .5f);
            }

            quest.RestoreStage(Map01Quest.ScoutStage);
            yield return WaitGameSeconds(.6f);
            var scouting = mission.GetComponent<Map01Scouting>();
            Assert.AreEqual("KHU VỰC NGHI NGỜ", guide.Label, "Scouting points at a rough area, not a camp.");
            Assert.IsTrue(NearestZoneIs(scouting, guide.Target), "The rough area of a camp still to scout.");
            scouting.RestoreFound(0b111);
            yield return WaitGameSeconds(.6f);
            Assert.IsFalse(guide.HasTarget, "Nothing left to scout once every camp is logged.");

            quest.RestoreStage(Map01Quest.CampsStage);
            yield return WaitGameSeconds(.6f);
            var nearest = NearestOutpostGuard(mission);
            Assert.AreEqual("DOANH TRẠI ĐỊCH", guide.Label);
            // Guards and the commander move while the guide re-targets every half second — within 2 m.
            Assert.Less(Vector3.Distance(guide.Target, nearest.transform.position), 2f, "The nearest manned outpost comes first.");

            quest.RestoreStage(Map01Quest.BossStage);
            yield return WaitGameSeconds(.6f);
            var boss = mission.Enemies.Single(e => e.IsBoss);
            Assert.AreEqual("CHỈ HUY ĐỊCH", guide.Label);
            Assert.Less(Vector3.Distance(guide.Target, boss.transform.position), 2f);

            boss.GetComponent<Health>().TakeDamage(999999, boss.transform.position, null);
            yield return WaitGameSeconds(.6f);
            Assert.AreEqual("BÁO CÁO HÙNG", guide.Label, "The commander's death is reported to Hùng too.");

            quest.RestoreStage(Map01Quest.CompleteStage);
            yield return WaitGameSeconds(.6f);
            Assert.IsFalse(guide.HasTarget, "Map 1 is complete — nothing left to point at.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator HungOnlyFollowsDuringTheEscortThenStaysAtTheBase()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var interaction = mission.GetComponent<Map01PlayerInteraction>();
            var controller = mission.player.GetComponent<CharacterController>();
            var points = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None);
            foreach (var e in mission.Enemies) e.enabled = false;
            // Map01PlayerInteraction.Update, which drives Hùng, only runs with a keyboard present.
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                // Wounded: opening the bag or the map used to pull him over to Nam.
                Vector3 wounded = mission.hung.position;
                mission.SetInventoryOpen(true);
                yield return WaitGameSeconds(1f);
                mission.SetMapOpen(true);
                yield return WaitGameSeconds(1f);
                mission.CloseGameplayPanel();
                Assert.Less(Vector3.Distance(mission.hung.position, wounded), .5f, "Wounded Hùng stays where he fell.");

                // Escort: he walks home with Nam (who starts at the base).
                quest.RestoreStage(Map01Quest.EscortStage);
                yield return WaitGameSeconds(6f);
                Assert.Less(Vector3.Distance(mission.hung.position, mission.player.position), 6f, "During the escort Hùng follows Nam.");
                interaction.Interact(points.Single(p => p.kind == ForestPointKind.Supplies));
                Assert.AreEqual(Map01Quest.BriefingStage, quest.Stage, "Hùng made it home with Nam.");

                // Home: he stays put however far Nam goes, with the bag or map open or not.
                yield return WaitGameSeconds(.5f);
                Vector3 home = mission.hung.position;
                var crate = points.Single(p => p.id == "tutorial_loot");
                controller.enabled = false; mission.player.position = crate.transform.position; controller.enabled = true;
                quest.RestoreStage(Map01Quest.CampsStage);
                yield return WaitGameSeconds(2f);
                mission.SetMapOpen(true);
                yield return WaitGameSeconds(1f);
                mission.CloseGameplayPanel();
                Assert.Less(Vector3.Distance(mission.hung.position, home), 1f, "After the escort Hùng stays at the base.");

                // Reports happen in person: from afar nothing, next to him [E] files it and takes the next order.
                quest.RestoreStage(Map01Quest.ReportCampsStage);
                quest.TalkToHung();
                Assert.AreEqual(Map01Quest.ReportCampsStage, quest.Stage, "A report needs Nam standing next to Hùng.");
                controller.enabled = false; mission.player.position = home + Vector3.right * 1.5f; controller.enabled = true;
                yield return null;
                Assert.IsTrue(quest.HungInRange);
                Assert.AreEqual("[E] Báo cáo với Hùng", quest.HungPrompt);
                typeof(Map01PlayerInteraction).GetMethod("HandleInteractKey", Private).Invoke(interaction, null);
                Assert.AreEqual(Map01Quest.BossStage, quest.Stage, "[E] next to Hùng files the report and takes the next order.");
                Assert.IsTrue(quest.BossSpawned);
            }
            finally { InputSystem.RemoveDevice(keyboard); }
            yield return new ExitPlayMode();
        }

        [Test]
        public void OldCheckpointStagesMapOntoTheReportFlow()
        {
            // v4 stages were: rescue, escort, scout, camps, boss, complete.
            CollectionAssert.AreEqual(
                new[] { Map01Quest.RescueStage, Map01Quest.EscortStage, Map01Quest.ScoutStage, Map01Quest.CampsStage, Map01Quest.BossStage, Map01Quest.CompleteStage },
                new[] { 0, 1, 2, 3, 4, 5 }.Select(Map01Quest.FromV4Stage));
            Assert.AreEqual(Map01Quest.CompleteStage + 1, Map01Quest.Objectives.Length, "One objective line per stage.");
            Assert.AreEqual(Map01Quest.CompleteStage + 1, Map01Quest.SaveLocations.Length, "One save-slot location per stage.");
        }

        [UnityTest]
        public IEnumerator WeaponHotbarHidesOnlyItsOwnRowNotTheCrosshairCanvas()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null; yield return null;
            var hotbar = Object.FindFirstObjectByType<WeaponHotbar>();
            Assert.IsNotNull(hotbar, "Map 1's migrated HUD must include the weapon hotbar.");
            // HudBuilder always puts WeaponHotbar directly on the HUD canvas GameObject.
            var canvasRoot = hotbar.GetComponent<Canvas>().transform;
            Assert.IsNotNull(canvasRoot.Find("Crosshair"), "The HUD canvas must still have its crosshair.");
            var rootGroup = canvasRoot.GetComponent<CanvasGroup>();
            Assert.IsTrue(rootGroup == null || rootGroup.alpha > 0,
                "Hiding the redundant hotbar must not hide the whole HUD canvas (and the crosshair with it).");
            var rowGroup = (CanvasGroup)typeof(WeaponHotbar).GetField("hotbarGroup", Private).GetValue(hotbar);
            Assert.IsNotNull(rowGroup, "hotbarGroup must resolve, wired or by falling back to the row named \"Hotbar\".");
            Assert.AreEqual(0f, rowGroup.alpha, "The hotbar row itself is still hidden — Map 1's own HUD already covers it.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator NoiseIsLocalAndLootCannotBeDuplicated()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var inventory = mission.GetComponent<Map01Inventory>();
            var interaction = mission.GetComponent<Map01PlayerInteraction>();
            var guards = Object.FindObjectsByType<Map01EnemyController>(FindObjectsSortMode.None);
            Assert.IsNotEmpty(guards);
            mission.EmitNoise(mission.player.position, 7);
            Assert.IsTrue(guards.All(g => !g.Alerted));
            mission.EmitNoise(guards[0].transform.position, 8);
            Assert.IsTrue(guards[0].Alerted);
            var loot = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None).Single(p => p.id == "tutorial_loot");
            interaction.Interact(loot); int cloth = inventory.Count("cloth");
            interaction.Interact(loot); Assert.AreEqual(cloth, inventory.Count("cloth"));
            yield return new ExitPlayMode();
        }
    }
}
