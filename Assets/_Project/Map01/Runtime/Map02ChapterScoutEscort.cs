using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using ShadowVale.Gameplay.Combat;

namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        public const float ScoutAuthoredSpeed=2.5f;
        public float ScoutEscortSpeed=>1.8f;
        public const string ScoutAmbushGroup="D2_04_ambush";
        public bool ScoutEscortActive=>State.activeTask=="D2_04"&&escort!=null&&escort.role=="Tâm"&&sceneTask==null;
        public int ScoutAmbushRemaining=>(State.scoutAmbushPassedBridge?State.scoutAmbushSpawned:5)-Enemies.Count(e=>e!=null&&e.group==ScoutAmbushGroup&&e.Dead);
        Map02ScoutEscortVisual scoutVisual;float scoutSpawnAt;
        bool scoutEquipmentReady,scoutBridgeReady;Vector3 scoutBridgeEntry,scoutBridgeForward;BoxCollider[] scoutBridgeDecks;
        void StartScoutEscort(bool restoring=false)
        {
            if(State.scoutAmbushPlanVersion!=5){State.scoutAmbushPoints.Clear();State.scoutAmbushPlanVersion=5;State.scoutAmbushPassedBridge=false;}
            escort=roles["Tâm"];escort.SetInjured(false);escort.PairedEscort=true;
            if(!State.scoutRescueOriginSet){State.scoutRescueOrigin=restoring?sites["scout"].position:Mission.Player.position;State.scoutRescueOriginSet=true;}
            if(State.scoutAmbushSpawned>0)State.scoutAmbushStarted=true;
            scoutEquipmentReady=false;player.RestoreMotion(false);
            scoutVisual??=gameObject.GetComponent<Map02ScoutEscortVisual>()??gameObject.AddComponent<Map02ScoutEscortVisual>();
            scoutVisual.Bind(this);if(!restoring)TickScoutEscort();
        }
        bool QueueScoutWarning()
        {
            if(State.scoutWarningHeard)return false;
            Bark("m2v3_scoutescort_warning_01");State.scoutWarningHeard=true;return true;
        }
        void TickScoutEscort()
        {
            player.AssistedWalkSpeed=ScoutEscortActive?ScoutEscortSpeed:(float?)null;
            if(!ScoutEscortActive){EndScoutEscort();return;}
            if(!scoutEquipmentReady&&Mission.Bag!=null)
            {
                Mission.Bag.PrepareScoutPistol();
                if(combat.EquippedKind!=WeaponKind.Pistol)return;
                if(!State.scoutPistolIssued)
                {
                    int reserve=Mission.Bag.Count("ammo_pistol"),needed=Mathf.Max(0,32-combat.MagazineRounds(WeaponKind.Pistol));
                    if(reserve<needed)Mission.Bag.Receive("ammo_pistol",needed-reserve);
                    if(combat.MagazineRounds(WeaponKind.Pistol)==0){int rounds=Mathf.Min(8,Mission.Bag.Count("ammo_pistol"));Mission.Bag.Spend("ammo_pistol",rounds);combat.RestoreMagazine(WeaponKind.Pistol,rounds);}
                    State.scoutPistolIssued=true;
                }
                scoutEquipmentReady=true;
            }
            if(combat.EquippedKind!=WeaponKind.Pistol)combat.Equip(WeaponKind.Pistol);
            if(Mission.InputBlocked||Time.time<scoutSpawnAt)return;
            if(QueueScoutWarning())Mission.StoreCheckpoint();
            if(!PrepareScoutBridge())return;
            if(UpdateScoutBridgeCrossing(Mission.Player.position)||UpdateScoutBridgeCrossing(escort.transform.position))Mission.StoreCheckpoint();
            if(State.scoutAmbushPassedBridge)return;
            if(!State.TryStartScoutAmbush(Mission.Player.position))return;
            var roster=Enemies.Where(e=>e!=null&&e.group==ScoutAmbushGroup).ToArray();
            State.scoutAmbushSpawned=Mathf.Max(State.scoutAmbushSpawned,roster.Length);
            if(!CanSpawnScoutAmbush(State.scoutAmbushSpawned,roster.Any(e=>!e.Dead)))return;
            if(!TryScoutAmbushPoint(State.scoutAmbushSpawned,out var point)){scoutSpawnAt=Time.time+.5f;return;}
            // Check the pair, not just Nam, before materializing at a fixed field-bank position.
            if(!ScoutAmbushDistanceClear(point,Mission.Player.position,escort.transform.position)){scoutSpawnAt=Time.time+.25f;return;}
            SpawnEnemy(ScoutAmbushGroup+"_"+State.scoutAmbushSpawned,ScoutAmbushGroup,point,100);
            Enemies[Enemies.Count-1].Hear(Mission.Player.position,true);
            State.scoutAmbushSpawned++;scoutSpawnAt=Time.time+1;
            Mission.StoreCheckpoint();
        }
        bool TryScoutAmbushPoint(int index,out Vector3 point)
        {
            point=default;if(index<0||index>=5)return false;
            if(!PrepareScoutBridge())return false;
            if(State.scoutAmbushPlanVersion!=5){State.scoutAmbushPoints.Clear();State.scoutAmbushPlanVersion=5;}
            if(State.scoutAmbushPoints.Count==5){point=State.scoutAmbushPoints[index];return true;}
            // Use only the field approach ending before the actual bridge deck.
            if(!NavMesh.SamplePosition(sites["scout"].position,out var start,8,NavMesh.AllAreas)||
               !NavMesh.SamplePosition(scoutBridgeEntry-scoutBridgeForward*3,out var end,2,NavMesh.AllAreas))return false;
            if(!NavMesh.SamplePosition(sites["bridge"].position,out var approach,3,NavMesh.AllAreas))return false;
            var corners=new System.Collections.Generic.List<Vector3>();
            // Follow the existing road waypoint before turning towards the bridge entrance.
            var stops=new[]{start.position,approach.position,end.position};
            for(int leg=1;leg<stops.Length;leg++)
            {
                var path=new NavMeshPath();
                if(!NavMesh.CalculatePath(stops[leg-1],stops[leg],NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)return false;
                corners.AddRange(path.corners.Skip(leg>1?1:0));
            }
            if(corners.Count<2)return false;
            float length=0;for(int i=1;i<corners.Count;i++)length+=Vector3.Distance(corners[i-1],corners[i]);
            var points=new System.Collections.Generic.List<Vector3>();
            for(int i=0;i<5;i++)
            {
                float distance=length*(.12f+i*.17f);int segment=1;
                while(segment<corners.Count-1&&distance>Vector3.Distance(corners[segment-1],corners[segment]))
                {distance-=Vector3.Distance(corners[segment-1],corners[segment]);segment++;}
                var origin=Vector3.MoveTowards(corners[segment-1],corners[segment],distance);
                var direction=Vector3.ProjectOnPlane(corners[segment]-corners[segment-1],Vector3.up).normalized;
                var side=Vector3.Cross(Vector3.up,direction)*(i%2==0?-1:1);bool found=false;
                for(int attempt=0;attempt<12;attempt++)
                {
                    var candidate=origin+side*(20.75f+attempt/3*1.5f)+direction*((attempt%3-1)*.75f);
                    if(!NavMesh.SamplePosition(candidate,out var hit,2,NavMesh.AllAreas)||Vector3.Dot(hit.position-origin,side)<20.5f)continue;
                    var connection=new NavMeshPath();
                    if(!NavMesh.CalculatePath(hit.position,end.position,NavMesh.AllAreas,connection)||connection.status!=NavMeshPathStatus.PathComplete||ScoutPathCrossesBridge(connection))continue;
                    points.Add(hit.position);found=true;break;
                }
                if(!found)return false;
            }
            State.scoutAmbushPoints=points;point=points[index];return true;
        }
        static bool ScoutDeckContains(BoxCollider deck,Vector3 point)
        {
            var local=deck.transform.InverseTransformPoint(point)-deck.center;
            return Mathf.Abs(local.x)<=deck.size.x*.5f&&Mathf.Abs(local.z)<=deck.size.z*.5f+.15f;
        }
        bool ScoutPathCrossesBridge(NavMeshPath path)
        {
            foreach(var deck in scoutBridgeDecks)
            {
                var box=new Bounds(deck.center,new Vector3(deck.size.x,1,deck.size.z));
                for(int i=1;i<path.corners.Length;i++)
                {
                    var a=deck.transform.InverseTransformPoint(path.corners[i-1]);var b=deck.transform.InverseTransformPoint(path.corners[i]);a.y=b.y=deck.center.y;
                    if(box.Contains(a)||box.Contains(b))return true;
                    var delta=b-a;if(delta.sqrMagnitude<.000001f)continue;
                    if(box.IntersectRay(new Ray(a,delta.normalized),out float entry)&&entry<=delta.magnitude)return true;
                }
            }
            return false;
        }
        bool UpdateScoutBridgeCrossing(Vector3 position)
        {
            if(State.scoutAmbushPassedBridge||!PrepareScoutBridge())return false;
            if(!scoutBridgeDecks.Any(deck=>ScoutDeckContains(deck,position)))return false;
            State.scoutAmbushPassedBridge=true;return true;
        }
        bool PrepareScoutBridge()
        {
            if(scoutBridgeReady)return true;
            if(!sites.TryGetValue("bridge",out var marker))return false;
            scoutBridgeDecks=FindObjectsByType<BoxCollider>(FindObjectsSortMode.None)
                .Where(c=>c.enabled&&(c.name=="Walkable bridge deck"||c.name=="Bridge deck")).ToArray();
            var deck=scoutBridgeDecks.OrderBy(c=>Vector3.Distance(c.bounds.center,marker.position)).FirstOrDefault();
            if(deck==null)return false;
            var a=deck.transform.TransformPoint(deck.center-Vector3.right*deck.size.x*.5f);
            var b=deck.transform.TransformPoint(deck.center+Vector3.right*deck.size.x*.5f);
            if(Vector3.Distance(a,sites["scout"].position)>Vector3.Distance(b,sites["scout"].position)){var swap=a;a=b;b=swap;}
            scoutBridgeEntry=a;scoutBridgeForward=Vector3.ProjectOnPlane(b-a,Vector3.up).normalized;
            scoutBridgeReady=true;return true;
        }
        bool ScoutAmbushCleared=>(State.scoutAmbushSpawned==5||State.scoutAmbushPassedBridge)&&!Enemies.Any(e=>e!=null&&e.group==ScoutAmbushGroup&&!e.Dead);
        public static bool ScoutAmbushDistanceClear(Vector3 point,Vector3 nam,Vector3 tam)=>
            Vector3.ProjectOnPlane(point-nam,Vector3.up).sqrMagnitude>=400&&Vector3.ProjectOnPlane(point-tam,Vector3.up).sqrMagnitude>=400;
        public static bool CanSpawnScoutAmbush(int spawned,bool hasLivingEnemy)=>spawned>=0&&spawned<5&&!hasLivingEnemy;
        void EndScoutEscort()
        {
            if(player!=null)player.AssistedWalkSpeed=null;scoutVisual?.Release();
            if(roles.TryGetValue("Tâm",out var tam))tam.PairedEscort=false;
            scoutEquipmentReady=false;scoutBridgeReady=false;
        }
    }
}
