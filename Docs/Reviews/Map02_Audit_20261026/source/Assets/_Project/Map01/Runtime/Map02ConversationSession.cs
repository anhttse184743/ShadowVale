using System;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Conversation acceptance only. Actor movement, inventory transfers and rescue arrivals
    /// remain separate gameplay events; no conversation can substitute for those events.
    /// </summary>
    public sealed class Map02ConversationSession
    {
        readonly Map02Progress progress;
        string conversation;
        Map02Stage startedStage;
        int index, count;
        public bool Active => conversation != null;
        public int Index => index;
        public string Conversation => conversation;

        public Map02ConversationSession(Map02Progress progress)
        {
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
        }

        public bool Begin(string id)
        {
            if (Active || progress.failed || !Allowed(id)) return false;
            count = Map02DialogueCatalog.Conversation(id).Length;
            conversation = id;
            startedStage = progress.stage;
            index = 0;
            return true;
        }

        // Called only for a new E press after the current voice has finished.
        // Advancing past the last line is a separate acceptance, not a timer callback.
        public bool Advance(bool currentVoiceFinished)
        {
            if (!Active || !currentVoiceFinished) return false;
            if (progress.failed || progress.stage != startedStage) { Cancel(); return false; }
            if (index + 1 < count) { index++; return true; }
            string completed = conversation;
            Cancel();
            return Commit(completed);
        }

        public void Cancel() { conversation = null; index = count = 0; }

        bool Allowed(string id)
        {
            switch (id)
            {
                case "m2_contact": return progress.stage == Map02Stage.Contact;
                case "m2_family_medicine": return progress.stage == Map02Stage.VillageHelp && progress.medicineDelivered;
                case "m2_scout_order": return progress.stage == Map02Stage.VillageHelp && progress.medicineDelivered && progress.crateStored;
                case "m2_scout_found": return progress.stage == Map02Stage.MissingScout && progress.scoutFound && !progress.scoutSheltered;
                case "m2_prepare": return progress.stage == Map02Stage.MissingScout && progress.scoutSheltered;
                case "m2_encirclement": return progress.stage == Map02Stage.Encirclement;
                case "m2_hold_lane": return progress.stage == Map02Stage.HoldLanes;
                case "m2_family_rescue": return progress.stage == Map02Stage.RescueFamilies;
                case "m2_breakout": return progress.stage == Map02Stage.RescueFamilies && progress.rescuedGroups == 3;
                // The original departure includes living Hùng speaking on the boat.
                // Keep it disabled until the newly approved sacrifice ending is scripted.
                case "m2_departure": return false;
                default: return false;
            }
        }

        bool Commit(string id)
        {
            if (!Allowed(id)) return false;
            switch (id)
            {
                case "m2_contact": return progress.ContactConfirmed();
                case "m2_scout_order": return progress.RequestScoutOrder();
                case "m2_prepare": return progress.ScoutReportConfirmed();
                case "m2_breakout": return progress.BreakoutOrderConfirmed();
                default: return true;
            }
        }
    }
}
