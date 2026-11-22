using System;
using System.Threading.Tasks;
using ShadowVale.AI.Coordination;
using ShadowVale.Content;
using ShadowVale.Core.Services;
using ShadowVale.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowVale.Bootstrap
{
    /// <summary>
    /// Entry point of the game. Created before the first scene loads, whichever scene that is, and kept
    /// across scene loads. Loads the content bundle: the web backend's published one (waiting at most
    /// <see cref="BootWaitSeconds"/>), else the cached one, else the one shipped in StreamingAssets, so
    /// losing the backend never blocks play. A bundle that arrives later is staged and used from the
    /// next scene load, never in the middle of a session.
    /// The solver is not chosen here: SquadCoordinator keeps its own order (SVL_SOLVER_VARIANT, then
    /// ai_settings), and the backend's per-session assignment comes with the telemetry session.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public const float BootWaitSeconds = 3f;

        [SerializeField] private SolverProfileConfig solverProfile;

        public static GameBootstrap Instance { get; private set; }
        public ContentService Content { get; private set; }

        private BundleCache _cache;
        private Task<BundleFetcher.Result> _fetch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateBeforeFirstScene()
        {
            if (Instance == null) new GameObject(nameof(GameBootstrap)).AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Content = new ContentService();
            _cache = new BundleCache();
            var client = BackendClient.FromConfig();
            // Command-line test runs stay offline unless they point at a backend on purpose
            var online = client.HasKey && !(Application.isBatchMode && Environment.GetEnvironmentVariable("SVL_API_BASE_URL") == null);
            if (online)
            {
                var fetcher = new BundleFetcher(client, _cache.Current?.Checksum);
                _fetch = Task.Run(() => fetcher.FetchAsync());
                // Only the first start without a cache waits long; a dead backend refuses at once
                _fetch.Wait(TimeSpan.FromSeconds(BootWaitSeconds));
            }

            if (_fetch != null && _fetch.IsCompleted && TakeDownload(out var fresh))
                Content.Apply(fresh, "backend");
            else if (!Content.Load(new IBundleSource[] { _cache, new FallbackBundleProvider() }, out var log))
            {
                Debug.LogError($"[Bootstrap] no content bundle could be loaded: {log}");
                enabled = false;
                return;
            }
            Debug.Log($"[Bootstrap] content v{Content.Version} from {Content.SourceName} (version id {Content.ContentVersionId ?? "none"}), api = {client.ApiBaseUrl}" +
                      (online ? "" : " (offline: no game key)"));

            ServiceLocator.Register(Content);
            if (solverProfile != null) ServiceLocator.Register(solverProfile);
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Update()
        {
            // A download that missed the boot wait: keep it for the next scene
            if (_fetch == null || !_fetch.IsCompleted) return;
            if (TakeDownload(out var bundle))
            {
                Content.Stage(bundle, "backend");
                Debug.Log($"[Bootstrap] content v{bundle.BundleVersion} downloaded, used from the next scene");
            }
        }

        private bool TakeDownload(out Data.Content.ContentBundle bundle)
        {
            bundle = null;
            var task = _fetch;
            _fetch = null;
            if (task.IsFaulted)
            {
                Debug.LogWarning($"[Bootstrap] content fetch failed: {task.Exception?.GetBaseException().Message}");
                return false;
            }
            var result = task.Result;
            if (result.Status == BundleFetcher.Status.UpToDate)
                return false;
            if (result.Status == BundleFetcher.Status.Unavailable)
            {
                Debug.Log($"[Bootstrap] backend content unavailable: {result.Error}");
                return false;
            }
            if (BundleFetcher.Accept(result, _cache, out bundle, out var error)) return true;
            Debug.LogWarning($"[Bootstrap] downloaded bundle rejected: {error}");
            return false;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode == LoadSceneMode.Single && Content.ApplyStaged())
                Debug.Log($"[Bootstrap] content v{Content.Version} from {Content.SourceName} for {scene.name}");
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }
}
