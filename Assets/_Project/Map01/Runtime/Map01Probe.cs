using UnityEngine;
namespace ShadowVale.Map01
{
    /// <summary>
    /// Raycasts for probes that run every frame: the hits land in one shared buffer instead of a new
    /// array each time. Read <see cref="Hits"/> up to the returned count before the next cast.
    /// </summary>
    internal static class Map01Probe
    {
        private static RaycastHit[] buffer=new RaycastHit[32];
        public static RaycastHit[] Hits=>buffer;
        /// <summary>Every collider on the ray, triggers ignored, in no particular order.</summary>
        public static int Raycast(Vector3 origin,Vector3 direction,float distance,int mask=~0)
        {
            int count;
            // A full buffer may have dropped hits: grow it and ask again.
            while((count=Physics.RaycastNonAlloc(origin,direction,buffer,distance,mask,QueryTriggerInteraction.Ignore))==buffer.Length)
                buffer=new RaycastHit[buffer.Length*2];
            return count;
        }
    }
}
