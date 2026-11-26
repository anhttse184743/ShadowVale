using UnityEngine;

namespace ShadowVale.Map01
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(false, sourceAssembly: "ShadowVale.Map01")]
    public sealed class Map02QuestLayout : MonoBehaviour
    {
        public Map02ObjectiveObject[] objects;
        public Transform scoutShelter, southAssembly, innerLane, southDock;
        public Transform[] observerRoutes;
        public Vector3[] homePositions;
    }
}
