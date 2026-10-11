using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ShadowVale.Debugging
{
    public enum AiLogCategory { State, Noise, Contact, Combat, Coordination, Solver, Boss }

    public readonly struct AiLogEntry
    {
        public readonly float Time;
        public readonly AiLogCategory Category;
        public readonly string Squad;
        public readonly string Text;

        public AiLogEntry(float time, AiLogCategory category, string squad, string text)
        {
            Time = time; Category = category; Squad = squad ?? ""; Text = text;
        }

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "[{0,7:0.00}] {1,-4} {2,-12} {3}", Time, Squad, Category.ToString().ToUpperInvariant(), Text);
    }

    /// <summary>Ring buffer of the last lines the AI reported. Oldest lines drop off once full.</summary>
    public sealed class AiConsoleLog
    {
        private readonly AiLogEntry[] _entries;
        private int _start, _count;

        public AiConsoleLog(int capacity = 500) { _entries = new AiLogEntry[Mathf.Max(16, capacity)]; }

        public int Count => _count;
        public bool Paused { get; set; }
        /// <summary>Bumped on every change, so the view knows when to scroll.</summary>
        public int Version { get; private set; }

        public AiLogEntry this[int i] => _entries[(_start + i) % _entries.Length];

        public void Add(AiLogEntry entry)
        {
            if (Paused) return;
            if (_count < _entries.Length) _entries[(_start + _count++) % _entries.Length] = entry;
            else { _entries[_start] = entry; _start = (_start + 1) % _entries.Length; }
            Version++;
        }

        public void Clear() { _start = 0; _count = 0; Version++; }

        /// <summary>Writes the buffer to persistentDataPath/debug and returns the file path.</summary>
        public string Export()
        {
            string dir = Path.Combine(Application.persistentDataPath, "debug");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "ai-console-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".log");
            var sb = new StringBuilder();
            for (int i = 0; i < _count; i++) sb.AppendLine(this[i].ToString());
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
            return path;
        }
    }
}
