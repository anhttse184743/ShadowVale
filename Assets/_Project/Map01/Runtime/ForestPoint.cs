using System;
using UnityEngine;

namespace ShadowVale.Map01
{
    public enum ForestPointKind { Supplies, Loot, Workbench, Documents, Exit, Hide, Cover }

    public sealed class ForestPoint : MonoBehaviour
    {
        public string id, label;
        public ForestPointKind kind;
        public float radius = 3;
        public bool used;
        public ForestIngredient[] items;
        [Tooltip("Seconds until a looted point refills with the same items; 0 = one-time pickup.")]
        public float restockSeconds;
        [NonSerialized] public float restockAt;

        public void MarkLooted() { used = true; restockAt = Time.time + restockSeconds; }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = kind == ForestPointKind.Hide ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
