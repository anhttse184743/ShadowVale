using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
namespace ShadowVale.EnvironmentReview
{
    /// <summary>Registers the saved surface before enabling scene-authored agents in a fresh player.</summary>
    [DefaultExecutionOrder(-32000)]
    public sealed class Map01ArtNavigationReady:MonoBehaviour
    {
        public NavMeshSurface surface;
        public NavMeshAgent[] initiallyEnabled;
        void OnEnable()
        {
            if(surface==null||surface.navMeshData==null)return;
            surface.AddData();
            foreach(var agent in initiallyEnabled)
            {
                if(agent==null||!agent.gameObject.activeInHierarchy)continue;
                if(NavMesh.SamplePosition(agent.transform.position,out var hit,2.5f,agent.areaMask))agent.transform.position=hit.position;
                agent.enabled=true;
                if(!agent.isOnNavMesh&&NavMesh.SamplePosition(agent.transform.position,out hit,3,agent.areaMask))agent.Warp(hit.position);
            }
        }
    }
}
