using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Combat;
using UnityEngine;
using UnityEngine.AI;
namespace ShadowVale.Map01
{
    /// <summary>Rescue rules and escort progress, independent of later quest stages.</summary>
    public sealed class Map01Rescue : MonoBehaviour
    {
        public static readonly string[] SquadNames={"patrol_0","patrol_1","patrol_2","rbl_patrol_3"};
        public enum Phase { Captive, Alarm, Failed, Untying, Rising, Escorting, EnteringShelter, Delivered }
        [Serializable] public sealed class Snapshot {
            public int revision,phase,waveMask;
            public bool safe,reward;
            public float escortDistance;
        }
        public IReadOnlyList<Map01EnemyController> Squad=>squad;
        public IReadOnlyList<Map01EnemyController> Pursuers=>pursuers;
        public Map01RescueLayout Layout{get;private set;}
        public Phase CurrentPhase{get;private set;}
        public float HungHealth{get;private set;}
        public float HungMaxHealth=>mission.Settings.hungHP>0?mission.Settings.hungHP:150;
        public bool HungDown=>CurrentPhase==Phase.Failed||HungHealth<=0;
        public bool Failed=>HungDown;
        public string FailureReason{get;private set;}
        public float HungHitAt{get;private set;}=float.NegativeInfinity;
        public Vector3 CaptivePost{get;private set;}
        public Vector3 RetryPoint{get;private set;}
        public bool OverseerAlive=>squad.Count==4&&squad[0]!=null&&squad[0].Alive;
        public int Remaining=>squad.Count(g=>g!=null&&g.Alive);
        public bool CanFree=>CurrentPhase==Phase.Captive&&squad.Count==4&&Remaining==0;
        public bool Exposed=>mission.IsInitialized&&!Failed&&CurrentPhase==Phase.Escorting&&!SafeReached;
        public bool SafeReached{get;private set;}
        public bool RewardDelivered{get;private set;}
        public int WaveMask{get;private set;}
        public float EscortDistance{get;private set;}
        public bool NonSaveable=>CurrentPhase==Phase.Alarm||CurrentPhase==Phase.Untying||CurrentPhase==Phase.Rising||CurrentPhase==Phase.EnteringShelter
            ||(mission.player!=null&&mission.player.TryGetComponent<Map01NamActions>(out var a)&&a.Busy);
        public bool HoldCaptive=>CurrentPhase==Phase.Captive||CurrentPhase==Phase.Untying||CurrentPhase==Phase.Alarm;
        private readonly List<Map01EnemyController> squad=new(),pursuers=new();
        private Map01Mission mission;
        private Map01Quest quest;
        private PlayerCombat boundCombat;
        private float nextChatter,nextWaveCheck,nextHitLine,calmUntil;
        private int chatterIndex;
        private bool whispered;
        private Coroutine release;
        private bool protectedRelease,releaseInvulnerability;
        private static readonly string[] Chatter={
            "Lính địch: Tên này mang hàng tiếp tế. Ta nghi nó là lính thông tin của bọn nó. Phải giữ lại tra hỏi.",
            "Lính địch: Trói chặt vào! Sáng mai giải nó về đồn chỉ huy khai thác.",
            "Lính địch: Canh cho kỹ — đồng bọn nó thế nào cũng mò tới cứu.",
            "Lính địch: Để xổng tù binh ở cái bến này thì cả tổ ăn đòn."
        };
        private void Awake(){mission=GetComponent<Map01Mission>();quest=GetComponent<Map01Quest>();}
        private void Start()
        {
            if(!mission.IsInitialized)return;
            CaptivePost=mission.hung.position;HungHealth=HungMaxHealth;
            Layout=FindFirstObjectByType<Map01RescueLayout>();
            if(Layout==null){var prefab=Resources.Load<Map01RescueLayout>("Rescue/Map01RescueLayout");if(prefab!=null)Layout=Instantiate(prefab);}
            foreach(var name in SquadNames){var go=GameObject.Find(name);if(go!=null&&go.TryGetComponent<Map01EnemyController>(out var guard))squad.Add(guard);}
            if(Layout==null||squad.Count!=4){Debug.LogError("Rescue layout or one of four original guards is missing.",this);enabled=false;return;}
            for(int i=0;i<4;i++){
                var route=Layout.patrolRoutes[i];
                var points=route==null?Array.Empty<Vector3>():Enumerable.Range(0,route.childCount).Select(j=>route.GetChild(j).position).ToArray();
                squad[i].ConfigureRescuePost(Layout.guardPosts[i],points,i==0,Layout);
            }
            RetryPoint=FindRetryPoint(mission.player.position);
            if(mission.ModernCombat!=null){boundCombat=mission.ModernCombat;boundCombat.TrySpecialMelee=TryPlayerTakedown;}
        }
        private Vector3 FindRetryPoint(Vector3 from)
        {
            var route=Path(CaptivePost,from);float walked=0;
            for(int i=1;i<route.Length;i++){
                float segment=Vector3.Distance(route[i-1],route[i]);
                if(walked+segment>=45)return Vector3.Lerp(route[i-1],route[i],(45-walked)/segment);
                walked+=segment;
            }return from;
        }
        public bool IsRescueGuard(Map01EnemyController enemy)=>squad.Contains(enemy);
        public void SyncRestoredStage(int stage)
        {
            CurrentPhase=stage==Map01Quest.RescueStage?Phase.Captive:stage==Map01Quest.EscortStage?Phase.Escorting:Phase.Delivered;
            RewardDelivered=stage>Map01Quest.EscortStage;
            if(CurrentPhase==Phase.Escorting&&Layout!=null&&EscortDistance<=0)EscortDistance=PathLength(Path(CaptivePost,Layout.shelterDoor.position));
        }
        public bool IsOverseer(Map01EnemyController enemy)=>squad.Count>0&&squad[0]==enemy;
        public bool HoldEnemy(Map01EnemyController enemy)=>SafeReached&&pursuers.Contains(enemy);
        public void ReportDetection(Map01Detection detection)
        {
            if(CurrentPhase!=Phase.Captive||!OverseerAlive||!IsRescueGuard(detection.observer)||mission.Cinematic
                ||Map01SaveSystem.IsRestoring||Time.time<calmUntil)return;
            // Alarm is committed before the hitscan; the same round cannot erase execution.
            CurrentPhase=Phase.Alarm;
            FailureReason=detection.cause==Map01DetectionCause.Gunshot?"Địch đã nghe tiếng súng.":"Nam đã bị phát hiện.";
            squad[0].GetComponent<Health>().CinematicInvulnerable=true;
            var cinematic=GetComponent<Map01RescueCinematic>();if(cinematic==null)cinematic=gameObject.AddComponent<Map01RescueCinematic>();
            cinematic.ExecuteHostage(this,squad[0]);
        }
        public void CompleteExecution()
        {
            if(CurrentPhase!=Phase.Alarm)return;
            squad[0].GetComponent<Health>().CinematicInvulnerable=false;
            HungHealth=0;HungHitAt=Time.time;CurrentPhase=Phase.Failed;
            mission.Say(FailureReason+" Hùng đã bị xử bắn. Giải cứu thất bại.",10);
        }
        private bool TryPlayerTakedown()
        {
            if(mission.Cinematic||mission.Stopped)return false;
            return mission.Enemies.Where(e=>e!=null&&e.Alive).OrderBy(e=>(e.transform.position-mission.player.position).sqrMagnitude).Any(e=>e.TrySilentTakedown());
        }
        public bool BeginFree()
        {
            if(!CanFree||mission.Stopped||release!=null)return false;
            if(!Map01NamActions.For(mission).PlayUntie(mission.hung))return false;
            releaseInvulnerability=mission.ModernHealth.CinematicInvulnerable;protectedRelease=true;mission.ModernHealth.CinematicInvulnerable=true;
            CurrentPhase=Phase.Untying;mission.Cinematic=true;release=StartCoroutine(FreeRoutine());return true;
        }
        private IEnumerator FreeRoutine()
        {
            var actions=Map01NamActions.For(mission);while(actions.Busy)yield return null;
            CurrentPhase=Phase.Rising;
            var visual=mission.hung.GetComponentInChildren<Map01HungVisual>();visual.BeginRescueRise();
            yield return null;while(visual.IsRising)yield return null;
            quest.BeginEscort();CurrentPhase=Phase.Escorting;
            visual.FinishRescueRise();
            EscortDistance=RemainingHomeDistance();WaveMask=0;SafeReached=false;release=null;mission.Cinematic=false;
            mission.ModernHealth.CinematicInvulnerable=releaseInvulnerability;protectedRelease=false;
            mission.ModernCombat?.RestoreAfterCinematic();
            mission.Say("Hùng: Cảm ơn Nam. Tôi đang đưa hàng tiếp tế thì bị bắt. Chúng tưởng tôi là lính thông tin. Đưa tôi về căn cứ nhé.",9);
            GetComponent<Map01SaveSystem>().MarkRescueCheckpoint(true);
        }
        private void Update()
        {
            if(!enabled||!mission.IsInitialized||mission.Stopped)return;
            if(mission.ModernCombat!=null&&mission.ModernCombat.TrySpecialMelee==null){boundCombat=mission.ModernCombat;boundCombat.TrySpecialMelee=TryPlayerTakedown;}
            if(quest.Stage==Map01Quest.RescueStage&&CurrentPhase==Phase.Captive){GetComponent<Map01SaveSystem>().EnsureRescueCheckpoint();Overhear();}
            if(CurrentPhase!=Phase.Escorting||Time.time<nextWaveCheck)return;
            GetComponent<Map01SaveSystem>().EnsureEscortCheckpoint();
            nextWaveCheck=Time.time+.4f;var nam=mission.player.position;var hung=mission.hung.position;
            if(!SafeReached&&Vector3.Distance(nam,Layout.safeEntry.position)<=Layout.safeRadius&&Vector3.Distance(hung,Layout.safeEntry.position)<=Layout.safeRadius){
                SafeReached=true;foreach(var guard in pursuers)if(guard!=null&&guard.Alive)guard.CeasePursuit();
                mission.Say("Nam: Đến căn cứ rồi. Hùng, vào hầm với tôi!",4);
            }
            if(SafeReached&&Vector3.Distance(nam,Layout.shelterDoor.position)<=Layout.doorRadius&&Vector3.Distance(hung,Layout.shelterDoor.position)<=Layout.doorRadius){
                CurrentPhase=Phase.EnteringShelter;
                var cinematic=GetComponent<Map01RescueCinematic>();if(cinematic==null)cinematic=gameObject.AddComponent<Map01RescueCinematic>();
                if(!cinematic.RunHome(this))CurrentPhase=Phase.Escorting;
            }else if(!SafeReached)TrySpawnWaves();
        }
        private void Overhear()
        {
            if(Time.time<nextChatter||squad.Any(g=>g.Alive&&g.Engaged))return;
            float distance=Vector3.Distance(mission.player.position,CaptivePost);if(distance>34)return;
            if(!whispered&&distance<12){whispered=true;nextChatter=Time.time+8;mission.Say("Hùng (thì thào): ...Nam? Chúng có bốn tên. Hạ tên canh tôi trước, đừng để chúng báo động.",6);return;}
            if(Remaining==0)return;mission.Say(Chatter[chatterIndex++%Chatter.Length],6);nextChatter=Time.time+15;
        }
        public static Vector3[] Path(Vector3 from,Vector3 to)
        {
            var path=new NavMeshPath();
            if(!NavMesh.SamplePosition(from,out var start,3,NavMesh.AllAreas)||!NavMesh.SamplePosition(to,out var end,3,NavMesh.AllAreas)
                ||!NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)return Array.Empty<Vector3>();
            return path.corners;
        }
        public float RemainingHomeDistance(){if(Layout==null)return float.PositiveInfinity;var points=Path(mission.hung.position,Layout.shelterDoor.position);return points.Length==0?float.PositiveInfinity:PathLength(points);}
        private static float PathLength(Vector3[] points){float d=0;for(int i=1;i<points.Length;i++)d+=Vector3.Distance(points[i-1],points[i]);return d;}
        private void TrySpawnWaves()
        {
            float distance=RemainingHomeDistance();if(!(EscortDistance>.1f)||float.IsInfinity(distance))return;
            for(int wave=0;wave<3;wave++){
                if((WaveMask&(1<<wave))!=0||distance>EscortDistance*Layout.waveThresholds[wave])continue;
                var sites=Layout.pursuitSites.Where(t=>Vector3.Distance(t.position,mission.player.position)>=Layout.spawnClearance
                    &&Vector3.Distance(t.position,mission.hung.position)>=Layout.spawnClearance&&Path(t.position,mission.hung.position).Length>0&&!VisibleSpawn(t.position))
                    .OrderBy(t=>Vector3.Distance(t.position,mission.hung.position)).Take(2).ToArray();
                if(sites.Length<2)return;
                for(int slot=0;slot<2;slot++)SpawnPursuer(wave,slot,sites[slot].position,true);
                WaveMask|=1<<wave;return;
            }
        }
        private bool VisibleSpawn(Vector3 p)
        {
            var v=mission.gameCamera.WorldToViewportPoint(p+Vector3.up);
            if(v.z<=0||v.x<-.08f||v.x>1.08f||v.y<-.08f||v.y>1.08f)return false;
            var eye=mission.gameCamera.transform.position;
            return !Physics.Linecast(eye,p+Vector3.up,mission.ObstructionMask,QueryTriggerInteraction.Ignore)
                &&(mission.SightCover==null||!mission.SightCover.Blocks(eye,p+Vector3.up));
        }
        private Map01EnemyController SpawnPursuer(int wave,int slot,Vector3 p,bool pursue)
        {
            string id=$"rescue_pursuit_{wave}_{slot}";
            var existing=pursuers.FirstOrDefault(e=>e!=null&&e.name==id);if(existing!=null)return existing;
            var go=Instantiate(squad[1].gameObject,p,Quaternion.identity,squad[1].transform.parent);go.name=id;
            var guard=go.GetComponent<Map01EnemyController>();var oldLoot=go.GetComponent<ForestPoint>();if(oldLoot!=null)Destroy(oldLoot);
            guard.InitializePursuer(mission,p,pursue);mission.AddEnemy(guard);pursuers.Add(guard);return guard;
        }
        public void HitHung(float damage)
        {
            if(!Exposed||damage<=0)return;
            HungHealth=Mathf.Max(0,HungHealth-damage);HungHitAt=Time.time;
            if(HungHealth<=0){CurrentPhase=Phase.Failed;FailureReason="Hùng đã hy sinh trên đường về căn cứ.";mission.Say(FailureReason,10);}
            else if(Time.time>=nextHitLine){nextHitLine=Time.time+12;mission.Say("Hùng: Tôi trúng đạn rồi... Nam, yểm hộ tôi!",4);}
        }
        public bool CompleteDelivery()
        {
            if(RewardDelivered||CurrentPhase!=Phase.EnteringShelter)return false;
            RewardDelivered=true;CurrentPhase=Phase.Delivered;quest.FinishRescueDelivery();
            // The patrol withdrew when both reached safety. Retain only real defeated corpses/loot.
            foreach(var guard in pursuers.Where(g=>g!=null&&g.Alive).ToArray()){
                mission.RemoveEnemy(guard);pursuers.Remove(guard);Destroy(guard.gameObject);
            }
            var supplies=mission.Interactables.FirstOrDefault(p=>p.kind==ForestPointKind.Supplies);var inventory=GetComponent<Map01Inventory>();
            inventory.Add("supplies",1);
            if(supplies!=null&&!supplies.used){foreach(var item in supplies.items)inventory.Add(item.item_id,item.count);supplies.MarkLooted();}
            mission.Say("Hùng: Về tới căn cứ rồi. Cảm ơn Nam! Hàng tiếp tế đã bàn giao. Gặp anh để nhận nhiệm vụ tiếp theo.",9);return true;
        }
        public void Retry(){if(!Failed&&mission.PlayerHealth>0)return;GetComponent<Map01SaveSystem>().RestartRescue(quest.Stage==Map01Quest.EscortStage);}
        public void ResetSquad(){foreach(var guard in squad)if(guard!=null)guard.ReturnToPost();}
        public void RestoreHealth(float value)=>HungHealth=value<0?HungMaxHealth:Mathf.Clamp(value,0,HungMaxHealth);
        public Snapshot Capture()=>new Snapshot{revision=1,phase=(int)CurrentPhase,waveMask=WaveMask,safe=SafeReached,reward=RewardDelivered,escortDistance=EscortDistance};
        public void Restore(Snapshot saved,Map01EnemyController.Snapshot[] enemies)
        {
            if(saved==null||saved.revision<=0){
                CurrentPhase=quest.Stage==Map01Quest.RescueStage?Phase.Captive:quest.Stage==Map01Quest.EscortStage?Phase.Escorting:Phase.Delivered;RewardDelivered=quest.Stage>Map01Quest.EscortStage;
                if(CurrentPhase==Phase.Escorting){EscortDistance=PathLength(Path(CaptivePost,Layout.shelterDoor.position));float distance=RemainingHomeDistance();for(int i=0;i<3;i++)if(distance<=EscortDistance*Layout.waveThresholds[i])WaveMask|=1<<i;}
            }else{CurrentPhase=(Phase)saved.phase;WaveMask=saved.waveMask;SafeReached=saved.safe;RewardDelivered=saved.reward;EscortDistance=saved.escortDistance;}
            foreach(var data in enemies??Array.Empty<Map01EnemyController.Snapshot>()){
                string shortId=data.id.Substring(data.id.LastIndexOf('/')+1);if(!shortId.StartsWith("rescue_pursuit_",StringComparison.Ordinal))continue;
                var parts=shortId.Split('_');var guard=SpawnPursuer(int.Parse(parts[2]),int.Parse(parts[3]),data.position,false);guard.RestoreSnapshot(data);if(SafeReached)guard.CeasePursuit();
            }calmUntil=Time.time+2;
        }
        private void OnDestroy(){
            if(boundCombat!=null&&boundCombat.TrySpecialMelee!=null&&ReferenceEquals(boundCombat.TrySpecialMelee.Target,this))boundCombat.TrySpecialMelee=null;
            if(mission!=null&&protectedRelease&&mission.ModernHealth!=null)mission.ModernHealth.CinematicInvulnerable=releaseInvulnerability;
        }
    }
}
