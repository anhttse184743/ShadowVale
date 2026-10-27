using System.Collections.Generic;
using NUnit.Framework;

namespace ShadowVale.Map01.Tests
{
    public sealed class Map02ConversationTests
    {
        [Test] public void ContactRequiresSeparateConfirmationAndCanBeCancelled()
        {
            var p = new Map02Progress();
            var session = new Map02ConversationSession(p);
            Assert.IsTrue(session.Begin("m2_contact"));
            Assert.IsFalse(session.Advance(false));
            Assert.AreEqual(0, session.Index);
            session.Advance(true);
            session.Cancel();
            Assert.AreEqual(Map02Stage.Contact, p.stage);
            Assert.IsTrue(session.Begin("m2_contact"));
            for (int i = 0; i < 3; i++) Assert.IsTrue(session.Advance(true));
            Assert.AreEqual(Map02Stage.Contact, p.stage);
            Assert.IsTrue(session.Active);
            Assert.IsTrue(session.Advance(true));
            Assert.AreEqual(Map02Stage.VillageHelp, p.stage);
            Assert.IsFalse(session.Advance(true));
            Assert.IsFalse(session.Begin("m2_contact"));
        }

        [Test] public void FamilyConversationCannotDeliverMedicineOrRescuePeople()
        {
            var p = new Map02Progress(); p.ContactConfirmed();
            var session = new Map02ConversationSession(p);
            Assert.IsFalse(session.Begin("m2_family_medicine"));
            p.DeliverMedicine();
            Assert.IsTrue(session.Begin("m2_family_medicine"));
            Finish(session);
            Assert.IsFalse(p.crateStored);
            Assert.AreEqual(0, p.rescuedGroups);
            Assert.IsFalse(session.Begin("m2_scout_order"));
            p.StoreCrate();
            Assert.IsTrue(session.Begin("m2_scout_order"));
            Finish(session);
            Assert.AreEqual(Map02Stage.MissingScout, p.stage);
        }

        [Test] public void TalkingToScoutDoesNotTeleportHimToSafety()
        {
            var p = new Map02Progress { stage = Map02Stage.MissingScout };
            var session = new Map02ConversationSession(p);
            Assert.IsFalse(session.Begin("m2_scout_found"));
            p.FindScout();
            Assert.IsTrue(session.Begin("m2_scout_found"));
            Finish(session);
            Assert.IsFalse(p.scoutSheltered);
            Assert.IsFalse(session.Begin("m2_prepare"));
            p.ShelterScout();
            Assert.IsTrue(session.Begin("m2_prepare"));
            Finish(session);
            Assert.AreEqual(Map02Stage.QuietPreparation, p.stage);
        }

        [Test] public void InterruptionOrFailureCannotCommitStaleConversation()
        {
            var p = new Map02Progress();
            var session = new Map02ConversationSession(p);
            Assert.IsFalse(session.Begin("unknown"));
            session.Begin("m2_contact");
            Assert.IsFalse(session.Begin("m2_contact"));
            p.Fail("Dân bị thương đã hy sinh.");
            Assert.IsFalse(session.Advance(true));
            Assert.IsFalse(session.Active);
            Assert.AreEqual(Map02Stage.Contact, p.stage);
        }

        [Test] public void OldDepartureWithLivingHungCannotPlayAfterSacrifice()
        {
            var p = new Map02Progress { stage = Map02Stage.Departure, hungSacrificeConfirmed = true };
            var session = new Map02ConversationSession(p);
            Assert.IsFalse(session.Begin("m2_departure"));
            Assert.AreEqual(Map02Stage.Departure, p.stage);
            Assert.IsFalse(p.ConfirmSafeReception());
        }

        [Test] public void ScriptHasUniqueIdsAndNamNeverUsesRemoteVoice()
        {
            var ids = new HashSet<string>();
            foreach (var group in new[] { "m2_contact", "m2_family_medicine", "m2_scout_order", "m2_scout_found", "m2_prepare", "m2_encirclement", "m2_hold_lane", "m2_family_rescue", "m2_breakout", "m2_departure" })
                foreach (var line in Map02DialogueCatalog.Conversation(group))
                {
                    Assert.IsTrue(ids.Add(line.id), line.id);
                    Assert.IsFalse(string.IsNullOrWhiteSpace(line.text), line.id);
                    if (line.speaker == "Nam") Assert.IsFalse(line.radio, line.id);
                    StringAssert.StartsWith("m2_", line.id);
                }
            Assert.AreEqual(33, ids.Count);
        }

        static void Finish(Map02ConversationSession session)
        {
            for (int i = 0; i < 10 && session.Active; i++) Assert.IsTrue(session.Advance(true));
            Assert.IsFalse(session.Active);
        }
    }
}
