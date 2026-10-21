using NUnit.Framework;
using ShadowVale.Gameplay.Combat;

namespace ShadowVale.Map01.Tests
{
    public sealed class MosinReloadTimingTests
    {
        int reserve;
        int Draw(int amount){int result=System.Math.Min(amount,reserve);reserve-=result;return result;}
        [Test] public void EachInsertionConsumesOneRoundAndCancellationKeepsOnlyInsertedRounds()
        {
            var magazine=new Magazine(5,5,Map01Mosin.InsertionTimes);magazine.Refill(0);reserve=10;
            Assert.IsTrue(magazine.BeginReload(reserve));magazine.Tick(1.48f,Draw);Assert.AreEqual(0,magazine.Rounds);
            magazine.Tick(.01f,Draw);Assert.AreEqual(1,magazine.Rounds);Assert.AreEqual(9,reserve);
            magazine.CancelReload();Assert.AreEqual(1,magazine.Rounds);Assert.IsFalse(magazine.IsReloading);Assert.AreEqual(9,reserve);
        }
        [Test] public void FullReloadRemainsFiveSecondsAndPartialReloadSkipsUnusedInsertionGestures()
        {
            var magazine=new Magazine(5,5,Map01Mosin.InsertionTimes);magazine.Refill(0);reserve=10;
            Assert.IsTrue(magazine.BeginReload(reserve));Assert.AreEqual(5,magazine.ReloadRemaining,.0001f);
            magazine.Tick(4,Draw);Assert.AreEqual(5,magazine.Rounds);Assert.IsTrue(magazine.IsReloading,"Closing the bolt still belongs to the same five-second take.");
            magazine.Tick(1,Draw);Assert.IsFalse(magazine.IsReloading);Assert.AreEqual(5,reserve);
            magazine.Refill(4);Assert.IsTrue(magazine.BeginReload(reserve));Assert.AreEqual(2.64f,magazine.ReloadRemaining,.001f);
            magazine.Tick(2.64f,Draw);Assert.AreEqual(5,magazine.Rounds);Assert.AreEqual(4,reserve);Assert.IsFalse(magazine.IsReloading);
        }
        [Test] public void ExhaustedReserveCannotDuplicateRoundsAndOrdinaryAKStillLoadsAtomically()
        {
            var magazine=new Magazine(5,5,Map01Mosin.InsertionTimes);magazine.Refill(0);reserve=2;
            Assert.IsTrue(magazine.BeginReload(reserve));magazine.Tick(5,Draw);Assert.AreEqual(2,magazine.Rounds);Assert.AreEqual(0,reserve);
            Assert.IsFalse(magazine.BeginReload(reserve));
            var ak=new Magazine(30,2.4f);ak.Refill(12);reserve=50;Assert.IsTrue(ak.BeginReload(reserve));ak.Tick(1.5f,Draw);Assert.AreEqual(12,ak.Rounds);Assert.AreEqual(50,reserve);ak.CancelReload();Assert.AreEqual(12,ak.Rounds);
        }
    }
}
