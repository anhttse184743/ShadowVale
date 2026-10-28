using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using ShadowVale.AI.Coordination;
using ShadowVale.AI.Fsm;
using ShadowVale.Content;
using ShadowVale.Core.Events;
using ShadowVale.Networking;
using ShadowVale.Telemetry;
using UnityEngine;

namespace ShadowVale.Tests.EditMode.Telemetry
{
    /// <summary>
    /// Against a real web backend (opt-in: SVL_E2E=1 with SVL_API_BASE_URL and SVL_GAME_KEY, a local backend with a
    /// published version; never production): the game's own code pulls the published bundle and caches it, registers
    /// a session, gets a solver and uploads a session the backend accepts in full.
    /// </summary>
    public class BackendEndToEndTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            if (Environment.GetEnvironmentVariable("SVL_E2E") != "1") Assert.Ignore("End-to-end run only (SVL_E2E=1).");
            _root = Path.Combine(Path.GetTempPath(), "svl-e2e-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_root != null && Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void ThePublishedBundleIsDownloadedVerifiedAndCached()
        {
            var client = BackendClient.FromConfig();
            var cache = new BundleCache(Path.Combine(_root, "content"));
            var fetch = Task.Run(() => new BundleFetcher(client, null).FetchAsync());
            Assert.IsTrue(fetch.Wait(TimeSpan.FromSeconds(90)));
            Assert.AreEqual(BundleFetcher.Status.Downloaded, fetch.Result.Status, fetch.Result.Error);
            Assert.IsTrue(BundleFetcher.Accept(fetch.Result, cache, out var bundle, out var error), error);
            Debug.Log($"[E2E] bundle v{bundle.BundleVersion} ({bundle.ContentVersionId}): {bundle.Weapons.Count} weapons, {bundle.Maps.Count} maps");
            Assert.AreEqual(fetch.Result.Manifest.VersionId, bundle.ContentVersionId);
            Assert.IsTrue(bundle.Maps.Any(m => m.SceneName == "Map 1"));

            var again = Task.Run(() => new BundleFetcher(client, cache.Current.Checksum).FetchAsync());
            Assert.IsTrue(again.Wait(TimeSpan.FromSeconds(30)));
            Assert.AreEqual(BundleFetcher.Status.UpToDate, again.Result.Status, "Same checksum: nothing downloaded again.");
            Assert.IsTrue(cache.TryLoad(out var cached, out error), error);
            Assert.AreEqual(bundle.ContentVersionId, cached.ContentVersionId);
        }

        [Test]
        public void ASessionIsRegisteredGetsASolverAndIsAcceptedInFull()
        {
            var client = BackendClient.FromConfig();
            var manifest = Task.Run(() => client.GetAsync(Endpoints.ContentManifest(client.ApiBaseUrl), TimeSpan.FromSeconds(30))).Result;
            var versionId = JObject.Parse(manifest.Text).Value<string>("versionId");

            double t = 0;
            var context = new SessionContext
            {
                MapId = "map01", ContentVersion = "e2e", ContentVersionId = versionId, BuildVersion = Application.version,
                Platform = "editor", InstallId = InstallId.Get(_root), StartedAtUtc = DateTime.UtcNow.AddMinutes(-2),
            };
            var start = new JObject
            {
                ["sessionId"] = context.SessionId, ["installId"] = context.InstallId, ["source"] = "human",
                ["contentVersionId"] = versionId, ["mapCode"] = "map01", ["clientVersion"] = context.BuildVersion,
                ["platform"] = context.Platform, ["startedAt"] = new DateTimeOffset(context.StartedAtUtc).ToString("o"),
            }.ToString();
            var started = Task.Run(() => client.SendJsonAsync(HttpMethod.Post, Endpoints.Sessions(client.ApiBaseUrl), start, TimeSpan.FromSeconds(30))).Result;
            Assert.AreEqual(BackendOutcome.Ok, started.Outcome, started.Error + " " + started.Text);
            var solver = JObject.Parse(started.Text)["solver"];
            Assert.AreEqual(JTokenType.Object, solver?.Type, "A human session gets an A/B arm.");
            string variant = solver.Value<string>("variant");
            Debug.Log($"[E2E] session {context.SessionId}: solver {solver.Value<string>("code")} ({variant})");

            var session = new TelemetrySession(context, new TelemetryConfig(), () => t);
            session.Subscribe();
            try
            {
                session.SamplePlayer(new Vector3(60, 0, 60));
                t = 3; EventBus.Publish(new PlayerContactEvent("guard_c1_0", new Vector3(62, 0, 64), true));
                EventBus.Publish(new EnemyStateChangedEvent("guard_c1_0", EnemyStateId.Patrol, EnemyStateId.TakeCover));
                EventBus.Publish(new EnemyStateChangedEvent("guard_c1_1", EnemyStateId.Patrol, EnemyStateId.TakeCover));
                EventBus.Publish(new ReplanStartedEvent("C1", "first_contact", variant, "e2e-1", 2, 24, 3));
                t = 3.1; EventBus.Publish(new PlanAppliedEvent("C1", variant, "e2e-1", 35, -10, 10, .75, false, null, new[]
                {
                    new AgentAssignment("guard_c1_0", 4, "block", new Vector3(70, 0, 70), 0),
                    new AgentAssignment("guard_c1_1", 7, "flank", new Vector3(50, 0, 72), 0),
                }));
                t = 4; EventBus.Publish(new PlayerAttackEvent("rifle_standard", new Vector3(62, 0, 64)));
                t = 5; EventBus.Publish(new EnemyKilledEvent("guard_c1_1", "weapon", "rifle_standard", new Vector3(50, 0, 72)));
                t = 9; EventBus.Publish(new EnemyStateChangedEvent("guard_c1_0", EnemyStateId.Engage, EnemyStateId.Investigate));
                t = 12; session.Tick();
                EventBus.Publish(new ObjectiveCompletedEvent("map01", "rescue_hung", 0));
                t = 40; EventBus.Publish(new PlayerDiedEvent(new Vector3(65, 0, 61)));
                t = 60;
            }
            finally { session.Unsubscribe(); }
            var dto = session.Finish(playerDead: true);
            var json = SessionUploadMapper.ToUpload(dto, session).ToString(Newtonsoft.Json.Formatting.None);

            var put = Task.Run(() => client.SendJsonAsync(HttpMethod.Put, Endpoints.Session(client.ApiBaseUrl, context.SessionId), json, TimeSpan.FromSeconds(30))).Result;
            Assert.AreEqual(BackendOutcome.Ok, put.Outcome, put.Error + " " + put.Text);
            var counts = JObject.Parse(put.Text);
            Debug.Log("[E2E] upload: " + counts.ToString(Newtonsoft.Json.Formatting.None));
            Assert.AreEqual(dto.Events.Count, (int)counts["events"]["accepted"]);
            Assert.AreEqual(0, (int)counts["events"]["rejected"]);
            Assert.AreEqual(1, (int)counts["coordinationResults"]["accepted"]);

            var resend = Task.Run(() => new TelemetryUploader(client.ApiBaseUrl, gameKey: BackendConfig.GameKey).SendAsync(context.SessionId, json)).Result;
            Assert.AreEqual(UploadOutcome.Sent, resend, "A retry of the same batch is accepted as duplicates.");
        }
    }
}
