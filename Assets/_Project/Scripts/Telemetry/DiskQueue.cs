using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ShadowVale.Telemetry
{
    /// <summary>
    /// Finished sessions waiting to be uploaded, one JSON file each in <c>pending/</c>, so nothing is
    /// lost while the backend is down or the game quits before the upload. A session the backend
    /// refuses outright (malformed) moves to <c>rejected/</c> instead of being retried forever.
    /// Writes go through a temp file and a rename: a crash mid-write never leaves half a batch to send.
    /// </summary>
    public sealed class DiskQueue
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        public string PendingDirectory { get; }
        public string RejectedDirectory { get; }
        public int MaxFiles { get; }

        public DiskQueue(string root, int maxFiles = 50)
        {
            PendingDirectory = Path.Combine(root, "pending");
            RejectedDirectory = Path.Combine(root, "rejected");
            MaxFiles = Math.Max(1, maxFiles);
        }

        /// <summary>Stores a session's batch under its id (replacing an older copy of the same session).</summary>
        public string Enqueue(string sessionId, string json)
        {
            Directory.CreateDirectory(PendingDirectory);
            string path = Path.Combine(PendingDirectory, Safe(sessionId) + ".json");
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, json, Utf8);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            Prune();
            return path;
        }

        /// <summary>Queued batches, oldest first.</summary>
        public IReadOnlyList<string> Pending()
        {
            if (!Directory.Exists(PendingDirectory)) return Array.Empty<string>();
            return new DirectoryInfo(PendingDirectory).GetFiles("*.json")
                .OrderBy(f => f.LastWriteTimeUtc).ThenBy(f => f.Name, StringComparer.Ordinal)
                .Select(f => f.FullName).ToList();
        }

        public static string SessionIdOf(string path) => Path.GetFileNameWithoutExtension(path);

        /// <summary>Uploaded: forget it.</summary>
        public void Complete(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        /// <summary>Refused by the backend: keep it aside for a developer to look at, never resend.</summary>
        public void Reject(string path)
        {
            if (!File.Exists(path)) return;
            Directory.CreateDirectory(RejectedDirectory);
            string to = Path.Combine(RejectedDirectory, Path.GetFileName(path));
            if (File.Exists(to)) File.Delete(to);
            File.Move(path, to);
        }

        /// <summary>A queue that cannot drain (no backend for months) must not grow without bound: drop the oldest.</summary>
        private void Prune()
        {
            var files = Pending();
            for (int i = 0; i < files.Count - MaxFiles; i++) File.Delete(files[i]);
        }

        private static string Safe(string id)
        {
            var sb = new StringBuilder(id.Length);
            foreach (char c in id) sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            return sb.Length > 0 ? sb.ToString() : "session";
        }
    }
}
