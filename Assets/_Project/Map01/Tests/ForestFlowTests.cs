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

        // Lambdas stay out of the test iterators unless they capture nothing: a lambda capturing one of
        // their locals moves it into a closure created before EnterPlayMode, and the runner's resume
        // after the domain reload skips that creation — the closure is null and the test NREs. Such
        // lambdas go in static helpers (Holds, PackOf, LaidGun… below).

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

        /// <summary>
        /// The whole route, order by order: the commander's rescue briefing, Hùng freed once his four guards are down and
        /// walked home, the order for point 1, then (points 1 and 2 are driven by Map01OperationsTests) the radio report
        /// that sends Nam to point 3, the officer's fall and the retreat report that starts the extraction.
        /// </summary>
        [UnityTest]
        public IEnumerator CommandersOrdersLeadFromHungsRescueToTheRetreat()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var story = mission.GetComponent<Map01Story>();
            var rescue = mission.GetComponent<Map01Rescue>();
            var inventory = mission.GetComponent<Map01Inventory>();
            Assert.IsTrue(mission.IsInitialized, "The serialized mission content must resolve before guards start.");
            Assert.AreEqual(Map01Quest.RescueStage, quest.Stage);
            foreach (var e in mission.Enemies) e.enabled = false;

            // Nothing is Nam's job before the commander's briefing.
            Assert.IsFalse(quest.RescueOrderAccepted);
            StringAssert.Contains("nhận nhiệm vụ cứu Hùng", quest.ObjectiveText);
            yield return TakeRescueOrder(mission);
            Assert.AreEqual(Map01Quest.RescueStage, quest.Stage, "The order sends Nam out; Hùng is still held at the jetty.");

            // The jetty: Hùng is untied only once all four of his guards are down.
            rescue.MarkIntroSeen();
            PlaceNam(mission, mission.hung.position - mission.hung.forward * .7f);
            quest.TryRescueHung();
            Assert.AreEqual(Map01Rescue.Phase.Captive, rescue.CurrentPhase, "Four guards still stand over him.");
            foreach (var guard in rescue.Squad) guard.GetComponent<Health>().TakeDamage(9999, guard.transform.position, null);
            quest.TryRescueHung();
            for (float until = Time.time + 18; quest.Stage == Map01Quest.RescueStage && Time.time < until;) yield return null;
            Assert.AreEqual(Map01Quest.EscortStage, quest.Stage, "Untied and on his feet, Hùng goes home with Nam.");

            // The shelter: arriving ends the escort and hands over the supplies he carried.
            int supplies = inventory.Count("supplies");
            PlaceNam(mission, rescue.Layout.shelterDoor.position);
            yield return WaitGameSeconds(.6f);
            var home = mission.GetComponent<Map01RescueCinematic>();
            Assert.IsTrue(home != null && home.IsPlaying, "Reaching the shelter door plays the homecoming.");
            home.Skip();
            Assert.AreEqual(Map01Quest.BriefingStage, quest.Stage, "Home with Hùng: report to the commander.");
            Assert.AreEqual(supplies + 1, inventory.Count("supplies"));

            // The commander, in person, gives point 1.
            yield return null;
            PlaceNam(mission, story.Commander.position + story.Commander.forward * 1.8f);
            Assert.IsTrue(story.TryInteract());
            yield return ConfirmConversation(mission);
            Assert.AreEqual(Map01Quest.ScoutStage, quest.Stage, "The commander's next order is point 1, the supply store.");

            // From a save taken once point 2's radio is silenced: the report by radio sends Nam to point 3.
            quest.RestoreStoryStage(Map01Quest.ReportCampsStage, RadioSilenced());
            Assert.IsTrue(story.SafeToReport);
            Assert.IsTrue(story.TryInteract(), "The radio report is made from where Nam stands.");
            yield return ConfirmConversation(mission);
            Assert.AreEqual(Map01Quest.BossStage, quest.Stage);

            // Point 3: the officer has been in his headquarters all along; once reported, his fall is the finale.
            var officer = quest.CommanderEnemy;
            Assert.IsTrue(officer != null && officer.Alive && officer.IsBoss);
            quest.RestoreStoryStage(Map01Quest.BossStage, OfficerReported());
            Assert.AreSame(officer, quest.CommanderEnemy, "The boss stage brings no second commander.");
            officer.GetComponent<Health>().TakeDamage(999999, officer.transform.position, null);
            Assert.AreEqual(Map01Quest.ReportBossStage, quest.Stage);
            var finale = mission.GetComponent<Map01EndingCutscene>();
            Assert.IsTrue(finale != null && finale.IsPlaying, "The officer's fall plays as the finale.");
            finale.Skip();
            Assert.AreEqual(Map01Quest.ReportBossStage, quest.Stage, "After the finale Nam still has to report for the way out.");
            yield return null;
            Assert.IsTrue(story.TryInteract());
            yield return ConfirmConversation(mission);
            Assert.AreEqual(Map01Quest.ExtractionStage, quest.Stage, "The retreat report starts the extraction.");
            Assert.IsTrue(mission.GetComponentInChildren<Map01Extraction>().Prepared);
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator InteractKeyFreesHungWhenInRangeElseFallsBackToNearbyPoint()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var rescue = mission.GetComponent<Map01Rescue>();
            var interaction = mission.GetComponent<Map01PlayerInteraction>();
            var loot = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None).Single(p => p.id == "tutorial_loot");
            var press = typeof(Map01PlayerInteraction).GetMethod("HandleInteractKey", Private);
            foreach (var e in mission.Enemies) e.enabled = false;
            // A save from just after the commander's briefing: the rescue is on.
            quest.RestoreStoryStage(Map01Quest.RescueStage, RescueOrdered());
            rescue.MarkIntroSeen();

            // Map01PlayerInteraction.Update() (which computes Nearby) bails out with no Keyboard
            // device present, and batchmode Play Mode tests do not attach a real one.
            var testKeyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                // Stage 0, standing at a point that is not Hùng: [E] must still trigger it — stage 0 once
                // routed every [E] press into the rescue check alone.
                PlaceNam(mission, loot.transform.position);
                yield return null;
                press.Invoke(interaction, null);
                Assert.IsTrue(loot.used, "Standing at a point other than Hùng must still interact with it.");
                // Nam takes what the crate held with his hands: [E] waits for that to finish (Map01Story.TryInteract).
                for (float until = Time.time + 6; Map01NamActions.For(mission).Busy && Time.time < until;) yield return null;

                // At Hùng with his guards still standing: [E] says what is missing and frees nobody.
                PlaceNam(mission, mission.hung.position - mission.hung.forward * .7f);
                yield return null;
                Assert.IsTrue(quest.HungInRange);
                press.Invoke(interaction, null);
                Assert.AreEqual(Map01Rescue.Phase.Captive, rescue.CurrentPhase);
                StringAssert.Contains("Còn 4 tên", mission.Dialogue, "[E] near Hùng must say why he cannot be freed yet.");

                // His four guards down: the same [E] unties him rather than doing nothing.
                foreach (var guard in rescue.Squad) guard.GetComponent<Health>().TakeDamage(9999, guard.transform.position, null);
                yield return null;
                press.Invoke(interaction, null);
                Assert.AreEqual(Map01Rescue.Phase.Untying, rescue.CurrentPhase, "[E] near Hùng must free him, not silently do nothing.");
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
        public IEnumerator KillingAnEnemyDropsHisPackAndHisWornRifleOnce()
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

            // His pack, rolled from his kind's loot table (Map01Loot.json): his spare rounds at least, nothing off the table.
            Assert.GreaterOrEqual(Holds(drop, "ammo_rifle"), SpareRounds(guard), "A guard always carries his spare rounds.");
            Assert.IsEmpty(OffTable(drop, guard), "Everything in the pack comes from his loot table.");
            var pack = PackOf(drop);
            var before = Counts(inventory, pack);
            interaction.Interact(drop);
            for (int i = 0; i < pack.Length; i++) Assert.AreEqual(before[i] + pack[i].count, inventory.Count(pack[i].item), pack[i].item);

            // His rifle lies beside him, worn within his kind's range, and he holds it no more.
            var gun = LaidGun(mission, guard);
            Assert.IsNotNull(gun, "A fallen guard drops his rifle.");
            var kind = Map01Loot.Load().EnemyOf(Map01Logistics.KindOf(guard));
            Assert.AreEqual(kind.weapon, gun.items[0].item_id);
            Assert.That(inventory.Fraction(kind.weapon, gun.items[0].condition), Is.InRange(kind.dropMin - 1e-4f, kind.dropMax + 1e-4f));
            Assert.Less(Vector3.Distance(gun.transform.position, guard.transform.position), 2f, "The rifle lies where he fell.");
            Assert.IsFalse(HoldsRifle(guard), "The rifle left his hands.");
            int rifles = inventory.Count(kind.weapon);
            interaction.Interact(gun);
            Assert.AreEqual(rifles + 1, inventory.Count(kind.weapon), "The captured rifle goes into the bag.");
            Assert.IsTrue(gun.used);

            var after = Counts(inventory, pack);
            interaction.Interact(drop);
            yield return WaitGameSeconds(.2f);
            interaction.Interact(drop);
            CollectionAssert.AreEqual(after, Counts(inventory, pack), "A corpse is searched once — it never restocks.");
            yield return new ExitPlayMode();
        }

        private static int Holds(ForestPoint point, string item) => point.items.Where(i => i.item_id == item).Sum(i => i.count);
        private static (string item, int count)[] PackOf(ForestPoint point) => point.items.Where(i => i.count > 0).Select(i => (i.item_id, i.count)).ToArray();
        private static int[] Counts(Map01Inventory inventory, (string item, int count)[] pack) => pack.Select(p => inventory.Count(p.item)).ToArray();
        private static Map01Loot.Table TableOf(Map01EnemyController guard) => Map01Loot.Load().TableOf(Map01Loot.Load().EnemyOf(Map01Logistics.KindOf(guard)).lootTable);
        private static int SpareRounds(Map01EnemyController guard) => TableOf(guard).entries.Where(e => e.tier == 5 && e.item == "ammo_rifle").Sum(e => e.min);
        /// <summary>Items in a guard's pack his table could not give (a dressing may come from the pity rule; the key from the keeper).</summary>
        private static string[] OffTable(ForestPoint pack, Map01EnemyController guard) =>
            pack.items.Select(i => i.item_id).Where(id => id != "medkit_small" && id != Map01Story.KeyItem && TableOf(guard).entries.All(e => e.item != id)).ToArray();
        private static ForestPoint LaidGun(Map01Mission mission, Map01EnemyController guard) =>
            Map01Drops.For(mission).LaidGuns.FirstOrDefault(p => p.id == Map01Logistics.GunPointPrefix + guard.LegacyId);
        private static bool HoldsRifle(Map01EnemyController guard)
        {
            var rifle = guard.GetComponentInChildren<Map01Rifle>(true);
            return rifle != null && rifle.weapon != null && rifle.weapon.gameObject.activeSelf;
        }

        [UnityTest]
        public IEnumerator BaseCrateGivesItsKitOnceAndTheShelfOpensTheStoreOnceHungIsHome()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var inventory = mission.GetComponent<Map01Inventory>();
            var interaction = mission.GetComponent<Map01PlayerInteraction>();
            var hud = mission.GetComponent<Map01Hud>();
            var logistics = Map01Logistics.For(mission);
            var points = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None);
            var crate = points.Single(p => p.id == "tutorial_loot");
            var supplies = points.Single(p => p.id == "supplies");
            Assert.AreEqual(0, crate.restockSeconds, "The starting crate gives its kit once: the base no longer refills itself.");
            Assert.AreEqual(0, supplies.restockSeconds);

            interaction.Interact(crate);
            interaction.Interact(crate);
            Assert.AreEqual(60, inventory.Count("ammo_rifle"), "An emptied crate must not pay out again.");
            Assert.AreEqual(2, inventory.Count("medkit_small"));
            yield return WaitGameSeconds(.3f);
            Assert.IsTrue(crate.used, "It never refills.");

            // The supply shelf is the base store, shut until Hùng is home.
            Teleport(mission, supplies.transform.position + Vector3.right * 1.2f);
            interaction.Interact(supplies);
            Assert.IsFalse(mission.BenchOpen, "No store before Hùng is brought home.");
            Assert.AreEqual(0, logistics.Credit);
            quest.RestoreStage(Map01Quest.BriefingStage);
            yield return null;
            Assert.AreEqual(Map01Loot.Load().logistics.operationCredit[0], logistics.Credit, "Hùng home: the command sends its first points.");
            interaction.Interact(supplies);
            Assert.IsTrue(mission.BenchOpen);
            Assert.AreEqual("store", hud.BenchTab, "The shelf opens the bench screen at the store.");
            Assert.IsFalse(supplies.used, "The shelf stays in reach.");

            // Points buy what the store issues at its base value; short of points, nothing is given.
            int credit = logistics.Credit, ammo = inventory.Count("ammo_rifle");
            var rounds = logistics.Stock.First(o => o.item == "ammo_rifle");
            Assert.AreEqual(Map01Loot.Load().BaseValue("ammo_rifle") * rounds.count, rounds.price);
            Assert.IsTrue(logistics.Issue(rounds));
            Assert.AreEqual(ammo + rounds.count, inventory.Count("ammo_rifle"));
            Assert.AreEqual(credit - rounds.price, logistics.Credit);
            var dear = logistics.Stock.OrderByDescending(o => o.price).First();
            while (logistics.Credit >= dear.price) logistics.Issue(dear);
            int left = logistics.Credit, dearHeld = inventory.Count(dear.item);
            Assert.IsFalse(logistics.Issue(dear), "Short of points: refused.");
            Assert.AreEqual(left, logistics.Credit); Assert.AreEqual(dearHeld, inventory.Count(dear.item));

            // A captured gun handed in: its base value, less as it is worn. The rifle in hand never goes.
            inventory.Draw(Map01Inventory.Slot.Primary);
            Assert.IsFalse(logistics.HandIn(Map01Inventory.Gun.InSlot(Map01Inventory.Slot.Primary)), "The rifle in hand is never handed in.");
            inventory.Receive("rifle_standard", 1, .25f * inventory.MaxCondition("rifle_standard"));
            var captured = inventory.Guns().First(g => g.Slot == null);
            int worth = inventory.TurnInValue(captured);
            Assert.AreEqual(Mathf.RoundToInt(Map01Loot.Load().BaseValue("rifle_standard") * (Map01Loot.Load().logistics.turnInFloor + (1 - Map01Loot.Load().logistics.turnInFloor) * .25f)), worth);
            Assert.IsTrue(logistics.HandIn(captured));
            Assert.AreEqual(left + worth, logistics.Credit);
            Assert.AreEqual(1, logistics.HandedIn);
            Assert.AreEqual(1, inventory.Count("rifle_standard"));
            mission.CloseGameplayPanel();
            yield return new ExitPlayMode();
        }

        private static void Teleport(Map01Mission mission, Vector3 to)
        {
            var controller = mission.player.GetComponent<CharacterController>();
            controller.enabled = false; mission.player.position = to; controller.enabled = true;
            Physics.SyncTransforms();
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
            // Map01PlayerInteraction, which releases SuppressFire, reads nothing without a
            // keyboard — and batch mode has none of its own.
            var keyboard = InputSystem.AddDevice<Keyboard>();
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
                // Shots come out of the magazine, the pack only refills it on a reload: count both.
                int rounds = combat.RoundsInMagazine + inventory.Count("ammo_rifle");
                var centre = new Vector2(Screen.width / 2f, Screen.height / 2f);
                InputSystem.QueueStateEvent(mouse, new MouseState { position = centre }.WithButton(MouseButton.Left)); InputSystem.Update();
                for (int i = 0; i < 5; i++) yield return null;
                InputSystem.QueueStateEvent(mouse, new MouseState { position = centre }); InputSystem.Update();
                Assert.Less(combat.RoundsInMagazine + inventory.Count("ammo_rifle"), rounds, "Holding fire with the rifle equipped and ammo on hand must shoot.");
            }
            finally { InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing); }
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
            foreach (var enemy in mission.Enemies.Where(e => !e.IsBoss)) // The commander, there from the start, is checked below.
            {
                Assert.AreEqual(expected, enemy.DamagePerShot, 1e-4f, enemy.name);
                Assert.AreEqual(mission.Settings.guardShotInterval, enemy.FireInterval, 1e-4f, enemy.name);
            }

            // Exercise both authored rifle loadouts with a real hit and a same-frame cooldown check.
            foreach (var enemy in mission.Enemies) enemy.enabled = false;
            foreach (var rifle in new[] { "rifle_standard", Map01M16.Id })
            {
                var guard = mission.Enemies.First(e => e.Alive && !e.IsBoss && e.RifleItemId == rifle);
                typeof(Map01EnemyController).GetField("obstructionMask", Private).SetValue(guard, (LayerMask)0);
                typeof(Map01EnemyController).GetField("_nextShot", Private).SetValue(guard, 0f);
                var accuracy = guard.Accuracy;
                guard.Accuracy = new ShadowVale.Gameplay.Enemies.ShotAccuracyProfile { enabled = false };
                float before = playerHealth.Current;
                float roundDamage = expected * (rifle == Map01M16.Id ? Map01M16.DamageRelativeToAk : 1f);
                guard.FireAtPlayer();
                Assert.AreEqual(before - roundDamage, playerHealth.Current, 1e-3f, rifle);
                guard.FireAtPlayer();
                Assert.AreEqual(before - roundDamage, playerHealth.Current, 1e-3f, rifle + " must respect its shot interval.");
                guard.Accuracy = accuracy;
            }
            // The commander keeps his multiplier on top of the baseline rather than being reset by it.
            // He is in his headquarters from the start (Map01C3Mission); the boss stage brings no second one.
            quest.RestoreStage(Map01Quest.BossStage);
            yield return null;
            var boss = mission.Enemies.Single(e => e.IsBoss);
            Assert.AreEqual(new ShadowVale.Data.Content.BossProfile().DamagePerRound, boss.DamagePerShot, 1e-3f,
                "His own damage a round (11.5), not a guard's times a multiplier.");
            Assert.Greater(boss.DamagePerShot, expected);
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
                // Point 3 with the officer already reported: the fight is on.
                quest.RestoreStoryStage(Map01Quest.BossStage, OfficerReported());
                Assert.AreEqual(Map01Quest.BossStage, quest.Stage);
                var bossHealth = mission.Enemies.Single(e => e.IsBoss).GetComponent<Health>();
                // A quarter of his health: short of the half at which he calls his men (Map01C3Mission).
                bossHealth.TakeDamage(bossHealth.Max * .25f, bossHealth.transform.position, null);
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

        /// <summary>The objective guide follows the commander's orders step by step (Map01Story.Target, then the rescue).</summary>
        [UnityTest]
        public IEnumerator ObjectiveGuideFollowsEachQuestStep()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var story = mission.GetComponent<Map01Story>();
            var rescue = mission.GetComponent<Map01Rescue>();
            var c3 = mission.GetComponent<Map01C3Mission>();
            var interaction = mission.GetComponent<Map01PlayerInteraction>();
            var guide = mission.GetComponent<Map01ObjectiveGuide>();
            Assert.IsNotNull(guide, "The HUD must bring the objective guide along.");
            // Frozen guards cannot wound Nam mid-test (a dead Nam stops the mission, hiding the guide).
            foreach (var e in mission.Enemies) e.enabled = false;

            // First the commander's briefing, then Hùng at the jetty.
            yield return WaitGameSeconds(.6f);
            Assert.IsTrue(guide.HasTarget);
            Assert.AreEqual("CHỈ HUY — NHẬN NHIỆM VỤ", guide.Label, "The game starts with the commander's briefing.");
            Assert.Less(Vector3.Distance(guide.Target, story.Commander.position), .5f);
            quest.RestoreStoryStage(Map01Quest.RescueStage, RescueOrdered());
            yield return WaitGameSeconds(.6f);
            Assert.AreEqual("BẾN TÀU — CỨU HÙNG", guide.Label, "Ordered out, the guide turns to Hùng at the jetty.");
            Assert.Less(Vector3.Distance(guide.Target, mission.hung.position), .5f);

            // From the jetty, home to the shelter on foot.
            rescue.MarkIntroSeen();
            PlaceNam(mission, mission.hung.position + Vector3.right);
            quest.RestoreStage(Map01Quest.EscortStage);
            yield return WaitGameSeconds(.6f);
            var door = rescue.Layout.shelterDoor.position;
            Assert.AreEqual("HẦM TRÚ ẨN — ĐƯA HÙNG VỀ", guide.Label);
            Assert.Less(Vector3.Distance(guide.Target, door), .5f);
            Assert.Greater(guide.Route.Count, 2, "The shelter must be reachable on the NavMesh.");
            Assert.Less(Vector3.Distance(guide.Route[0], mission.player.position), 3f, "The route starts at Nam.");
            Assert.GreaterOrEqual(guide.Distance + .5f, Vector3.Distance(mission.player.position, door),
                "A walking route is never shorter than the straight line.");
            Assert.Less(Vector3.Distance(guide.Route[guide.Route.Count - 1], door), 4f, "The route ends at the shelter door.");

            // Home: the commander's next order, then point 1's store.
            quest.RestoreStage(Map01Quest.BriefingStage);
            yield return WaitGameSeconds(.6f);
            Assert.AreEqual("CHỈ HUY — NHẬN NHIỆM VỤ", guide.Label);
            Assert.Less(Vector3.Distance(guide.Target, story.Commander.position), .5f);
            quest.RestoreStage(Map01Quest.ScoutStage);
            yield return WaitGameSeconds(.6f);
            Assert.AreEqual("ĐIỂM 1 — KHO TIẾP TẾ", guide.Label, "Point 1 while its guards stand: the store's door.");
            Assert.Less(Vector3.Distance(guide.Target, story.Layout.doorApproach.position), .5f);

            // The supplies taken: first the radio report from wherever Nam is, then the hand-over at the base.
            quest.RestoreStage(Map01Quest.ReportScoutStage);
            yield return WaitGameSeconds(.6f);
            Assert.AreEqual("TÌM CHỖ KÍN — BÁO CÁO [E]", guide.Label, "A radio report is made where Nam stands.");
            Assert.Less(Vector3.Distance(guide.Target, mission.player.position), .5f);
            quest.RestoreStoryStage(Map01Quest.ReportScoutStage, new Map01Story.Snapshot {
                rescueAccepted = true, supplies = true, keyCollected = true, doorUnlocked = true, chestUnlocked = true, supplyReported = true });
            yield return WaitGameSeconds(.6f);
            Assert.AreEqual("HẦM — BÀN GIAO TIẾP TẾ", guide.Label, "Reported: carry the supplies to the commander.");
            Assert.Less(Vector3.Distance(guide.Target, story.Commander.position), .5f);

            // Point 2's radio, then its report.
            quest.RestoreStage(Map01Quest.CampsStage);
            yield return WaitGameSeconds(.6f);
            Assert.AreEqual("ĐIỂM 2 — BẢNG MẠCH ĐÀI", guide.Label);
            Assert.Less(Vector3.Distance(guide.Target, story.Layout.radioApproach.position), .5f);
            quest.RestoreStage(Map01Quest.ReportCampsStage);
            yield return WaitGameSeconds(.6f);
            Assert.AreEqual("TÌM CHỖ KÍN — BÁO CÁO [E]", guide.Label);

            // Point 3: the scouts' Mosin first, then the observation, then the officer himself.
            quest.RestoreStage(Map01Quest.BossStage);
            yield return WaitGameSeconds(.6f);
            Assert.AreEqual("ĐỒI ĐÔNG NAM — MOSIN TRINH SÁT", guide.Label, "The order points out the scouts' rifle first.");
            Assert.Less(Vector3.Distance(guide.Target, c3.CachePoint.transform.position), .5f);
            interaction.Interact(c3.CachePoint);
            yield return WaitGameSeconds(.6f);
            var field = mission.GetComponent<Map01FieldSupplies>().ApproachPoint;
            Assert.AreEqual("ĐỒI ĐÔNG NAM — BĂNG, KHÓI, LỰU ĐẠN", guide.Label);
            Assert.Less(Vector3.Distance(guide.Target, field.transform.position), .5f);
            interaction.Interact(field); Map01NamActions.For(mission).CancelAction();
            yield return WaitGameSeconds(.6f);
            Assert.AreEqual("ĐIỂM 3 — QUAN SÁT DOANH TRẠI [F]", guide.Label);
            Assert.Less(Vector3.Distance(guide.Target, story.Layout.commanderPost.position), .5f);
            quest.RestoreStoryStage(Map01Quest.BossStage, OfficerReported());
            yield return WaitGameSeconds(.6f);
            var officer = quest.CommanderEnemy;
            Assert.AreEqual("ĐIỂM 3 — HẠ SĨ QUAN", guide.Label, "Reported, the guide follows the officer himself.");
            // He walks his rounds while the guide re-targets every half second — within 2 m.
            Assert.Less(Vector3.Distance(guide.Target, officer.transform.position), 2f);

            // His fall plays the finale; then the radio report for the way out, and the boat.
            officer.GetComponent<Health>().TakeDamage(999999, officer.transform.position, null);
            mission.GetComponent<Map01EndingCutscene>().Skip();
            yield return WaitGameSeconds(.6f);
            Assert.AreEqual(Map01Quest.ReportBossStage, quest.Stage);
            Assert.AreEqual("TÌM CHỖ KÍN — BÁO CÁO [E]", guide.Label, "After the finale Nam reports by radio for the way out.");
            // This fixture freezes guards to inspect guide targets. The C3 death response
            // still marks them engaged; end that simulated fight before making the safe radio call.
            c3.enabled = false;
            Map01BodyDiscovery.For(mission).enabled = false;
            foreach (var enemy in mission.Enemies.Where(e => e != null && e.Alive)) {
                enemy.ReturnToPost(); enemy.enabled = false;
            }
            // Earlier guide probes only selected distant targets; Nam is still at the jetty.
            // Report from C3 so extraction does not immediately advance into auto-boarding.
            PlaceNam(mission, field.transform.position);
            Assert.IsTrue(story.SafeToReport);
            Assert.IsTrue(story.TryInteract());
            yield return ConfirmConversation(mission);
            Assert.AreEqual(Map01Quest.ExtractionStage, quest.Stage, "The radio report opens the route to the boat.");
            yield return WaitGameSeconds(.6f);
            Assert.AreEqual("NORTHERN JETTY", guide.Label, "The retreat leads to the waiting boat.");

            quest.RestoreStage(Map01Quest.CompleteStage);
            yield return WaitGameSeconds(.6f);
            Assert.IsFalse(guide.HasTarget, "Map 1 is complete — nothing left to point at.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator HungHeadsHomeIndependentlyThenStaysAtTheBase()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var quest = mission.GetComponent<Map01Quest>();
            var story = mission.GetComponent<Map01Story>();
            var rescue = mission.GetComponent<Map01Rescue>();
            var points = Object.FindObjectsByType<ForestPoint>(FindObjectsSortMode.None);
            foreach (var e in mission.Enemies) e.enabled = false;
            // Input is present, but Hùng's world movement must not target Nam.
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                // Tied up at the jetty: opening the bag or the map used to pull him over to Nam.
                Vector3 held = mission.hung.position;
                mission.SetInventoryOpen(true);
                yield return WaitGameSeconds(1f);
                mission.SetMapOpen(true);
                yield return WaitGameSeconds(1f);
                mission.CloseGameplayPanel();
                Assert.Less(Vector3.Distance(mission.hung.position, held), .5f, "Held at the jetty, Hùng stays where he is tied.");

                // Escort: freed at the jetty, he independently takes the route to the shelter.
                Vector3 door = rescue.Layout.shelterDoor.position;
                // A few steps down the road home — the way Nam leads him from the jetty to the shelter.
                var road = new UnityEngine.AI.NavMeshPath();
                Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(held, out var from, 2f, UnityEngine.AI.NavMesh.AllAreas));
                Assert.IsTrue(UnityEngine.AI.NavMesh.SamplePosition(door, out var to, 2f, UnityEngine.AI.NavMesh.AllAreas));
                Assert.IsTrue(UnityEngine.AI.NavMesh.CalculatePath(from.position, to.position, UnityEngine.AI.NavMesh.AllAreas, road));
                Vector3 beside = road.corners[road.corners.Length - 1];
                float walked = 0;
                for (int i = 1; i < road.corners.Length; i++)
                {
                    float leg = Vector3.Distance(road.corners[i - 1], road.corners[i]);
                    if (walked + leg >= 9f) { beside = Vector3.Lerp(road.corners[i - 1], road.corners[i], (9f - walked) / leg); break; }
                    walked += leg;
                }
                rescue.MarkIntroSeen();
                PlaceNam(mission, beside);
                quest.RestoreStage(Map01Quest.EscortStage);
                float initialDistance=rescue.RemainingHomeDistance();
                // He first gets up off his knees, then limps home independently.
                yield return WaitGameSeconds(8f);
                var hungAgent = mission.hung.GetComponent<UnityEngine.AI.NavMeshAgent>();
                Assert.Less(Vector3.Distance(hungAgent.destination, to.position), 1f, "The destination is the shelter, never Nam.");
                Assert.Less(rescue.RemainingHomeDistance(),initialDistance-1f,"Hùng advances toward base without waiting for Nam.");
                Assert.AreEqual(Map01HungVisual.LimpSpeed, hungAgent.speed, .01f, "He limps home at his own pace, never sprints.");
                PlaceNam(mission,held);
                float beforeCovering=rescue.RemainingHomeDistance();
                yield return WaitGameSeconds(2f);
                Assert.Less(rescue.RemainingHomeDistance(),beforeCovering-1f,"Nam falling behind must not pull Hùng back.");

                // ...all the way to the shelter (the walk itself is the NavMesh's business).
                hungAgent.Warp(door + Vector3.right * 2f);
                PlaceNam(mission, door);
                yield return WaitGameSeconds(.6f);
                var homecoming = mission.GetComponent<Map01RescueCinematic>();
                Assert.IsTrue(homecoming != null && homecoming.IsPlaying, "Reaching the shelter door plays the homecoming.");
                homecoming.Skip();
                Assert.AreEqual(Map01Quest.BriefingStage, quest.Stage, "Hùng made it home with Nam.");

                // Home: he stays put however far Nam goes, with the bag or map open or not.
                yield return WaitGameSeconds(.5f);
                Vector3 home = mission.hung.position;
                var crate = points.Single(p => p.id == "tutorial_loot");
                PlaceNam(mission, crate.transform.position);
                quest.RestoreStage(Map01Quest.CampsStage);
                yield return WaitGameSeconds(2f);
                mission.SetMapOpen(true);
                yield return WaitGameSeconds(1f);
                mission.CloseGameplayPanel();
                Assert.Less(Vector3.Distance(mission.hung.position, home), 1f, "After the escort Hùng stays at the base.");

                // Reports go to the commander over the radio now, not to Hùng in person.
                quest.RestoreStage(Map01Quest.ReportCampsStage);
                PlaceNam(mission, home + Vector3.right * 1.5f);
                yield return null;
                Assert.IsFalse(quest.HungInRange, "Hùng no longer takes reports.");
                quest.TalkToHung();
                Assert.AreEqual(Map01Quest.ReportCampsStage, quest.Stage, "Talking to Hùng files nothing.");
                Assert.IsTrue(story.TryInteract(), "[E] calls the commander on the radio.");
                yield return ConfirmConversation(mission);
                Assert.AreEqual(Map01Quest.BossStage, quest.Stage, "The radio report takes the next order.");
            }
            finally { InputSystem.RemoveDevice(keyboard); }
            yield return new ExitPlayMode();
        }

        [Test]
        public void PlayIsCappedAtSixtyFramesASecond()
        {
            // Uncapped, a light scene still ran the machine at full power drawing frames nobody sees.
            // Checked directly rather than in Play: the runner re-runs SetUp after entering Play,
            // and ForestSceneTestBase's SetUp takes the cap off again for the other tests.
            var apply = typeof(FramePacing).GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic);
            var startup = apply.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.IsNotNull(startup, "The cap is applied as the game starts.");
            Assert.AreEqual(RuntimeInitializeLoadType.BeforeSceneLoad, startup.loadType);
            UnityEditor.SessionState.EraseBool(FramePacing.UncappedKey);
            try
            {
                apply.Invoke(null, null);
                Assert.AreEqual(FramePacing.TargetFps, Application.targetFrameRate);
            }
            finally { Application.targetFrameRate = -1; }
        }

        [Test]
        public void EveryStageHasAnObjectiveAndASaveLocation()
        {
            Assert.AreEqual(Map01Quest.LastStage + 1, Map01Quest.Objectives.Length, "One objective line per stage.");
            Assert.AreEqual(Map01Quest.LastStage + 1, Map01Quest.SaveLocations.Length, "One save-slot location per stage.");
        }

        [UnityTest]
        public IEnumerator WeaponHotbarHidesOnlyItsOwnRowNotTheCrosshairCanvas()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null; yield return null;
            var hotbar = Object.FindFirstObjectByType<WeaponHotbar>();
            Assert.IsNotNull(hotbar, "Map 1's migrated HUD must include the weapon hotbar.");
            // Map 1's HUD has WeaponHotbar directly on the HUD canvas GameObject.
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
