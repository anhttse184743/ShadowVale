using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Coordination;
using ShadowVale.Content;
using ShadowVale.Core.Services;
using ShadowVale.Data.Telemetry;
using ShadowVale.Networking;
using UnityEngine;

namespace ShadowVale.Telemetry
{
    /// <summary>
    /// Runs a <see cref="TelemetrySession"/> for as long as the map it is attached to exists: the
    /// session starts with the map and ends when the map goes (restart, main menu, quit), then is
    /// stored and uploaded by <see cref="TelemetryPipeline"/>. Any map turns it on with one line —
    /// <c>TelemetryService.Attach(missionObject, "map01")</c> — and publishes the shared gameplay
    /// events; nothing else in the game refers to telemetry.
    /// At the start the session is registered with the web backend (POST /api/game/sessions), which
    /// answers with the solver this session plays (the A/B arm); offline the map keeps its own solver and
    /// the session still uploads later, just outside the AI comparison.
    /// </summary>
    public sealed class TelemetryService : MonoBehaviour
    {
        public const string SourceEnvironment = "SVL_SESSION_SOURCE";
        private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(10);

        public TelemetrySession Session { get; private set; }
        public TelemetryConfig Config { get; private set; }
        /// <summary>The finished batch, once the session has ended.</summary>
        public TelemetrySessionDto Result { get; private set; }
        /// <summary>The solver configuration code the backend assigned, once it answered.</summary>
        public string AssignedSolver { get; private set; }
        /// <summary>The game's weapon names (rifle, pistol…) as bundle item codes for the weapon stats; set by the map.</summary>
        public Func<string, string> WeaponCode { get; set; }

        private SquadCoordinator _coordinator;
        private bool _playerDead;
        private Task<BackendResponse> _start;

        /// <summary>Starts telemetry for a map on <paramref name="host"/>; null when telemetry is off.</summary>
        public static TelemetryService Attach(GameObject host, string mapId, TelemetryConfig config = null)
        {
            config ??= TelemetryConfig.FromEnvironment();
            if (!config.Enabled) return null;
            var service = host.GetComponent<TelemetryService>();
            if (service == null) service = host.AddComponent<TelemetryService>();
            service.Begin(mapId, config);
            return service;
        }

        private void Begin(string mapId, TelemetryConfig config)
        {
            if (Session != null && !Session.Finished) return;
            Config = config;
            _coordinator = GetComponent<SquadCoordinator>();
            var hasContent = ServiceLocator.TryResolve<ContentService>(out var content);
            var context = new SessionContext
            {
                MapId = mapId,
                BuildVersion = Application.version,
                Platform = SessionContext.PlatformName(),
                ContentVersion = hasContent && content.Version != null ? content.Version : "embedded",
                ContentVersionId = hasContent ? content.ContentVersionId : null,
                InstallId = string.IsNullOrEmpty(config.RootDirectory) ? Guid.NewGuid().ToString() : InstallId.Get(config.RootDirectory),
                Source = Env(SourceEnvironment) == "replay" ? "replay" : "human",
                ConfiguredVariant = ServiceLocator.TryResolve<ISquadSolver>(out var solver) ? solver.VariantId
                    : _coordinator != null ? _coordinator.StartingVariant : "greedy",
            };
            Session = new TelemetrySession(context, config, () => Time.time);
            Session.Subscribe();
            Result = null;
            AssignedSolver = null;
            StartOnBackend(context);
            // Whatever earlier sessions left queued goes now that the game is up again.
            TelemetryPipeline.TryFlush(config);
        }

        private void StartOnBackend(SessionContext context)
        {
            _start = null;
            if (!Config.UploadEnabled) return;
            var client = BackendClient.FromConfig();
            if (!client.HasKey) return;
            var body = new JObject
            {
                ["sessionId"] = context.SessionId,
                ["installId"] = context.InstallId,
                ["source"] = context.Source,
                // Replay runs ask for the configuration they benchmark; humans get an A/B arm
                ["requestedVariant"] = context.Source == "replay" ? Env(SquadCoordinator.VariantEnvironment) : null,
                ["contentVersionId"] = context.ContentVersionId,
                ["mapCode"] = context.MapId,
                ["clientVersion"] = context.BuildVersion,
                ["platform"] = context.Platform,
                ["startedAt"] = new DateTimeOffset(context.StartedAtUtc).ToString("o"),
            }.ToString(Newtonsoft.Json.Formatting.None);
            var url = Endpoints.Sessions(client.ApiBaseUrl);
            _start = Task.Run(() => client.SendJsonAsync(HttpMethod.Post, url, body, StartTimeout));
        }

