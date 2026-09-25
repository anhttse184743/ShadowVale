using ShadowVale.AI.Coordination;
using ShadowVale.AI.Solvers;
using ShadowVale.Content;
using ShadowVale.Core.Services;
using UnityEngine;

namespace ShadowVale.Bootstrap
{
    /// <summary>
    /// Entry point of the game (scene 00_Boot). Registers services, loads the content bundle
    /// (backend → cache → fallback) and picks the session's solver. Survives scene loads.
    /// Bundle sources for backend and cache are added in P2 (W3); today only the fallback exists.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private SolverProfileConfig solverProfile;

        public static GameBootstrap Instance { get; private set; }
        public ContentService Content { get; private set; }
        public ISquadSolver Solver { get; private set; }
        public string SolverFallbackReason { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Content = new ContentService();
            var sources = new IBundleSource[]
            {
                // TODO(P2/W3): new BundleFetcher(AppConfig.ApiBaseUrl), new BundleCache(),
                new FallbackBundleProvider(),
            };
            if (!Content.Load(sources, out var log))
            {
                Debug.LogError($"[Bootstrap] no content bundle could be loaded: {log}");
                enabled = false;
                return;
            }
            Debug.Log($"[Bootstrap] content bundle {Content.Version} loaded from {Content.SourceName} ({log})");

            var ai = Content.Ai;
            var budget = ai?.LatencyBudgetMs ?? (solverProfile != null ? solverProfile.FallbackLatencyBudgetMs : 120);
            Solver = SolverFactory.Create(ai?.DefaultSolverVariant ?? "greedy", budget, out var reason);
            SolverFallbackReason = reason;
            if (reason != null) Debug.LogWarning($"[Bootstrap] solver: {reason}");
            Debug.Log($"[Bootstrap] solver variant = {Solver.VariantId}, latency budget = {Solver.LatencyBudgetMs} ms, api = {AppConfig.ApiBaseUrl}, sidecar = {AppConfig.SolverUrl}");

            ServiceLocator.Register(Content);
            ServiceLocator.Register(Solver);
            if (solverProfile != null) ServiceLocator.Register(solverProfile);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
