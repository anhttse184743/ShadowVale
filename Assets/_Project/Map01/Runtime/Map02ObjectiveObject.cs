using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Inspector-authored interaction on a real prop; no civilian mesh is required.</summary>
    public sealed class Map02ObjectiveObject : MonoBehaviour
    {
        public string saveId;
        public Map02Work work;
        public string prompt;
        [Min(.5f)] public float radius = 2.4f;
        public Transform standPoint;
        public bool WithinReach(Transform player)
        {
            if (player == null || !isActiveAndEnabled) return false;
            var point = standPoint != null ? standPoint.position : transform.position;
            return Vector3.Distance(player.position, point) <= radius;
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.85f,.7f,.3f,.8f);
            Gizmos.DrawWireSphere(standPoint != null ? standPoint.position : transform.position, radius);
        }
    }
}
