using UnityEngine;

namespace ShadowVale.AI.Tactical
{
    /// <summary>
    /// Optional hand-placed hint for the tactical graph baker: forces a node here and marks it as
    /// cover (facing = this transform's forward, the side it protects from) or as an escape route.
    /// Maps work without any: the baker samples the NavMesh and finds cover and chokepoints itself.
    /// </summary>
    public sealed class TacticalNodeMarker : MonoBehaviour
    {
        public enum Kind { Waypoint, Cover, Escape }

        public Kind kind = Kind.Waypoint;

        private void OnDrawGizmos()
        {
            Gizmos.color = kind == Kind.Cover ? Color.cyan : kind == Kind.Escape ? Color.red : Color.white;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * .3f, .6f);
            if (kind == Kind.Cover) Gizmos.DrawRay(transform.position + Vector3.up * .3f, transform.forward * 1.5f);
        }
    }
}
