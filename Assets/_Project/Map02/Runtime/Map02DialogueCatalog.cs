using System;
using System.Collections.Generic;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Exact Map 2 script. Unrecorded lines never borrow a Map 1 recording.</summary>
    public static class Map02DialogueCatalog
    {
        [Serializable] public sealed class Entry
        {
            public string id, conversation, speaker, text;
            public bool radio;
            /// <summary>Optional: another recording this line replays (Hùng's promise echoed after the blast).</summary>
            public string recording;
            public string RecordingId => string.IsNullOrEmpty(recording) ? id : recording;
            public string Caption => speaker + (radio ? " (bộ đàm): " : ": ") + text;
        }

        [Serializable] sealed class Data { public Entry[] entries; }
        const string ResourcePath = "Dialogue/Map02Story/script";

        public static Entry[] Conversation(string id)
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new InvalidOperationException("Missing Map 2 dialogue script.");
            var data = JsonUtility.FromJson<Data>(asset.text);
            var result = new List<Entry>();
            foreach (var line in data.entries ?? Array.Empty<Entry>())
                if (line.conversation == id) result.Add(line);
            if (result.Count == 0) throw new ArgumentException("Unknown Map 2 conversation: " + id, nameof(id));
            return result.ToArray();
        }

        public static bool HasApprovedRecording(Entry line)
        {
            if (line == null) return false;
            var recording = DialogueVoice.Find(line.RecordingId);
            return recording != null && recording.clip != null &&
                recording.radio == line.radio && recording.caption == line.Caption;
        }
    }
}
