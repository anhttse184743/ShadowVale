using ShadowVale.Core.Services;
using UnityEngine;

namespace ShadowVale.AI.Coordination
{
    /// <summary>
    /// Technical constants for reaching the solver sidecar. NOT balance data (weights and budgets
    /// come from the content bundle's ai_settings); this only says where the sidecar lives.
    /// </summary>
    [CreateAssetMenu(menuName = "ShadowVale/Solver Profile", fileName = "SolverProfile_Default")]
    public sealed class SolverProfileConfig : ScriptableObject
    {
        [Tooltip("Base URL of the Python sidecar. Keep it on loopback.")]
        public string SolverBaseUrl = "http://127.0.0.1:8001";

        [Tooltip("Path of svl-solver.exe relative to the game executable's folder (build) or the project root (editor).")]
        public string SidecarRelativePath = "Solver/svl-solver.exe";

        [Tooltip("Start the sidecar process when a remote variant is chosen and nothing answers yet.")]
        public bool AutoLaunchSidecar = true;

        [Tooltip("Seconds to wait for /health after launching before falling back to greedy.")]
        public float SidecarStartupTimeoutSeconds = 5f;

        [Tooltip("Used only when the content bundle has no ai_settings (should never happen).")]
        public int FallbackLatencyBudgetMs = 120;

        private static SolverProfileConfig _defaults;

        /// <summary>
        /// The profile GameBootstrap registered, else these defaults (the same values as
        /// SolverProfile_Default.asset). No map scene holds the Boot object, so a map played on its
        /// own — every map today — never had a profile, and choosing a remote solver silently meant
        /// greedy: the sidecar was neither found nor started.
        /// </summary>
        public static SolverProfileConfig Current
        {
            get
            {
                if (ServiceLocator.TryResolve<SolverProfileConfig>(out var registered) && registered != null) return registered;
                if (_defaults == null)
                {
                    _defaults = CreateInstance<SolverProfileConfig>();
                    _defaults.hideFlags = HideFlags.HideAndDontSave;
                }
                return _defaults;
            }
        }
    }
}
