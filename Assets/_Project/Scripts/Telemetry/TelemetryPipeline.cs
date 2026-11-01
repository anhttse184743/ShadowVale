using System;
using System.Threading;
using System.Threading.Tasks;
using ShadowVale.Content;
using ShadowVale.Data.Telemetry;
using UnityEngine;

namespace ShadowVale.Telemetry
{
    /// <summary>
    /// Where a finished session goes: the research export (always), the upload queue (always), and
    /// the backend (in the background, when it answers). One per process, shared by every map's
    /// sessions, so a backend outage backs off once for all of them.
    /// </summary>
    public static class TelemetryPipeline
    {
        private static int _flushing;
        private static int _failures;
        private static long _nextAttemptTicks;

        /// <summary>Sessions sent this process (debug display, tests).</summary>
        public static int SentCount;

        /// <summary>
        /// Writes the batch to disk: the research export (the game's own detailed format) and the upload queue
        /// (<paramref name="uploadJson"/>, the backend's SessionUpload). Returns the research JSON.
        /// </summary>
        public static string Store(TelemetrySessionDto session, TelemetryConfig config, string uploadJson = null)
        {
            string json = ContentJson.Serialize(session);
            try
            {
                if (!string.IsNullOrEmpty(config.ResearchDirectory)) new ResearchExporter(config.ResearchDirectory).Append(json);
                if (!string.IsNullOrEmpty(config.RootDirectory))
                    new DiskQueue(config.RootDirectory, config.MaxQueuedSessions).Enqueue(session.SessionId, uploadJson ?? json);
            }
            catch (Exception e) { Debug.LogWarning("[Telemetry] could not store session: " + e.Message); }
            return json;
        }

        /// <summary>
        /// Starts a background upload of the queue unless one is running, uploads are off, or the
        /// last failure's back-off has not passed. <paramref name="force"/> ignores the back-off.
        /// </summary>
        public static Task<TelemetryUploader.FlushResult> TryFlush(TelemetryConfig config, bool force = false)
        {
            if (config == null || !config.UploadEnabled || string.IsNullOrEmpty(config.RootDirectory)) return null;
            if (!force && DateTime.UtcNow.Ticks < Interlocked.Read(ref _nextAttemptTicks)) return null;
            if (Interlocked.CompareExchange(ref _flushing, 1, 0) != 0) return null;
            var queue = new DiskQueue(config.RootDirectory, config.MaxQueuedSessions);
            var uploader = new TelemetryUploader(config.ApiBaseUrl, gameKey: config.GameKey);
            return Task.Run(async () =>
            {
                try
                {
                    var r = await uploader.FlushAsync(queue).ConfigureAwait(false);
                    Interlocked.Add(ref SentCount, r.Sent);
                    if (r.Sent > 0 || r.Rejected > 0)
                        Debug.Log($"[Telemetry] uploaded {r.Sent} session(s), rejected {r.Rejected}, {r.Waiting} waiting");
                    if (r.Drained) _failures = 0;
                    else
                    {
                        if (r.Sent == 0 && _failures == 0)
                            Debug.Log($"[Telemetry] backend not reachable at {config.ApiBaseUrl}; {r.Waiting} session(s) stay queued");
                        // Some went through: the backend is back, only briefly busy — retry soon.
                        _failures = r.Sent > 0 ? 1 : _failures + 1;
                    }
                    Interlocked.Exchange(ref _nextAttemptTicks, DateTime.UtcNow.AddSeconds(RetryPolicy.DelaySeconds(_failures)).Ticks);
                    return r;
                }
                finally { Interlocked.Exchange(ref _flushing, 0); }
            });
        }

        /// <summary>Forget back-off state (tests; a new backend URL).</summary>
        public static void Reset()
        {
            _failures = 0;
            Interlocked.Exchange(ref _nextAttemptTicks, 0);
            Interlocked.Exchange(ref SentCount, 0);
        }
    }
}
