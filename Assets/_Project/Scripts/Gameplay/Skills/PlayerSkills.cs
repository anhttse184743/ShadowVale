using System;
using UnityEngine;

namespace ShadowVale.Gameplay.Skills
{
    public enum SkillKind { Shooting, Stealth, Engineering }

    [Serializable]
    public sealed class SkillProgress
    {
        public int shooting, stealth, engineering;
    }

    [Serializable]
    public sealed class SkillMissionProgress
    {
        public string id, attemptId;
        public int shooting, stealth;
        public bool failed;
    }

    [Serializable]
    public sealed class SkillCareerProgress
    {
        public int engineering;
        public string[] repairTransactions;
    }

    /// <summary>Read-only balance input. Replace this input when content is connected, not player XP.</summary>
    [Serializable]
    public sealed class SkillBalance
    {
        public int[] shootingThresholds, stealthThresholds, engineeringThresholds;
        public float[] recoilMultipliers, takedownMultipliers, repairBonusFractions;
        public float[] reloadMultipliers;
        /// <summary>Optional, per Engineering level: how far (0..1 of the maximum) a repair kit can bring a gun in the
        /// field; a bench repair always goes to full. Absent: 0.8 at every level.</summary>
        public float[] fieldRepairCaps;

        public int[] Thresholds(SkillKind kind) => kind switch
        {
            SkillKind.Shooting => shootingThresholds,
            SkillKind.Stealth => stealthThresholds,
            _ => engineeringThresholds
        };
        public float[] Multipliers(SkillKind kind) => kind switch
        {
            SkillKind.Shooting => recoilMultipliers,
            SkillKind.Stealth => takedownMultipliers,
            _ => throw new ArgumentException("Engineering uses additive durability fractions, not a multiplier.", nameof(kind))
        };
        public bool IsValid()
        {
            if (reloadMultipliers != null)
            {
                if (shootingThresholds == null || reloadMultipliers.Length == 0 || reloadMultipliers.Length != shootingThresholds.Length || reloadMultipliers[0] != 1f) return false;
                for (int i = 0; i < reloadMultipliers.Length; i++)
                    if (float.IsNaN(reloadMultipliers[i]) || float.IsInfinity(reloadMultipliers[i]) || reloadMultipliers[i] <= 0
                        || (i > 0 && reloadMultipliers[i] > reloadMultipliers[i - 1])) return false;
            }
            if (fieldRepairCaps != null && fieldRepairCaps.Length > 0)
            {
                if (engineeringThresholds == null || fieldRepairCaps.Length != engineeringThresholds.Length) return false;
                for (int i = 0; i < fieldRepairCaps.Length; i++)
                    if (float.IsNaN(fieldRepairCaps[i]) || fieldRepairCaps[i] <= 0 || fieldRepairCaps[i] > 1
                        || (i > 0 && fieldRepairCaps[i] < fieldRepairCaps[i - 1])) return false;
            }
            foreach (SkillKind kind in Enum.GetValues(typeof(SkillKind)))
            {
                var thresholds = Thresholds(kind);
                var multipliers = kind == SkillKind.Engineering ? repairBonusFractions : Multipliers(kind);
                if (thresholds == null || multipliers == null || thresholds.Length < 2 || thresholds.Length > 5
                    || thresholds.Length != multipliers.Length || thresholds[0] != 0
                    || multipliers[0] != (kind == SkillKind.Engineering ? 0f : 1f)) return false;
                for (int i = 0; i < thresholds.Length; i++)
                {
                    if (float.IsNaN(multipliers[i]) || float.IsInfinity(multipliers[i])
                        || (kind == SkillKind.Engineering ? multipliers[i] < 0 || multipliers[i] > 1 : multipliers[i] <= 0)) return false;
                    if (i > 0 && (thresholds[i] <= thresholds[i - 1] || (kind == SkillKind.Shooting
                        ? multipliers[i] > multipliers[i - 1] : multipliers[i] < multipliers[i - 1]))) return false;
                }
            }
            return true;
        }
    }

