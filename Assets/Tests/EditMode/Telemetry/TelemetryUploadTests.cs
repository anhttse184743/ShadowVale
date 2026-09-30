using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using ShadowVale.Telemetry;

namespace ShadowVale.Tests.EditMode.Telemetry
{
    /// <summary>
    /// M7: a finished session is never lost and never blocks the game. It waits on disk while the
    /// backend is down or not built yet, goes when the backend answers, and is set aside (not retried
    /// forever) when the backend calls it malformed. The backend is a local HttpListener.
    /// </summary>
    public class TelemetryUploadTests
    {
        private string _root;

        [SetUp]
        public void CreateRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "svl-telemetry-test-" + Guid.NewGuid().ToString("N"));
            TelemetryPipeline.Reset();
        }

        [TearDown]
        public void DeleteRoot()
        {
            try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch (IOException) { }
            TelemetryPipeline.Reset();
        }

        // ---- Disk queue ------------------------------------------------------------------------

        [Test]
        public void TheQueueKeepsOneFilePerSessionOldestFirstAndDropsTheOldestPastItsLimit()
        {
            var q = new DiskQueue(_root, maxFiles: 3);
            for (int i = 0; i < 4; i++)
            {
                string path = q.Enqueue("s" + i, "{\"i\":" + i + "}");
                File.SetLastWriteTimeUtc(path, new DateTime(2026, 1, 1).AddMinutes(i));
            }
            q.Enqueue("s3", "{\"i\":33}"); // Same session again: replaced, not duplicated.
            var pending = q.Pending();
            CollectionAssert.AreEqual(new[] { "s1", "s2", "s3" }, pending.Select(DiskQueue.SessionIdOf));
            Assert.AreEqual("{\"i\":33}", File.ReadAllText(pending[2]));
            Assert.IsEmpty(Directory.GetFiles(q.PendingDirectory, "*.tmp"), "No half-written temp file left behind.");

            q.Reject(pending[0]);
            q.Complete(pending[1]);
            CollectionAssert.AreEqual(new[] { "s3" }, q.Pending().Select(DiskQueue.SessionIdOf));
            Assert.IsTrue(File.Exists(Path.Combine(q.RejectedDirectory, "s1.json")));
        }

        [Test]
        public void SessionIdsCannotEscapeTheQueueDirectory()
        {
            var q = new DiskQueue(_root);
            string path = q.Enqueue("../../evil", "{}");
            Assert.AreEqual(Path.GetFullPath(q.PendingDirectory), Path.GetDirectoryName(Path.GetFullPath(path)));
        }

        // ---- Uploader --------------------------------------------------------------------------

        [TestCase(200, UploadOutcome.Sent)]
        [TestCase(201, UploadOutcome.Sent)]
        [TestCase(409, UploadOutcome.Sent)]
        [TestCase(400, UploadOutcome.Rejected)]
        [TestCase(422, UploadOutcome.Rejected)]
        [TestCase(404, UploadOutcome.RetryLater)]
        [TestCase(401, UploadOutcome.RetryLater)]
        [TestCase(500, UploadOutcome.RetryLater)]
        [TestCase(503, UploadOutcome.RetryLater)]
        public void StatusCodesDecideWhatHappensToABatch(int status, UploadOutcome expected)
            => Assert.AreEqual(expected, TelemetryUploader.Classify(status));

        [Test]
        public void NoBackendLeavesEverythingQueuedWithoutWaiting()
        {
            var q = new DiskQueue(_root);
            q.Enqueue("a", "{}");
            var clock = Stopwatch.StartNew();
            var r = new TelemetryUploader("http://127.0.0.1:9", TimeSpan.FromSeconds(3)).FlushAsync(q).Result;
            Assert.AreEqual(0, r.Sent);
            Assert.AreEqual(1, r.Waiting);
            Assert.Less(clock.ElapsedMilliseconds, 3500, "Bounded by the upload timeout, off the main thread in the game.");
        }

        [Test]
        public void AFailingBackendKeepsTheBatchAndARecoveredOneTakesIt()
        {
            var statuses = new ConcurrentQueue<int>(new[] { 500, 201, 201 });
            var seen = new ConcurrentQueue<(string key, string body, string path)>();
            using var server = new FakeBackend(statuses, seen);
            var q = new DiskQueue(_root);
            q.Enqueue("first", "{\"session_id\":\"first\"}");
            q.Enqueue("second", "{\"session_id\":\"second\"}");
            File.SetLastWriteTimeUtc(Path.Combine(q.PendingDirectory, "first.json"), DateTime.UtcNow.AddMinutes(-1));
            var uploader = new TelemetryUploader(server.Url);

            var r1 = uploader.FlushAsync(q).Result;
            Assert.AreEqual(0, r1.Sent);
            Assert.AreEqual(2, r1.Waiting, "A 500 stops the flush: the rest are not hammered at a failing backend.");

            var r2 = uploader.FlushAsync(q).Result;
            Assert.AreEqual(2, r2.Sent);
            Assert.IsTrue(r2.Drained);
            Assert.IsEmpty(q.Pending());

            var requests = seen.ToArray();
            Assert.AreEqual(3, requests.Length);
            Assert.AreEqual("first", requests[0].key, "Idempotency-Key is the session id.");
            Assert.AreEqual("first", requests[1].key, "The failed one is retried first.");
            Assert.AreEqual("second", requests[2].key);
            StringAssert.Contains("\"session_id\":\"first\"", requests[1].body);
            Assert.AreEqual("/api/v1/telemetry/sessions", requests[0].path);
        }

        [Test]
        public void AMalformedBatchIsSetAsideAndTheRestStillGo()
        {
            var statuses = new ConcurrentQueue<int>(new[] { 422, 201 });
            using var server = new FakeBackend(statuses, new ConcurrentQueue<(string, string, string)>());
            var q = new DiskQueue(_root);
            q.Enqueue("bad", "{}");
            File.SetLastWriteTimeUtc(Path.Combine(q.PendingDirectory, "bad.json"), DateTime.UtcNow.AddMinutes(-1));
            q.Enqueue("good", "{}");
            var r = new TelemetryUploader(server.Url).FlushAsync(q).Result;
            Assert.AreEqual(1, r.Rejected);
            Assert.AreEqual(1, r.Sent);
            Assert.IsEmpty(q.Pending());
            Assert.IsTrue(File.Exists(Path.Combine(q.RejectedDirectory, "bad.json")));
        }

        [Test]
        public void ThePipelineStoresForResearchAndQueueAndUploadsInTheBackground()
        {
            var statuses = new ConcurrentQueue<int>(new[] { 201 });
            using var server = new FakeBackend(statuses, new ConcurrentQueue<(string, string, string)>());
            var config = new TelemetryConfig
            {
                ApiBaseUrl = server.Url, RootDirectory = Path.Combine(_root, "telemetry"), ResearchDirectory = Path.Combine(_root, "research"),
            };
            var dto = new ShadowVale.Data.Telemetry.TelemetrySessionDto { SessionId = "abc", MapId = "map01" };
            TelemetryPipeline.Store(dto, config);
            string research = Path.Combine(config.ResearchDirectory, ResearchExporter.FileName);
            Assert.AreEqual(1, File.ReadAllLines(research).Length);
            StringAssert.Contains("\"session_id\":\"abc\"", File.ReadAllText(research));
            Assert.AreEqual(1, new DiskQueue(config.RootDirectory).Pending().Count);

            var flush = TelemetryPipeline.TryFlush(config, force: true);
            Assert.IsNotNull(flush);
            Assert.IsTrue(flush.Wait(5000));
            Assert.AreEqual(1, flush.Result.Sent);
            Assert.IsEmpty(new DiskQueue(config.RootDirectory).Pending());
            Assert.IsTrue(File.Exists(research), "The research copy stays after the upload.");
        }

        [Test]
        public void AfterAFailureThePipelineBacksOff()
        {
            var config = new TelemetryConfig
            {
                ApiBaseUrl = "http://127.0.0.1:9", RootDirectory = Path.Combine(_root, "telemetry"), ResearchDirectory = null,
            };
            TelemetryPipeline.Store(new ShadowVale.Data.Telemetry.TelemetrySessionDto { SessionId = "x" }, config);
            var first = TelemetryPipeline.TryFlush(config, force: true);
            Assert.IsTrue(first.Wait(15000));
            Assert.AreEqual(1, first.Result.Waiting);
            Assert.IsNull(TelemetryPipeline.TryFlush(config), "Within the back-off nothing is attempted.");
            Assert.IsNull(TelemetryPipeline.TryFlush(new TelemetryConfig { UploadEnabled = false, RootDirectory = config.RootDirectory }, force: true),
                "Uploads off: never attempted.");
        }

        /// <summary>Answers each POST with the next status from the queue (201 when it runs out).</summary>
        private sealed class FakeBackend : IDisposable
        {
            private readonly HttpListener _listener = new HttpListener();
            private readonly Task _loop;
            public string Url { get; }

            public FakeBackend(ConcurrentQueue<int> statuses, ConcurrentQueue<(string, string, string)> seen)
            {
                int port = 19000 + (Environment.TickCount & 0x7fffffff) % 1000;
                Url = $"http://127.0.0.1:{port}";
                _listener.Prefixes.Add(Url + "/");
                _listener.Start();
                _loop = Task.Run(() =>
                {
                    while (_listener.IsListening)
                    {
                        try
                        {
                            var ctx = _listener.GetContext();
                            string body;
                            using (var reader = new StreamReader(ctx.Request.InputStream)) body = reader.ReadToEnd();
                            seen.Enqueue((ctx.Request.Headers["Idempotency-Key"], body, ctx.Request.Url.AbsolutePath));
                            ctx.Response.StatusCode = statuses.TryDequeue(out int s) ? s : 201;
                            ctx.Response.Close();
                        }
                        catch (Exception) { return; } // Listener stopped.
                    }
                });
            }

            public void Dispose()
            {
                try { _listener.Stop(); _listener.Close(); } catch (Exception) { }
                _loop.Wait(2000);
            }
        }
    }
}
