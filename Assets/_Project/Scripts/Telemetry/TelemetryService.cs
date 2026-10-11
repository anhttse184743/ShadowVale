using System.IO;
using ShadowVale.AI.Contracts;
using ShadowVale.AI.Coordination;
using ShadowVale.Content;
using ShadowVale.Core.Services;
using ShadowVale.Data.Telemetry;
using UnityEngine;

namespace ShadowVale.Telemetry
{
    /// <summary>
    /// Runs a <see cref="TelemetrySession"/> for as long as the map it is attached to exists: the
    /// session starts with the map and ends when the map goes (restart, main menu, quit), then is
    /// stored and uploaded by <see cref="TelemetryPipeline"/>. Any map turns it on with one line —
    /// <c>TelemetryService.Attach(missionObject, "map01")</c> — and publishes the shared gameplay
    /// events; nothing else in the game refers to telemetry.
    /// </summary>
    public sealed class TelemetryService : MonoBehaviour
    {
        public TelemetrySession Session { get; private set; }
        public TelemetryConfig Config { get; private set; }
        /// <summary>The finished batch, once the session has ended.</summary>
        public TelemetrySessionDto Result { get; private set; }

        private SquadCoordinator _coordinator;
        private bool _playerDead;

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
            var context = new SessionContext
            {
                MapId = mapId,
                BuildVersion = Application.version,
                Platform = SessionContext.PlatformName(),
                ContentVersion = ServiceLocator.TryResolve<ContentService>(out var content) && content.Version != null
                    ? content.Version : "embedded",
                ConfiguredVariant = ServiceLocator.TryResolve<ISquadSolver>(out var solver) ? solver.VariantId
                    : _coordinator != null ? _coordinator.Settings.Variant : "greedy",
            };
            Session = new TelemetrySession(context, config, () => Time.time);
            Session.Subscribe();
            Result = null;
            // Whatever earlier sessions left queued goes now that the game is up again.
            TelemetryPipeline.TryFlush(config);
        }

        private void Update()
        {
            if (Session == null || Session.Finished) return;
            Session.Tick();
            if (ServiceLocator.TryResolve<IPlayerObservable>(out var player))
            {
                _playerDead = !player.IsAlive;
                if (!_playerDead) Session.SamplePlayer(player.Position);
            }
            TelemetryPipeline.TryFlush(Config); // No-op until the back-off after a failure has passed.
        }

        /// <summary>Ends the session now (idempotent): stores it and starts an upload.</summary>
        public TelemetrySessionDto End(bool upload = true)
        {
            if (Session == null) return null;
            if (Result != null) return Result;
            string log = _coordinator != null && _coordinator.RecordPath != null ? Path.GetFileName(_coordinator.RecordPath) : null;
            Result = Session.Finish(_playerDead, log);
            TelemetryPipeline.Store(Result, Config);
            if (upload) TelemetryPipeline.TryFlush(Config, force: true);
            return Result;
        }

        // Quitting: store only; the process is going away, the next launch uploads it.
        private void OnApplicationQuit() => End(upload: false);

        private void OnDestroy() => End();
    }
}
