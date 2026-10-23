using System;
using System.Net.Http;
using System.Text;
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
    /// Sends queued sessions to <c>POST /api/v1/telemetry/sessions</c>. Off the main thread, never
    /// throws, never blocks gameplay. Each session goes with an <c>Idempotency-Key</c> header equal to
    /// its id, so a retry after a lost response does not store it twice.
    /// Only 400/413/422 reject a batch; any other failure, 404 included (endpoint not deployed yet),
    /// leaves it queued.
    /// </summary>
    public sealed class TelemetryUploader
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        private readonly string _url;
        private readonly TimeSpan _timeout;

        public TelemetryUploader(string apiBaseUrl, TimeSpan? timeout = null)
        {
            _url = Endpoints.TelemetrySessions(apiBaseUrl);
            _timeout = timeout ?? TimeSpan.FromSeconds(10);
        }

        public async Task<UploadOutcome> SendAsync(string sessionId, string json, CancellationToken ct = default)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(_timeout);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
                request.Headers.TryAddWithoutValidation("Idempotency-Key", sessionId);
                using var response = await Http.SendAsync(request, deadline.Token).ConfigureAwait(false);
                return Classify((int)response.StatusCode);
            }
            catch (OperationCanceledException) { return UploadOutcome.RetryLater; }
            catch (HttpRequestException) { return UploadOutcome.RetryLater; }
            catch (Exception) { return UploadOutcome.RetryLater; } // Socket/IO errors surface as several types across runtimes.
        }

        public static UploadOutcome Classify(int status)
        {
            if (status >= 200 && status < 300 || status == 409) return UploadOutcome.Sent;
            if (status == 400 || status == 413 || status == 422) return UploadOutcome.Rejected;
            return UploadOutcome.RetryLater;
        }

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
