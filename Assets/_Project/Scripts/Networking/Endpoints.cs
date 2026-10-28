namespace ShadowVale.Networking
{
    /// <summary>
    /// Every URL the client calls, in one place. Backend paths are the game API of ShadowVale_web
    /// (backend/shadowvale_be: GameController, docs/game-integration.md); every call needs the
    /// <c>X-Game-Key</c> header (<see cref="BackendClient"/>). Solver paths are the sidecar's.
    /// </summary>
    public static class Endpoints
    {
        public const string GamePrefix = "/api/game";

        public static string Health(string apiBase) => Join(apiBase, "/health");
        public static string ContentManifest(string apiBase) => Join(apiBase, GamePrefix + "/content/manifest");
        public static string PublishedBundle(string apiBase) => Join(apiBase, GamePrefix + "/content/bundle");
        public static string BundleByVersion(string apiBase, string versionId) => Join(apiBase, GamePrefix + "/content/versions/" + versionId + "/bundle");
        public static string Sessions(string apiBase) => Join(apiBase, GamePrefix + "/sessions");
        public static string Session(string apiBase, string sessionId) => Join(apiBase, GamePrefix + "/sessions/" + sessionId);

        // Old contract, still used by TelemetryUploader until it sends the backend's SessionUpload to Session().
        // The web backend answers 404 here, which keeps every session queued on disk instead of rejected.
        public static string TelemetrySessions(string apiBase) => Join(apiBase, "/api/v1/telemetry/sessions");

        public static string SolverHealth(string solverBase) => Join(solverBase, "/health");
        public static string SolverVariants(string solverBase) => Join(solverBase, "/variants");
        public static string SolverSolve(string solverBase) => Join(solverBase, "/solve");

        private static string Join(string baseUrl, string path) => baseUrl.TrimEnd('/') + path;
    }
}
