using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ShadowVale.AI.Coordination;
using ShadowVale.Networking;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace ShadowVale.AI.Solvers.Remote
{
    /// <summary>
    /// Makes sure the solver sidecar is up when a remote variant is chosen: asks /health, and if
    /// nobody answers and the profile allows it, starts the packaged <c>svl-solver.exe</c> (build:
    /// next to the game; editor: the project root, or the sibling solver repo's venv) and waits
    /// for it. The process it started is stopped when the game quits.
    /// </summary>
    public static class SidecarLauncher
    {
        /// <summary>
        /// The one HTTP client for the loopback sidecar, shared with <see cref="RemoteSolverClient"/> so
        /// that /health opens the connection the solves then reuse. No proxy: a loopback call never needs
        /// one, and looking for one is part of what the first request of a session pays. No client-wide
        /// timeout: every call sets its own.
        /// </summary>
        internal static readonly HttpClient Http = new HttpClient(new HttpClientHandler { UseProxy = false })
            { Timeout = Timeout.InfiniteTimeSpan };
        private const int HealthTimeoutMs = 400;
        private static Process _process;

        public static async Task<bool> IsHealthyAsync(string baseUrl, CancellationToken ct = default)
        {
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(HealthTimeoutMs);
                using var response = await Http.GetAsync(Endpoints.SolverHealth(baseUrl), deadline.Token).ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception) { return false; }
        }

        /// <summary>True once /health answers; false if it could not be reached or started in time.</summary>
        public static async Task<bool> EnsureRunningAsync(SolverProfileConfig profile, string baseUrl, CancellationToken ct = default)
        {
            if (await IsHealthyAsync(baseUrl, ct).ConfigureAwait(false)) return true;
            if (profile == null || !profile.AutoLaunchSidecar) return false;
            if (_process == null || _process.HasExited)
            {
                string exe = FindExecutable(profile);
                if (exe == null) { Debug.LogWarning("[Sidecar] no svl-solver executable found; remote solvers fall back to greedy."); return false; }
                int port = new Uri(baseUrl).Port;
                // --parent-pid: the sidecar ends itself when the game ends, crash included. Killing
                // _process alone would leave the server running: the packaged exe is a launcher
                // whose child is the server (and its solver worker).
                int self = Process.GetCurrentProcess().Id;
                _process = Process.Start(new ProcessStartInfo(exe, $"--port {port} --log-level warning --parent-pid {self}")
                    { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(exe) });
                Application.quitting -= Stop;
                Application.quitting += Stop;
                Debug.Log($"[Sidecar] started {exe} on port {port}");
            }
            var until = DateTime.UtcNow.AddSeconds(Math.Max(.5f, profile.SidecarStartupTimeoutSeconds));
            while (DateTime.UtcNow < until && !ct.IsCancellationRequested)
            {
                if (await IsHealthyAsync(baseUrl, ct).ConfigureAwait(false)) return true;
                await Task.Delay(150, ct).ConfigureAwait(false);
            }
            return false;
        }

        private static string FindExecutable(SolverProfileConfig profile)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? ".";
            string[] candidates =
            {
                Environment.GetEnvironmentVariable("SVL_SIDECAR_EXE"),
                Path.Combine(root, profile.SidecarRelativePath ?? ""),
                // Editor, multi-repo layout: <Capstone>/ShadowVale + <Capstone>/shadowvale-solver.
                Path.Combine(root, "..", "shadowvale-solver", "dist", "svl-solver.exe"),
                Path.Combine(root, "..", "shadowvale-solver", ".venv", "Scripts", "svl-api.exe"),
            };
            foreach (var c in candidates)
                if (!string.IsNullOrEmpty(c) && File.Exists(c)) return Path.GetFullPath(c);
            return null;
        }

        public static void Stop()
        {
            try { if (_process != null && !_process.HasExited) _process.Kill(); }
            catch (Exception) { /* Already gone. */ }
            _process = null;
        }
    }
}