    /// <summary>Local character progression. All effective parameters are derived from unmodified bases.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSkills : MonoBehaviour
    {
        private SkillBalance balance;
        private SkillProgress progress = new();
        private SkillMissionProgress mission;
        private readonly System.Collections.Generic.HashSet<string> repairTransactions = new(StringComparer.Ordinal);
        public string MissionId => mission?.id;
        public bool MissionFailed => mission != null && mission.failed;
        public event Action<SkillKind, int> LevelChanged;
        public event Action ProgressChanged;

        private void Awake()
        {
            var asset = Resources.Load<TextAsset>("Skills/SkillBalance");
            if (asset == null) throw new InvalidOperationException("Missing Skills/SkillBalance content.");
            Configure(JsonUtility.FromJson<SkillBalance>(asset.text));
        }

        /// <summary>Content adapter entry point; validates the whole input before switching it.</summary>
        public void Configure(SkillBalance content)
        {
            if (content == null || !content.IsValid()) throw new ArgumentException("Invalid skill balance.", nameof(content));
            // Own a copy: applying player upgrades must never mutate the supplied content.
            balance = JsonUtility.FromJson<SkillBalance>(JsonUtility.ToJson(content));
            ProgressChanged?.Invoke();
        }
        public int Xp(SkillKind kind) => kind switch
        {
            SkillKind.Shooting => progress.shooting,
            SkillKind.Stealth => progress.stealth,
            _ => progress.engineering
        };
        public int MaxLevel(SkillKind kind) => balance.Thresholds(kind).Length - 1;
        public int Level(SkillKind kind)
        {
            var thresholds = balance.Thresholds(kind); int level = 0;
            while (level + 1 < thresholds.Length && Xp(kind) >= thresholds[level + 1]) level++;
            return level;
        }
        public int Threshold(SkillKind kind, int level) => balance.Thresholds(kind)[Mathf.Clamp(level, 0, MaxLevel(kind))];
        public float Multiplier(SkillKind kind, int level) => balance.Multipliers(kind)[Mathf.Clamp(level, 0, MaxLevel(kind))];
        public float RecoilMultiplier => Multiplier(SkillKind.Shooting, Level(SkillKind.Shooting));
        public float ReloadMultiplierAt(int level) => balance.reloadMultipliers == null ? 1f
            : balance.reloadMultipliers[Mathf.Clamp(level, 0, MaxLevel(SkillKind.Shooting))];
        public float ReloadMultiplier => ReloadMultiplierAt(Level(SkillKind.Shooting));
        public float TakedownRange(float baseRange) => baseRange * Multiplier(SkillKind.Stealth, Level(SkillKind.Stealth));
        public float RepairBonusAt(int level) => balance.repairBonusFractions[Mathf.Clamp(level, 0, MaxLevel(SkillKind.Engineering))];
        /// <summary>How far a field repair can bring a gun at Engineering <paramref name="level"/> (0..1 of its maximum).</summary>
        public float FieldRepairCapAt(int level) => balance.fieldRepairCaps == null || balance.fieldRepairCaps.Length == 0 ? .8f
            : balance.fieldRepairCaps[Mathf.Clamp(level, 0, balance.fieldRepairCaps.Length - 1)];
        public float FieldRepairCap => FieldRepairCapAt(Level(SkillKind.Engineering));
        public float RepairGainAt(float baseGain, float maxDurability, int level)
        {
            if (float.IsNaN(baseGain) || float.IsInfinity(baseGain) || baseGain < 0
                || float.IsNaN(maxDurability) || float.IsInfinity(maxDurability) || maxDurability <= 0) return 0;
            return Mathf.Min(maxDurability, baseGain + maxDurability * RepairBonusAt(level));
        }
        public float RepairGain(float baseGain, float maxDurability) => RepairGainAt(baseGain, maxDurability, Level(SkillKind.Engineering));

        /// <summary>Future repair transaction hook: only a paid successful repair from damaged to full counts.</summary>
        public bool RecordFullRepair(float before, float after, float maxDurability, bool materialsConsumed, string transactionId = null)
        {
            if (string.IsNullOrWhiteSpace(transactionId) || !materialsConsumed || float.IsNaN(before) || float.IsInfinity(before)
                || float.IsNaN(after) || float.IsInfinity(after) || float.IsNaN(maxDurability) || float.IsInfinity(maxDurability)
                || maxDurability <= 0 || before < 0 || before >= maxDurability || after < maxDurability || after > maxDurability) return false;
            if (!repairTransactions.Add(transactionId)) return false;
            Award(SkillKind.Engineering);
            ProgressChanged?.Invoke(); // Persist the transaction even when XP is already capped.
            return true;
        }

        /// <summary>Call only after the owning gameplay transaction succeeds. Engineering has no producer yet.</summary>
        public void Award(SkillKind kind, int amount = 1)
        {
            if (amount <= 0) return;
            if (kind != SkillKind.Engineering && MissionFailed) return;
            if (Level(kind) == MaxLevel(kind)) return;
            int before = Level(kind);
            int xp = (int)Math.Min((long)Threshold(kind, MaxLevel(kind)), (long)Xp(kind) + amount);
            if (xp == Xp(kind)) return;
            switch (kind)
            {
                case SkillKind.Shooting: progress.shooting = xp; break;
                case SkillKind.Stealth: progress.stealth = xp; break;
                case SkillKind.Engineering: progress.engineering = xp; break;
            }
            ProgressChanged?.Invoke();
            if (Level(kind) > before) LevelChanged?.Invoke(kind, Level(kind));
        }
        public SkillProgress Capture() => new() { shooting = progress.shooting, stealth = progress.stealth, engineering = progress.engineering };
        public void ResetForNewRun()
        {
            progress = new SkillProgress(); mission = null; repairTransactions.Clear(); ProgressChanged?.Invoke();
        }
        public void BeginMission(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Mission id is required.", nameof(id));
            if (MissionId == id) return;
            mission = new SkillMissionProgress { id = id, attemptId = Guid.NewGuid().ToString("N"), shooting = progress.shooting, stealth = progress.stealth };
            ProgressChanged?.Invoke();
        }
        public void CompleteMission() { mission = null; ProgressChanged?.Invoke(); }
        public bool FailMission()
        {
            if (mission == null || mission.failed) return false;
            progress.shooting = mission.shooting; progress.stealth = mission.stealth;
            mission.failed = true; ProgressChanged?.Invoke(); return true;
        }
        public SkillMissionProgress CaptureMission() => mission == null ? null : new SkillMissionProgress
        {
            id = mission.id, attemptId = mission.attemptId, shooting = mission.shooting, stealth = mission.stealth, failed = mission.failed
        };
        public void RestoreMission(SkillMissionProgress saved)
        {
            mission = saved == null || string.IsNullOrEmpty(saved.id) ? null : new SkillMissionProgress
            {
                id = saved.id, attemptId = string.IsNullOrEmpty(saved.attemptId) ? Guid.NewGuid().ToString("N") : saved.attemptId,
                shooting = Math.Max(0, saved.shooting), stealth = Math.Max(0, saved.stealth), failed = saved.failed
            };
            if (MissionFailed) { progress.shooting = mission.shooting; progress.stealth = mission.stealth; }
            ProgressChanged?.Invoke();
        }
        public SkillCareerProgress CaptureCareer() => new() { engineering = progress.engineering, repairTransactions = new System.Collections.Generic.List<string>(repairTransactions).ToArray() };
        public void RestoreCareer(SkillCareerProgress saved)
        {
            if (saved == null) return;
            progress.engineering = Math.Max(progress.engineering, saved.engineering);
            if (saved.repairTransactions != null)
                foreach (var id in saved.repairTransactions) if (!string.IsNullOrWhiteSpace(id)) repairTransactions.Add(id);
            ProgressChanged?.Invoke();
        }
        public void Restore(SkillProgress saved)
        {
            progress = new SkillProgress
            {
                shooting = Math.Max(0, saved?.shooting ?? 0), stealth = Math.Max(0, saved?.stealth ?? 0),
                engineering = Math.Max(0, saved?.engineering ?? 0)
            };
            // Loading never announces a level-up or awards XP. Keep total XP if future content raises the cap.
            ProgressChanged?.Invoke();
        }
    }
}
