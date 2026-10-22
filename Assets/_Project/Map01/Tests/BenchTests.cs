using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace ShadowVale.Map01.Tests
{
    /// <summary>
    /// The workbench screen (Map01Hud.Craft.cs) and its queue (Map01Inventory): [E] at the bench opens it and
    /// shuts it, Tab goes over to the bag; crafts queue several at a time and go on while Nam walks; a save
    /// keeps the craft under way and gives back the rest; gunpowder makes rounds; the temporary test kit.
    /// An [Explicit] pass photographs the screen in the game at 1920x1080.
    /// </summary>
    public sealed class BenchTests : ForestSceneTestBase
    {
        // Lambdas stay in static helpers or capture only static fields (the domain reload of EnterPlayMode).
        private static Map01Mission mission;
        private static Map01Inventory inventory;
        private static Map01Hud hud;
        private static Keyboard keyboard;

        private static void OpenMap()
        {
            Map01OpeningCutscene.CancelPending();
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 1.unity");
        }

        private static void Settle()
        {
            mission = Object.FindFirstObjectByType<Map01Mission>();
            inventory = mission.GetComponent<Map01Inventory>();
            hud = mission.GetComponent<Map01Hud>();
            foreach (var enemy in mission.Enemies) { enemy.enabled = false; enemy.GetComponent<NavMeshAgent>().enabled = false; }
            mission.GetComponent<Map01Rescue>().MarkIntroSeen();
        }

        private static ForestPoint Bench() => mission.Interactables.First(p => p.kind == ForestPointKind.Workbench);

        private static void StandAtBench()
        {
            var b = Bench().transform.position;
            NavMesh.SamplePosition(b, out var near, 3, NavMesh.AllAreas);
            var side = Vector3.ProjectOnPlane(near.position - b, Vector3.up);
            if (side.sqrMagnitude < .01f) side = Vector3.right;
            var controller = mission.player.GetComponent<CharacterController>();
            NavMesh.SamplePosition(b + side.normalized * 1.1f, out var ground, 2, NavMesh.AllAreas);
            controller.enabled = false;
            mission.player.SetPositionAndRotation(ground.position, Quaternion.LookRotation(Vector3.ProjectOnPlane(b - ground.position, Vector3.up)));
            controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private static void Empty(params string[] ids) { foreach (var id in ids) inventory.Spend(id, inventory.Count(id)); }
        private static int Saved(ForestIngredient[] items, string id) => items.Where(i => i.item_id == id).Sum(i => i.count);

        [UnityTest]
        public IEnumerator BenchOpensWithEQueuesCraftsAndGunpowderMakesRounds()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            Assert.AreEqual(0, inventory.Count("gunpowder"), "Tests start without the bench test kit.");

            // The test kit, as Map01Balance.json's testCraftingKit gives it on a new game.
            SessionState.EraseBool(Map01Inventory.NoTestKitKey);
            mission.Settings.testCraftingKit = true;
            inventory.SeedStartingLoadout(6);
            SessionState.SetBool(Map01Inventory.NoTestKitKey, true);
            foreach (var (id, count) in Map01Inventory.TestCraftingKit) Assert.AreEqual(count, inventory.Count(id), "Test kit: " + id);
            foreach (var recipe in inventory.Recipes) Assert.GreaterOrEqual(inventory.CanMake(recipe), 2, "The kit makes " + recipe.id + " at least twice.");
            CollectionAssert.AreEqual(new[] { "craft_medkit_small", "craft_repair_kit", "craft_smoke_bomb", "craft_grenade_f1", "craft_ammo_pistol", "craft_ammo_rifle", "craft_ammo_556", "craft_ammo_sniper" },
                inventory.Recipes.Select(r => r.id).ToArray());

            // [E] at the bench opens the screen; Tab goes over to the bag; Tab again shuts it; [E] twice opens and shuts.
            var routing = RouteInputToGame(); keyboard = InputSystem.AddDevice<Keyboard>();
            StandAtBench();
            yield return null; yield return null;
            Assert.AreSame(Bench(), mission.GetComponent<Map01PlayerInteraction>().Nearby);
            yield return Press(Key.E);
            Assert.IsTrue(mission.BenchOpen); Assert.IsTrue(mission.InventoryOpen, "The bench holds play as the bag does.");
            Assert.IsFalse(mission.CameraInputEnabled);
            yield return Press(Key.Tab);
            Assert.IsFalse(mission.BenchOpen); Assert.IsTrue(mission.InventoryOpen, "Tab goes over to the bag.");
            yield return Press(Key.Tab);
            Assert.IsFalse(mission.InventoryOpen);
            yield return Press(Key.E);
            Assert.IsTrue(mission.BenchOpen);
            yield return Press(Key.E);
            Assert.IsFalse(mission.BenchOpen); Assert.IsFalse(mission.InventoryOpen);
            InputSystem.RemoveDevice(keyboard); RestoreInputRouting(routing);

            // Two lots of rifle rounds: the gunpowder and scrap go at once, the bench works one at a time.
            int powder = inventory.Count("gunpowder"), scrap = inventory.Count("scrap_metal"), rounds = inventory.Count("ammo_rifle");
            Assert.IsTrue(inventory.TryCraft("craft_ammo_rifle", 2));
            Assert.AreEqual(powder - 4, inventory.Count("gunpowder"));
            Assert.AreEqual(scrap - 2, inventory.Count("scrap_metal"));
            Assert.AreEqual(2, inventory.Queue.Count);
            // While it works, Nam moves and fights: crafting no longer holds him at the bench.
            Assert.IsTrue(mission.ModernPlayer.InputAllowed(), "Nam walks while the bench works.");
            // A save now keeps the one under way and gives back what the waiting one took.
            var saved = inventory.CaptureItems();
            Assert.AreEqual(powder - 2, Saved(saved, "gunpowder"));
            Assert.AreEqual(scrap - 1, Saved(saved, "scrap_metal"));
            yield return WaitGameSeconds(5.3f);
            Assert.AreEqual(rounds + 10, inventory.Count("ammo_rifle"), "The first lot is made.");
            Assert.AreEqual(1, inventory.Queue.Count, "The second has started.");
            yield return WaitGameSeconds(5.3f);
            Assert.AreEqual(rounds + 20, inventory.Count("ammo_rifle"));
            Assert.IsFalse(inventory.IsCrafting);
            StringAssert.Contains("Chế tạo xong: 20 đạn súng trường", mission.Dialogue, "One line for the whole lot.");
            Assert.IsTrue(hud.PickupFeed.Any(p => p.id == "ammo_rifle"), "The rounds show in the pickup feed.");

            // K-54 rounds: 1 gunpowder + 1 scrap make 8. Without gunpowder, nothing.
            int pistol = inventory.Count("ammo_pistol");
            Assert.IsTrue(inventory.TryCraft("craft_ammo_pistol"));
            yield return WaitGameSeconds(3.3f);
            Assert.AreEqual(pistol + 8, inventory.Count("ammo_pistol"));
            Empty("gunpowder");
            Assert.AreEqual(0, inventory.CanMake(inventory.Recipe("craft_ammo_pistol")));
            Assert.IsFalse(inventory.TryCraft("craft_ammo_pistol"));
            StringAssert.Contains("Chưa đủ vật liệu", mission.Dialogue);

            // A loaded save picks up the craft that was under way.
            inventory.RestoreFromSave(inventory.CaptureItems(), inventory.Stones, "craft_medkit_small", 1f);
            Assert.AreEqual("craft_medkit_small", inventory.CraftingId);
            int dressings = inventory.Count("medkit_small");
            yield return WaitGameSeconds(1.3f);
            Assert.AreEqual(dressings + 1, inventory.Count("medkit_small"));

            // Away from the bench, nothing is made.
            mission.player.GetComponent<CharacterController>().enabled = false;
            mission.player.position += Vector3.forward * 30;
            mission.player.GetComponent<CharacterController>().enabled = true;
            Assert.IsFalse(inventory.TryCraft("craft_medkit_small"));

            // Gunpowder is in the C1 and C2 chests; everything on the bench has its icon and its large picture.
            Assert.IsTrue(Map01CampChests.Guaranteed(1).Any(c => c.item == "gunpowder"));
            Assert.IsTrue(Map01CampChests.Guaranteed(2).Any(c => c.item == "gunpowder"));
            Assert.IsNotNull(hud.ItemIcon("gunpowder"));
            Assert.AreEqual("Thuốc súng", ForestInventory.Name("gunpowder"));
            foreach (var recipe in inventory.Recipes)
            {
                Assert.IsNotNull(hud.ItemRender(recipe.output_item_id), "Large picture of " + recipe.output_item_id);
                foreach (var input in recipe.inputs) Assert.IsNotNull(hud.ItemRender(input.item_id), "Large picture of " + input.item_id);
            }
            yield return new ExitPlayMode();
        }

        // ---- the screen in the game, at 1920x1080 --------------------------------------------------------

        private const string Out = "Docs/Previews/Map01Items/";

        private static IEnumerator Photo(string name)
        {
            string path = Out + name + ".png";
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            float until = Time.realtimeSinceStartup + 5;
            while (!File.Exists(path) && Time.realtimeSinceStartup < until) yield return null;
            for (int i = 0; i < 3; i++) yield return null;
        }

        [UnityTest, Explicit("Photographs for review; writes Docs/Previews/Map01Items/game-bench-*.png.")]
        public IEnumerator PhotographTheBenchScreenInTheGame()
        {
            OpenMap();
            yield return new EnterPlayMode(); yield return null; yield return null;
            Settle();
            SetPreviewResolution(1920, 1080);
            Directory.CreateDirectory(Out);
            foreach (var (id, count) in Map01Inventory.TestCraftingKit) inventory.Add(id, count);
            StandAtBench();
            yield return WaitGameSeconds(.5f);
            mission.Say(null, -1f);

            mission.SetBenchOpen(true);
            hud.SelectBenchRecipe("craft_medkit_small");
            for (int i = 0; i < 6; i++) yield return null;
            yield return Photo("game-bench-01-du-vat-lieu");

            // Eight recipes no longer fit at full height (112 a row in 650): the list scrolls to its foot instead of
            // closing its rows up. The scroll view clamps a far-off position to the last row.
            var scroll = typeof(Map01Hud).GetField("recipeScroll", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            scroll.SetValue(hud, new Vector2(0, 10000));
            for (int i = 0; i < 4; i++) yield return null;
            yield return Photo("game-bench-01b-cuon-cuoi-danh-sach");
            float foot = ((Vector2)scroll.GetValue(hud)).y;
            Assert.Greater(foot, 0f, "The recipe list scrolls.");
            Assert.LessOrEqual(foot, mission.Bundle.craft_recipes.Length * 112f - 8f - 650f + .5f, "…no further than its last row.");
            scroll.SetValue(hud, Vector2.zero);

            hud.SelectBenchRecipe("craft_ammo_rifle", 3);
            for (int i = 0; i < 4; i++) yield return null;
            yield return Photo("game-bench-02-dan-ak-x3");

            int powder = inventory.Count("gunpowder");
            Empty("gunpowder", "gun_oil");
            inventory.Add("gunpowder", 1);
            hud.SelectBenchRecipe("craft_ammo_rifle");
            for (int i = 0; i < 4; i++) yield return null;
            yield return Photo("game-bench-03-thieu-vat-lieu");
            hud.SelectBenchRecipe("craft_repair_kit");
            for (int i = 0; i < 4; i++) yield return null;
            yield return Photo("game-bench-04-thieu-dau-sung");
            inventory.Add("gunpowder", powder); inventory.Add("gun_oil", 4);

            hud.SelectBenchRecipe("craft_smoke_bomb");
            Assert.IsTrue(inventory.TryCraft("craft_medkit_small", 1));
            yield return WaitGameSeconds(4.2f);
            Assert.IsTrue(inventory.TryCraft("craft_smoke_bomb", 3));
            yield return WaitGameSeconds(1.8f);
            yield return Photo("game-bench-05-dang-che-tao");

            // Walked off: the line under the mission panel, and the feed when it is done.
            mission.CloseGameplayPanel();
            yield return WaitGameSeconds(.4f);
            yield return Photo("game-bench-06-roi-ban");
            yield return WaitGameSeconds(10.5f);
            yield return Photo("game-bench-07-xong");

            mission.SetPaused(true); mission.SetInventoryOpen(true);
            typeof(Map01Hud).GetField("selectedStack", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(hud, inventory.InventoryStacks().FindIndex(s => s.Id == "gunpowder"));
            for (int i = 0; i < 6; i++) yield return null;
            yield return Photo("game-bench-08-tui-do-thuoc-sung");
            mission.SetInventoryOpen(false); mission.SetPaused(false);
            yield return new ExitPlayMode();
        }
    }
}
