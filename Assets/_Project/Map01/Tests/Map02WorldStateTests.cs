using NUnit.Framework;
using UnityEngine;

namespace ShadowVale.Map01.Tests
{
    public sealed class Map02WorldStateTests
    {
        [Test] public void CargoCannotBeDeliveredBeforePickupOrCollectedTwice()
        {
            var state = new Map02WorldState();
            Assert.IsFalse(state.Transfer(Map02Work.MedicinePickup));
            state.story.ContactConfirmed();
            Assert.IsFalse(state.Transfer(Map02Work.MedicineDelivery));
            Assert.IsTrue(state.Transfer(Map02Work.MedicinePickup));
            Assert.IsFalse(state.Transfer(Map02Work.MedicinePickup));
            Assert.IsTrue(state.Transfer(Map02Work.MedicineDelivery));
            Assert.IsFalse(state.medicineCarried);
            Assert.IsTrue(state.story.medicineDelivered);
            Assert.IsFalse(state.Transfer(Map02Work.MedicineDelivery));
            Assert.IsFalse(state.Available(Map02Work.ScoutOrder));
            Assert.IsTrue(state.Transfer(Map02Work.CratePickup));
            Assert.IsTrue(state.Transfer(Map02Work.CrateDelivery));
            Assert.IsTrue(state.Available(Map02Work.ScoutOrder));
        }

        [Test] public void PreparationStockDoesNotRefillPlayerAmmunitionOrSkipPhysicalDelivery()
        {
            var state = new Map02WorldState(); state.story.stage = Map02Stage.QuietPreparation;
            Assert.IsFalse(state.Transfer(Map02Work.SouthStockDelivery));
            state.Transfer(Map02Work.SouthStockPickup);
            Assert.IsTrue(state.southStockCarried);
            Assert.IsFalse(state.story.southStockMoved);
            Assert.IsTrue(state.Transfer(Map02Work.SouthStockDelivery));
            Assert.IsFalse(state.Transfer(Map02Work.SouthStockPickup));
            Assert.IsFalse(state.Available(Map02Work.LastBoat));
        }

        [Test] public void SaveRoundTripKeepsCargoAndRejectsDuplicatePickup()
        {
            var state = new Map02WorldState(); state.story.ContactConfirmed();
            state.Transfer(Map02Work.CratePickup);
            var loaded = JsonUtility.FromJson<Map02WorldState>(JsonUtility.ToJson(state));
            Assert.IsTrue(loaded.crateCarried);
            Assert.IsFalse(loaded.Transfer(Map02Work.CratePickup));
            Assert.IsTrue(loaded.Transfer(Map02Work.CrateDelivery));
        }

        [Test] public void ReachingPropMeansThePlayerIsActuallyWithinItsRadius()
        {
            var prop = new GameObject("Medicine prop"); var player = new GameObject("Nam");
            try
            {
                var target = prop.AddComponent<Map02ObjectiveObject>(); target.radius = 2.4f;
                player.transform.position = new Vector3(0,0,3);
                Assert.IsFalse(target.WithinReach(player.transform));
                player.transform.position = new Vector3(0,0,2);
                Assert.IsTrue(target.WithinReach(player.transform));
                prop.SetActive(false);
                Assert.IsFalse(target.WithinReach(player.transform));
            }
            finally { Object.DestroyImmediate(prop); Object.DestroyImmediate(player); }
        }
    }
}
