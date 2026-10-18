using System;
using ShadowVale.AI.Coordination;
using ShadowVale.Core.Services;

namespace ShadowVale.AI.Solvers.Remote
{
    /// <summary>Where the Python sidecar listens: SVL_SOLVER_URL, else the registered solver profile, else loopback:8001.</summary>
    public static class SolverEndpoint
    {
        public const string DefaultBaseUrl = "http://127.0.0.1:8001";

        public static string BaseUrl
        {
            get
            {
                string env = Environment.GetEnvironmentVariable("SVL_SOLVER_URL");
                if (!string.IsNullOrEmpty(env)) return env;
                if (ServiceLocator.TryResolve<SolverProfileConfig>(out var profile) && !string.IsNullOrEmpty(profile.SolverBaseUrl))
                    return profile.SolverBaseUrl;
                return DefaultBaseUrl;
            }
        }
    }
}
