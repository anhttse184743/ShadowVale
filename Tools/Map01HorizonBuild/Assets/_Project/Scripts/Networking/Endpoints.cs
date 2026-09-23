namespace ShadowVale.Networking
{
    /// <summary>
    /// Every URL the client calls, in one place. Paths mirror the backend's OpenAPI contract
    /// (shadowvale-backend/contracts/openapi.yaml) and the solver sidecar.
    /// </summary>
    public static class Endpoints
    {
        public const string ApiPrefix = "/api/v1";

        public static string Health(string apiBase) => Join(apiBase, "/health");
        public static string ActiveBundle(string apiBase) => Join(apiBase, ApiPrefix + "/content/bundles/active");
        public static string BundleByVersion(string apiBase, string version) => Join(apiBase, ApiPrefix + "/content/bundles/" + version);
        public static string TelemetrySessions(string apiBase) => Join(apiBase, ApiPrefix + "/telemetry/sessions");

        public static string SolverHealth(string solverBase) => Join(solverBase, "/health");
        public static string SolverVariants(string solverBase) => Join(solverBase, "/variants");
        public static string SolverSolve(string solverBase) => Join(solverBase, "/solve");

        private static string Join(string baseUrl, string path) => baseUrl.TrimEnd('/') + path;
    }
}