        private void Update()
        {
            if (Session == null || Session.Finished) return;
            if (_start != null && _start.IsCompleted) ApplyAssignment();
            Session.Tick();
            if (ServiceLocator.TryResolve<IPlayerObservable>(out var player))
            {
                _playerDead = !player.IsAlive;
                if (!_playerDead) Session.SamplePlayer(player.Position);
            }
            TelemetryPipeline.TryFlush(Config); // No-op until the back-off after a failure has passed.
        }

        /// <summary>
        /// The backend's answer to the session start: plan with its solver, weights and budget. A solver
        /// pinned by SVL_SOLVER_VARIANT (batch tests, replay) is kept; null = the map's own settings.
        /// </summary>
        private void ApplyAssignment()
        {
            var response = _start.Result;
            _start = null;
            if (response.Outcome != BackendOutcome.Ok)
            {
                Debug.Log($"[Telemetry] session not registered ({response.Error}); it uploads later, outside the AI comparison");
                return;
            }
            JObject solver;
            try { solver = JObject.Parse(response.Text)["solver"] as JObject; }
            catch (Exception) { return; }
            if (solver == null) return;
            AssignedSolver = solver.Value<string>("code");
            if (_coordinator == null || !string.IsNullOrWhiteSpace(Env(SquadCoordinator.VariantEnvironment))) return;

            var settings = _coordinator.Settings;
            settings.LatencyBudgetMs = solver.Value<int?>("timeBudgetMs") ?? settings.LatencyBudgetMs;
            if (solver["quboWeights"] is JObject w)
                settings.Weights = new CoordinationWeights(
                    w.Value<double?>("weight_coverage") ?? settings.Weights.Coverage,
                    w.Value<double?>("weight_redundancy") ?? settings.Weights.Redundancy,
                    w.Value<double?>("weight_flanking") ?? settings.Weights.Flanking,
                    w.Value<double?>("weight_cover") ?? settings.Weights.Cover,
                    w.Value<double?>("weight_distance") ?? settings.Weights.Distance);
            // The variant's id is what the sidecar runs; the configuration code is what the plans report
            var variant = solver.Value<string>("variant");
            settings.Variant = variant ?? settings.Variant;
            // Recreated even for the same variant, so the new latency budget applies
            if (variant != null) _coordinator.SetVariant(variant);
            Session.Context.ConfiguredVariant = AssignedSolver ?? variant;
            Debug.Log($"[Telemetry] backend assigned solver {AssignedSolver} ({variant}, {settings.LatencyBudgetMs} ms)");
        }

        /// <summary>Ends the session now (idempotent): stores it and starts an upload.</summary>
        public TelemetrySessionDto End(bool upload = true)
        {
            if (Session == null) return null;
            if (Result != null) return Result;
            string log = _coordinator != null && _coordinator.RecordPath != null ? Path.GetFileName(_coordinator.RecordPath) : null;
            Result = Session.Finish(_playerDead, log);
            var uploadJson = SessionUploadMapper.ToUpload(Result, Session, WeaponCode).ToString(Newtonsoft.Json.Formatting.None);
            TelemetryPipeline.Store(Result, Config, uploadJson);
            if (upload) TelemetryPipeline.TryFlush(Config, force: true);
            return Result;
        }

        // Quitting: store only; the process is going away, the next launch uploads it.
        private void OnApplicationQuit() => End(upload: false);

        private void OnDestroy() => End();

        private static string Env(string key)
        {
            try
            {
                var v = Environment.GetEnvironmentVariable(key);
                return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
            }
            catch { return null; }
        }
    }
}
