using System.Linq;
using ShadowVale.AI.Boss;
using ShadowVale.Core.Events;
using ShadowVale.Data.Content;
using ShadowVale.Gameplay.Combat;
using ShadowVale.Gameplay.Enemies.Boss;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>Commander-led rescue, supply recovery, communications disruption and extraction.</summary>
    public sealed class Map01Quest : MonoBehaviour, IMapObjectives
    {
        // Save-format IDs. The former scouting/camp/report IDs now carry distinct operations.
        public const int RescueStage=0, EscortStage=1, BriefingStage=2, ScoutStage=3, ReportScoutStage=4,
            CampsStage=5, ReportCampsStage=6, BossStage=7, ReportBossStage=8, CompleteStage=9,
            ExtractionStage=10, BoardingStage=11, LastStage=BoardingStage;

        public bool IsComplete => Stage == CompleteStage;

        /// <summary>Where the objective marker points for each stage (the story's own targets come first,
        /// in Map01ObjectiveGuide).</summary>
        public bool TryGetObjectiveTarget(out Vector3 target, out string label)
        {
            target = default; label = null;
            switch (Stage)
            {
                case ExtractionStage:
                    var extraction = mission.GetComponentInChildren<Map01Extraction>();
                    if (extraction == null) return false;
                    target = extraction.ObjectivePosition; label = "NORTHERN JETTY"; return true;
                case RescueStage:
                    target = mission.hung.position; label = "BẾN TÀU — CỨU HÙNG";
                    return true;
                case ScoutStage:
                    // Only the rough area of the nearest camp not yet logged — never the camp itself.
                    var scoutingOrder = GetComponent<Map01Scouting>();
                    var zone = scoutingOrder == null ? null : scoutingOrder.Camps.Where(c => !c.Found)
                        .OrderBy(c => (c.ZoneCenter - mission.player.position).sqrMagnitude).FirstOrDefault();
                    if (zone == null) return false;
                    target = zone.ZoneCenter; label = "KHU VỰC NGHI NGỜ";
                    return true;
                case EscortStage:
                    var rescueRun = mission.GetComponent<Map01Rescue>();
                    if (rescueRun == null || rescueRun.Layout == null) return false;
                    target = rescueRun.Layout.shelterDoor.position; label = "HẦM TRÚ ẨN — ĐƯA HÙNG VỀ";
                    return true;
                case CampsStage:
                    var guard = mission.Enemies.Where(e => e.Alive && e.name.StartsWith("Outpost guard "))
                        .OrderBy(e => (e.transform.position - mission.player.position).sqrMagnitude).FirstOrDefault();
                    if (guard == null) return false;
                    target = guard.transform.position; label = "DOANH TRẠI ĐỊCH";
                    return true;
                case BossStage:
                    var commander = mission.Enemies.FirstOrDefault(e => e.IsBoss && e.Alive);
                    if (commander == null) return false;
                    target = commander.transform.position; label = "CHỈ HUY ĐỊCH";
                    return true;
                default:
                    return false; // The complete stage has nowhere left to go.
            }
        }
        public static readonly string[] Objectives = {
            "Giải cứu Hùng tại bến phía bắc: giữ im lặng khi giám sát còn canh Hùng; hạ đủ bốn lính rồi cởi trói [E]",
            "Yểm hộ Hùng về hầm trú ẩn, vượt qua ba đợt truy kích",
            "Gặp chỉ huy tại căn cứ để báo cáo và nhận lệnh [E]",
            "Điểm 1: hạ bốn lính bảo vệ, lấy chìa mở kho rồi giải khóa hòm tiếp tế [E]",
            "Báo cáo đã lấy hàng qua đàm, mang túi về hầm và bàn giao chỉ huy [E]",
            "Điểm 2: hạ năm lính bảo vệ, đấu bảng mạch để vô hiệu hóa đài [E]",
            "Rời giao tranh rồi báo cáo đài đã bị vô hiệu với chỉ huy [E]",
            "Điểm 3: đứng xa doanh trại ít nhất 18 m, giữ ống nhòm [F] nhìn về khu trại rồi báo cáo",
            "Sĩ quan đã bị hạ. Thoát giao tranh rồi báo cáo để nhận đường rút [E]",
            "Đã rời bến. Tiếp tục tới điểm liên lạc ở làng ven sông",
            "Tới bến phía bắc, lên thuyền cùng Hùng và chỉ huy",
            "Lên thuyền và rút khỏi khu rừng"
        };
        public static readonly string[] SaveLocations = {
            "Giải cứu Hùng ở bến tàu", "Hộ tống về hầm", "Báo cáo chỉ huy", "Điểm 1 — tiếp tế", "Mang tiếp tế về hầm",
            "Điểm 2 — đài liên lạc", "Báo cáo qua bộ đàm", "Điểm 3 — chỉ huy địch", "Lệnh rút lui", "Đã rời bến", "Bến phía bắc", "Lên thuyền"
        };
        // Kept for old menu/test callers; binoculars are now a tool, not a forced perfect-stealth mission.
        public const string ScoutOrder="Chỉ huy: Hạ nhóm bảo vệ điểm một, lấy lại thuốc rồi mang về hầm.";
        /// <summary>Stable ids of the stages, for telemetry's progression funnel (index = stage number).
        /// The ids keep their names across the operations rework: they are part of the research data's format.</summary>
        public static readonly string[] StageIds = {
            "rescue_hung", "escort_hung", "briefing", "scout_camps", "report_scouting",
            "destroy_camps", "report_camps", "defeat_commander", "report_commander", "complete",
            "extraction", "boarding"
        };

        /// <summary>Where a stage falls in play: the extraction and the boarding (appended to the
        /// checkpoint format as 10 and 11) come after the commander and before Complete (9).</summary>
        public static int PlayOrder(int stage) => stage == CompleteStage ? int.MaxValue : stage;

        /// <summary>Moving forward completes the stage left behind (telemetry <c>objective_completed</c>);
        /// restoring a checkpoint does not.</summary>
        public int Stage
        {
            get => stage;
            private set
            {
                int from = stage;
                stage = value;
                if (PlayOrder(value) <= PlayOrder(from) || Map01SaveSystem.IsRestoring) return;
                EventBus.Publish(new ObjectiveCompletedEvent(Map01Mission.MapId, StageIds[Mathf.Clamp(from, 0, LastStage)], from));
                if (value == CompleteStage) EventBus.Publish(new MissionEndedEvent(Map01Mission.MapId, "completed"));
            }
        }
        private int stage;
        public bool RescueOrderAccepted => story != null && story.Progress.rescueAccepted;
        public bool AwaitingReport => Stage == BriefingStage;
        public bool HungInRange { get; private set; }
        public string HungPrompt => rescue.CanFree ? "[E] Cởi trói Hùng" : $"Cần hạ hết bốn lính — còn {rescue.Remaining}";
        public bool BaseResupplyOpen => Stage > EscortStage;
        public bool BossSpawned => boss != null;
        public Map01EnemyController CommanderEnemy => boss;
        public string ObjectiveText {
            get {
                if (Stage == RescueStage && !RescueOrderAccepted) return "Gặp chỉ huy tại hầm để nhận nhiệm vụ cứu Hùng [E]";
                if (Stage == RescueStage && rescue != null) {
                    if (rescue.CanFree) return "Đã hạ đủ bốn lính. Cởi trói Hùng [E].";
                    return Objectives[0]+$" — còn {rescue.Remaining}/4 lính; "+(!rescue.OverseerAlive?"đã hạ giám sát, có thể giao chiến":rescue.CanExecuteCaptive?"giám sát đang canh Hùng":"giám sát đã rời Hùng");
                }
                if (story!=null && story.Objective!=null) return story.Objective;
                return Objectives[Mathf.Clamp(Stage,0,LastStage)];
            }
        }
        Map01Mission mission;
        Map01Rescue rescue;
        Map01Story story;
        NavMeshAgent companion;
        Map01EnemyController boss;
        void Awake()
        {
            mission=GetComponent<Map01Mission>();
            if(GetComponent<Map01Scouting>()==null)gameObject.AddComponent<Map01Scouting>();
            rescue=GetComponent<Map01Rescue>();if(rescue==null)rescue=gameObject.AddComponent<Map01Rescue>();
            story=GetComponent<Map01Story>();if(story==null)story=gameObject.AddComponent<Map01Story>();
        }
        void Start(){if(mission.IsInitialized)companion=mission.hung.GetComponent<NavMeshAgent>();}
        public bool ShouldFollowPlayer()=>Stage==EscortStage&&rescue.CurrentPhase==Map01Rescue.Phase.Escorting;
        bool NearHung(float distance)=>mission.hung!=null&&Vector3.Distance(mission.player.position,mission.hung.position)<=distance;
        public void TalkToHung(){if(Stage==RescueStage&&!mission.Cinematic)TryRescueHung();}
        public bool CanTreatHung=>rescue!=null&&rescue.CanFree;
        public void TryRescueHung()
        {
            if(Stage!=RescueStage||!NearHung(mission.Settings.interactRange))return;
            if(!rescue.CanFree){mission.Say($"Hạ hết bốn lính trước khi cởi trói Hùng. Còn {rescue.Remaining} tên.",3);return;}
            rescue.BeginFree();
        }
        public void BeginEscort(){if(Stage==RescueStage)Stage=EscortStage;}
        public void FinishRescueDelivery()
        {
            if(Stage!=EscortStage)return;Stage=BriefingStage;
            if(companion!=null&&companion.isOnNavMesh){companion.ResetPath();companion.isStopped=true;}
        }
        public bool TryDeliverSupplies()=>false;
        public void BeginSupplyOrder(){if(Stage==BriefingStage){Stage=ScoutStage;mission.Say("Điểm 1: thu hồi thuốc và vật tư tiếp tế [E].",6);}}
        public void SuppliesRecovered(){if(Stage==ScoutStage){Stage=ReportScoutStage;mission.Say("Đã thu hồi vật tư. Rời giao tranh rồi liên lạc với chỉ huy [E].",6);}}
        public void BeginRadioOrder(){if(Stage==ReportScoutStage&&story.Progress.suppliesDelivered&&story.Progress.supplyReported)Stage=CampsStage;}
        public void RadioDisabled(){if(Stage==CampsStage){Stage=ReportCampsStage;mission.Say("Đài liên lạc đã ngừng hoạt động. Rút khỏi trại rồi báo cáo chỉ huy [E].",6);}}
        public void BeginCommanderOrder()
        {
            if(Stage!=ReportCampsStage||!story.Progress.radioDisabled)return;Stage=BossStage;EnsureCommander();
            // The scouts' rifle (Map01C3Mission): no voiced line yet, so the order's last word is shown, not spoken.
            if(GetComponent<Map01C3Mission>()?.CachePoint!=null)
                mission.Say("Chỉ huy (bộ đàm): \"Trinh sát để lại một khẩu Mosin, năm viên, ở vai đồi phía Đông Nam điểm ba. Từ đó nhìn được chòi canh.\"",8);
        }
        // Scanning a camp provides intel only. It cannot complete supply/radio objectives.
        public void CompleteScouting()=>mission.Say("Đã ghi nhận ba vị trí. Tiếp tục mục tiêu chỉ huy đã giao.",5);
        void Update(){if(mission.IsInitialized)HungInRange=Stage==RescueStage&&RescueOrderAccepted&&!mission.Stopped&&NearHung(mission.Settings.interactRange);}

        public void RestoreStage(int value)=>RestoreStoryStage(value,null);
        public void RestoreStoryStage(int value,Map01Story.Snapshot snapshot)
        {
            story.Restore(snapshot,value);
            // The field, not the setter: restoring is not progress (no objective_completed events).
            stage=snapshot==null&&value==ReportBossStage?CompleteStage:value==BoardingStage?ExtractionStage:Mathf.Clamp(value,0,LastStage);
            rescue?.SyncRestoredStage(Stage);
            EnsureCommander();
            if(Stage==ExtractionStage)Map01Extraction.Get(mission)?.Begin(false);
            if(Stage==ReportBossStage&&story.Progress.retreatReported)CompleteBossDefeat(false);
        }
        /// <summary>Exists at the third outpost from map start, never materializes beside a corpse.</summary>
        public void EnsureCommander()
        {
            if(boss!=null||story.Layout==null||story.Layout.commanderPost==null||mission.Enemies.Length==0)return;
            var template=mission.Enemies.Where(e=>e!=null&&e.name.StartsWith("Outpost guard "))
                .OrderBy(e=>e.SaveId,System.StringComparer.Ordinal).FirstOrDefault();
            if(template==null)return;
            var marker=story.Layout.commanderPost;
            var instance=Instantiate(template.gameObject,marker.position,marker.rotation,story.Layout.transform);
            instance.name="Chỉ huy địch";
            boss=instance.GetComponent<Map01EnemyController>();boss.Configure(System.Array.Empty<Vector3>());
            boss.operationIndex=3;boss.carriesSupplyKey=false;boss.suppressOperationLoot=false;
            boss.BindMission(mission); // Before ConfigureAsBoss: binding resets damage to the balance baseline.
            // The boss_commander archetype (Map01Content.json): his boss profile, and his health (600) rather
            // than a guard's x3, so he lives long enough for the men he calls to arrive.
            var archetype=mission.Bundle?.enemy_archetypes?.FirstOrDefault(a=>a!=null&&a.is_boss);
            var profile=archetype?.boss!=null?archetype.boss.ToProfile():new BossProfile();
            boss.ConfigureAsBoss(profile);
            var bossHealth=instance.GetComponent<Health>();
            if(archetype!=null&&archetype.max_hp>0)bossHealth.SetMaxHealth(archetype.max_hp);
            mission.AddEnemy(boss);
            // Relocation, grenades, the pincer and the call for reinforcements (A04), from the same profile. At C3 the
            // call is answered by the command post itself (Map01C3Reinforcements: its men not yet fighting, the kẻng's
            // sleepers, the runners down the phone line), and he fights from its cover, never off into the forest.
            var c3=GetComponent<Map01C3Mission>();
            IReinforcementProvider men=c3!=null?new Map01C3Reinforcements(c3):new Map01Reinforcements(mission,template);
            var behaviour=BossBehaviour.Attach(instance,boss,profile,men,seed:1);
            behaviour.Suspended=()=>mission.Stopped;
            if(c3!=null){
                c3.AutoCallAtHalfHealth=false; // BossBehaviour makes the call now (reinforce_hp_threshold).
                behaviour.AllowedPosition=p=>c3.Data==null||c3.InsideFence(p);
                boss.ConfineMoves=c3.ClampIntoFence; // Without cover in reach the FSM closes in on Nam: up to the fence.
            }
            bossHealth.onDied.AddListener(OnBossDown);
        }
        public void CompleteBossDefeat(bool showRadio=true)
        {
            if(!story.Progress.retreatReported)return;
            if(Stage!=ReportBossStage || !story.CommanderObjectiveComplete)return;Stage=ExtractionStage;
            Map01Extraction.Get(mission)?.Begin(showRadio&&!Map01SaveSystem.IsRestoring);
        }
        public void BeginBoarding(){if(Stage==ExtractionStage)Stage=BoardingStage;}
        public void CompleteExtraction(){if(Stage==BoardingStage)Stage=CompleteStage;}
        void OnBossDown()
        {
            story.Progress.commanderKilled=true;
            TryCompleteCommanderObjective(true);
        }
        public void TryCompleteCommanderObjective(bool playDeath=false)
        {
            if(Stage!=BossStage || !story.CommanderObjectiveComplete)return;
            Stage=ReportBossStage;
            // A body inspected after an earlier fight must not replay its death animation.
            if(!playDeath){CompleteBossDefeat(!Map01SaveSystem.IsRestoring);return;}
            if(Map01SaveSystem.IsRestoring||boss==null||!boss.gameObject.activeInHierarchy){CompleteBossDefeat(false);return;}
            var ending=GetComponent<Map01EndingCutscene>();if(ending==null)ending=gameObject.AddComponent<Map01EndingCutscene>();
            if(!ending.Begin(mission,boss))CompleteBossDefeat();
        }
    }
}
