using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ShadowVale.Data.Content;
using ShadowVale.Networking;
using ShadowVale.Networking.Dto;

namespace ShadowVale.Content
{
    /// <summary>
    /// Pulls the published bundle from the web backend (docs/game-integration.md §2):
    /// manifest → same checksum as the cache? done : bundle with If-None-Match → verify the SHA-256 of
    /// the bytes against the manifest and the ETag. <see cref="FetchAsync"/> runs off the main thread and
    /// only downloads; <see cref="Accept"/> (main thread) validates, stores it in the cache and returns it.
    /// A failure of any kind leaves the cache as it was.
    /// </summary>
    public sealed class BundleFetcher
    {
        public static readonly TimeSpan ManifestTimeout = TimeSpan.FromSeconds(30);
        // Render's free tier sleeps: the first request can take tens of seconds
        public static readonly TimeSpan BundleTimeout = TimeSpan.FromSeconds(60);

        public enum Status { Downloaded, UpToDate, Unavailable }

        public sealed class Result
        {
            public Status Status;
            public ContentManifest Manifest;
            public byte[] Bytes;
            public string Error;
        }

        private readonly BackendClient _client;
        private readonly string _cachedChecksum;

        /// <param name="cachedChecksum">Checksum of the cached bundle (read on the main thread), or null.</param>
        public BundleFetcher(BackendClient client, string cachedChecksum)
        {
            _client = client;
            _cachedChecksum = cachedChecksum;
        }

        public async Task<Result> FetchAsync(CancellationToken ct = default)
        {
            if (!_client.HasKey) return Unavailable("no game key configured (SVL_GAME_KEY or StreamingAssets/Config/backend.json)");

            var manifestResponse = await _client.GetAsync(Endpoints.ContentManifest(_client.ApiBaseUrl), ManifestTimeout, ct: ct).ConfigureAwait(false);
            if (manifestResponse.Outcome != BackendOutcome.Ok)
                return Unavailable(manifestResponse.Status == 404 ? "nothing is published yet" : "manifest: " + manifestResponse.Error);
            var manifest = ContentManifest.Parse(manifestResponse.Text);
            if (manifest == null) return Unavailable("manifest unreadable");
            if (manifest.Checksum == _cachedChecksum) return new Result { Status = Status.UpToDate, Manifest = manifest };

            var bundleResponse = await _client.GetAsync(Endpoints.PublishedBundle(_client.ApiBaseUrl), BundleTimeout, _cachedChecksum, ct).ConfigureAwait(false);
            if (bundleResponse.Outcome == BackendOutcome.NotModified) return new Result { Status = Status.UpToDate, Manifest = manifest };
            if (bundleResponse.Outcome != BackendOutcome.Ok || bundleResponse.Body == null) return Unavailable("bundle: " + bundleResponse.Error);

            var checksum = BundleCache.Sha256(bundleResponse.Body);
            // A publish between the two calls: the next fetch picks the new version up
            if (checksum != manifest.Checksum || (bundleResponse.ETag != null && bundleResponse.ETag.ToLowerInvariant() != checksum))
                return Unavailable("bundle does not match the manifest checksum");
            return new Result { Status = Status.Downloaded, Manifest = manifest, Bytes = bundleResponse.Body };
        }

        /// <summary>Main thread: validate a downloaded bundle, store it as the cached current one and return it.</summary>
        public static bool Accept(Result result, BundleCache cache, out ContentBundle bundle, out string error)
        {
            bundle = null;
            if (result?.Status != Status.Downloaded)
            {
                error = result?.Error ?? "nothing downloaded";
                return false;
            }
            var m = result.Manifest;
            if (!FallbackBundleProvider.TryParse(Encoding.UTF8.GetString(result.Bytes), out bundle, out error, m.VersionId, m.Checksum))
                return false;
            bundle.PublishedAt = m.PublishedAt;
            cache.Save(new BundleCache.Meta { VersionId = m.VersionId, VersionNo = m.VersionNo, Checksum = m.Checksum, PublishedAt = m.PublishedAt }, result.Bytes);
            return true;
        }

        private static Result Unavailable(string error) => new Result { Status = Status.Unavailable, Error = error };
    }
}
