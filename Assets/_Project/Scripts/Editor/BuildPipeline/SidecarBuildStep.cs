using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace ShadowVale.Editor.BuildPipeline
{
    /// <summary>
    /// After a Windows build, copies the packaged solver sidecar next to the game as
    /// <c>Solver/svl-solver.exe</c> — where <c>SidecarLauncher</c> looks in a build
    /// (<c>SolverProfileConfig.SidecarRelativePath</c>). Source: <c>SVL_SIDECAR_EXE</c>, else the
    /// sibling solver repo's <c>dist/svl-solver.exe</c> (built by <c>scripts/build_sidecar.ps1</c>).
    /// No sidecar found is a warning, not a failure: the game then plays with greedy only.
    /// </summary>
    public sealed class SidecarBuildStep : IPostprocessBuildWithReport
    {
        public const string RelativeTarget = "Solver/svl-solver.exe";

        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            var target = report.summary.platform;
            if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64) return;
            string source = FindSource();
            if (source == null)
            {
                Debug.LogWarning("[Build] no svl-solver.exe to ship (run shadowvale-solver/scripts/build_sidecar.ps1 or set " +
                                 "SVL_SIDECAR_EXE); this build plays with the in-process greedy solver only.");
                return;
            }
            string gameDir = Path.GetDirectoryName(report.summary.outputPath);
            string dest = Path.Combine(gameDir ?? ".", RelativeTarget);
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(source, dest, overwrite: true);
            Debug.Log($"[Build] solver sidecar copied: {source} → {dest}");
        }

        public static string FindSource()
        {
            string env = Environment.GetEnvironmentVariable("SVL_SIDECAR_EXE");
            if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return Path.GetFullPath(env);
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? ".";
            string sibling = Path.Combine(root, "..", "shadowvale-solver", "dist", "svl-solver.exe");
            return File.Exists(sibling) ? Path.GetFullPath(sibling) : null;
        }
    }
}
