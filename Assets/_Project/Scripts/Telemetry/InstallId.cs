using System;
using System.IO;

namespace ShadowVale.Telemetry
{
    /// <summary>
    /// The backend's <c>installId</c>: a random GUID made on first use and kept in
    /// <c>persistentDataPath/telemetry/install_id</c>. It only tells sessions of the same copy of the game
    /// apart from others (player count, sessions per player); it is not derived from the machine, the user
    /// or any account, and deleting the file makes a new one.
    /// </summary>
    public static class InstallId
    {
        public const string FileName = "install_id";

        public static string Get(string telemetryRoot)
        {
            var path = Path.Combine(telemetryRoot, FileName);
            try
            {
                if (File.Exists(path) && Guid.TryParse(File.ReadAllText(path).Trim(), out var existing)) return existing.ToString();
                Directory.CreateDirectory(telemetryRoot);
                var id = Guid.NewGuid().ToString();
                File.WriteAllText(path, id);
                return id;
            }
            // Read-only disk: still a valid id, just a new one each launch
            catch (IOException) { return Guid.NewGuid().ToString(); }
            catch (UnauthorizedAccessException) { return Guid.NewGuid().ToString(); }
        }
    }
}
