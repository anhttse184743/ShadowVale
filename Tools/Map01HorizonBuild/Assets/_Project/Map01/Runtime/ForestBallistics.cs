using System;
using UnityEngine;

namespace ShadowVale.Map01
{
    public static class ForestBallistics
    {
        static readonly RaycastHit[] Hits = new RaycastHit[64];
        public static int SolidMask => LayerMask.GetMask("Default", "Obstacle", "Cover", "VisionBlocker", "Enemy", "Player");
        static bool Own(Collider c,Transform owner) => owner!=null && (c.transform==owner || c.transform.IsChildOf(owner));
        public static bool Cast(Vector3 origin,Vector3 direction,float range,Transform shooter,out RaycastHit nearest)
        {
            nearest=default;if(direction.sqrMagnitude<.0001f)return false;
            int count=Physics.RaycastNonAlloc(origin,direction.normalized,Hits,range,SolidMask,QueryTriggerInteraction.Ignore);
            var hits=Hits;
            // Dense foliage must never silently discard the closest blocker.
            if(count==Hits.Length){hits=Physics.RaycastAll(origin,direction.normalized,range,SolidMask,QueryTriggerInteraction.Ignore);count=hits.Length;}
            float distance=float.PositiveInfinity;
            for(int i=0;i<count;i++)if(!Own(hits[i].collider,shooter)&&hits[i].distance<distance){nearest=hits[i];distance=nearest.distance;}
            return distance<float.PositiveInfinity;
        }
        public static bool MuzzleBlocked(Vector3 origin,Transform shooter)
        {
            foreach(var c in Physics.OverlapSphere(origin,.025f,SolidMask,QueryTriggerInteraction.Ignore))
                if(!Own(c,shooter)&&(c.ClosestPoint(origin)-origin).sqrMagnitude<.0004f)return true;
            return false;
        }
    }
}
