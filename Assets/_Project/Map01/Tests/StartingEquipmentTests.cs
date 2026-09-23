using NUnit.Framework;
using UnityEngine;

namespace ShadowVale.Map01.Tests
{
    public sealed class StartingEquipmentTests
    {
        [Test]
        public void OldSavesGainMissingEquipmentWithoutDuplicatingOrReplacingSavedCounts()
        {
            var root = new GameObject("Inventory migration test");
            try {
                var inventory = root.AddComponent<Map01Inventory>();
                inventory.RestoreFromSave(new[] { new ForestIngredient { item_id = "ammo_rifle", count = 17 } }, 3, null, 0);
                Assert.AreEqual(1, inventory.Count("rifle_standard"));
                Assert.AreEqual(1, inventory.Count("knife"));
                Assert.AreEqual(17, inventory.Count("ammo_rifle"));
                inventory.RestoreFromSave(inventory.CaptureItems(), inventory.Stones, null, 0);
                Assert.AreEqual(1, inventory.Count("knife"));
                Assert.AreEqual(1, inventory.StackLimit("knife"));
                inventory.RestoreFromSave(new[] { new ForestIngredient { item_id = "knife", count = 0 } }, 0, null, 0);
                Assert.AreEqual(0, inventory.Count("knife"), "An explicit saved absence must not be granted again.");
            } finally { Object.DestroyImmediate(root); }
        }
    }
}
