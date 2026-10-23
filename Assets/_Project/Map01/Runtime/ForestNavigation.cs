using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace ShadowVale.Map01
{
    // The source scene can be opened and played before an editor bake is available.
    // Build Forest Scene replaces this with persisted navigation data for production.
    [DefaultExecutionOrder(-1000)]
    public sealed class ForestNavigation : MonoBehaviour
    {
        private void Awake()
        {
            var surface = GetComponent<NavMeshSurface>();
            if (surface == null) surface = gameObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            if (surface.navMeshData == null) surface.BuildNavMesh();
            foreach (var agent in FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None)) agent.enabled = true;
        }
    }
}
