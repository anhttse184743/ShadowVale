using System.Collections.Generic;
using UnityEngine;

namespace ShadowVale.Map01
{
    [DefaultExecutionOrder(180)]
    public sealed class Map01EnemyGrounding : MonoBehaviour
    {
        Map01EnemyController owner;
        Animator actor;
        SkinnedMeshRenderer[] skins;
        Mesh baked;
        readonly List<Vector3> vertices = new();
        readonly RaycastHit[] hits = new RaycastHit[64];
        public float LastCorrection { get; private set; }
        public static Map01EnemyGrounding Attach(Map01EnemyController boss)
        {
            var result = boss.GetComponent<Map01EnemyGrounding>() ?? boss.gameObject.AddComponent<Map01EnemyGrounding>();
            result.owner = boss; result.actor = boss.GetComponentInChildren<Animator>();
            result.skins = result.actor.GetComponentsInChildren<SkinnedMeshRenderer>();
            result.baked = new Mesh { name = "Boss ground contact scratch" };
            return result;
        }
        void LateUpdate() { if (owner != null && owner.WeaponPoseAvailable) GroundPose(); }
        public void GroundPose()
        {
            if (actor == null || baked == null) return;
            float lowest = float.PositiveInfinity;
            Vector3 sole = actor.transform.position;
            foreach (var skin in skins)
            {
                if (skin == null || !skin.enabled || !skin.gameObject.activeInHierarchy) continue;
                skin.BakeMesh(baked, true); baked.GetVertices(vertices);
                foreach (var vertex in vertices)
                {
                    var point = skin.transform.TransformPoint(vertex);
                    if (point.y < lowest) { lowest = point.y; sole = point; }
                }
            }
            if (float.IsInfinity(lowest)) return;
            float floor = float.NegativeInfinity;
            int count = Physics.RaycastNonAlloc(new Vector3(sole.x, transform.position.y + 2, sole.z), Vector3.down, hits, 7, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.transform.IsChildOf(transform) || hit.normal.y < .55f || hit.point.y > transform.position.y + .65f) continue;
                // A character's capsule is not the ground beneath this body.
                if (hit.collider is CharacterController || hit.collider.GetComponentInParent<Map01EnemyController>() != null) continue;
                floor = Mathf.Max(floor, hit.point.y);
            }
            if (float.IsNegativeInfinity(floor)) return;
            LastCorrection = floor - lowest;
            actor.transform.position += Vector3.up * LastCorrection;
        }
        void OnDestroy()
        {
            if (baked == null) return;
            if (Application.isPlaying) Destroy(baked); else DestroyImmediate(baked);
        }
    }
}
