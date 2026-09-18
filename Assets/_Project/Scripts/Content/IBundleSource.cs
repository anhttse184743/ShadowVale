using ShadowVale.Data.Content;

namespace ShadowVale.Content
{
    /// <summary>
    /// Where a content bundle comes from. Boot tries sources in order:
    /// backend (BundleFetcher, W3) → local cache (BundleCache, W3) → StreamingAssets fallback.
    /// </summary>
    public interface IBundleSource
    {
        /// <summary>backend | cache | fallback — logged and written to telemetry.</summary>
        string SourceName { get; }

        bool TryLoad(out ContentBundle bundle, out string error);
    }
}
