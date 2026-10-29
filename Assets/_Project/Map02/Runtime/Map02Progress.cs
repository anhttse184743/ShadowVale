using System;
namespace ShadowVale.Map01
{
    public enum Map02Stage { Contact, VillageHelp, MissingScout, QuietPreparation, Encirclement, HoldLanes, RescueFamilies, Breakout, Departure, Reception, Complete }
    /// <summary>Map 2 story rules, independent of Map 1 stage IDs and rescue actors.</summary>
    [Serializable] public sealed class Map02Progress
    {
        public int version=2;
        public Map02Stage stage;
        public bool medicineDelivered,crateStored,scoutFound,scoutSheltered,weakGroupGathered,southStockMoved,boatsChecked;
        public bool firstWaveCleared,secondWaveCleared,frontGroupSafe,fireteamCleared,boatsEvacuated;
        public bool hungSacrificeConfirmed;
        public int rescuedGroups;
        public bool failed;
        public string failureCause;
        public bool ContactConfirmed(){if(!At(Map02Stage.Contact))return false;stage=Map02Stage.VillageHelp;return true;}
        public bool DeliverMedicine(){if(!At(Map02Stage.VillageHelp)||medicineDelivered)return false;medicineDelivered=true;return true;}
        public bool StoreCrate(){if(!At(Map02Stage.VillageHelp)||crateStored)return false;crateStored=true;return true;}
        public bool RequestScoutOrder(){if(!At(Map02Stage.VillageHelp)||!medicineDelivered||!crateStored)return false;stage=Map02Stage.MissingScout;return true;}
        public bool FindScout(){if(!At(Map02Stage.MissingScout)||scoutFound)return false;scoutFound=true;return true;}
        public bool ShelterScout(){if(!At(Map02Stage.MissingScout)||!scoutFound||scoutSheltered)return false;scoutSheltered=true;return true;}
        public bool ScoutReportConfirmed(){if(!At(Map02Stage.MissingScout)||!scoutSheltered)return false;stage=Map02Stage.QuietPreparation;return true;}
        public bool GatherWeakGroup(){if(!At(Map02Stage.QuietPreparation)||weakGroupGathered)return false;weakGroupGathered=true;return true;}
        public bool MoveSouthStock(){if(!At(Map02Stage.QuietPreparation)||southStockMoved)return false;southStockMoved=true;return true;}
        public bool CheckBoats(){if(!At(Map02Stage.QuietPreparation)||boatsChecked)return false;boatsChecked=true;return true;}
        public bool BeginEncirclement(){if(!At(Map02Stage.QuietPreparation)||!weakGroupGathered||!southStockMoved||!boatsChecked)return false;stage=Map02Stage.Encirclement;return true;}
        public bool FinishEncirclement(){if(!At(Map02Stage.Encirclement))return false;stage=Map02Stage.HoldLanes;return true;}
        public bool ClearWave(int wave){if(!At(Map02Stage.HoldLanes))return false;if(wave==0&&!firstWaveCleared){firstWaveCleared=true;return true;}if(wave==1&&firstWaveCleared&&!secondWaveCleared){secondWaveCleared=true;return true;}return false;}
        public bool FrontGroupReachedSafety(){if(!At(Map02Stage.HoldLanes)||frontGroupSafe)return false;frontGroupSafe=true;return true;}
        public bool WithdrawToInnerLane(){if(!At(Map02Stage.HoldLanes)||!firstWaveCleared||!secondWaveCleared||!frontGroupSafe)return false;stage=Map02Stage.RescueFamilies;return true;}
        public bool RescueGroup(int group){if(!At(Map02Stage.RescueFamilies)||group<0||group>1||(rescuedGroups&(1<<group))!=0)return false;rescuedGroups|=1<<group;return true;}
        public bool BreakoutOrderConfirmed(){if(!At(Map02Stage.RescueFamilies)||rescuedGroups!=3)return false;stage=Map02Stage.Breakout;return true;}
        public bool ClearFireteam(){if(!At(Map02Stage.Breakout)||fireteamCleared)return false;fireteamCleared=true;return true;}
        public bool EvacuateBoats(){if(!At(Map02Stage.Breakout)||!fireteamCleared||boatsEvacuated)return false;boatsEvacuated=true;return true;}
        // Set by the south-dock cinematic at its actual death beat (or the same skip result).
        // This story sacrifice is distinct from an escort failure earlier in the chapter.
        public bool ConfirmHungSacrifice(){if(!At(Map02Stage.Breakout)||!fireteamCleared||!boatsEvacuated||hungSacrificeConfirmed)return false;hungSacrificeConfirmed=true;return true;}
        public bool BoardLastBoat(){if(!At(Map02Stage.Breakout)||!fireteamCleared||!boatsEvacuated||!hungSacrificeConfirmed)return false;stage=Map02Stage.Departure;return true;}
        public bool FinishDeparture(){if(!At(Map02Stage.Departure))return false;stage=Map02Stage.Reception;return true;}
        public bool ConfirmSafeReception(){if(!At(Map02Stage.Reception))return false;stage=Map02Stage.Complete;return true;}
        public bool Fail(string cause){if(failed||stage==Map02Stage.Complete)return false;failed=true;failureCause=cause;return true;}
        public bool IsCinematic=>stage==Map02Stage.Encirclement||stage==Map02Stage.Departure||stage==Map02Stage.Reception;
        public bool MaySave=>!failed&&!IsCinematic;
        bool At(Map02Stage expected)=>!failed&&stage==expected;
    }
}
