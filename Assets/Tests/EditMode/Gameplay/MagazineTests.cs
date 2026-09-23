using NUnit.Framework;
using ShadowVale.Gameplay.Combat;

namespace ShadowVale.Tests.EditMode.Gameplay
{
    public sealed class MagazineTests
    {
        private const int Capacity = 30;
        private const float PartialReload = 2.4f;
        private const float EmptyReload = 2.9f;

        private static Magazine Fresh() => new(Capacity, PartialReload, EmptyReload);

        [Test]
        public void StartsLoaded()
        {
            var magazine = Fresh();
            Assert.AreEqual(Capacity, magazine.Rounds);
            Assert.IsTrue(magazine.IsFull);
            Assert.IsFalse(magazine.IsReloading);
        }

        [Test]
        public void SpendsExactlyCapacityRoundsBeforeRunningDry()
        {
            var magazine = Fresh();
            for (var i = 0; i < Capacity; i++)
            {
                Assert.IsTrue(magazine.TryConsume(), $"round {i + 1} should fire");
            }

            Assert.IsTrue(magazine.IsEmpty);
            Assert.IsFalse(magazine.TryConsume(), "the 31st pull is a click");
        }

        [Test]
        public void ReloadingFromEmptyTakesLongerThanToppingUp()
        {
            var empty = Fresh();
            for (var i = 0; i < Capacity; i++) empty.TryConsume();
            empty.BeginReload(reserve: 90);

            var partial = Fresh();
            partial.TryConsume();
            partial.BeginReload(reserve: 90);

            Assert.Greater(empty.ReloadRemaining, partial.ReloadRemaining,
                "running dry has to cost more than swapping early, or there is no reason to reload in cover");
            Assert.AreEqual(EmptyReload, empty.ReloadRemaining, 1e-4f);
            Assert.AreEqual(PartialReload, partial.ReloadRemaining, 1e-4f);
        }

        [Test]
        public void CannotFireWhileReloading()
        {
            var magazine = Fresh();
            magazine.TryConsume();
            magazine.BeginReload(reserve: 90);

            Assert.IsFalse(magazine.TryConsume(), "the magazine is out of the weapon");
            Assert.AreEqual(Capacity - 1, magazine.Rounds, "and no round was quietly lost");
        }

        [Test]
        public void ReloadDrawsOnlyWhatIsMissing()
        {
            var magazine = Fresh();
            for (var i = 0; i < 10; i++) magazine.TryConsume();

            var asked = -1;
            magazine.BeginReload(reserve: 90);
            magazine.Tick(EmptyReload, wanted => { asked = wanted; return wanted; });

            Assert.AreEqual(10, asked, "topping up a 20-round magazine must not cost 30 from the pack");
            Assert.AreEqual(Capacity, magazine.Rounds);
        }

        [Test]
        public void AShortReserveGivesAPartialMagazine()
        {
            var magazine = Fresh();
            for (var i = 0; i < Capacity; i++) magazine.TryConsume();

            magazine.BeginReload(reserve: 7);
            magazine.Tick(EmptyReload, _ => 7);

            Assert.AreEqual(7, magazine.Rounds);
            Assert.IsFalse(magazine.IsFull);
            Assert.IsFalse(magazine.IsReloading);
        }

        [Test]
        public void ReloadIsRefusedWhenFullOrWithNothingToLoad()
        {
            Assert.IsFalse(Fresh().BeginReload(reserve: 90), "a full magazine has nothing to gain");

            var magazine = Fresh();
            magazine.TryConsume();
            Assert.IsFalse(magazine.BeginReload(reserve: 0), "an empty pack cannot reload");
            Assert.IsFalse(magazine.IsReloading);
        }

        [Test]
        public void ReloadCompletesOnlyAfterTheFullDuration()
        {
            var magazine = Fresh();
            magazine.TryConsume();
            magazine.BeginReload(reserve: 90);

            magazine.Tick(PartialReload - 0.05f, w => w);
            Assert.IsTrue(magazine.IsReloading, "still short of the full time");
            Assert.AreEqual(Capacity - 1, magazine.Rounds);

            magazine.Tick(0.05f, w => w);
            Assert.IsFalse(magazine.IsReloading);
            Assert.AreEqual(Capacity, magazine.Rounds);
        }

        [Test]
        public void ReloadProgressRunsFromZeroToOne()
        {
            var magazine = Fresh();
            magazine.TryConsume();
            Assert.AreEqual(0f, magazine.ReloadProgress, 1e-4f, "not reloading reads as zero");

            magazine.BeginReload(reserve: 90);
            Assert.AreEqual(0f, magazine.ReloadProgress, 1e-4f);

            magazine.Tick(PartialReload * 0.5f, w => w);
            Assert.AreEqual(0.5f, magazine.ReloadProgress, 0.02f);

            magazine.Tick(PartialReload * 0.5f, w => w);
            Assert.AreEqual(0f, magazine.ReloadProgress, 1e-4f, "finished reloads stop reporting progress");
        }

        [Test]
        public void CancellingLeavesTheWeaponAbleToFire()
        {
            // Dying mid-reload goes through here. A magazine left believing it is reloading
            // refuses to fire, and nothing would ever clear it after the respawn.
            var magazine = Fresh();
            magazine.TryConsume();
            magazine.BeginReload(reserve: 90);
            magazine.CancelReload();

            Assert.IsFalse(magazine.IsReloading);
            Assert.IsTrue(magazine.TryConsume());
            Assert.AreEqual(Capacity - 2, magazine.Rounds, "the interrupted reload loaded nothing");
        }

        [Test]
        public void TickingWithoutAReloadDoesNothing()
        {
            var magazine = Fresh();
            magazine.TryConsume();
            magazine.Tick(10f, _ => 999);
            Assert.AreEqual(Capacity - 1, magazine.Rounds, "rounds must not appear on their own");
        }

        [Test]
        public void RefillRestoresACheckpointAndClearsAnyReload()
        {
            var magazine = Fresh();
            magazine.TryConsume();
            magazine.BeginReload(reserve: 90);

            magazine.Refill(12);

            Assert.AreEqual(12, magazine.Rounds);
            Assert.IsFalse(magazine.IsReloading);
        }

        [Test]
        public void RefillIsClampedToTheMagazine()
        {
            var magazine = Fresh();
            magazine.Refill(999);
            Assert.AreEqual(Capacity, magazine.Rounds);
            magazine.Refill(-5);
            Assert.AreEqual(0, magazine.Rounds);
        }

        [Test]
        public void ANullSupplierFillsOutright()
        {
            // Greybox scenes have no inventory to draw from; they should still reload.
            var magazine = Fresh();
            for (var i = 0; i < Capacity; i++) magazine.TryConsume();
            magazine.BeginReload(reserve: -1);
            magazine.Tick(EmptyReload);

            Assert.AreEqual(Capacity, magazine.Rounds);
        }

        [Test]
        public void ASupplierCannotOverfillTheMagazine()
        {
            var magazine = Fresh();
            magazine.TryConsume();
            magazine.BeginReload(reserve: 90);
            magazine.Tick(EmptyReload, _ => 500);

            Assert.AreEqual(Capacity, magazine.Rounds);
        }
    }
}
