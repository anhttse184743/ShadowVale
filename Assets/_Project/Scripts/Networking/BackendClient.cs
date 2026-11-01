using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShadowVale.Networking
{
    /// <summary>What the backend said, reduced to what the game can do about it.</summary>
    public enum BackendOutcome
    {
        /// <summary>2xx, or 409 for an upload the backend already has.</summary>
        Ok,
        /// <summary>304: the cached copy is still current.</summary>
        NotModified,
        /// <summary>400/413/422: the request itself is wrong, sending it again cannot help.</summary>
        Rejected,
        /// <summary>Anything else (offline, timeout, 401, 404, 429, 5xx): keep what we have, try later.</summary>
        RetryLater,
    }

    public sealed class BackendResponse
    {
        public BackendOutcome Outcome;
        public int Status;
        public byte[] Body;
        /// <summary>The ETag without quotes (the bundle's SHA-256), or null.</summary>
        public string ETag;
        public string Error;

        public string Text => Body == null ? null : Encoding.UTF8.GetString(Body);
    }

    /// <summary>
    /// The game's only HTTP client for the web backend. Adds <c>X-Game-Key</c>, never throws and
    /// never blocks the main thread. Uses <see cref="HttpClient"/>, which is fine for the Windows
    /// build the proposal targets (it would not work in WebGL).
    /// </summary>
    public sealed class BackendClient
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        private readonly string _gameKey;

        public BackendClient(string apiBaseUrl, string gameKey)
        {
            ApiBaseUrl = apiBaseUrl;
            _gameKey = gameKey;
        }

        public string ApiBaseUrl { get; }
        public bool HasKey => !string.IsNullOrEmpty(_gameKey);

        public static BackendClient FromConfig() => new BackendClient(BackendConfig.ApiBaseUrl, BackendConfig.GameKey);

        public Task<BackendResponse> GetAsync(string url, TimeSpan timeout, string ifNoneMatch = null, CancellationToken ct = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(ifNoneMatch))
                request.Headers.TryAddWithoutValidation("If-None-Match", "\"" + ifNoneMatch + "\"");
            return SendAsync(request, timeout, ct);
        }

        public Task<BackendResponse> SendJsonAsync(HttpMethod method, string url, string json, TimeSpan timeout, CancellationToken ct = default)
        {
            var request = new HttpRequestMessage(method, url) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            return SendAsync(request, timeout, ct);
        }

        public static BackendOutcome Classify(int status)
        {
            if (status >= 200 && status < 300 || status == 409) return BackendOutcome.Ok;
            if (status == 304) return BackendOutcome.NotModified;
            if (status == 400 || status == 413 || status == 422) return BackendOutcome.Rejected;
            return BackendOutcome.RetryLater;
        }

        private async Task<BackendResponse> SendAsync(HttpRequestMessage request, TimeSpan timeout, CancellationToken ct)
        {
            using (request)
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                if (!HasKey)
                    return new BackendResponse { Outcome = BackendOutcome.RetryLater, Error = "no game key configured (SVL_GAME_KEY)" };
                request.Headers.TryAddWithoutValidation("X-Game-Key", _gameKey);
                deadline.CancelAfter(timeout);
                try
                {
                    using (var response = await Http.SendAsync(request, deadline.Token).ConfigureAwait(false))
                    {
                        var status = (int)response.StatusCode;
                        var result = new BackendResponse
                        {
                            Status = status,
                            Outcome = Classify(status),
                            ETag = response.Headers.ETag?.Tag?.Trim('"'),
                        };
                        if (response.StatusCode != HttpStatusCode.NotModified)
                            result.Body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                        if (result.Outcome != BackendOutcome.Ok && result.Outcome != BackendOutcome.NotModified)
                            result.Error = $"HTTP {status}";
                        return result;
                    }
                }
                catch (OperationCanceledException) { return Failed("timed out"); }
                // Socket/IO errors surface as several exception types across runtimes
                catch (Exception e) { return Failed(e.Message); }
            }
        }

        private static BackendResponse Failed(string error) => new BackendResponse { Outcome = BackendOutcome.RetryLater, Error = error };
    }
}
