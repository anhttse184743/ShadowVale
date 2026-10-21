using System;
using System.Collections.Generic;
using System.Linq;
using ShadowVale.Gameplay.Audio;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    /// <summary>
    /// Night 3 geography: the tank seen at the third lane and again at the head of the dock road, the boats
    /// on the river, and where Anh Lực and Tâm stand. Every pose follows from the finished tasks, so a load
    /// puts things where the story has them; nothing here is saved.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        readonly Dictionary<string,Vector3> water=new Dictionary<string,Vector3>();
        RiverBuoyantBody lastBoat;Transform secondBoat;Vector3 lastBoatHome;Quaternion lastBoatHomeRotation;bool boatsOwned;
        Vector3[] tankLeg=Array.Empty<Vector3>();float tankLegClock,gunClock;
        Light gunFlash;ParticleSystem splash;
        public GameObject Tank=>tank;
        public string TankStage {get;private set;}="hidden";
        public Transform LastBoat=>lastBoat!=null?lastBoat.transform:null;
        public Transform SecondBoat=>secondBoat;
        public Vector3 WaterPoint(string id)=>water[id];

        void RegisterNightThreeSites()
        {
            var ditch=sites["ditch"].position;
            Site("tankBreach",sites["lane3"].position+Vector3.forward*8);
            // The road from the lanes to the south dock: the tank waits in the bamboo and blocks its head, 18 m from the bank.
            var road=new NavMeshPath();bool known=NavMesh.CalculatePath(ditch,sites["lane3"].position,NavMesh.AllAreas,road)&&road.status==NavMeshPathStatus.PathComplete;
            Site("tankDock",known?Along(road.corners,18):ditch+Vector3.forward*18);Site("tankHide",known?Along(road.corners,30):ditch+Vector3.forward*30);
            Site("tamCover",sites["fallen"].position+Vector3.left*6);
            lastBoat=FindObjectsByType<RiverBuoyantBody>(FindObjectsSortMode.None).OrderBy(b=>Vector3.Distance(b.transform.position,sites["dock"].position)).FirstOrDefault();
            var moor=lastBoat!=null?lastBoat.transform.position:sites["dock"].position+Vector3.right*15;
            if(lastBoat!=null){lastBoatHome=moor;lastBoatHomeRotation=lastBoat.transform.rotation;}
            Water("boatDock",moor);Water("boatDock2",moor+Vector3.back*3.5f);Water("boatMid",moor+new Vector3(1.5f,0,-2));
            Water("boatFar",moor+new Vector3(6.5f,0,-4.5f));Water("boatFar2",moor+new Vector3(4.5f,0,2.5f));
        }
        void Water(string id,Vector3 fallback){var authored=Layout?.sites?.FirstOrDefault(s=>s.id==id);water[id]=authored!=null?authored.position:fallback;}

        // ---- Tank: breach at the third lane, turns into the village road, hidden in Nhịp 4, blocks the dock road in Nhịp 5.
        string TankStageNow()
        {
            if(!State.Done("N3_05C"))return "hidden";
            if(!State.Done("N3_CRAWL"))return "breach";
            if(!State.Done("N3_06"))return "leaving";
            if(State.tankDestroyed)return "destroyed";
            if(!State.Done("N3_07"))return sceneTask?.id=="N3_07"?(sceneClock<5?"emerging":"blocking"):"waiting";
            return "blocking";
        }
        void TickTank(float dt,bool snap=false)
        {
            var stage=TankStageNow();
            if(stage=="hidden"){if(tank!=null)tank.SetActive(false);TankStage=stage;return;}
            if(tank==null)CreateTank();
            if(stage!=TankStage){TankStage=stage;tankLegClock=0;tankLeg=stage=="leaving"?Leg("tankBreach","tankHide"):stage=="emerging"?Leg("tankHide","tankDock"):Array.Empty<Vector3>();}
            tankLegClock=snap?1e5f:tankLegClock+dt; // A load lands at the end of the drive and stays there.
            bool visible=stage!="waiting";Vector3 at,face;
            if(tankLeg.Length>1)
            {
                float travelled=tankLegClock*(stage=="leaving"?4.5f:3f);
                at=Along(tankLeg,travelled);face=Along(tankLeg,Mathf.Min(travelled,Length(tankLeg)-.05f)+1)-at;
                if(stage=="leaving"&&travelled>=Length(tankLeg))visible=false; // Out of sight in the village until it reappears at the dock road.
            }
            else
            {
                at=sites[stage=="breach"?"tankBreach":stage=="waiting"?"tankHide":"tankDock"].position;
                face=(stage=="breach"?sites["lane3"].position:water["boatMid"])-at; // Turret toward Nam, then toward the last boat.
            }
            tank.SetActive(visible);tank.transform.position=at;
            var flat=Vector3.ProjectOnPlane(face,Vector3.up);
            if(flat.sqrMagnitude>.01f)tank.transform.rotation=snap?Quaternion.LookRotation(flat):Quaternion.RotateTowards(tank.transform.rotation,Quaternion.LookRotation(flat),70*Mathf.Max(dt,.0001f));
            if(tankSmoke!=null)
            {
                if(State.tankDestroyed&&!tankSmoke.isPlaying)tankSmoke.Play();
                else if(!State.tankDestroyed&&sceneTask?.id!="N3_09"&&tankSmoke.isPlaying)tankSmoke.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            TickMachineGun(dt,stage);
        }
        Vector3[] Leg(string from,string to)
        {
            var path=new NavMeshPath();
            return NavMesh.CalculatePath(sites[from].position,sites[to].position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete?path.corners:new[]{sites[from].position,sites[to].position};
        }
        static float Length(Vector3[] path){float total=0;for(int i=1;i<path.Length;i++)total+=Vector3.Distance(path[i-1],path[i]);return total;}
        /// <summary>Bursts into the water before the last boat while the tank holds the dock road; silent after the blast.</summary>
        void TickMachineGun(float dt,string stage)
        {
            if(gunFlash==null)
            {
                var flash=new GameObject("Tank machine-gun flash");flash.transform.SetParent(tank.transform,false);flash.transform.localPosition=new Vector3(0,2.3f,2.8f);
                gunFlash=flash.AddComponent<Light>();gunFlash.type=LightType.Point;gunFlash.range=9;gunFlash.intensity=4;gunFlash.color=new Color(1,.78f,.42f);gunFlash.shadows=LightShadows.None;gunFlash.enabled=false;
                splash=Map01Fx.System(root.transform,"Machine-gun splashes",water["boatMid"],Map01Fx.Unlit,1,90);
                var main=splash.main;main.startLifetime=.7f;main.startSpeed=3.2f;main.startSize=.3f;main.gravityModifier=1;main.startColor=new Color(.85f,.92f,1,.8f);
                var shape=splash.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=18;shape.radius=.15f;shape.rotation=new Vector3(-90,0,0);
            }
            gunFlash.enabled=false;
            bool firing=(stage=="emerging"||stage=="blocking")&&!(sceneTask?.id=="N3_09"&&sceneClock>=sacrificeTravel+8);
            if(!firing||dt<=0)return;
            float before=Mathf.Repeat(gunClock,.15f);gunClock+=dt;float cycle=Mathf.Repeat(gunClock,2.6f);
            if(cycle>.9f)return; // A burst, then a pause.
            gunFlash.enabled=Mathf.Repeat(gunClock,.15f)<.05f;
            if(Mathf.Repeat(gunClock,.15f)>=before)return;
            var boat=water["boatMid"];var toward=Vector3.ProjectOnPlane(tank.transform.position-boat,Vector3.up).normalized;
            splash.transform.position=boat+toward*UnityEngine.Random.Range(1.5f,4f)+Vector3.Cross(Vector3.up,toward)*UnityEngine.Random.Range(-1.5f,1.5f);splash.Emit(7);
            var sound=Mission.Player.GetComponent<CombatAudio>();
            if(sound!=null&&sound.Gunshot.IsValid){var clip=sound.Gunshot;PooledAudioService.Resolve().PlayOneShot(clip.clip,gunFlash.transform.position,.45f*clip.EffectiveGain,1,0,.85f,clip.startTime,clip.duration);}
        }

        // ---- Boats: boat 1 leaves at 30 %, boat 2 at 60 %; boat 1 comes back for the last trip and is pinned mid-river.
        void TickBoats(float dt,bool snap=false)
        {
            if(lastBoat==null)return;
            if(State.phase<5){ReleaseBoats();return;}
            if(!boatsOwned){boatsOwned=true;var body=lastBoat.GetComponent<Rigidbody>();if(body!=null)body.isKinematic=true;lastBoat.enabled=false;secondBoat=CopyBoat(lastBoat.transform);snap=true;}
            float pace=sceneTask?.id=="N3_09"?.6f:1.8f; // The last boat drifts away slowly after the blast.
            MoveBoat(lastBoat.transform,water[LastBoatSpot()],snap,pace*dt);MoveBoat(secondBoat,water[State.phase>=6?"boatDock2":State.Done("N3_04")?"boatFar2":"boatDock2"],snap,1.8f*dt);
            bool pinned=TankStage=="emerging"||TankStage=="blocking";
            float rock=pinned&&sceneTask?.id!="N3_09"?Mathf.Sin(Time.time*5.5f)*4:0; // It rocks under the gun.
            lastBoat.transform.rotation=lastBoatHomeRotation*Quaternion.Euler(rock,0,rock*.6f);
            if(State.phase==5&&State.Done("N3_06"))
            {
                Seat("Chị Lành",new Vector3(-.5f,.45f,-1.5f));Seat("Ông Sáu",new Vector3(.5f,.45f,-.3f));Seat("Bé Tí",new Vector3(-.4f,.45f,.9f));Seat("Chú Bảy",new Vector3(0,.55f,2.6f));
            }
        }
        string LastBoatSpot()
        {
            if(State.phase>=6)return "boatDock"; // The village is back on its bank at dawn.
            if(!State.Done("N3_04"))return State.activeTask=="N3_04"&&State.taskProgress>=SecondWaveAt?"boatFar":"boatDock";
            if(!State.Done("N3_06"))return "boatDock"; // Back for the last trip.
            if(State.Done("N3_09")||sceneTask?.id=="N3_09"&&sceneClock>=sacrificeTravel+9)return "boatFar";
            return "boatMid";
        }
        static void MoveBoat(Transform boat,Vector3 target,bool snap,float step)
        {
            if(boat==null)return;target.y=boat.position.y;
            boat.position=snap?target:Vector3.MoveTowards(boat.position,target,step);
        }
        void Seat(string role,Vector3 local){var actor=roles[role];actor.StopAt(lastBoat.transform.TransformPoint(local));actor.transform.rotation=lastBoat.transform.rotation;}
        Transform CopyBoat(Transform source)
        {
            // Chú Bảy's second boat: the same hull, render-only, beside the first.
            var copy=new GameObject("Second evacuation boat (render copy)").transform;copy.SetParent(root.transform);copy.SetPositionAndRotation(water["boatDock2"],source.rotation);
            foreach(var renderer in source.GetComponentsInChildren<MeshRenderer>())
            {
                var filter=renderer.GetComponent<MeshFilter>();if(filter==null)continue;
                var part=new GameObject(renderer.name);part.transform.SetParent(copy,false);
                part.transform.localPosition=source.InverseTransformPoint(renderer.transform.position);part.transform.localRotation=Quaternion.Inverse(source.rotation)*renderer.transform.rotation;part.transform.localScale=renderer.transform.lossyScale;
                part.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;part.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
            }
            return copy;
        }
        void ReleaseBoats()
        {
            if(!boatsOwned)return;boatsOwned=false;
            lastBoat.transform.SetPositionAndRotation(lastBoatHome,lastBoatHomeRotation);var body=lastBoat.GetComponent<Rigidbody>();if(body!=null)body.isKinematic=false;lastBoat.enabled=true;
            if(secondBoat!=null)Destroy(secondBoat.gameObject);secondBoat=null;
        }

        // ---- Where Anh Lực and Tâm are in Night 3.
        Vector3 LucNightThree()=>!State.Done("N3_00")?sites["rest"].position+Vector3.left*1.5f:!State.Done("N3_04")?sites["gate"].position:!State.Done("N3_05C")?sites["lane2"].position+Vector3.left*3:sites["cover"].position+Vector3.back*1.2f;
        void StageNightThree(Map02ChapterTask finished)
        {
            if(State.phase!=5)return;
            if(finished.id=="N3_00"||finished.id=="N3_04")roles["Anh Lực"].GoTo(LucNightThree(),2.2f); // Out to the outer post, then back to the tamarind.
            if(finished.id=="N3_05C")roles["Anh Lực"].StopAt(LucNightThree()); // Already in cover when the tank fires.
            if(finished.id=="N3_CRAWL")roles["Tâm"].StopAt(sites["tamCover"].position);
        }
    }
}
