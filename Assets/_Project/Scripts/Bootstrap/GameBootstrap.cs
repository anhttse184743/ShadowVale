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
    /// <summary>Loads offline content immediately and checks published content at the title screen.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public const string MenuScene = "01_MainMenu";
        private const float MenuCheckIntervalSeconds = 60f;
        [SerializeField] private SolverProfileConfig solverProfile;

        public static GameBootstrap Instance { get; private set; }
        public ContentService Content { get; private set; }
        public string MenuContentMessage { get; private set; }
        public bool MenuCanRetry { get; private set; }
        public bool MenuPlayBlocked => _fetch != null || MenuCanRetry || !Content.IsLoaded;

        private BundleCache _cache;
        private BackendClient _client;
        private Task<BundleFetcher.Result> _fetch;
        private float _nextMenuCheck;
        private bool _onlineEnabled;

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
            _client = BackendClient.FromConfig();
            _onlineEnabled = _client.HasKey &&
                (!Application.isBatchMode || Environment.GetEnvironmentVariable("SVL_API_BASE_URL") != null);
            if (!Content.Load(new IBundleSource[] { _cache, new FallbackBundleProvider() }, out var log))
                Debug.LogError($"[Bootstrap] no offline bundle could be loaded: {log}");
            ServiceLocator.Register(Content);
            if (solverProfile != null) ServiceLocator.Register(solverProfile);
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name == MenuScene) CheckMenuContent();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            Content.ApplyStaged();
            if (scene.name == MenuScene) CheckMenuContent();
        }

        public void CheckMenuContent()
        {
            if (_fetch != null || SceneManager.GetActiveScene().name != MenuScene) return;
            _nextMenuCheck = Time.realtimeSinceStartup + MenuCheckIntervalSeconds;
            MenuCanRetry = false;
            if (!_onlineEnabled || Application.internetReachability == NetworkReachability.NotReachable)
            {
                MenuContentMessage = "Chế độ ngoại tuyến";
                return;
            }

            // Only advertise a cache checksum when that exact bundle was successfully loaded.
            var fetcher = new BundleFetcher(_client, Content.Bundle?.Checksum);
            MenuContentMessage = "Đang kiểm tra bản cập nhật...";
            _fetch = Task.Run(() => fetcher.FetchAsync());
        }

        private void Update()
        {
            if (_fetch == null)
            {
                if (_onlineEnabled && !MenuCanRetry && Time.realtimeSinceStartup >= _nextMenuCheck &&
                    SceneManager.GetActiveScene().name == MenuScene &&
                    Application.internetReachability != NetworkReachability.NotReachable)
                    CheckMenuContent();
                return;
            }
            if (!_fetch.IsCompleted) return;
            var request = _fetch;
            _fetch = null;
            _nextMenuCheck = Time.realtimeSinceStartup + MenuCheckIntervalSeconds;
            if (request.IsFaulted || request.IsCanceled)
            {
                RefreshFailed(request.Exception?.GetBaseException().Message);
                return;
            }

            var result = request.Result;
            switch (result.Status)
            {
                case BundleFetcher.Status.UpToDate:
                    MenuContentMessage = $"Đã có bản mới nhất · #{result.Manifest.VersionNo}";
                    break;
                case BundleFetcher.Status.Downloaded:
                    try
                    {
                        if (!BundleFetcher.Accept(result, _cache, out var bundle, out var error))
                        {
                            RefreshFailed(error);
                            return;
                        }
                        if (SceneManager.GetActiveScene().name == MenuScene) Content.Apply(bundle, "backend");
                        else Content.Stage(bundle, "backend");
                        MenuContentMessage = $"Đã cập nhật bản mới nhất · #{result.Manifest.VersionNo}";
                    }
                    catch (Exception e) { RefreshFailed(e.Message); }
                    break;
                case BundleFetcher.Status.NotPublished:
                    MenuContentMessage = "Máy chủ chưa có bản phát hành. Đang dùng dữ liệu trên máy.";
                    break;
                case BundleFetcher.Status.Unavailable:
                    MenuCanRetry = !Content.IsLoaded;
                    MenuContentMessage = "Không kết nối được máy chủ. Đang dùng dữ liệu trên máy.";
                    break;
                default:
                    RefreshFailed(result.Error);
                    break;
            }
        }

        private void RefreshFailed(string error)
        {
            MenuCanRetry = Application.internetReachability != NetworkReachability.NotReachable || !Content.IsLoaded;
            MenuContentMessage = MenuCanRetry ? "Chưa cập nhật được nội dung. Vui lòng thử lại." : "Chế độ ngoại tuyến";
            Debug.LogWarning($"[Bootstrap] content refresh: {error}");
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }
}
