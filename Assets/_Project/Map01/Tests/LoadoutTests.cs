using System.Collections;
using System.Linq;
using NUnit.Framework;
using ShadowVale.Gameplay.Combat;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using Slot = ShadowVale.Map01.Map01Inventory.Slot;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// The approved loadout (Map01Inventory.Bag, Map01Hud.Bag, Map01Drops, Map01Smoke, Map01StoneThrow): the 24-cell
    /// bag and its moves, the weapon slots with no bare hands, putting things on the ground and picking them up
    /// again (also across a save), and the two new throwables.
    /// </summary>
    public sealed class LoadoutTests : ForestSceneTestBase
    {
        private static IEnumerator Press(Keyboard keyboard, Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private static int CellOf(Map01Inventory inventory, string id)
        {
            for (int i = 0; i < Map01Inventory.BagSize; i++) if (inventory.Cell(i)?.id == id) return i;
            return -1;
        }
        // Lambdas that capture an iterator local break after EnterPlayMode's domain reload: these stay static.
        private static int FirstEmpty(Map01Inventory inventory) { for (int i = 0; i < Map01Inventory.BagSize; i++) if (inventory.Cell(i) == null) return i; return -1; }
        private static int Stacks(Map01Inventory inventory, string id) { int n = 0; for (int i = 0; i < Map01Inventory.BagSize; i++) if (inventory.Cell(i)?.id == id) n++; return n; }
        private static int LastCellOf(Map01Inventory inventory, string id) { for (int i = Map01Inventory.BagSize - 1; i >= 0; i--) if (inventory.Cell(i)?.id == id) return i; return -1; }
        private static int OtherCellOf(Map01Inventory inventory, string id, int not) { for (int i = 0; i < Map01Inventory.BagSize; i++) if (i != not && inventory.Cell(i)?.id == id) return i; return -1; }

        [UnityTest]
        public IEnumerator BagMovesSwapsMergesSplitsAndSorts()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var inventory = mission.GetComponent<Map01Inventory>();
            Assert.AreEqual("rifle_standard", inventory.SlotItem(Slot.Primary), "The AK starts in slot 1.");
            Assert.AreEqual("knife", inventory.SlotItem(Slot.Knife), "The knife starts in slot 3.");
            Assert.AreEqual(-1, CellOf(inventory, "rifle_standard"), "What a slot holds is not in the bag.");
            inventory.Add("cloth", 7); inventory.Add("herb", 3); inventory.Add("ammo_rifle", 100);
            int cloth = CellOf(inventory, "cloth"), herb = CellOf(inventory, "herb");
            Assert.AreEqual(new[] { 90, 10 }, Enumerable.Range(0, Map01Inventory.BagSize).Select(inventory.Cell).Where(c => c?.id == "ammo_rifle").Select(c => c.count).ToArray(), "Stacks fill to their size (90 rounds).");

            int empty = FirstEmpty(inventory);
            Assert.IsTrue(inventory.Move(cloth, empty));
            Assert.AreEqual("cloth", inventory.Cell(empty).id, "Onto an empty cell: it moves there.");
            Assert.IsNull(inventory.Cell(cloth));
            Assert.IsTrue(inventory.Move(empty, herb));
            Assert.AreEqual("cloth", inventory.Cell(herb).id, "Onto another item: they swap…");
            Assert.AreEqual("herb", inventory.Cell(empty).id, "…both ways.");

            Assert.IsTrue(inventory.Split(herb));
            Assert.AreEqual(2, Stacks(inventory, "cloth"), "Split makes two stacks.");
            Assert.AreEqual(7, inventory.Count("cloth"), "Splitting loses nothing.");
            int second = OtherCellOf(inventory, "cloth", herb);
            Assert.IsTrue(inventory.Move(second, herb));
            Assert.AreEqual(7, inventory.Cell(herb).count, "Onto the same item: they merge.");

            inventory.Sort();
            var order = Enumerable.Range(0, Map01Inventory.BagSize).Select(inventory.Cell).Where(c => c != null).Select(c => c.id).ToList();
            Assert.Less(order.IndexOf("ammo_rifle"), order.IndexOf("stone"), "Sorted: rounds before throwables…");
            Assert.Less(order.IndexOf("stone"), order.IndexOf("herb"), "…throwables before healing…");
            Assert.Less(order.IndexOf("herb"), order.IndexOf("cloth"), "…healing before materials.");
            Assert.IsNotNull(inventory.Cell(0), "Sorted stacks start in the first cell.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator WeaponSlotsKeepSomethingInHandAlways()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var inventory = mission.GetComponent<Map01Inventory>();
            foreach (var e in mission.Enemies) e.enabled = false;
            var routing = RouteInputToGame();
            var keyboard = InputSystem.AddDevice<Keyboard>();
            try
            {
                Assert.AreEqual("pistol_k54", inventory.SlotItem(Slot.Pistol), "Nam starts with his K-54 in slot 2.");
                Assert.AreEqual(mission.Settings.startingPistolRounds, inventory.Count("ammo_pistol"), "…and two magazines for it.");
                inventory.Receive("rifle_mosin", 1);
                inventory.Receive("pistol_k54", 1);
                Assert.GreaterOrEqual(CellOf(inventory, "pistol_k54"), 0, "A second pistol found goes into the bag.");
                int mosin = CellOf(inventory, "rifle_mosin");
                Assert.GreaterOrEqual(mosin, 0, "A second long gun goes into the bag.");
                Assert.IsTrue(inventory.IsEquipped("rifle_standard"));

                // Dragging the Mosin onto slot 1: the AK takes its cell, and the knife is drawn (no bare hands).
                Assert.IsTrue(inventory.EquipFromCell(mosin));
                Assert.AreEqual("rifle_mosin", inventory.SlotItem(Slot.Primary));
                Assert.AreEqual("rifle_standard", inventory.Cell(mosin).id, "The AK goes where the Mosin was.");
                Assert.AreEqual(WeaponKind.Knife, mission.ModernCombat.EquippedKind, "The rifle in hand left its slot: the knife comes out.");

                // The Mosin and the K-54 have no animations yet: their keys say so and keep the knife in hand.
                yield return Press(keyboard, Key.Digit1);
                Assert.AreEqual(WeaponKind.Knife, mission.ModernCombat.EquippedKind);
                StringAssert.Contains("chưa có hoạt ảnh", mission.Dialogue);
                yield return Press(keyboard, Key.Digit2);
                Assert.AreEqual(WeaponKind.Knife, mission.ModernCombat.EquippedKind);

                // Back to the AK, through the bag: then 1 draws it again.
                Assert.IsTrue(inventory.EquipFromCell(CellOf(inventory, "rifle_standard")));
                yield return Press(keyboard, Key.Digit1);
                Assert.IsTrue(inventory.IsEquipped("rifle_standard"));

                // The knife never leaves: not into the bag, not onto the ground.
                Assert.IsFalse(inventory.Unequip(Slot.Knife));
                Assert.IsFalse(inventory.DropSlot(Slot.Knife));
                // Dropping the AK in hand leaves the knife in hand.
                Assert.IsTrue(inventory.DropSlot(Slot.Primary));
                Assert.IsNull(inventory.SlotItem(Slot.Primary));
                Assert.AreEqual(WeaponKind.Knife, mission.ModernCombat.EquippedKind);
                yield return Press(keyboard, Key.Digit1);
                Assert.AreEqual(WeaponKind.Knife, mission.ModernCombat.EquippedKind, "An empty slot 1 draws nothing.");
            }
            finally { InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing); }
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator DroppedThingsLieOnTheGroundAndComeBackWithASave()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var inventory = mission.GetComponent<Map01Inventory>();
            var drops = Map01Drops.For(mission);
            inventory.Add("cloth", 6); inventory.Add("supplies", 1);
            int cloth = CellOf(inventory, "cloth");
            Assert.IsTrue(inventory.Drop(cloth, 4));
            Assert.AreEqual(2, inventory.Count("cloth"), "Part of a stack can be put down.");
            Assert.IsFalse(inventory.Drop(CellOf(inventory, "supplies"), 1), "Mission things cannot be dropped.");
            var pile = drops.Piles.Single();
            Assert.AreEqual(("cloth", 4), (pile.id, pile.count));
            Assert.Less(Vector3.Distance(pile.position, mission.player.position), 2f, "It lies at Nam's feet.");
            var point = mission.Interactables.Single(p => p.id != null && p.id.StartsWith(Map01Drops.IdPrefix));
            Assert.IsNotNull(point.GetComponentInChildren<Renderer>(), "It is shown in the world.");

            // A save keeps it lying there and the bag as arranged (saved to a slot of the test's own, then loaded).
            int moved = Map01Inventory.BagSize - 1;
            Assert.IsTrue(inventory.Move(CellOf(inventory, "cloth"), moved));
            var rootField = typeof(ForestSaveSlots).GetField("storageRoot", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            rootField.SetValue(null, System.IO.Path.Combine(Application.temporaryCachePath, "loadout-test-" + System.Guid.NewGuid().ToString("N")));
            Assert.IsTrue(mission.GetComponent<Map01SaveSystem>().SaveSlot(1, out var error), error);
            Map01SaveSystem.BeginGame(1);
            yield return null;
            var pending = typeof(Map01SaveSystem).GetField("pendingCheckpoint", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (pending.GetValue(null) != null && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.IsNull(pending.GetValue(null), "The save loads.");
            mission = Object.FindFirstObjectByType<Map01Mission>();
            inventory = mission.GetComponent<Map01Inventory>();
            drops = Map01Drops.For(mission);
            Assert.AreEqual(1, drops.Piles.Count(), "The pile is still on the ground after loading.");
            Assert.AreEqual("cloth", inventory.Cell(moved)?.id, "The bag comes back as it was arranged.");
            Assert.AreEqual(0, inventory.Count("supplies"), "A save that still carries the retired \"supplies\" quest item loads without it.");
            point = mission.Interactables.Single(p => p.id != null && p.id.StartsWith(Map01Drops.IdPrefix));

            // [E] at the pile picks it up again.
            mission.GetComponent<Map01PlayerInteraction>().Interact(point);
            yield return null; yield return null;
            Assert.AreEqual(6, inventory.Count("cloth"), "Picked up again.");
            Assert.AreEqual(0, drops.Piles.Count(), "The pile is gone.");
            Assert.AreEqual("cloth", inventory.Cell(moved).id, "Cells keep the place they were given.");
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SmokeHidesAndTheGrenadeKillsWoundsAndIsHeard()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            var inventory = mission.GetComponent<Map01Inventory>();
            var thrower = mission.GetComponent<Map01StoneThrow>();
            // Smoke: a line through the cloud is cut, one beside it is not.
            var at = mission.player.position + mission.player.forward * 8f;
            var cloud = Map01Smoke.Spawn(at, Map01StoneThrow.SmokeRadius, Map01StoneThrow.SmokeSeconds);
            Vector3 c = cloud.Centre;
            Assert.IsTrue(Map01Smoke.Blocks(c - Vector3.right * 10f, c + Vector3.right * 10f), "Through the cloud: no sight.");
            Assert.IsFalse(Map01Smoke.Blocks(c + Vector3.forward * 10f - Vector3.right * 10f, c + Vector3.forward * 10f + Vector3.right * 10f), "Beside it: sight.");

            // The grenade: a guard 5 m off dies, one 10 m off is wounded, one 17 m off is untouched but hears it.
            var guards = mission.Enemies.Where(g => g != null && g.Alive).Take(3).ToArray();
            Assert.AreEqual(3, guards.Length);
            foreach (var g in guards) { g.enabled = false; var agent = g.GetComponent<UnityEngine.AI.NavMeshAgent>(); if (agent != null) agent.enabled = false; }
            var blast = mission.player.position + Vector3.forward * 30f;
            guards[0].transform.position = blast + Vector3.right * 5f;
            guards[1].transform.position = blast + Vector3.right * 10f;
            guards[2].transform.position = blast + Vector3.right * 17f;
            float wounded = guards[1].GetComponent<Health>().Max;
            float hp = mission.PlayerHealth;
            var (killed, hurt) = thrower.Explode(blast);
            Assert.AreEqual((1, 1), (killed, hurt));
            Assert.IsFalse(guards[0].Alive, "Within 6 m: killed.");
            Assert.IsTrue(guards[1].Alive, "Within 14 m: wounded…");
            Assert.Less(guards[1].GetComponent<Health>().Current, wounded, "…and hurt.");
            Assert.IsTrue(guards[2].Alive);
            Assert.AreEqual(hp, mission.PlayerHealth, "Nam 30 m off is not hurt.");

            // Key 4 picks the grenade once he has one; [Q] then aims it with its own rings.
            inventory.Add("grenade_f1", 1);
            inventory.SelectThrowable("grenade_f1");
            Assert.AreEqual("grenade_f1", inventory.ReadyThrowable());
            Assert.Less(Vector3.Distance(guards[2].transform.position, blast), Map01StoneThrow.GrenadeNoise);
            yield return new ExitPlayMode();
        }

        // ---- a look at it in the game, at 1920x1080 ------------------------------------------------------------

        private const string Out = "Docs/Previews/Map01Items/";
        private const System.Reflection.BindingFlags Private = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        private static void SetPrivate(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);

        /// <summary>Nam out of the hut, on a path (the first trail stone site), so the throws have room.</summary>
        private static void MoveOut(Map01Mission mission)
        {
            var data = Resources.Load<Map01StoneSiteData>(Map01StoneSiteData.ResourcePath);
            var site = data.sites.First(s => s.source == Map01StoneSiteData.Source.Trail);
            var controller = mission.player.GetComponent<CharacterController>();
            controller.enabled = false; mission.player.position = site.position + Vector3.up * .1f; controller.enabled = true;
        }

        private static IEnumerator Photo(string name)
        {
            string path = Out + name + ".png";
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            for (int i = 0; i < 3; i++) yield return null;
            ScreenCapture.CaptureScreenshot(path);
            float until = Time.realtimeSinceStartup + 5;
            while (!System.IO.File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
            for (int i = 0; i < 3; i++) yield return null;
        }

        [UnityTest, Explicit("Photographs for review; writes Docs/Previews/Map01Items/game-loadout-*.png.")]
        public IEnumerator PhotographTheLoadoutInTheGame()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
            yield return new EnterPlayMode();
            yield return null;
            var mission = Object.FindFirstObjectByType<Map01Mission>();
            mission.GetComponent<Map01Rescue>().MarkIntroSeen();
            foreach (var e in mission.Enemies) e.enabled = false;
            var inventory = mission.GetComponent<Map01Inventory>();
            var hud = mission.GetComponent<Map01Hud>();
            UnityEditor.SessionState.EraseBool(Map01Inventory.NoTestKitKey);
            mission.Settings.testCraftingKit = true;
            inventory.SeedStartingLoadout(6);
            UnityEditor.SessionState.SetBool(Map01Inventory.NoTestKitKey, true);
            inventory.Add("supplies", 1);
            MoveOut(mission);
            SetPreviewResolution(1920, 1080);
            mission.Say(null, -1f);
            for (int i = 0; i < 10; i++) yield return null;

            mission.SetInventoryOpen(true);
            SetPrivate(hud, "bagPick", CellOf(inventory, "rifle_mosin"));
            yield return Photo("game-loadout-01-tui-do");
            SetPrivate(hud, "menuOpen", true); SetPrivate(hud, "menuCell", CellOf(inventory, "cloth"));
            SetPrivate(hud, "menuAt", new Vector2(Screen.width / (Screen.height / 900f) - 560, 330));
            yield return Photo("game-loadout-02-menu-chuot-phai");
            SetPrivate(hud, "menuOpen", false);
            SetPrivate(hud, "askDrop", true); SetPrivate(hud, "askCell", CellOf(inventory, "cloth")); SetPrivate(hud, "askAmount", 6);
            yield return Photo("game-loadout-03-hoi-vut");
            SetPrivate(hud, "askDrop", false);
            inventory.EquipFromCell(CellOf(inventory, "rifle_mosin"));
            SetPrivate(hud, "bagPick", CellOf(inventory, "rifle_standard"));
            yield return Photo("game-loadout-04-doi-sang-mosin");

            mission.SetInventoryOpen(false);
            inventory.SelectThrowable("grenade_f1");
            inventory.CycleThrowable(); inventory.CycleThrowable(); inventory.CycleThrowable();   // round to the grenade, highlight on
            mission.Say(null, -1f);
            yield return Photo("game-loadout-05-thanh-trang-bi");

            var thrower = mission.GetComponent<Map01StoneThrow>();
            inventory.SelectThrowable("grenade_f1");
            var rig = mission.gameCamera.GetComponent<ShadowVale.Gameplay.Player.ThirdPersonCamera>();
            typeof(ShadowVale.Gameplay.Player.ThirdPersonCamera).GetField("_pitch", Private).SetValue(rig, 27f);   // aim down at the open path a few metres ahead
            thrower.BeginAim();
            for (int i = 0; i < 10; i++) yield return null;
            yield return Photo("game-loadout-06-ngam-luu-dan");
            thrower.Cancel();
            inventory.SelectThrowable("smoke_bomb");
            Assert.IsTrue(thrower.ThrowAt(mission.player.position + mission.player.forward * 9f));
            yield return WaitGameSeconds(3.5f);
            mission.Say(null, -1f);
            yield return Photo("game-loadout-07-may-khoi");

            inventory.Drop(CellOf(inventory, "cloth"), 4);
            inventory.Drop(CellOf(inventory, "rifle_standard"), 1);
            yield return WaitGameSeconds(14f);                                   // the smoke clears
            mission.Say(null, -1f);
            yield return Photo("game-loadout-08-vut-tren-dat");
            yield return new ExitPlayMode();
        }
    }
}
