using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// The modelled tank's moving parts: the turret traverses to its target (Nam at the third lane, the last boat
    /// at the dock road), the .50 points down at the water, the wheels turn with the distance driven; burnt out,
    /// the gun droops and the commander's hatch is thrown open. Rotations are about the tank's own axes.
    /// </summary>
    public sealed partial class Map02ChapterDirector
    {
        Transform turret,gunPart,mgMount,mgPart,hatchCommander,mgMuzzle;
        Quaternion turretRest,gunRest,mountRest,mgRest,hatchRest;
        readonly List<(Transform part,float radius)> tankWheels=new List<(Transform,float)>();
        Vector3 lastTankPosition;float turretYaw,mgYaw;
        // Each track is a loop of identical links (rear, ground run, front, top run). Driving shifts every link a
        // fraction of the way to its neighbour, which reads as the track rolling round the wheels.
        sealed class TrackLoop {public Transform[] links;public Vector3[] at;public Quaternion[] turn;public float pitch;}
        readonly List<TrackLoop> trackLoops=new List<TrackLoop>();
        public float TrackTravel {get;private set;}
        public Transform TrackLink(string side,int index)=>trackLoops.Count==0?null:trackLoops[side=="L"?0:Mathf.Min(1,trackLoops.Count-1)].links[index];
        public float TurretYaw=>turretYaw;
        public Transform TankTurret=>turret;
        public Transform TankCommanderHatch=>hatchCommander;
        /// <summary>Degrees the commander's hatch stands open from its closed pose.</summary>
        public float CommanderHatchOpen=>hatchCommander==null?0:Quaternion.Angle(tank.transform.rotation*Quaternion.AngleAxis(turretYaw,Vector3.up)*hatchRest,hatchCommander.rotation);

        void BindTankParts(Transform art)
        {
            var all=art.GetComponentsInChildren<Transform>(true);Transform Find(string n)=>all.FirstOrDefault(t=>t.name==n);
            turret=Find("Turret");gunPart=Find("Gun");mgMount=Find("MG_Mount");mgPart=Find("MG");hatchCommander=Find("Hatch_Commander");mgMuzzle=Find("MG_Muzzle");
            var toTank=Quaternion.Inverse(tank.transform.rotation);
            if(turret!=null)turretRest=toTank*turret.rotation;if(gunPart!=null)gunRest=toTank*gunPart.rotation;
            if(mgMount!=null)mountRest=toTank*mgMount.rotation;if(mgPart!=null)mgRest=toTank*mgPart.rotation;if(hatchCommander!=null)hatchRest=toTank*hatchCommander.rotation;
            tankWheels.Clear();
            foreach(var t in all)
                if(t.name.StartsWith("Wheel_")||t.name.StartsWith("Sprocket_")||t.name.StartsWith("Idler_")||t.name.StartsWith("Roller_"))
                {var r=t.GetComponent<Renderer>();tankWheels.Add((t,r!=null?Mathf.Max(.05f,r.bounds.extents.y):.3f));}
            trackLoops.Clear();TrackTravel=0;
            foreach(var tag in new[]{"L","R"})
            {
                var holder=Find("Track_"+tag);if(holder==null)continue;
                var links=holder.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Link_")).OrderBy(t=>t.name).ToArray();
                if(links.Length<3)continue;
                var loop=new TrackLoop{links=links,at=links.Select(l=>l.localPosition).ToArray(),turn=links.Select(l=>l.localRotation).ToArray()};
                float sum=0;for(int i=0;i<links.Length;i++)sum+=Vector3.Distance(loop.at[i],loop.at[(i+1)%links.Length]);loop.pitch=sum/links.Length;
                trackLoops.Add(loop);
            }
            lastTankPosition=tank.transform.position;
        }
        void RollTracks(float driven)
        {
            if(trackLoops.Count==0)return;
            TrackTravel+=driven;
            foreach(var loop in trackLoops)
            {
                // Forward drive moves the ground run rearward: each link heads toward the previous one in the loop.
                float f=Mathf.Repeat(TrackTravel/loop.pitch,1);int n=loop.links.Length;
                for(int i=0;i<n;i++)
                {
                    int back=(i-1+n)%n;
                    loop.links[i].localPosition=Vector3.Lerp(loop.at[i],loop.at[back],f);
                    loop.links[i].localRotation=Quaternion.Slerp(loop.turn[i],loop.turn[back],f);
                }
            }
        }
        float YawTo(Vector3 target)
        {
            var local=Quaternion.Inverse(tank.transform.rotation)*(target-(turret!=null?turret.position:tank.transform.position));
            return Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg;
        }
        void AnimateTankParts(float dt,string stage,bool snap)
        {
            if(turret==null)return;
            bool atBoat=stage=="emerging"||stage=="blocking";
            // It rolls out with the gun fore and aft, then swings it onto the boat once it stands.
            Vector3? target=stage=="breach"?Mission.Player.position:stage=="blocking"?water["boatMid"]:(Vector3?)null;
            float aim=target.HasValue?YawTo(target.Value):stage=="destroyed"?turretYaw:0;
            turretYaw=snap?aim:Mathf.MoveTowardsAngle(turretYaw,aim,(stage=="blocking"?32:22)*dt); // "nòng pháo quay chậm về phía Nam"
            var hull=tank.transform.rotation;var traverse=Quaternion.AngleAxis(turretYaw,Vector3.up);
            turret.rotation=hull*traverse*turretRest;
            float droop=stage=="destroyed"?6:0;
            if(gunPart!=null)gunPart.rotation=hull*traverse*Quaternion.AngleAxis(droop,Vector3.right)*gunRest;
            float mgAim=atBoat?YawTo(water["boatMid"])-turretYaw:0;
            mgYaw=snap?mgAim:Mathf.MoveTowardsAngle(mgYaw,mgAim,60*dt);
            var mgTraverse=Quaternion.AngleAxis(turretYaw+mgYaw,Vector3.up);
            if(mgMount!=null)mgMount.rotation=hull*mgTraverse*mountRest;
            if(mgPart!=null)mgPart.rotation=hull*mgTraverse*Quaternion.AngleAxis(atBoat?9:0,Vector3.right)*mgRest; // Down at the water.
            if(hatchCommander!=null)hatchCommander.rotation=hull*traverse*Quaternion.AngleAxis(stage=="destroyed"?-75:0,Vector3.right)*hatchRest;
            float driven=Vector3.Dot(tank.transform.position-lastTankPosition,tank.transform.forward);lastTankPosition=tank.transform.position;
            if(!snap&&Mathf.Abs(driven)>.0001f)
            {
                foreach(var (part,radius) in tankWheels)part.Rotate(tank.transform.right,driven/radius*Mathf.Rad2Deg,Space.World);
                RollTracks(driven);
            }
        }
    }
}
