using System.IO;
using System.Text;
using UnityEngine;

namespace ShadowVale.Telemetry
{
    /// <summary>
    /// Appends every finished session, one JSON object per line, to
    /// <c>research/telemetry-sessions.jsonl</c> — next to the coordination-instance logs. The
    /// experiments (capture rate, escape time, coordination score per variant) read this file, so
    /// they never depend on the backend being up or on uploads having gone through.
    /// </summary>
    public sealed class ResearchExporter
    {
        public const string FileName = "telemetry-sessions.jsonl";

        public string Path { get; }

        public ResearchExporter(string directory)
        {
            Path = System.IO.Path.Combine(directory, FileName);
        }

        public void Append(string json)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
                File.AppendAllText(Path, json.Replace('\n', ' ') + "\n", new UTF8Encoding(false));
            }
            catch (IOException e) { Debug.LogWarning("[Telemetry] research export: " + e.Message); }
        }
    }
}
