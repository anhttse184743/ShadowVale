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

        [Tooltip("Start the sidecar process from GameBootstrap when the bundle asks for a remote variant.")]
        public bool AutoLaunchSidecar = true;

        [Tooltip("Seconds to wait for /health after launching before falling back to greedy.")]
        public float SidecarStartupTimeoutSeconds = 5f;

        [Tooltip("Used only when the content bundle has no ai_settings (should never happen).")]
        public int FallbackLatencyBudgetMs = 120;
    }
}
