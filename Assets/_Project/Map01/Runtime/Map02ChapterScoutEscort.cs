using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using ShadowVale.Gameplay.Combat;

namespace ShadowVale.Map01
{
    public sealed partial class Map02ChapterDirector
    {
        public const float ScoutAuthoredSpeed=7f/3.6f;
        public float ScoutEscortSpeed=>player.WalkSpeed;
        public const string ScoutAmbushGroup="D2_04_ambush";
        public bool ScoutEscortActive=>State.activeTask=="D2_04"&&escort!=null&&escort.role=="Tâm"&&sceneTask==null;
        public int ScoutAmbushRemaining=>5-Enemies.Count(e=>e!=null&&e.group==ScoutAmbushGroup&&e.Dead);
        Map02ScoutEscortVisual scoutVisual;float scoutSpawnAt;
        bool scoutEquipmentReady;
        void StartScoutEscort(bool restoring=false)
        {
            escort=roles["Tâm"];escort.SetInjured(false);escort.PairedEscort=true;
            if(!State.scoutRescueOriginSet){State.scoutRescueOrigin=restoring?sites["scout"].position:Mission.Player.position;State.scoutRescueOriginSet=true;}
            if(State.scoutAmbushSpawned>0)State.scoutAmbushStarted=true;
            scoutEquipmentReady=false;player.RestoreMotion(false);
            scoutVisual??=gameObject.GetComponent<Map02ScoutEscortVisual>()??gameObject.AddComponent<Map02ScoutEscortVisual>();
            scoutVisual.Bind(this);if(!restoring)TickScoutEscort();
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
            if(!State.TryStartScoutAmbush(Mission.Player.position))return;
            var roster=Enemies.Where(e=>e!=null&&e.group==ScoutAmbushGroup).ToArray();
            State.scoutAmbushSpawned=Mathf.Max(State.scoutAmbushSpawned,roster.Length);
            if(!CanSpawnScoutAmbush(State.scoutAmbushSpawned,roster.Any(e=>!e.Dead)))return;
            if(!TryScoutAmbushPoint(State.scoutAmbushSpawned,out var point)){scoutSpawnAt=Time.time+.5f;return;}
            SpawnEnemy(ScoutAmbushGroup+"_"+State.scoutAmbushSpawned,ScoutAmbushGroup,point,100);
            Enemies[Enemies.Count-1].Hear(Mission.Player.position,true);
            State.scoutAmbushSpawned++;scoutSpawnAt=Time.time+1;
            Mission.StoreCheckpoint();
        }
        bool TryScoutAmbushPoint(int index,out Vector3 point)
        {
            var origin=Mission.Player.position;var path=new NavMeshPath();var direction=sites["command"].position-origin;
            if(NavMesh.CalculatePath(origin,sites["command"].position,NavMesh.AllAreas,path))
                foreach(var corner in path.corners)if(Vector3.Distance(origin,corner)>4){direction=corner-origin;break;}
            direction=Vector3.ProjectOnPlane(direction,Vector3.up).normalized;
            if(direction.sqrMagnitude<.5f)direction=Vector3.ProjectOnPlane(sites["command"].position-sites["scout"].position,Vector3.up).normalized;
            var side=Vector3.Cross(Vector3.up,direction)*(index%2==0?-1:1);
            for(int attempt=0;attempt<12;attempt++)
            {
                var candidate=origin+direction*(8+attempt%3*4)+side*(12+attempt/3*3);
                if(!NavMesh.SamplePosition(candidate,out var hit,2,NavMesh.AllAreas)||Vector3.Dot(hit.position-origin,side)<8)continue;
                var connection=new NavMeshPath();if(!NavMesh.CalculatePath(hit.position,origin,NavMesh.AllAreas,connection)||connection.status!=NavMeshPathStatus.PathComplete)continue;
                point=hit.position;return true;
            }
            point=default;return false;
        }
        bool ScoutAmbushCleared=>State.scoutAmbushSpawned==5&&!Enemies.Any(e=>e!=null&&e.group==ScoutAmbushGroup&&!e.Dead);
        public static bool CanSpawnScoutAmbush(int spawned,bool hasLivingEnemy)=>spawned>=0&&spawned<5&&!hasLivingEnemy;
        void EndScoutEscort()
        {
            if(player!=null)player.AssistedWalkSpeed=null;scoutVisual?.Release();
            if(roles.TryGetValue("Tâm",out var tam))tam.PairedEscort=false;
            scoutEquipmentReady=false;
        }
    }
}
