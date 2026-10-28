using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ShadowVale.Data.Content;
using UnityEngine;

namespace ShadowVale.Content
{
    /// <summary>
    /// The last bundle downloaded from the backend, kept on disk so the game plays the same content
    /// offline (NFR reliability): <c>persistentDataPath/content/&lt;versionId&gt;.json</c> holds the exact
    /// bytes, <c>current.json</c> says which one is current and its checksum. Writes go through a temp
    /// file and a rename, so a crash never leaves a half-written bundle as current.
    /// </summary>
    public sealed class BundleCache : IBundleSource
    {
        private const string MetaFile = "current.json";

        public sealed class Meta
        {
            public string VersionId;
            public long VersionNo;
            public string Checksum;
            public string PublishedAt;
        }

        private readonly string _directory;

        /// <param name="directory">Defaults to persistentDataPath/content (main thread only).</param>
        public BundleCache(string directory = null)
        {
            _directory = directory ?? Path.Combine(Application.persistentDataPath, "content");
        }

        public string SourceName => "cache";

        public Meta Current
        {
            get
            {
                try
                {
                    var path = Path.Combine(_directory, MetaFile);
                    if (!File.Exists(path)) return null;
                    var o = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(path),
                        new JsonSerializerSettings { DateParseHandling = DateParseHandling.None });
                    return new Meta
                    {
                        VersionId = o.Value<string>("version_id"),
                        VersionNo = o.Value<long?>("version_no") ?? 0,
                        Checksum = o.Value<string>("checksum"),
                        PublishedAt = o["published_at"]?.ToString(),
                    };
                }
                catch (Exception) { return null; }
            }
        }

        public bool TryLoad(out ContentBundle bundle, out string error)
        {
            bundle = null;
            var meta = Current;
            if (meta == null || string.IsNullOrEmpty(meta.VersionId))
            {
                error = "no cached bundle";
                return false;
            }
            try
            {
                var path = BundlePath(meta.VersionId);
                if (!File.Exists(path))
                {
                    error = $"cached bundle {meta.VersionId} missing";
                    return false;
                }
                var bytes = File.ReadAllBytes(path);
                if (Sha256(bytes) != meta.Checksum)
                {
                    error = $"cached bundle {meta.VersionId} is corrupt (checksum mismatch)";
                    return false;
                }
                if (!FallbackBundleProvider.TryParse(Encoding.UTF8.GetString(bytes), out bundle, out error, meta.VersionId, meta.Checksum))
                    return false;
                bundle.PublishedAt = meta.PublishedAt;
                return true;
            }
            catch (Exception e)
            {
                error = $"cache read failed: {e.Message}";
                return false;
            }
        }

        /// <summary>Stores a verified bundle as current and drops older ones.</summary>
        public void Save(Meta meta, byte[] bytes)
        {
            Directory.CreateDirectory(_directory);
            WriteAtomic(BundlePath(meta.VersionId), bytes);
            var json = new JObject
            {
                ["version_id"] = meta.VersionId,
                ["version_no"] = meta.VersionNo,
                ["checksum"] = meta.Checksum,
                ["published_at"] = meta.PublishedAt,
            }.ToString();
            WriteAtomic(Path.Combine(_directory, MetaFile), Encoding.UTF8.GetBytes(json));

            foreach (var old in Directory.GetFiles(_directory, "*.json")
                         .Where(f => Path.GetFileName(f) != MetaFile && f != BundlePath(meta.VersionId)))
            {
                try { File.Delete(old); }
                catch (IOException) { }
            }
        }

        public static string Sha256(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
        }

        private string BundlePath(string versionId) => Path.Combine(_directory, versionId + ".json");

        private static void WriteAtomic(string path, byte[] bytes)
        {
            var temp = path + ".tmp";
            File.WriteAllBytes(temp, bytes);
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
        }
    }
}
