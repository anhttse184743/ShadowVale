using System;

namespace ShadowVale.Map01
{
    public enum Map02Work { Contact, MedicinePickup, MedicineDelivery, CratePickup, CrateDelivery, ScoutOrder, ScoutAid, ScoutReport, GatherPeople, SouthStockPickup, SouthStockDelivery, CheckBoats, InnerLane, RescueFamily, RescueNeighbours, LastBoat }

    /// <summary>Object tasks and cargo transactions. Carries no Map 1 stage or loot identifiers.</summary>
    [Serializable]
    public sealed class Map02WorldState
    {
        public int version = 1;
        public Map02Progress story = new Map02Progress();
        public bool medicineTaken, medicineCarried, crateTaken, crateCarried, southStockTaken, southStockCarried;

        public bool Available(Map02Work work)
        {
            if (story == null || story.failed) return false;
            switch (work)
            {
                case Map02Work.Contact: return story.stage == Map02Stage.Contact;
                case Map02Work.MedicinePickup: return story.stage == Map02Stage.VillageHelp && !medicineTaken;
                case Map02Work.MedicineDelivery: return story.stage == Map02Stage.VillageHelp && medicineCarried && !story.medicineDelivered;
                case Map02Work.CratePickup: return story.stage == Map02Stage.VillageHelp && !crateTaken;
                case Map02Work.CrateDelivery: return story.stage == Map02Stage.VillageHelp && crateCarried && !story.crateStored;
                case Map02Work.ScoutOrder: return story.stage == Map02Stage.VillageHelp && story.medicineDelivered && story.crateStored;
                case Map02Work.ScoutAid: return story.stage == Map02Stage.MissingScout && !story.scoutFound;
                case Map02Work.ScoutReport: return story.stage == Map02Stage.MissingScout && story.scoutSheltered;
                case Map02Work.GatherPeople: return story.stage == Map02Stage.QuietPreparation && !story.weakGroupGathered;
                case Map02Work.SouthStockPickup: return story.stage == Map02Stage.QuietPreparation && !southStockTaken;
                case Map02Work.SouthStockDelivery: return story.stage == Map02Stage.QuietPreparation && southStockCarried && !story.southStockMoved;
                case Map02Work.CheckBoats: return story.stage == Map02Stage.QuietPreparation && !story.boatsChecked;
                case Map02Work.InnerLane: return story.stage == Map02Stage.HoldLanes && story.firstWaveCleared && story.secondWaveCleared && story.frontGroupSafe;
                case Map02Work.RescueFamily: return story.stage == Map02Stage.RescueFamilies && (story.rescuedGroups & 1) == 0;
                case Map02Work.RescueNeighbours: return story.stage == Map02Stage.RescueFamilies && (story.rescuedGroups & 2) == 0;
                case Map02Work.LastBoat: return story.stage == Map02Stage.Breakout && story.fireteamCleared && story.boatsEvacuated && story.hungSacrificeConfirmed;
                default: return false;
            }
        }

        public bool Transfer(Map02Work work)
        {
            if (!Available(work)) return false;
            switch (work)
            {
                case Map02Work.MedicinePickup: medicineTaken = medicineCarried = true; return true;
                case Map02Work.MedicineDelivery:
                    if (!story.DeliverMedicine()) return false;
                    medicineCarried = false; return true;
                case Map02Work.CratePickup: crateTaken = crateCarried = true; return true;
                case Map02Work.CrateDelivery:
                    if (!story.StoreCrate()) return false;
                    crateCarried = false; return true;
                case Map02Work.SouthStockPickup: southStockTaken = southStockCarried = true; return true;
                case Map02Work.SouthStockDelivery:
                    if (!story.MoveSouthStock()) return false;
                    southStockCarried = false; return true;
                default: return false;
            }
        }

        public string Objective
        {
            get
            {
                if (story.failed) return story.failureCause;
                switch (story.stage)
                {
                    case Map02Stage.Contact: return "Tới nhà liên lạc, nhấn E để bắt liên lạc với lực lượng trong làng.";
                    case Map02Stage.VillageHelp: return "Đưa thuốc tới gia đình cuối xóm và chuyển kiện tiếp tế vào kho. Xong việc, trở lại nhà liên lạc.";
                    case Map02Stage.MissingScout: return story.scoutSheltered ? "Trở lại nhà liên lạc, báo cáo đã đưa trinh sát về." : story.scoutFound ? "Đưa trinh sát bị thương về điểm trú an toàn." : "Tìm trinh sát mất liên lạc gần cầu và đường ruộng.";
                    case Map02Stage.QuietPreparation: return "Tập kết người cần giúp đỡ, chuyển vật tư xuống bến nam, kiểm tra thuyền. Có thể chọn thứ tự.";
                    case Map02Stage.Encirclement: return "Địch đang khép vòng. Nghe lệnh rút dân.";
                    case Map02Stage.HoldLanes: return "Giữ ngõ qua hai đợt tấn công, bảo vệ đoàn dân tới chỗ an toàn rồi lùi vào chốt trong.";
                    case Map02Stage.RescueFamilies: return "Mở lối và đưa hai nhóm dân xuống điểm tập kết phía nam.";
                    case Map02Stage.Breakout: return "Mở tuyến bờ ruộng ra bến nam, bảo vệ các chuyến sơ tán và chuyến cuối.";
                    case Map02Stage.Departure: return "Rời bến nam theo đoàn thuyền.";
                    case Map02Stage.Reception: return "Kiểm tra đoàn người tại nơi tiếp nhận.";
                    case Map02Stage.Complete: return "Đoàn đã tới nơi an toàn. Hùng đã hy sinh ở bến nam để yểm trợ chuyến cuối.";
                    default: return "";
                }
            }
        }
    }
}
