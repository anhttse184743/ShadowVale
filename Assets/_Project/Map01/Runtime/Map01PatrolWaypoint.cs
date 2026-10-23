using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>
    /// One point of a hand-placed beat (a child of <see cref="Map01OperationsLayout.supplyBeat"/> or
    /// <see cref="Map01OperationsLayout.radioBeat"/>): the guard stops here this long, facing this
    /// transform's forward. Without it a point gets <see cref="Map01PatrolBeats.DefaultStop"/>.
    /// </summary>
    public sealed class Map01PatrolWaypoint : MonoBehaviour
    {
        [Min(0)] public float waitSeconds = 2f;

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, .75f, .2f);
            Gizmos.DrawWireSphere(transform.position + Vector3.up * .1f, .35f);
            Gizmos.DrawRay(transform.position + Vector3.up * .1f, transform.forward * 1.2f);
            var route = transform.parent;
            if (route == null) return;
            int i = transform.GetSiblingIndex();
            var next = route.GetChild((i + 1) % route.childCount);
            Gizmos.DrawLine(transform.position + Vector3.up * .1f, next.position + Vector3.up * .1f);
        }
    }
}
