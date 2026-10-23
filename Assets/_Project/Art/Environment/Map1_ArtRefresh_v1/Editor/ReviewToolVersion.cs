using System;
using System.IO;
using UnityEditor;

namespace ShadowVale.EnvironmentReview
{
    internal static class ReviewToolVersion
    {
        // Informational stamps must never prevent editor callbacks from registering.
        internal static void Schedule(string directory, string version)
        {
            EditorApplication.delayCall += () =>
            {
                try
                {
                    string path = Path.Combine(directory, "tool_version.txt");
                    if (File.Exists(path) && File.ReadAllText(path).Trim() == version) return;
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(path, version);
                }
                catch (IOException) { /* Another editor or a mapped file can hold the stamp. */ }
                catch (UnauthorizedAccessException) { /* Read-only review output is also optional. */ }
            };
        }
    }
}
