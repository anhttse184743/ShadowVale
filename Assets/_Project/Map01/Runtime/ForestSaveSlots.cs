using System;
using System.IO;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Checkpoint envelopes. The checkpoint payload remains owned by ForestMission.</summary>
    public static class ForestSaveSlots
    {
        [Serializable] public sealed class Entry
        {
            public int version = 1;
            public string savedAt, location, checkpoint, thumbnail;
            public float playSeconds;
        }
        public const int AutoSlot = 4;
        public const int Count = 5;
        private static string storageRoot;
        private static string Root => storageRoot ?? Application.persistentDataPath;
        public static string PathFor(int slot)
        {
            if (slot < 0 || slot >= Count) throw new ArgumentOutOfRangeException(nameof(slot));
            return Path.Combine(Root, "shadowvale-slot-" + slot + ".json");
        }
        public static bool Exists(int slot) => File.Exists(PathFor(slot));
        public static Entry Read(int slot)
        {
            if (!Exists(slot)) return null;
            return Parse(File.ReadAllText(PathFor(slot)));
        }
        public static Entry Parse(string json)
        {
            var entry = JsonUtility.FromJson<Entry>(json);
            if (entry == null || entry.version != 1 || string.IsNullOrEmpty(entry.checkpoint) ||
                !DateTime.TryParse(entry.savedAt, out _) || float.IsNaN(entry.playSeconds) || float.IsInfinity(entry.playSeconds) || entry.playSeconds < 0)
                throw new InvalidDataException("Bản lưu không hợp lệ hoặc không được hỗ trợ.");
            return entry;
        }
        public static void Write(int slot, Entry entry)
        {
            var path = PathFor(slot);
            Directory.CreateDirectory(Root);
            var temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(entry));
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
        }
        public static void Delete(int slot) { File.Delete(PathFor(slot)); }
        public static int Latest()
        {
            int result = -1; DateTime newest = DateTime.MinValue;
            for (int i = 0; i < Count; i++)
                try { var e = Read(i); if (e != null && DateTime.Parse(e.savedAt).ToUniversalTime() > newest)
                    { newest = DateTime.Parse(e.savedAt).ToUniversalTime(); result = i; } }
                catch (Exception) { /* A damaged slot must not hide other valid slots. */ }
            return result;
        }
        public static string Title(int slot) => slot == AutoSlot ? "TỰ ĐỘNG LƯU KHI THOÁT" : slot == 0 ? "LƯU NHANH" : "Ô LƯU " + slot.ToString("00");
        public static string Duration(float seconds) => TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss");
    }
}
