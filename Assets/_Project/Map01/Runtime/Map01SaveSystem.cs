using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ShadowVale.Map01
{
    /// <summary>Save/load for Map 1 — checkpoint format v7, the first on the detailed layout (base
    /// in the underground shelter, jetty B, camps C1–C3). Older saves hold positions on the old
    /// Map 1's terrain, which no longer exists, so they are refused rather than loaded.</summary>
    [DefaultExecutionOrder(100)]
    public sealed partial class Map01SaveSystem : MonoBehaviour
    {
        /// <summary>The first checkpoint version on the current map layout.</summary>
        public const int LayoutVersion = 7;
        public const string OldLayoutMessage = "Bản lưu thuộc Map 1 cũ (bản đồ trước khi làm lại) — hãy chọn Chơi mới.";

        [Serializable]
        private sealed class CheckpointData
        {
            public int version = LayoutVersion, stage, stones;
            public ShadowVale.Gameplay.Skills.SkillProgress skills;
            public ShadowVale.Gameplay.Skills.SkillMissionProgress skillMission;
            public string skillRunId, missionStart;
            public MapExplorationState exploration;
            public float hp, stamina;
            public Vector3 player, hung;
            public bool alarmed, crouched;
            public string[] used;
            public ForestIngredient[] items;
            public string crafting;
            public float craftRemaining, playerYaw, hungYaw;
            public int equippedWeapon; // Older saves also carry quickSlots; the shortcut bar is gone and JsonUtility skips it.
            public int scoutedCamps; // Bitmask of camps logged during the scouting order; 0 in older saves.
            public float attackRemaining;
            public int roundsInMagazine = -1;
            public int pistolRoundsInMagazine; // Optional addition to v7: older saves have an empty K54 and keep their reserve.
            public int mosinRoundsInMagazine;
            public float hungHealth = -1; // -1 in older saves: unhurt.
            public string scoutStart; // The checkpoint at Hùng's scouting order, in saves made while scouting.
            public Map01EnemyController.Snapshot[] enemies;
            public Map01Rescue.Snapshot rescue;
            public Map01Story.Snapshot story;
            public string rescueStart, escortStart, operationStart;
            public Map01Inventory.Layout bag;      // Where each stack sits, the slots and the throwable picked; null in older saves.
            public Map01Drops.Pile[] dropped;     // What Nam put on the ground and has not picked up again.
            public Map01Logistics.State logistics; // The run's loot seed, rolled guard packs, the base store's points; seed 0 in older saves.
            public Map01C3Mission.State c3;        // The C3 command post: kẻng, telephone line, check-ins, who has been called; null in older saves.
        }

        /// <summary>True while a checkpoint load is pending/applying — blocks manual saves and
        /// counts as "Stopped" so nothing runs a frame on half-restored state.</summary>
        public static bool IsRestoring => pendingCheckpoint != null;
        private static string pendingCheckpoint;
        private static float pendingSeconds;
        /// <summary>Shown instead of the usual "restored" line once the pending checkpoint is applied.</summary>
        private static string pendingMessage;
        /// <summary>The checkpoint taken when Hùng gave the scouting order (MarkScoutingStart).</summary>
        private static string scoutStart;

        private Map01Mission mission;
        private Map01Quest quest;
        private Map01Inventory inventory;
        private string rescueStart, escortStart;
        private string missionStart;
        private SkillCampaignPersistence skillCampaign;
        private bool syncingSkills;
        private string operationStart;
        public bool HasOperationCheckpoint=>!string.IsNullOrEmpty(operationStart);
        private string SavePath => Path.Combine(Path.GetDirectoryName(ForestSaveSlots.PathFor(0)), "shadowvale-map01-checkpoint.json");

        /// <summary>The real map. Any other scene running this mission (the "Test Map 1 AI" copy) is a
        /// sandbox: it saves to files of its own and never touches the game's slots or checkpoint.</summary>
        public const string MainScene = "Map 1";
        private string SceneName => gameObject.scene.name;
        public bool IsSandbox => SceneName != MainScene;
        private string SandboxPath(int slot) => Path.Combine(Path.GetDirectoryName(ForestSaveSlots.PathFor(0)), "sandbox",
            string.Concat(SceneName.Select(c => char.IsLetterOrDigit(c) ? c : '_')) + "-slot-" + slot + ".json");

        private void Awake()
        {
            mission = GetComponent<Map01Mission>();
            quest = GetComponent<Map01Quest>();
            inventory = GetComponent<Map01Inventory>();
        }
        private void Start()
        {
            if (!mission.IsInitialized) return;
            try { EnsureSkillCampaign(); }
            catch (Exception e) { mission.Say("Không thể đọc tiến trình kỹ năng: " + e.Message, 12); enabled = false; return; }
            if (pendingCheckpoint != null) Invoke(nameof(Restore), .1f);
            else SyncSkillMission();
        }
        private void LateUpdate() { if (mission.IsInitialized && skillCampaign != null) SyncSkillMission(); }

        private CheckpointData PrepareRetry(CheckpointData data)
        {
            SyncSkillMission();
            var baseline = mission.Skills.CaptureMission();
            if (baseline != null)
            {
                baseline.failed = false; baseline.attemptId = Guid.NewGuid().ToString("N");
                data.skillMission = baseline;
                data.skills = new ShadowVale.Gameplay.Skills.SkillProgress
                { shooting = baseline.shooting, stealth = baseline.stealth, engineering = mission.Skills.Xp(ShadowVale.Gameplay.Skills.SkillKind.Engineering) };
            }
            else { data.skills = mission.Skills.Capture(); data.skillMission = null; }
            data.skillRunId = skillCampaign.RunId; data.missionStart = missionStart;
            data.exploration = GetComponent<Map01Minimap>()?.CaptureExploration();
            if (!skillCampaign.TryFlush(out var error)) throw new IOException(error);
            return data;
        }
        public bool RestartCurrentMission()
        {
            if(quest.Stage>=Map01Quest.ScoutStage&&quest.Stage<=Map01Quest.ReportBossStage&&HasOperationCheckpoint)return RestartOperation();
            SyncSkillMission();
            if (string.IsNullOrEmpty(missionStart)) return false;
            var data = PrepareRetry(JsonUtility.FromJson<CheckpointData>(missionStart));
            pendingCheckpoint = JsonUtility.ToJson(data); pendingSeconds = mission.PlaySeconds;
            pendingMessage = "Thử lại nhiệm vụ. Shooting và Stealth đã trở về mốc đầu nhiệm vụ.";
            Time.timeScale = 1; SceneManager.LoadScene("Map 1"); return true;
        }

        private static CheckpointData NormalizeFailedAttempt(CheckpointData data, string json)
        {
            string runId = RunId(data, json);
            var record = SkillCampaignPersistence.Read(runId);
            data.skillRunId = runId;
            var state = data.skillMission;
            if (state == null || string.IsNullOrEmpty(state.id) || !record.failedAttempts.Contains(state.attemptId)) return data;
            var start = string.IsNullOrEmpty(data.missionStart) ? data : JsonUtility.FromJson<CheckpointData>(data.missionStart);
            start.skills = new ShadowVale.Gameplay.Skills.SkillProgress { shooting = state.shooting, stealth = state.stealth, engineering = record.career.engineering };
            start.skillRunId = runId;
            start.exploration = data.exploration;
            start.skillMission = new ShadowVale.Gameplay.Skills.SkillMissionProgress
            { id = state.id, attemptId = Guid.NewGuid().ToString("N"), shooting = state.shooting, stealth = state.stealth };
            start.operationStart = data.operationStart; start.missionStart = data.missionStart; start.rescueStart = data.rescueStart; start.escortStart = data.escortStart; start.scoutStart = data.scoutStart;
            return start;
        }

        public string ManualSaveBlockReason()
        {
            if (mission.Cinematic || GetComponent<Map01Rescue>().NonSaveable || GetComponent<Map01Story>().Operating || Map01NamActions.For(mission).Busy) return "Đang thao tác hoặc phát cảnh. Vui lòng đợi cảnh kết thúc.";
            if (pendingCheckpoint != null) return "Đang tải bản lưu. Vui lòng đợi giây lát.";
            if (mission.PlayerHealth <= 0) return "Không thể lưu khi nhân vật đã gục ngã.";
            if (GetComponent<Map01Rescue>().HungDown) return "Hùng đã hy sinh. Làm lại đoạn giải cứu trước khi lưu.";
            if (GetComponent<Map01Scouting>().FailedRun) return "Trinh sát thất bại. Làm lại nhiệm vụ trước khi lưu.";
            if (quest.Stage == Map01Quest.CompleteStage) return "Màn chơi đã kết thúc. Về menu để tự lưu kết quả.";
            if (quest.Stage == Map01Quest.BossStage) return "Đang đối đầu chỉ huy địch. Hãy thoát nguy hiểm trước khi lưu thủ công.";
            if (inventory.IsCrafting) return "Hãy hoàn thành chế tạo trước khi lưu thủ công.";
            if (mission.Enemies.Any(g => g.Alive && (g.Alerted || Vector3.Distance(g.transform.position, mission.player.position) <= 18)))
                return "Lính đang ở gần hoặc đang cảnh giác. Hãy thoát nguy hiểm trước khi lưu thủ công.";
            return null;
        }
        public bool CanSave() => ManualSaveBlockReason() == null;

        public bool AutoSaveOnExit(out string error)
        {
            error = null;
            if (!mission.IsInitialized) return true;
            if (mission.IsInitialized)
            {
                try { SyncSkillMission(); if (!skillCampaign.TryFlush(out error)) return false; }
                catch (Exception e) { error = "Không thể lưu tiến trình kỹ năng: " + e.Message; return false; }
            }
            if (pendingCheckpoint != null) { error = "Đang khôi phục bản lưu. Vui lòng thử lại sau giây lát."; return false; }
            // Save a healthy mission-start checkpoint on failure, never the failed world state.
            if (mission.PlayerHealth <= 0 || GetComponent<Map01Rescue>().HungDown || GetComponent<Map01Scouting>().FailedRun || mission.Skills.MissionFailed)
            {
                try
                {
                    if (string.IsNullOrEmpty(missionStart)) { error = "Chưa có mốc đầu nhiệm vụ để lưu lại."; return false; }
                    var data = PrepareRetry(JsonUtility.FromJson<CheckpointData>(missionStart));
                    ForestSaveSlots.Write(ForestSaveSlots.AutoSlot, new ForestSaveSlots.Entry
                    {
                        sceneName = "Map 1", savedAt = DateTime.UtcNow.ToString("o"), playSeconds = mission.PlaySeconds,
                        location = Map01Quest.SaveLocations[Mathf.Clamp(data.stage, 0, Map01Quest.LastStage)], checkpoint = JsonUtility.ToJson(data)
                    });
                    return true;
                }
                catch (Exception e) { error = "Không thể lưu mốc đầu nhiệm vụ: " + e.Message; return false; }
            }
            if (mission.Cinematic || GetComponent<Map01Rescue>().NonSaveable || GetComponent<Map01Story>().Operating || Map01NamActions.For(mission).Busy) return true;
            return SaveSlot(ForestSaveSlots.AutoSlot, out error, true);
        }

        public bool SaveSlot(int slot, out string error, bool automatic = false)
        {
            error = null;
            if (!mission.IsInitialized) { error = "Màn chơi chưa sẵn sàng để lưu."; return false; }
            // Field crafting owns a cancelable handling pose. Refund it before the general busy-action save guard.
            inventory.CancelFieldCraft(false);
            try { SyncSkillMission(); if (!skillCampaign.TryFlush(out error)) return false; }
            catch (Exception e) { error = "Không thể lưu tiến trình kỹ năng: " + e.Message; return false; }
            if (mission.Cinematic || GetComponent<Map01Story>().Operating || Map01NamActions.For(mission).Busy || GetComponent<Map01Rescue>().NonSaveable || GetComponent<Map01Rescue>().Failed
                || GetComponent<Map01Scouting>().FailedRun || mission.Skills.MissionFailed || mission.PlayerHealth<=0) { error = "Cannot save during a cinematic or failed mission."; return false; }
            if (!automatic && (error = ManualSaveBlockReason()) != null) { mission.Say(error); return false; }
            var data = Capture();
            if (IsSandbox)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(SandboxPath(slot)));
                    File.WriteAllText(SandboxPath(slot), JsonUtility.ToJson(data));
                    if (!automatic) mission.Say($"Đã lưu ({SceneName} — riêng, không đụng tới bản lưu của Map 1).");
                    return true;
                }
                catch (Exception e) { error = "Không thể lưu: " + e.Message; mission.Say(error); return false; }
            }
            try
            {
                string thumbnail = null;
                try { thumbnail = CaptureThumbnail(); } catch (Exception) { /* A missing preview must not prevent saving progress on exit. */ }
                ForestSaveSlots.Write(slot, new ForestSaveSlots.Entry
                {
                    sceneName = SceneManager.GetActiveScene().name,
                    savedAt = DateTime.UtcNow.ToString("o"), playSeconds = mission.PlaySeconds,
                    location = Map01Quest.SaveLocations[Mathf.Clamp(quest.Stage, 0, Map01Quest.LastStage)],
                    checkpoint = JsonUtility.ToJson(data), thumbnail = thumbnail
                });
                mission.Say("Đã lưu tiến trình."); return true;
            }
            catch (Exception e) { error = "Không thể lưu: " + e.Message; mission.Say(error); return false; }
        }

        private CheckpointData Capture(bool includeRetry = true)
        {
            SyncSkillMission();
            return new CheckpointData
            {
                story = GetComponent<Map01Story>()?.Capture(),
                stage = quest.Stage, stones = inventory.Stones, hp = mission.PlayerHealth, stamina = mission.Stamina,
                player = mission.player.position, hung = mission.hung.position, alarmed = mission.Alarmed,
                used = mission.Points.Where(p => p.used).Select(p => p.id).ToArray(),
                items = inventory.CaptureItems(),
                bag = inventory.CaptureLayout(),
                dropped = Map01Drops.For(mission).Capture(),
                logistics = Map01Logistics.For(mission).Capture(),
                c3 = GetComponent<Map01C3Mission>()?.Capture(),
                skills = mission.Skills.Capture(),
                exploration = GetComponent<Map01Minimap>()?.CaptureExploration(),
                skillMission = mission.Skills.CaptureMission(), skillRunId = skillCampaign.RunId,
                missionStart = includeRetry ? missionStart : null,
                crafting = inventory.CraftingId, craftRemaining = inventory.CraftRemaining,
                playerYaw = mission.player.eulerAngles.y, hungYaw = mission.hung.eulerAngles.y,
                crouched = mission.Crouched,
                equippedWeapon = mission.ModernCombat != null ? (int)mission.ModernCombat.EquippedKind : 0,
                attackRemaining = mission.ModernCombat != null ? mission.ModernCombat.AttackCooldownRemaining : 0,
                roundsInMagazine = mission.ModernCombat != null ? mission.ModernCombat.MagazineRounds(ShadowVale.Gameplay.Combat.WeaponKind.Rifle) : -1,
                pistolRoundsInMagazine = mission.ModernCombat != null ? mission.ModernCombat.MagazineRounds(ShadowVale.Gameplay.Combat.WeaponKind.Pistol) : 0,
                mosinRoundsInMagazine = mission.ModernCombat != null ? mission.ModernCombat.MagazineRounds(ShadowVale.Gameplay.Combat.WeaponKind.Sniper) : 0,
                hungHealth = GetComponent<Map01Rescue>().HungHealth,
                rescue = GetComponent<Map01Rescue>().Capture(),
                rescueStart = includeRetry ? rescueStart : null, escortStart = includeRetry ? escortStart : null,
                operationStart=includeRetry?operationStart:null,
                enemies = mission.Enemies.Select(e => e.Capture()).ToArray(),
                scoutedCamps = GetComponent<Map01Scouting>().FoundMask,
                // A save made mid-scouting carries the moment the order was given, so a failed
                // run after loading it still starts over from there.
                scoutStart = quest.Stage == Map01Quest.ScoutStage ? scoutStart : null
            };
        }

        private string CaptureThumbnail()
        {
            var rt = RenderTexture.GetTemporary(320, 180, 24);
            var previous = mission.gameCamera.targetTexture; var active = RenderTexture.active;
            var texture = new Texture2D(320, 180, TextureFormat.RGB24, false);
            try
            {
                mission.gameCamera.targetTexture = rt; mission.gameCamera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 320, 180), 0, 0); texture.Apply();
                return Convert.ToBase64String(texture.EncodeToJPG(70));
            }
            finally { mission.gameCamera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); Destroy(texture); }
        }

        public static void BeginGame(int slot = -1)
        {
            FlushCurrentSkillRun();
            pendingCheckpoint = null; pendingSeconds = 0; pendingMessage = null;
            if (slot < 0) scoutStart = null;
            string sceneName = "Map 1";
            if (slot >= 0)
            {
                Map01OpeningCutscene.CancelPending();
                var entry = ForestSaveSlots.Read(slot);
                if (entry == null) throw new IOException("Ô lưu trống.");
                sceneName = string.IsNullOrEmpty(entry.sceneName) ? "Map 1" : entry.sceneName;
                if (sceneName != "Map 1") throw new IOException("Bản lưu thuộc màn chơi chưa được hỗ trợ.");
                var data = JsonUtility.FromJson<CheckpointData>(entry.checkpoint);
                if (data == null || data.version < 4 || data.items == null || data.used == null)
                    throw new IOException("Dữ liệu checkpoint bị hỏng.");
                if (data.version < LayoutVersion) throw new IOException(OldLayoutMessage);
                pendingCheckpoint = JsonUtility.ToJson(NormalizeFailedAttempt(data, entry.checkpoint)); pendingSeconds = entry.playSeconds;
            }
            Time.timeScale = 1;
            SceneManager.LoadScene(sceneName);
        }

        private void OnDisable() { Time.timeScale = 1; }
    }
}
