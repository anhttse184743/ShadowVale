using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ShadowVale.Networking;

namespace ShadowVale.Telemetry
{
    public enum UploadOutcome
    {
        /// <summary>Stored by the backend (or it already had this session).</summary>
        Sent,
        /// <summary>Backend unreachable, busy or not deployed yet: keep it queued, try later.</summary>
        RetryLater,
        /// <summary>The backend says the batch itself is wrong: retrying cannot help.</summary>
        Rejected,
    }

    /// <summary>
    /// Sends queued sessions to the web backend: <c>PUT /api/game/sessions/{id}</c> with the game key, the
    /// body being the session's <c>SessionUpload</c> (<see cref="SessionUploadMapper"/>). Off the main thread,
    /// never throws, never blocks gameplay. The backend stores events and plan rows by their ids, so a retry
    /// after a lost response stores nothing twice. 400/413/422 set a batch aside; anything else (offline,
    /// no key, 401, 404, 429, 5xx) leaves it queued. A file still in the pre-backend format (snake_case,
    /// no installId) can never be accepted and is set aside without being sent.
    /// </summary>
    public sealed class TelemetryUploader
    {
        private readonly BackendClient _client;
        private readonly TimeSpan _timeout;

        public TelemetryUploader(string apiBaseUrl, TimeSpan? timeout = null, string gameKey = null)
        {
            _client = new BackendClient(apiBaseUrl, gameKey);
            _timeout = timeout ?? TimeSpan.FromSeconds(10);
        }

        public async Task<UploadOutcome> SendAsync(string sessionId, string json, CancellationToken ct = default)
        {
            if (!json.Contains("\"installId\"")) return UploadOutcome.Rejected;
            var response = await _client.SendJsonAsync(HttpMethod.Put, Endpoints.Session(_client.ApiBaseUrl, sessionId), json, _timeout, ct)
                .ConfigureAwait(false);
            return response.Status == 0 ? UploadOutcome.RetryLater : Classify(response.Status);
        }

        public static UploadOutcome Classify(int status) => BackendClient.Classify(status) switch
        {
            BackendOutcome.Ok => UploadOutcome.Sent,
            BackendOutcome.Rejected => UploadOutcome.Rejected,
            _ => UploadOutcome.RetryLater,
        };

        /// <summary>
        /// Sends every queued session, oldest first, and stops at the first one that has to wait (the
        /// backend is down: no point hammering it with the rest).
        /// </summary>
        public async Task<FlushResult> FlushAsync(DiskQueue queue, CancellationToken ct = default)
        {
            var result = new FlushResult();
            foreach (string path in queue.Pending())
            {
                if (ct.IsCancellationRequested) break;
                string json;
                try { json = System.IO.File.ReadAllText(path); }
                catch (System.IO.IOException) { continue; } // Being written this very moment; next flush.
                var outcome = await SendAsync(DiskQueue.SessionIdOf(path), json, ct).ConfigureAwait(false);
                try
                {
                    if (outcome == UploadOutcome.Sent) { queue.Complete(path); result.Sent++; }
                    else if (outcome == UploadOutcome.Rejected) { queue.Reject(path); result.Rejected++; }
                    else { result.Waiting = queue.Pending().Count; return result; }
                }
                catch (System.IO.IOException) { }
            }
            result.Waiting = queue.Pending().Count;
            return result;
        }

        public sealed class FlushResult
        {
            public int Sent, Rejected, Waiting;
            public bool Drained => Waiting == 0;
        }
    }

    /// <summary>
    /// When to try again after a failed flush: 5 s, 10 s, 20 s … capped at 5 minutes. No jitter —
    /// one client per machine, and deterministic timing keeps tests simple.
    /// </summary>
    public static class RetryPolicy
    {
        public const double BaseSeconds = 5, MaxSeconds = 300;

        public static double DelaySeconds(int failures) =>
            failures <= 0 ? 0 : Math.Min(MaxSeconds, BaseSeconds * Math.Pow(2, Math.Min(failures - 1, 16)));
    }
}
