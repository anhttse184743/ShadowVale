using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ShadowVale.Gameplay.Skills;
using UnityEngine;

namespace ShadowVale.Map01
{
    /// <summary>Per-run permanent Engineering and failed-attempt journal. Travels with the player to Map 2.</summary>
    [DisallowMultipleComponent]
    public sealed class SkillCampaignPersistence : MonoBehaviour
    {
        [Serializable] public sealed class Record
        {
            public int version = 1;
            public SkillCareerProgress career = new();
            public List<string> failedAttempts = new();
        }
        public string RunId { get; private set; }
        private PlayerSkills skills;
        private Record record;
        private string writtenJson;
        private bool restoring;

        public static Record Read(string runId)
        {
            string path = ForestSaveSlots.SkillCareerPath(runId);
            if (!File.Exists(path)) return new Record();
            var saved = JsonUtility.FromJson<Record>(File.ReadAllText(path));
            if (saved == null || saved.version != 1 || saved.career == null || saved.career.engineering < 0)
                throw new InvalidDataException("Tiến trình kỹ năng của lượt chơi không hợp lệ.");
            saved.failedAttempts ??= new List<string>();
            return saved;
        }
        public void Initialize(string runId)
        {
            if (RunId == runId) return;
            var loaded = Read(runId); // Read before switching, so damaged data is never replaced with an empty record.
            if (skills != null) skills.ProgressChanged -= Changed;
            RunId = runId; record = loaded; writtenJson = JsonUtility.ToJson(record);
            skills = GetComponent<PlayerSkills>();
            restoring = true;
            try { skills.ResetForNewRun(); skills.RestoreCareer(record.career); }
            finally { restoring = false; }
            skills.ProgressChanged += Changed;
        }
        public void Restore(SkillProgress progress, SkillMissionProgress mission)
        {
            restoring = true;
            try { skills.Restore(progress); skills.RestoreCareer(record.career); skills.RestoreMission(mission); }
            finally { restoring = false; }
            Changed();
        }
        private void Changed()
        {
            if (!restoring) TryFlush(out _);
        }
        public bool TryFlush(out string error)
        {
            error = null;
            if (skills == null || record == null) return true;
            try
            {
                var current = skills.CaptureCareer();
                record.career.engineering = Math.Max(record.career.engineering, current.engineering);
                record.career.repairTransactions = (record.career.repairTransactions ?? Array.Empty<string>())
                    .Concat(current.repairTransactions ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToArray();
                var mission = skills.CaptureMission();
                if (mission != null && mission.failed && !record.failedAttempts.Contains(mission.attemptId)) record.failedAttempts.Add(mission.attemptId);
                if (record.career.engineering == 0 && record.career.repairTransactions.Length == 0 && record.failedAttempts.Count == 0) return true;
                string json = JsonUtility.ToJson(record);
                if (json == writtenJson) return true;
                string path = ForestSaveSlots.SkillCareerPath(RunId);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path + ".tmp", json);
                if (File.Exists(path)) File.Replace(path + ".tmp", path, null); else File.Move(path + ".tmp", path);
                writtenJson = json;
                return true;
            }
            catch (Exception e) { error = "Không thể lưu tiến trình kỹ năng: " + e.Message; return false; }
        }
        private void OnApplicationQuit() { TryFlush(out _); }
        private void OnDestroy() { if (skills != null) skills.ProgressChanged -= Changed; }
    }
}
