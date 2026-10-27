using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using L=ShadowVale.Map01.Map01StoryDialogue.Line;

namespace ShadowVale.Map01
{
    public sealed partial class Map01Story:MonoBehaviour
    {
        public const string KeyItem="m1_supply_key", CargoItem="m1_supply_cargo";
        [Serializable] public sealed class Snapshot
        {
            public int revision=5;
            public bool powerDisabled;
            public int powerSeed,powerStep;
            public bool rescueAccepted,supplies,radioDisabled,commanderConfirmed,commanderKilled,commanderAreaReached;
            public bool keyCollected,doorUnlocked,chestUnlocked,supplyReported,suppliesDelivered,supplyRewardGranted;
            public bool radioResponseCalled,radioResponseSpawned,commanderReported,commanderDiscovery,retreatReported;
            public int puzzleSeed,lockAttemptStart;
            public int circuitSeed,circuitRound,circuitRevision;
            public Map01LockGuess[] lockHistory=Array.Empty<Map01LockGuess>();
            public int[] circuitRotations;
        }
        public Snapshot Progress{get;private set;}=new();
        public Map01OperationsLayout Layout{get;private set;}
        public Transform Commander=>opening!=null?opening.CommanderTransform:null;
        public string Prompt{get;private set;}
        public bool Operating{get;private set;}
        public float ObservationProgress=>Layout!=null?Mathf.Clamp01(confirmTime/Mathf.Max(.1f,Mathf.Min(1.2f,Layout.observationSeconds))):0;
        public string ObservationHint{get;private set;}
        public bool SafeToReport=>mission!=null&&Hunters==0;
        /// <summary>Enemies still after Nam: fighting, or searching within 35 m. While any are, he cannot radio in.</summary>
        public int Hunters=>mission==null?0:mission.Enemies.Count(e=>e!=null&&e.Alive&&(e.Engaged||e.Alerted&&Vector3.Distance(e.transform.position,mission.player.position)<35));
        /// <summary>After the officer falls, Nam radios for the way out by himself once clear of the fight this long (E still does it at once).</summary>
        public const float AutoRetreatReportSeconds=3f;
        float retreatClearFor;
        public bool CanBrief=>quest.Stage==0&&!Progress.rescueAccepted||quest.Stage==2;
        public bool RadioReport=>quest.Stage==4&&!Progress.supplyReported||quest.Stage==6||quest.Stage==8;
        public bool CanDeliver=>quest.Stage==4&&Progress.supplyReported&&!Progress.suppliesDelivered;
        public bool CommanderObjectiveComplete=>Progress.supplies&&Progress.suppliesDelivered&&Progress.radioDisabled&&Progress.powerDisabled&&Progress.commanderAreaReached&&Progress.commanderConfirmed&&Progress.commanderReported&&Progress.commanderKilled;
        Map01Mission mission; Map01Quest quest; Map01OpeningCutscene opening; Map01StoryDialogue dialogue;
        Map01OperationPuzzleUI puzzleUI;float confirmTime;Vector3 previousObservationPosition;bool checkpointPending;
        void Awake()
        {
            mission=GetComponent<Map01Mission>();quest=GetComponent<Map01Quest>();
            dialogue=GetComponent<Map01StoryDialogue>();if(dialogue==null)dialogue=gameObject.AddComponent<Map01StoryDialogue>();
            puzzleUI=GetComponent<Map01OperationPuzzleUI>();if(puzzleUI==null)puzzleUI=gameObject.AddComponent<Map01OperationPuzzleUI>();
        }
        public void Initialize()
        {
            opening=GetComponentInChildren<Map01OpeningCutscene>();Layout=FindFirstObjectByType<Map01OperationsLayout>();
            if(Layout==null){var prefab=Resources.Load<Map01OperationsLayout>("Story/Map01Operations");if(prefab!=null)Layout=Instantiate(prefab);}
            if(Layout==null){Debug.LogError("[Map01] Missing operations layout.",this);return;}
            Progress.puzzleSeed=NewPuzzleSeed();RefreshProps();Map01InteractionWarmup.For(mission);
            Map01PatrolBeats.Apply(Layout); // Supply and radio guards walk fixed beats; a checkpoint restored later keeps its own positions.
        }
        public Snapshot Capture()=>JsonUtility.FromJson<Snapshot>(JsonUtility.ToJson(Progress));
        public void Restore(Snapshot snapshot,int stage)
        {
            Progress=snapshot!=null?JsonUtility.FromJson<Snapshot>(JsonUtility.ToJson(snapshot)):new Snapshot{revision=0};
            if(Progress.revision<1){Progress.rescueAccepted=stage>0;Progress.supplies=stage>=4;Progress.radioDisabled=stage>=6;Progress.commanderConfirmed=stage>=8;Progress.commanderKilled=stage>=8;}
            if(Progress.revision<3){
                Progress.keyCollected=Progress.doorUnlocked=Progress.chestUnlocked=Progress.supplies;Progress.supplyRewardGranted=Progress.supplies;
                Progress.supplyReported=Progress.suppliesDelivered=stage>=5;
                Progress.commanderReported=Progress.commanderDiscovery=Progress.commanderConfirmed;Progress.retreatReported=stage>=8;
                if(Progress.revision<2&&Progress.commanderConfirmed)Progress.commanderAreaReached=true;
            }
            if(stage>=8){Progress.supplies=Progress.suppliesDelivered=Progress.supplyReported=Progress.supplyRewardGranted=true;Progress.radioDisabled=Progress.commanderConfirmed=Progress.commanderReported=Progress.commanderKilled=Progress.commanderAreaReached=true;}
            // Keep an in-progress lock and its notebook from old saves. Unstarted
            // fixed-seed saves are upgraded to a new code instead of repeating 331.
            if(Progress.puzzleSeed==0 || Progress.revision<4&&(Progress.lockHistory==null||Progress.lockHistory.Length==0)&&!Progress.chestUnlocked)
                Progress.puzzleSeed=NewPuzzleSeed();
            if(stage>=6)Progress.powerDisabled=true;
            Progress.revision=5;confirmTime=0;RefreshProps();
        }
        public void AfterInventoryRestore()
        {
            var bag=GetComponent<Map01Inventory>();
            if(Progress.supplies&&!Progress.suppliesDelivered&&bag.Count(CargoItem)==0)bag.Add(CargoItem,1);
            if(Progress.keyCollected&&!Progress.doorUnlocked&&bag.Count(KeyItem)==0)bag.Add(KeyItem,1);RefreshProps();
        }
        void RefreshProps()
        {
            if(Layout==null)return;Layout.Present(Progress.supplies,Progress.radioDisabled);
            if(Layout.sabotage!=null){Layout.sabotage.Present(Progress.radioDisabled,Progress.powerDisabled);if(Progress.radioDisabled&&Layout.radioDisabled!=null)Layout.radioDisabled.SetActive(false);}
            if(Layout.supplyProps!=null)Layout.supplyProps.Present(Progress.doorUnlocked,Progress.chestUnlocked,Progress.supplies,Progress.suppliesDelivered,mission!=null?mission.player:null);
        }
        bool Near(Transform t,float range)=>t!=null&&Vector3.Distance(mission.player.position,t.position)<=range;
        bool At(Transform t)=>Layout!=null&&Near(t,Layout.interactionRange);
        public Map01EnemyController[] Guards(int point)=>Layout==null?Array.Empty<Map01EnemyController>():
            (point==1?Layout.supplyGuards:point==2?Layout.radioGuards:Layout.commanderGuards)??Array.Empty<Map01EnemyController>();
        public int RemainingGuards(int point)=>Guards(point).Count(e=>e!=null&&e.Alive);
        /// <summary>1 or 2 for a guard of point 1 or 2, else 0.</summary>
        public int PointOf(Map01EnemyController guard)=>Array.IndexOf(Guards(1),guard)>=0?1:Array.IndexOf(Guards(2),guard)>=0?2:0;
        /// <summary>
        /// A guard of point 1 or 2 found a comrade's body (Map01BodyDiscovery): the point's men search round it and stay
        /// wary. At point 2, while the radio still works, they call the response squad, as a wrong circuit does.
        /// </summary>
        public void OnBodyFound(int point,Map01EnemyController finder,Vector3 at)
        {
            Map01BodyDiscovery.Search(mission,finder,at,Guards(point),"Địch phát hiện xác đồng đội — chúng đang lục soát quanh đó.");
            if(point==2&&!Progress.radioDisabled)CallRadioResponse("Địch ở trạm đài gọi bộ đàm báo có người bị hạ — quân phản ứng đang tới.",finder);
        }
        /// <summary>The radio station's alarm, once: its bell, and the response squad on its way. <paramref name="caller"/>
        /// set: a guard's radio call, read only within earshot.</summary>
        void CallRadioResponse(string line,Map01EnemyController caller=null)
        {
            if(Progress.radioResponseCalled||Layout==null)return;
            Progress.radioResponseCalled=true;Map01OperationAudio.Play("radio_alarm",Layout.radio.position,.75f);
            if(caller==null)mission.Say(line,5);else Map01BodyDiscovery.Shout(mission,caller,line);
            TrySpawnResponse();
        }
        public int ResponseRemaining=>mission.Enemies.Count(e=>e!=null&&e.name.StartsWith("operation_radio_response_",StringComparison.Ordinal)&&e.Alive);
        public bool ResponsePending=>Progress.radioResponseCalled&&(!Progress.radioResponseSpawned||ResponseRemaining>0);
        public string Objective
        {
            get{
                if(quest.Stage==3){
                    if(RemainingGuards(1)>0)return $"Điểm 1: hạ nhóm bảo vệ kho — còn {RemainingGuards(1)}/4 lính. Tìm chìa trên lính gác kho.";
                    if(!Progress.keyCollected)return "Lục xác lính gác kho [E] để lấy chìa khóa.";
                    if(!Progress.doorUnlocked)return "Đã có chìa. Tới cửa kho và mở khóa [E].";
                    if(!Progress.chestUnlocked)return "Vào kho, giải khóa ba vòng số trên hòm tiếp tế [E].";
                    return "Mở hòm và lấy túi tiếp tế của Hùng [E].";
                }
                if(quest.Stage==4)return Progress.supplyReported?"Mang túi tiếp tế về hầm, gặp chỉ huy để bàn giao [E].":"Đã lấy túi thuốc. Tìm chỗ an toàn và báo cáo qua bộ đàm [E].";
                if(quest.Stage==5){
                    if(ResponsePending)return Progress.radioResponseSpawned?$"Báo động tại đài! Hạ nhóm phản ứng — còn {ResponseRemaining}/3 lính.":"Có quân đang tới kiểm tra trạm đài. Chuẩn bị chiến đấu.";
                    if(RemainingGuards(2)>0)return $"Điểm 2: hạ nhóm bảo vệ trạm đài — còn {RemainingGuards(2)}/5 lính.";
                    if(Progress.radioDisabled)return "Đài đã hỏng. Tới bộ nguồn cạnh trạm và ngắt ba cầu chì [E] để chúng không sửa lại được.";
                    return "Mở bảng mạch đài [E]. Xoay 20 đoạn dây để nối NGUỒN tới NGẮT ĐÀI thành một đường liền mạch.";
                }
                if(quest.Stage==6)return "Đài đã ngừng phát. Rời giao tranh rồi báo cáo chỉ huy qua bộ đàm [E].";
                if(quest.Stage==7){
                    var c3=GetComponent<Map01C3Mission>();
                    if(Progress.commanderReported)return c3!=null&&c3.Alarmed?"Trại C3 báo động! Dùng vật che, hạ sĩ quan tại điểm 3 rồi rút.":"Lẻn vào nhà chỉ huy C3 và hạ sĩ quan. Tùy chọn: hạ lính tháp canh, tháo dùi kẻng ở cổng, cắt dây điện thoại sau trại.";
                    if(c3!=null&&c3.CachePoint!=null&&!c3.CachePoint.used)return "Điểm 3: lấy khẩu Mosin trinh sát giấu ở vai đồi Đông Nam doanh trại (5 viên), rồi đứng xa trại ít nhất 18 m, giữ [F] quan sát để báo cáo.";
                    return "Điểm 3: đứng xa doanh trại ít nhất 18 m, giữ [F] nhìn về khu trại để báo cáo. Không cần nhắm đúng sĩ quan.";
                }
                if(quest.Stage==8)return SafeToReport?"Sĩ quan đã bị hạ. Liên lạc chỉ huy [E] để nhận đường rút.":$"Sĩ quan đã bị hạ. Thoát giao tranh rồi liên lạc chỉ huy [E] — còn {Hunters} lính đang truy lùng gần bạn.";
                return null;
            }
        }
        void Update()
        {
            Prompt=null;if(!mission.IsInitialized||mission.Stopped||mission.InventoryOpen||mission.MapOpen||Layout==null)return;
            if(checkpointPending&&!Map01NamActions.For(mission).Busy)Checkpoint();
            var inventory=GetComponent<Map01Inventory>();
            if(!Progress.keyCollected&&inventory.Count(KeyItem)>0){Progress.keyCollected=true;Checkpoint();}
            if(CanBrief&&Near(Commander,mission.Settings.interactRange))Prompt="[E] Nói chuyện với chỉ huy";
            else if(CanDeliver&&Near(Commander,mission.Settings.interactRange))Prompt="[E] Bàn giao túi tiếp tế — nhận lệnh điểm 2";
            else if(RadioReport)Prompt=SafeToReport?"[E] Liên lạc chỉ huy qua bộ đàm":$"Thoát giao tranh rồi liên lạc chỉ huy · còn {Hunters} lính truy lùng";
            else if(quest.Stage==3){
                if(!Progress.doorUnlocked&&At(Layout.doorApproach))Prompt=RemainingGuards(1)>0?$"Kho còn {RemainingGuards(1)} lính bảo vệ":!Progress.keyCollected?"CỬA KHO BỊ KHÓA · Lấy chìa từ lính gác":"[E] Dùng chìa mở cửa kho";
                else if(Progress.doorUnlocked&&At(Layout.chestApproach))Prompt=Progress.chestUnlocked?"[E] Mở hòm — lấy túi thuốc":"[E] Giải khóa hòm tiếp tế · 3 vòng số";
            }else if(quest.Stage==5&&Progress.radioDisabled&&Layout.sabotage!=null&&At(Layout.sabotage.powerApproach))Prompt=ResponsePending||RemainingGuards(2)>0?"Dọn trạm đài trước khi phá nguồn":"[E] Phá bộ cấp điện · Ngắt ba cầu chì";
            else if(quest.Stage==5&&!Progress.radioDisabled&&At(Layout.radioApproach))Prompt=ResponsePending?"Xử lý nhóm phản ứng trước khi thao tác đài":RemainingGuards(2)>0?$"Còn {RemainingGuards(2)} lính bảo vệ đài":"[E] Mở bảng mạch · Vô hiệu hóa đài";
            if(Progress.radioResponseCalled&&!Progress.radioResponseSpawned)TrySpawnResponse();ObserveCommander();
            AutoRetreatReport();
        }
        /// <summary>The officer is down and Nam is clear of the fight: he radios for the way out without waiting for E,
        /// so nobody walks to the jetty and waits for a boat that was never called (21/10/2026).</summary>
        void AutoRetreatReport()
        {
            bool ready=quest.Stage==8&&!Progress.retreatReported&&SafeToReport&&!dialogue.IsPlaying&&!mission.Cinematic&&!Map01NamActions.For(mission).Busy;
            retreatClearFor=ready?retreatClearFor+Time.deltaTime:0;
            if(retreatClearFor<AutoRetreatReportSeconds)return;
            retreatClearFor=0;Report();
        }
        public bool TryInteract()
        {
            if(mission.Stopped||Layout==null)return false;
            if(Map01NamActions.For(mission).Busy){mission.Say("Chờ Nam hoàn thành thao tác rồi nhấn E.",2);return true;}
            if(CanBrief&&Near(Commander,mission.Settings.interactRange)){Brief();return true;}
            if(CanDeliver&&Near(Commander,mission.Settings.interactRange)){StartCoroutine(Deliver());return true;}
            if(RadioReport){if(SafeToReport)Report();else mission.Say("Nam: Phải thoát giao tranh trước khi gọi chỉ huy.",4);return true;}
            if(quest.Stage==3){
                if(!Progress.doorUnlocked&&At(Layout.doorApproach)){
                    if(RemainingGuards(1)>0||!Progress.keyCollected){mission.Say(Prompt??Objective,4);return true;}
                    Progress.doorUnlocked=true;if(GetComponent<Map01Inventory>().Count(KeyItem)>0)GetComponent<Map01Inventory>().Spend(KeyItem,1);
                    Map01OperationAudio.Play("door_open",Layout.doorApproach.position);
                    RefreshProps();mission.Say("Cửa kho đã mở. Hòm tiếp tế bên trong vẫn khóa mã — tới gần và nhấn E.",6);Checkpoint();return true;
                }
                if(Progress.doorUnlocked&&At(Layout.chestApproach)){
                    if(RemainingGuards(1)>0||!SafeToReport){mission.Say("Dọn nhóm bảo vệ và thoát giao tranh trước khi mở hòm.",4);return true;}
                    if(Progress.chestUnlocked)Collect();else BeginLock();return true;
                }
            }
            if(quest.Stage==5&&Progress.radioDisabled&&Layout.sabotage!=null&&At(Layout.sabotage.powerApproach)){
                if(RemainingGuards(2)>0||ResponsePending||!SafeToReport){mission.Say(Prompt??Objective,4);return true;}
                BeginPower();return true;
            }
            if(quest.Stage==5&&!Progress.radioDisabled&&At(Layout.radioApproach)){
                if(RemainingGuards(2)>0||ResponsePending||!SafeToReport){mission.Say(Prompt??Objective,4);return true;}
                BeginCircuit();return true;
            }return false;
        }
        void Brief()
        {
            if(quest.Stage==0)dialogue.Begin(new[]{
                new L("Chỉ huy","Trinh sát đã thấy Hùng ở bến phía bắc. Anh ấy còn sống, nhưng đang bị giữ.",false,"m1_story_rescue_order_01"),
                new L("Nam","Có mấy tên lính canh?",false,"m1_story_rescue_order_02"),
                new L("Chỉ huy","Chưa rõ. Cậu tới quan sát trước, tìm cách đưa Hùng ra. Đừng nổ súng khi chúng còn giữ anh ấy.",false,"m1_story_rescue_order_03"),
                new L("Chỉ huy","Kiểm tra trang bị trước khi đi. Cứu được người thì đưa thẳng về hầm.",false,"m1_story_rescue_order_05"),
                Map01OperationLines.Acknowledge()
            },()=>{Progress.rescueAccepted=true;GetComponent<Map01SaveSystem>().MarkRescueCheckpoint(false);});
            else dialogue.Begin(Map01OperationLines.SupplyOrder,()=>{quest.BeginSupplyOrder();Checkpoint();});
        }
        void Report()
        {
            if(dialogue.IsPlaying)return; // Already on the radio (the call after the officer falls starts by itself).
            if(quest.Stage==4)dialogue.Begin(Map01OperationLines.SupplyReport,()=>{Progress.supplyReported=true;Checkpoint();},"Mang hàng về hầm",true);
            else if(quest.Stage==6)dialogue.Begin(Map01OperationLines.RadioReport,()=>{quest.BeginCommanderOrder();Checkpoint();},"Tới điểm 3",true);
            else if(quest.Stage==8)dialogue.Begin(Map01OperationLines.RetreatReport,()=>{Progress.retreatReported=true;quest.CompleteBossDefeat(false);Checkpoint();},"Rút ra bến",true);
        }
        static int NewPuzzleSeed(){int seed=Guid.NewGuid().GetHashCode();return seed==0?1:seed;}
        void BeginLock()
        {
            int secret=Map01SupplyLock.GenerateCode(Progress.puzzleSeed);
            var puzzle=new Map01SupplyLock(secret,Progress.lockHistory,Progress.lockAttemptStart);Operating=true;
            if(!puzzleUI.Begin(mission,puzzle,null,Layout.supply.position+Vector3.up*.5f,result=>{
                Progress.lockHistory=puzzle.History.ToArray();Progress.lockAttemptStart=puzzle.AttemptStart;Operating=false;
                if(result==Map01OperationResult.Success){Progress.chestUnlocked=true;Collect();}
            }))Operating=false;
        }
        void BeginCircuit()
        {
            if(Progress.circuitRevision!=1||Progress.circuitSeed==0){Progress.circuitSeed=NewPuzzleSeed();Progress.circuitRound=0;Progress.circuitRotations=null;Progress.circuitRevision=1;}
            var puzzle=new Map01RadioCircuit(Progress.circuitSeed,Progress.circuitRotations,Map01RadioCircuit.Seconds,Progress.circuitRound);Operating=true;
            var focus=Layout.radioRightContact!=null&&Layout.radioLeftContact!=null
                ?(Layout.radioRightContact.position+Layout.radioLeftContact.position)*.5f:Layout.radio.position+Vector3.up*.8f;
            if(!puzzleUI.Begin(mission,null,puzzle,focus,result=>{
                Progress.circuitRotations=(int[])puzzle.Rotations.Clone();Progress.circuitRound=puzzle.Round;Operating=false;
                if(result==Map01OperationResult.Success){Progress.radioDisabled=true;mission.Say("Đài đã hỏng. Phá tiếp bộ nguồn cạnh trạm trước khi báo cáo chỉ huy.",6);Map01OperationAudio.Play("radio_off",Layout.radio.position);RefreshProps();Checkpoint();}
                else if(result==Map01OperationResult.Failure){
                    Progress.circuitSeed=0;Progress.circuitRound=0;Progress.circuitRotations=null;
                    if(!Progress.radioResponseCalled)CallRadioResponse("Chuông báo động đã bật! Có quân đang tới kiểm tra trạm đài.");
                    else mission.Say("Mạch chưa đúng. Đài vẫn đang phát; có thể thử lại.",4);
                }
            }))Operating=false;
        }
        void BeginPower(){
            if(Progress.powerSeed==0)Progress.powerSeed=NewPuzzleSeed();
            var puzzle=new Map01PowerSabotage(Progress.powerSeed,Progress.powerStep);Operating=true;
            var props=Layout.sabotage;var focus=(props.powerRightContact.position+props.powerLeftContact.position)*.5f;
            if(!puzzleUI.Begin(mission,null,null,focus,result=>{
                Operating=false;Progress.powerStep=puzzle.Step;
                if(result==Map01OperationResult.Success){
                    Progress.powerDisabled=true;Map01OperationAudio.Play("radio_off",focus,.7f);RefreshProps();quest.RadioDisabled();Checkpoint();
                }else if(result==Map01OperationResult.Failure){Progress.powerSeed=0;Progress.powerStep=0;mission.Say("Chưa phá được bộ nguồn. Có thể thử lại [E].",4);}
            },puzzle))Operating=false;
        }
        void Collect()
        {
            if(Operating||Progress.supplies)return;
            // The solved lock is the complete interaction: quest cargo goes straight into the bag.
            Progress.supplies=true;
            var bag=GetComponent<Map01Inventory>();if(bag.Count(CargoItem)==0)bag.Add(CargoItem,1);
            quest.SuppliesRecovered();RefreshProps();Checkpoint();
        }
        IEnumerator Deliver()
        {
            if(Operating||!CanDeliver)yield break;Operating=true;
            bool proceed=false;
            if(!dialogue.Begin(Map01OperationLines.Handoff.Take(2).ToArray(),()=>proceed=true,"Bàn giao túi tiếp tế")){Operating=false;yield break;}
            while(dialogue.IsPlaying)yield return null;
            if(!proceed){Operating=false;yield break;}
            // Hand over directly where the commander conversation takes place.
            Operating=false;
            dialogue.Begin(Map01OperationLines.Handoff.Skip(2).ToArray(),()=>{
                if(Progress.suppliesDelivered)return;Progress.suppliesDelivered=true;
                var bag=GetComponent<Map01Inventory>();if(bag.Count(CargoItem)>0)bag.Spend(CargoItem,1);
                GiveSupplyReward();
                RefreshProps();quest.BeginRadioOrder();Checkpoint();
            },"Nhận nhiệm vụ điểm 2",true);
        }
        void GiveSupplyReward()
        {
            if (Progress.supplyRewardGranted) return;
            Progress.supplyRewardGranted = true;
            var bag = GetComponent<Map01Inventory>();
            foreach (var item in Map01Logistics.For(mission).ContainerLoot("supply_delivery_reward"))
                bag.Receive(item.item_id, item.count, item.condition);
        }
        void ObserveCommander()
        {
            if(quest.Stage!=7||quest.CommanderEnemy==null||Operating)return;
            if(Progress.commanderConfirmed&&Progress.commanderReported){quest.TryCompleteCommanderObjective();return;}
            var target=Layout.commanderPost.position+Vector3.up*1.5f;
            var eye=mission.player.position+Vector3.up*1.6f;float distance=Vector3.Distance(eye,target);
            bool still=!mission.ModernPlayer.IsMoving&&Vector3.Distance(previousObservationPosition,mission.player.position)<=.08f;previousObservationPosition=mission.player.position;
            float angle=Vector3.Angle(mission.gameCamera.transform.forward,target-mission.gameCamera.transform.position);
            bool visible=CampObservationQualifies(distance,angle,GetComponent<Map01Scouting>().Binoculars,still);
            ObservationHint=distance<18?"Lùi ra ngoài trại, cách ít nhất 18 m.":distance>120?"Tiến gần khu doanh trại hơn (trong 120 m).":!still?"Dừng di chuyển và giữ ống nhòm.":angle>35?"Hướng ống nhòm về khu doanh trại, không cần nhắm vào người.":"Đang quan sát doanh trại…";
            confirmTime=visible?confirmTime+Time.deltaTime:0;if(confirmTime<Mathf.Min(1.2f,Layout.observationSeconds))return;
            Progress.commanderAreaReached=true;confirmTime=0;
            dialogue.Begin(Progress.commanderKilled?Map01OperationLines.EarlyCommanderReport:Map01OperationLines.CommanderReport,()=>{
                Progress.commanderConfirmed=Progress.commanderReported=true;
                if(!Progress.commanderKilled)DiscoverCommander();else quest.TryCompleteCommanderObjective();Checkpoint();
            },"Tiếp tục",true);
        }
        public static bool CampObservationQualifies(float distance,float angle,bool binoculars,bool still)=>binoculars&&still&&distance>=18&&distance<=120&&angle<=35;
        void DiscoverCommander()
        {
            if(Progress.commanderDiscovery)return;Progress.commanderDiscovery=true;
            var sentry=Guards(3).Concat(new[]{quest.CommanderEnemy}).FirstOrDefault(e=>e!=null&&e.Alive&&
                !Physics.Linecast(e.transform.position+Vector3.up*1.3f,mission.player.position+Vector3.up*1.6f,mission.ObstructionMask,QueryTriggerInteraction.Ignore));
            // The C3 command post is a stealth mission now (Map01C3Mission): reporting in does not give Nam away.
            if(GetComponent<Map01C3Mission>()!=null)return;
            if(sentry!=null){sentry.FaceImmediate(mission.player.position);mission.Say("Lính địch: Có người ở mé rừng! Bên kia!",2.5f);PlayOperationCue("m1_ops_enemy_spotted",sentry);}
            StartCoroutine(CommanderAlarm());
            foreach(var enemy in Guards(3).Concat(new[]{quest.CommanderEnemy}))if(enemy!=null&&enemy.Alive)enemy.BeginOperationCombat(mission.player.position,1.5f);
        }
        IEnumerator CommanderAlarm(){yield return new WaitForSeconds(Mathf.Max(2.6f,DialogueVoice.Length("m1_ops_enemy_spotted")+.2f));if(quest.CommanderEnemy!=null&&quest.CommanderEnemy.Alive){mission.Say("Sĩ quan địch: Tản ra! Chặn đường hắn!",3);PlayOperationCue("m1_ops_enemy_order",quest.CommanderEnemy);}}
        void PlayOperationCue(string id,Map01EnemyController speaker=null){var clip=DialogueVoice.Find(id)?.clip;if(clip==null)return;if(speaker!=null)ShadowVale.Gameplay.Audio.PooledAudioService.Resolve().PlayOneShot(clip,speaker.transform.position+Vector3.up*1.5f,1,1,0,1);else DialogueVoice.For(gameObject).Play(id);}
        void Checkpoint(){if(Map01SaveSystem.IsRestoring)return;checkpointPending=mission.Cinematic||Operating||Map01NamActions.For(mission).Busy;if(!checkpointPending)GetComponent<Map01SaveSystem>().MarkOperationCheckpoint();}
        public bool Target(out Vector3 at,out string label)
        {
            at=default;label=null;
            if((CanBrief||CanDeliver)&&Commander!=null){at=Commander.position;label=CanDeliver?"HẦM — BÀN GIAO TIẾP TẾ":"CHỈ HUY — NHẬN NHIỆM VỤ";return true;}
            if(RadioReport){at=mission.player.position;label="TÌM CHỖ KÍN — BÁO CÁO [E]";return true;}
            if(Layout==null)return false;
            if(quest.Stage==3){
                var keeper=Layout.keyKeeper;
                if(RemainingGuards(1)==0&&!Progress.keyCollected&&keeper!=null){at=keeper.transform.position;label="LÍNH GÁC — LẤY CHÌA [E]";}
                else{at=(Progress.doorUnlocked?Layout.chestApproach:Layout.doorApproach??Layout.supplyApproach).position;label=Progress.doorUnlocked?"HÒM TIẾP TẾ — GIẢI KHÓA":"ĐIỂM 1 — KHO TIẾP TẾ";}return true;
            }
            if(quest.Stage==5){
                if(Progress.radioDisabled&&Layout.sabotage!=null){at=Layout.sabotage.powerApproach.position;label="ĐIỂM 2 — PHÁ NGUỒN ĐIỆN [E]";return true;}
                at=Layout.radioApproach.position;label=ResponsePending?"TRẠM ĐÀI — NHÓM PHẢN ỨNG":"ĐIỂM 2 — BẢNG MẠCH ĐÀI";return true;}
            if(quest.Stage==7){
                var c3=GetComponent<Map01C3Mission>();
                if(c3!=null&&c3.CachePoint!=null&&c3.CachePoint.isActiveAndEnabled&&!c3.CachePoint.used){at=c3.CachePoint.transform.position;label="ĐỒI ĐÔNG NAM — MOSIN TRINH SÁT";return true;}
                var field=GetComponent<Map01FieldSupplies>()?.ApproachPoint;
                if(field!=null&&field.isActiveAndEnabled&&!field.used){at=field.transform.position;label="ĐỒI ĐÔNG NAM — BĂNG, KHÓI, LỰU ĐẠN";return true;}
                bool hunt=Progress.commanderReported&&quest.CommanderEnemy!=null&&quest.CommanderEnemy.Alive;
                at=hunt?quest.CommanderEnemy.transform.position:Layout.commanderPost.position;label=hunt?"ĐIỂM 3 — HẠ SĨ QUAN":"ĐIỂM 3 — QUAN SÁT DOANH TRẠI [F]";return true;
            }return false;
        }
    }
}
