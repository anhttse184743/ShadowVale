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
        [Header("Beats (Map01PatrolBeats) — empty: a ring round the area is laid out at run time")]
        [Tooltip("Hand-placed beat round the supply area: children in order are the points (Map01PatrolWaypoint for the stop, forward = where to look). Bake one to edit: ShadowVale > Map 1 > Bake patrol beats.")]
        public Transform supplyBeat;
        [Tooltip("Hand-placed beat round the radio station; the radio responders walk it too once they have checked the station.")]
        public Transform radioBeat;
        public Map01SupplyProps supplyProps;
        public Map01SabotageProps sabotage;
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
            // The beats as the guards will walk them (hand-placed, else the ring laid out at run time).
            Gizmos.color = new Color(1f, .75f, .2f);
            DrawBeat(Map01PatrolBeats.SupplyBeat(this));
            DrawBeat(Map01PatrolBeats.RadioBeat(this));
        }

        private static void DrawBeat(Map01PatrolBeats.Beat beat)
        {
            for (int i = 0; i < beat.Count; i++)
            {
                var p = beat.Points[i] + Vector3.up * .15f;
                Gizmos.DrawWireSphere(p, .3f);
                Gizmos.DrawLine(p, beat.Points[(i + 1) % beat.Count] + Vector3.up * .15f);
                Gizmos.DrawRay(p, Quaternion.Euler(0, beat.Yaws[i], 0) * Vector3.forward);
            }
        }
    }
}
