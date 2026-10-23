using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.TestTools;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Player;
using Object = UnityEngine.Object;

namespace ShadowVale.Map01.Tests
{
    public sealed class FieldCraftingTests : ForestSceneTestBase
    {
        [Test]
        public void ResourceLoopHasLossFinitePacksAndSeparateWorkshopRecipes()
        {
            var bundle = JsonUtility.FromJson<ForestBundle>(File.ReadAllText("Assets/_Project/Map01/Generated/Map01Content.json"));
            ForestRecipe Recipe(string id) => bundle.craft_recipes.Single(r => r.id == id);
            var recover = Recipe(Map01Inventory.RecoverM16Recipe); var ak = Recipe(Map01Inventory.AssembleAkRecipe);
            Assert.AreEqual(Map01M16.Ammo, recover.inputs.Single().item_id);
            Assert.AreEqual(20, recover.inputs.Single().count * ak.inputs.Single(i => i.item_id == Map01Inventory.RecoveredAmmo).count);
            Assert.AreEqual("ammo_rifle", ak.output_item_id); Assert.AreEqual(10, ak.output_count);
            Assert.AreEqual(1, ak.inputs.Single(i => i.item_id == Map01Inventory.AkLoadingPack).count);
            Assert.AreEqual(8, ak.field_seconds); Assert.AreEqual(5, ak.craft_seconds);
            Assert.AreEqual(3, recover.field_max_batch);
            foreach (string id in new[] { "craft_grenade_f1", "craft_ammo_sniper", "craft_ammo_rifle", "craft_ammo_556", "craft_ammo_pistol" })
                Assert.AreEqual(0, Recipe(id).field_seconds, id + " requires the workshop.");
            foreach (string id in new[] { "craft_medkit_small", "craft_repair_kit", "craft_smoke_bomb" }) Assert.Greater(Recipe(id).field_seconds, 0);
            var loot = Map01Loot.Load(); CollectionAssert.IsEmpty(loot.Problems());
            int Guaranteed(string tag, string id) => loot.ContainerTable(tag).entries.Where(e => e.tier == 5 && e.item == id).Sum(e => e.min);
            Assert.AreEqual(2, Guaranteed("supply_delivery_reward", Map01Inventory.AkLoadingPack));
            Assert.AreEqual(2, Guaranteed("camp_chest_c2", Map01Inventory.AkLoadingPack));
            Assert.AreEqual(2, Guaranteed("c3_field_ak_v1", Map01Inventory.AkLoadingPack));
            Assert.AreEqual(30, Guaranteed("c3_field_ak_v1", "ammo_rifle"));
            Assert.AreNotEqual(Map01FieldSupplies.ApproachId, loot.fieldCaches.Single(c => c.id == "c3_field_ak_v1").id);
            Assert.AreEqual(3, loot.ItemOf(Map01Inventory.AkLoadingPack).baseValue);
            Assert.Greater(20 * loot.ItemOf(Map01M16.Ammo).baseValue + 3, 10 * loot.ItemOf("ammo_rifle").baseValue);
            Assert.AreEqual(64, bundle.items.Single(i => i.id == Map01Inventory.RecoveredAmmo).stack_max);
            Assert.AreEqual(64, bundle.items.Single(i => i.id == Map01Inventory.AkLoadingPack).stack_max);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator MapOneConversionKeepsMagazinesAndRefundsDamagePauseSaveAndOverflowOnce()
        {
            K54IntegrationTests.OpenSandbox(); SetPreviewResolution(1600, 900); yield return new EnterPlayMode(); yield return WaitForMap01Ready();
            var m = Object.FindFirstObjectByType<Map01Mission>(); var bag = m.GetComponent<Map01Inventory>();
            Assert.IsTrue(bag.HasFieldToolkit); Assert.AreEqual(2, bag.Count(Map01Inventory.AkLoadingPack));
            m.GetComponent<Map01Rescue>().MarkIntroSeen(); m.Cinematic = false;
            foreach (var guard in m.Enemies) { guard.enabled = false; var nav = guard.GetComponent<NavMeshAgent>(); if (nav != null) nav.enabled = false; }
            m.ModernHealth.CinematicInvulnerable = false;
            yield return WaitGameSeconds(1);
            bag.Add(Map01M16.Id, 1); Assert.IsTrue(bag.EquipItem(Map01M16.Id));
            m.ModernCombat.RestoreMagazine(17);
            SetCount(bag, Map01M16.Ammo, 20); SetCount(bag, Map01Inventory.RecoveredAmmo, 0); SetCount(bag, "ammo_rifle", 0);
            SetCount(bag, Map01Inventory.AkLoadingPack, 2);
            m.SetInventoryOpen(true); m.GetComponent<Map01Hud>().ShowFieldCrafting();
            Assert.IsFalse(bag.TryFieldCraft("craft_grenade_f1")); Assert.IsFalse(bag.TryFieldCraft("craft_ammo_rifle"));
            Assert.IsTrue(bag.TryFieldCraft(Map01Inventory.RecoverM16Recipe, 2), bag.FieldCraftRefusal(Map01Inventory.RecoverM16Recipe, 2));
            Assert.AreEqual(0, bag.Count(Map01M16.Ammo)); Assert.AreEqual(0, bag.Count(Map01Inventory.RecoveredAmmo));
            Assert.IsFalse(bag.TryFieldCraft(Map01Inventory.RecoverM16Recipe));
            yield return WaitGameSeconds(3.2f);
            Assert.IsFalse(bag.IsFieldCrafting); Assert.AreEqual(2, bag.Count(Map01Inventory.RecoveredAmmo));
            Assert.AreEqual(17, m.ModernCombat.RoundsInMagazine);
            Assert.IsTrue(bag.TryFieldCraft(Map01Inventory.AssembleAkRecipe), bag.FieldCraftRefusal(Map01Inventory.AssembleAkRecipe));
            Capture("map1-field-crafting");
            yield return WaitGameSeconds(8.2f);
            Assert.AreEqual(10, bag.Count("ammo_rifle")); Assert.AreEqual(1, bag.Count(Map01Inventory.AkLoadingPack));
            Assert.AreEqual(0, bag.Count(Map01Inventory.RecoveredAmmo)); Assert.AreEqual(17, m.ModernCombat.RoundsInMagazine);

            SetCount(bag, Map01M16.Ammo, 20);
            Assert.IsTrue(bag.TryFieldCraft(Map01Inventory.RecoverM16Recipe, 2));
            m.ModernHealth.TakeDamage(1, m.player.position, m.gameObject);
            Assert.IsFalse(bag.IsFieldCrafting); Assert.AreEqual(20, bag.Count(Map01M16.Ammo));
            bag.CancelFieldCraft(); Assert.AreEqual(20, bag.Count(Map01M16.Ammo));
            Assert.IsTrue(bag.TryFieldCraft(Map01Inventory.RecoverM16Recipe, 2));
            m.SetPaused(true); float remaining = bag.FieldCraftRemaining;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(remaining, bag.FieldCraftRemaining); m.SetPaused(false);
            Assert.IsTrue(m.GetComponent<Map01SaveSystem>().SaveSlot(0, out var saveError, true), saveError);
            Assert.IsFalse(bag.IsFieldCrafting); Assert.AreEqual(20, bag.Count(Map01M16.Ammo));
            var items = bag.CaptureItems(); var layout = bag.CaptureLayout();
            Assert.IsFalse(bag.IsFieldCrafting); Assert.AreEqual(20, bag.Count(Map01M16.Ammo));
            bag.RestoreFromSave(items, bag.Stones, null, 0); bag.RestoreLayout(layout); bag.RestoreFieldToolkit(layout.fieldKitVersion);
            Assert.AreEqual(20, bag.Count(Map01M16.Ammo)); Assert.AreEqual(1, bag.Count(Map01Inventory.AkLoadingPack));
            // Old checkpoints receive the starter packs; the next saved layout prevents repeated migration.
            bag.RestoreFieldToolkit(0); Assert.AreEqual(3, bag.Count(Map01Inventory.AkLoadingPack));
            layout = bag.CaptureLayout(); bag.RestoreFieldToolkit(layout.fieldKitVersion); Assert.AreEqual(3, bag.Count(Map01Inventory.AkLoadingPack));

            Assert.IsTrue(bag.TryFieldCraft(Map01Inventory.RecoverM16Recipe, 2));
            m.SetInventoryOpen(false); yield return null; yield return null;
            Assert.IsFalse(bag.IsFieldCrafting); Assert.AreEqual(20, bag.Count(Map01M16.Ammo));
            m.SetInventoryOpen(true);
            Assert.IsTrue(bag.TryFieldCraft(Map01Inventory.RecoverM16Recipe, 2));
            bag.Draw(Map01Inventory.Slot.Knife);
            Assert.IsFalse(bag.IsFieldCrafting); Assert.AreEqual(20, bag.Count(Map01M16.Ammo));

            SetCount(bag, Map01M16.Ammo, 10);
            Assert.IsTrue(bag.TryFieldCraft(Map01Inventory.RecoverM16Recipe));
            while (bag.FreeCells > 0) bag.Add("cloth", 64);
            int dropped = Map01Drops.For(m).Piles.Where(p => p.id == Map01M16.Ammo).Sum(p => p.count);
            bag.CancelFieldCraft(); bag.CancelFieldCraft();
            Assert.AreEqual(0, bag.Count(Map01M16.Ammo));
            Assert.AreEqual(dropped + 10, Map01Drops.For(m).Piles.Where(p => p.id == Map01M16.Ammo).Sum(p => p.count));
            yield return new ExitPlayMode();
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator C3AkPouchIsReachableIndependentOfOldCachesAndCannotRefillAfterLoad()
        {
            K54IntegrationTests.OpenSandbox(); yield return new EnterPlayMode(); yield return WaitForMap01Ready();
            var m = Object.FindFirstObjectByType<Map01Mission>();
            m.GetComponent<Map01Rescue>().MarkIntroSeen(); m.Cinematic = false;
            foreach (var guard in m.Enemies) { guard.enabled = false; var nav = guard.GetComponent<NavMeshAgent>(); if (nav != null) nav.enabled = false; }
            m.ModernHealth.CinematicInvulnerable = true;
            var quest = m.GetComponent<Map01Quest>(); quest.RestoreStage(Map01Quest.BossStage); yield return null;
            var supplies = m.GetComponent<Map01FieldSupplies>();
            supplies.ApproachPoint.MarkLooted();
            var point = m.Points.Single(p => p.id == "c3_field_ak_v1");
            Assert.IsTrue(point.gameObject.activeInHierarchy); Assert.IsFalse(point.used);
            Assert.IsTrue(NavMesh.SamplePosition(point.transform.position, out var walkable, .5f, NavMesh.AllAreas));
            Assert.Less(Vector3.Distance(point.transform.position, walkable.position), .2f);
            var path = new NavMeshPath();
            Assert.IsTrue(NavMesh.CalculatePath(supplies.ApproachPoint.transform.position, point.transform.position, NavMesh.AllAreas, path));
            Assert.AreEqual(NavMeshPathStatus.PathComplete, path.status);
            PlaceNam(m, point.transform.position + Vector3.back * .8f);
            var bag = m.GetComponent<Map01Inventory>(); int rounds = bag.Count("ammo_rifle"), packs = bag.Count(Map01Inventory.AkLoadingPack);
            m.GetComponent<Map01PlayerInteraction>().Interact(point); Map01NamActions.For(m).CancelAction();
            Assert.AreEqual(rounds + 30, bag.Count("ammo_rifle")); Assert.AreEqual(packs + 2, bag.Count(Map01Inventory.AkLoadingPack));
            Assert.IsTrue(point.used); Assert.IsTrue(supplies.ApproachPoint.used);
            Assert.IsTrue(m.GetComponent<Map01SaveSystem>().SaveSlot(0, out var error, true), error);
            m.GetComponent<Map01SaveSystem>().Load(); yield return null; yield return WaitForMap01CheckpointRestore();
            m = Object.FindFirstObjectByType<Map01Mission>(); bag = m.GetComponent<Map01Inventory>();
            point = m.Points.Single(p => p.id == "c3_field_ak_v1");
            Assert.IsTrue(point.used); Assert.IsTrue(m.GetComponent<Map01FieldSupplies>().ApproachPoint.used);
            Assert.AreEqual(rounds + 30, bag.Count("ammo_rifle")); Assert.AreEqual(packs + 2, bag.Count(Map01Inventory.AkLoadingPack));
            m.GetComponent<Map01PlayerInteraction>().Interact(point);
            Assert.AreEqual(rounds + 30, bag.Count("ammo_rifle")); Assert.AreEqual(packs + 2, bag.Count(Map01Inventory.AkLoadingPack));
            yield return new ExitPlayMode();
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator MapTwoFieldPanelLeavesWorldActiveAndSaveCancelsEscrowWithoutDuplicatingPacks()
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/Maps/Map 2.unity"); SetPreviewResolution(1600, 900); yield return new EnterPlayMode();
            Map02Mission m = null; float deadline = Time.realtimeSinceStartup + 100;
            while (Time.realtimeSinceStartup < deadline)
            {
                m = Object.FindFirstObjectByType<Map02Mission>();
                if (m?.Ready == true && m.Bag != null && m.Chapter != null) break;
                Object.FindFirstObjectByType<Map02Arrival>()?.Skip(); yield return null;
            }
            Assert.NotNull(m?.Bag);
            Type.GetType("ShadowVale.Map01.Editor.Map02RoutePreview, ShadowVale.Map01.Editor", true).GetMethod("ApplyToMission").Invoke(null, new object[] { m, 2 });
            foreach (var enemy in m.Chapter.Enemies) enemy.enabled = false;
            yield return WaitGameSeconds(1);
            SetCount(m.Bag, Map01M16.Ammo, 20); SetCount(m.Bag, Map01Inventory.AkLoadingPack, 2);
            m.SetCharacterPanel(1); m.CharacterHud.ShowFieldCrafting();
            Assert.IsTrue(m.InputBlocked); Assert.IsFalse(m.CharacterWorldBlocked, "The panel blocks Nam's controls, not enemy simulation.");
            Assert.IsTrue(m.Bag.TryFieldCraft(Map01Inventory.RecoverM16Recipe, 2), m.Bag.FieldCraftRefusal(Map01Inventory.RecoverM16Recipe, 2));
            Assert.AreEqual(0, m.Bag.Count(Map01M16.Ammo));
            Capture("map2-field-crafting"); yield return null; yield return null;
            var snapshot = Map02Mission.Parse(JsonUtility.ToJson(m.Capture()));
            Assert.IsFalse(m.Bag.IsFieldCrafting); Assert.AreEqual(20, m.Bag.Count(Map01M16.Ammo));
            m.Restore(snapshot); m.Restore(snapshot);
            Assert.AreEqual(20, m.Bag.Count(Map01M16.Ammo)); Assert.AreEqual(2, m.Bag.Count(Map01Inventory.AkLoadingPack));
            Assert.IsFalse(m.Bag.IsFieldCrafting);
            yield return new ExitPlayMode();
        }

        static void SetCount(Map01Inventory bag, string id, int count) { bag.Spend(id, bag.Count(id)); bag.Add(id, count); }
        static void Capture(string name)
        {
            string folder = Environment.GetEnvironmentVariable("SVL_REGRESSION_REPORT") ?? "Logs/FieldCraftingTests";
            Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
        }
    }
}
