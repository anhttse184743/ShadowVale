using ShadowVale.AI.Contracts;
using ShadowVale.AI.Perception;
using ShadowVale.Core.Services;
using UnityEngine;

namespace ShadowVale.AI.Tactical
{
    /// <summary>
    /// Loads the scene's baked tactical graph on map start, registers it as the
    /// <see cref="IWorldQuery"/> service and builds its visibility matrix over the next frames.
    /// A map's mission adds it with <see cref="Attach"/>; nothing else in the map changes.
    /// </summary>
    public sealed class TacticalGraphHost : MonoBehaviour
    {
        [SerializeField] private float visibilityRange = 30f;
        [SerializeField] private int visibilityPairsPerFrame = 600;

        private int _obstructionMask = ~0;
        private ISightOccluder _occluder;

        public TacticalGraph Graph { get; private set; }

        public static TacticalGraphHost Attach(GameObject owner, int obstructionMask, ISightOccluder occluder)
        {
            var host = owner.GetComponent<TacticalGraphHost>();
            if (host == null) host = owner.AddComponent<TacticalGraphHost>();
            host._obstructionMask = obstructionMask;
            host._occluder = occluder;
            return host;
        }

        private void Start()
        {
            var asset = TacticalGraphAsset.LoadForScene(gameObject.scene.name);
            if (asset == null || asset.NodeCount == 0)
            {
                Debug.LogWarning($"[TacticalGraph] No baked graph for scene '{gameObject.scene.name}' " +
                                 "(ShadowVale ▸ AI ▸ Bake Tactical Graph). Squads fall back to fighting individually.", this);
                return;
            }
            Graph = new TacticalGraph(asset) { ObstructionMask = _obstructionMask };
            ServiceLocator.Register<IWorldQuery>(Graph);
            StartCoroutine(VisibilityCache.Build(Graph, visibilityRange, _occluder, visibilityPairsPerFrame, Graph.SetVisibility));
        }

        private void OnDestroy()
        {
            if (Graph != null && ServiceLocator.TryResolve<IWorldQuery>(out var world) && ReferenceEquals(world, Graph))
                ServiceLocator.Unregister<IWorldQuery>();
        }
    }
}
