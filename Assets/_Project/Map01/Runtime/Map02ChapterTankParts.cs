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
            lastTankPosition=tank.transform.position;
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
            Vector3? target=stage=="breach"?Mission.Player.position:atBoat?water["boatMid"]:(Vector3?)null;
            float aim=target.HasValue?YawTo(target.Value):stage=="destroyed"?turretYaw:0;
            turretYaw=snap?aim:Mathf.MoveTowardsAngle(turretYaw,aim,22*dt); // "nòng pháo quay chậm về phía Nam"
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
                foreach(var (part,radius) in tankWheels)part.Rotate(tank.transform.right,driven/radius*Mathf.Rad2Deg,Space.World);
        }
    }
}
