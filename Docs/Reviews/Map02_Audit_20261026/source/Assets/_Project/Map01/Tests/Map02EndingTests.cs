using NUnit.Framework;
using UnityEngine;
namespace ShadowVale.Map01.Tests
{
    public sealed class Map02EndingTests
    {
        [Test] public void SacrificeCannotSkipEvacuationOrTriggerDuringEarlierEscort()
        {
            var p = new Map02Progress { stage = Map02Stage.VillageHelp };
            Assert.IsFalse(p.ConfirmHungSacrifice());
            p.stage = Map02Stage.Breakout;
            Assert.IsFalse(p.ConfirmHungSacrifice());
            p.ClearFireteam();
            Assert.IsFalse(p.ConfirmHungSacrifice());
            p.EvacuateBoats();
            Assert.IsFalse(p.BoardLastBoat());
            Assert.IsTrue(p.ConfirmHungSacrifice());
            Assert.IsFalse(p.failed);
            Assert.IsFalse(p.ConfirmHungSacrifice());
            Assert.IsTrue(p.BoardLastBoat());
        }
        [Test] public void SaveKeepsEndingEventAndDoesNotReplayIt()
        {
            var p = new Map02Progress { stage = Map02Stage.Breakout, fireteamCleared = true, boatsEvacuated = true };
            p.ConfirmHungSacrifice();
            var loaded = JsonUtility.FromJson<Map02Progress>(JsonUtility.ToJson(p));
            Assert.IsTrue(loaded.hungSacrificeConfirmed);
            Assert.IsFalse(loaded.ConfirmHungSacrifice());
            Assert.IsTrue(loaded.BoardLastBoat());
            Assert.IsTrue(loaded.FinishDeparture());
            Assert.IsTrue(loaded.ConfirmSafeReception());
        }
    }
}
