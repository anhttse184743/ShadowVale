using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Editable, scene-authored positions. World geometry and the NavMesh stay unchanged.</summary>
    public sealed class Map01OperationsLayout : MonoBehaviour
    {
        public Transform supply, supplyApproach, radio, radioApproach, commanderPost;
        public GameObject supplyFull, supplyEmpty, radioPowered, radioDisabled;
        [Header("Three operations — editable world references")]
        public Map01EnemyController[] supplyGuards, radioGuards, commanderGuards;
        public Map01EnemyController keyKeeper;
        public Transform warehouseDoor, doorApproach, chestApproach, handoff, cargoSocket;
        public Transform radioRightContact, radioLeftContact;
        public Transform radioWorkbench;
        public bool radioWorkspacePrepared;
        public Transform[] responseSites, observationSites;
        public Map01SupplyProps supplyProps;
        [Min(0)] public int puzzleSeed = 241;
        [Min(18)] public float responseClearance = 18;
        [Min(1)] public float observationSeconds = 2;
        public Vector2 observationDistance = new Vector2(25,45);
        [Min(.5f)] public float interactionRange = 2.4f;
        [Min(1f)] public float supplySeconds = 3.2f, radioSeconds = 3.5f;
        public void Present(bool collected, bool disabled)
        {
            if (supplyProps==null && supplyFull != null) supplyFull.SetActive(!collected);
            if (supplyProps==null && supplyEmpty != null) supplyEmpty.SetActive(collected);
            if (radioPowered != null) radioPowered.SetActive(!disabled);
            if (radioDisabled != null) radioDisabled.SetActive(disabled);
        }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            foreach (var marker in new[] { supplyApproach, radioApproach, commanderPost })
                if (marker != null) { Gizmos.DrawWireSphere(marker.position, .35f); Gizmos.DrawRay(marker.position, marker.forward); }
        }
    }
}
