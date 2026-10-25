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
    public sealed class Map01SaveSystem : MonoBehaviour
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
        public void MarkOperationCheckpoint()
        {
            if(IsRestoring||mission.Cinematic||mission.PlayerHealth<=0||GetComponent<Map01Story>().Operating||Map01NamActions.For(mission).Busy)return;
            operationStart=JsonUtility.ToJson(Capture(false));
            SaveSlot(ForestSaveSlots.AutoSlot,out _,true);
        }
        public bool RestartOperation()
        {
            if(!HasOperationCheckpoint)return false;
            var data=PrepareRetry(JsonUtility.FromJson<CheckpointData>(operationStart));data.operationStart=operationStart;
            RestartInteractionCheckpoint(JsonUtility.ToJson(data));return true;
        }
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

        private static string RunId(CheckpointData data, string json)
        {
            if (!string.IsNullOrEmpty(data?.skillRunId)) return data.skillRunId;
            // Stable migration for the same pre-feature save, without rewriting the user's old slot.
            using var hash = System.Security.Cryptography.SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(json))).Replace("-", "").ToLowerInvariant();
        }
        private void EnsureSkillCampaign()
        {
            if (skillCampaign != null && !string.IsNullOrEmpty(skillCampaign.RunId)) return;
            skillCampaign = mission.player.GetComponent<SkillCampaignPersistence>();
            if (skillCampaign == null) skillCampaign = mission.player.gameObject.AddComponent<SkillCampaignPersistence>();
            if (pendingCheckpoint == null) skillCampaign.Initialize(Guid.NewGuid().ToString("N"));
            else skillCampaign.Initialize(RunId(JsonUtility.FromJson<CheckpointData>(pendingCheckpoint), pendingCheckpoint));
        }
        private static string SkillMissionId(int stage) => stage switch
        {
            Map01Quest.RescueStage => "rescue", Map01Quest.EscortStage => "escort", Map01Quest.ScoutStage => "scouting",
            Map01Quest.CampsStage => "camps", Map01Quest.BossStage => "boss",
            Map01Quest.ExtractionStage or Map01Quest.BoardingStage => "extraction", _ => null
        };
        public void SyncSkillMission()
        {
            if (syncingSkills || IsRestoring || !mission.IsInitialized) return;
            EnsureSkillCampaign();
            syncingSkills = true;
            try
            {
                if (mission.PlayerHealth <= 0 || (mission.ModernHealth != null && mission.ModernHealth.IsDead)
                    || GetComponent<Map01Rescue>().HungDown || GetComponent<Map01Rescue>().CurrentPhase == Map01Rescue.Phase.Alarm
                    || GetComponent<Map01Scouting>().FailedRun)
                { mission.Skills.FailMission(); return; }
                string id = SkillMissionId(quest.Stage);
                if (mission.Skills.MissionId == id)
                {
                    if (missionStart == null) missionStart = JsonUtility.ToJson(Capture(false));
                    return;
                }
                mission.Skills.CompleteMission();
                missionStart = null;
                if (id != null)
                {
                    mission.Skills.BeginMission(id);
                }
                missionStart = JsonUtility.ToJson(Capture(false));
            }
            finally { syncingSkills = false; }
        }

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

        /// <summary>Hùng has just given the scouting order: remember this moment, for a failed run to
        /// start over from (<see cref="RestartScouting"/>).</summary>
        public void MarkScoutingStart()
        {
            scoutStart = null; // Not nested into itself.
            scoutStart = JsonUtility.ToJson(Capture());
        }

        public bool HasScoutStart => !string.IsNullOrEmpty(scoutStart);
        public void EnsureRescueCheckpoint()
        {
            if(string.IsNullOrEmpty(rescueStart)&&!mission.Cinematic&&!IsRestoring)MarkRescueCheckpoint(false);
        }
        public void EnsureEscortCheckpoint()
        {
            if(string.IsNullOrEmpty(escortStart)&&!mission.Cinematic&&!IsRestoring)MarkRescueCheckpoint(true);
        }
        public void MarkRescueCheckpoint(bool escort)
        {
            var data=Capture(false);
            if(!escort){data.player=GetComponent<Map01Rescue>().RetryPoint;data.alarmed=false;}
            string checkpoint=JsonUtility.ToJson(data);
            if(escort)escortStart=checkpoint;else rescueStart=checkpoint;
        }
        public bool RestartRescue(bool escort)
        {
            string checkpoint=escort?escortStart:rescueStart;
            if(string.IsNullOrEmpty(checkpoint)){mission.Say("Chưa có checkpoint nhiệm vụ. Hãy tải bản lưu hoặc chơi lại.",5);return false;}
            var data=PrepareRetry(JsonUtility.FromJson<CheckpointData>(checkpoint));
            if(!escort&&GetComponent<Map01Rescue>().IntroSeen&&data.rescue!=null){
                data.rescue.introSeen=true;
                data.rescue.revision=Map01Rescue.SnapshotRevision;
            }
            // Keep the non-recursive retry snapshots across repeated retries.
            data.rescueStart=rescueStart;data.escortStart=escortStart;
            pendingCheckpoint=JsonUtility.ToJson(data);pendingSeconds=mission.PlaySeconds;
            pendingMessage=escort?"Làm lại đoạn hộ tống Hùng về căn cứ.":"Làm lại: lén tiếp cận, hạ lính giám sát bằng dao rồi cứu Hùng.";
            Time.timeScale=1;SceneManager.LoadScene("Map 1");return true;
        }
        // Kept in memory before an interaction, never written while it is running.
        public string CaptureInteractionCheckpoint()=>JsonUtility.ToJson(Capture());
        public void RestartInteractionCheckpoint(string checkpoint){
            if(string.IsNullOrEmpty(checkpoint))return;
            pendingCheckpoint=checkpoint;pendingSeconds=mission.PlaySeconds;pendingMessage="Đã trở về trước lúc phục kích.";
            Time.timeScale=1;SceneManager.LoadScene("Map 1");
        }

        /// <summary>
        /// No recorded moment of the order (a save from before it was kept): make one as if Hùng
        /// had just given it — Nam beside him, no camp logged, every one of
        /// <paramref name="campGuards"/> fresh at his post; everything else as it is now.
        /// </summary>
        public void MarkFreshScoutingStart(IEnumerable<Map01EnemyController> campGuards)
        {
            scoutStart = null;
            var data = Capture();
            Vector3 beside = mission.hung.position + mission.hung.forward * 2f;
            if (NavMesh.SamplePosition(beside, out var hit, 3f, NavMesh.AllAreas)) beside = hit.position;
            data.player = beside;
            var toHung = mission.hung.position - beside;
            data.playerYaw = Mathf.Atan2(toHung.x, toHung.z) * Mathf.Rad2Deg;
            data.scoutedCamps = 0;
            data.alarmed = false;
            var fresh = campGuards.ToDictionary(g => g.SaveId, g => g.CaptureAtPost());
            for (int i = 0; i < data.enemies.Length; i++)
                if (fresh.TryGetValue(data.enemies[i].id, out var atPost)) data.enemies[i] = atPost;
            scoutStart = JsonUtility.ToJson(data);
        }

        /// <summary>Back to the moment Hùng gave the scouting order — Map 1 reloads there, with
        /// <paramref name="message"/> on screen. False if that moment was never recorded.</summary>
        public bool RestartScouting(string message)
        {
            if (string.IsNullOrEmpty(scoutStart)) return false;
            pendingCheckpoint = JsonUtility.ToJson(PrepareRetry(JsonUtility.FromJson<CheckpointData>(scoutStart))); pendingSeconds = mission.PlaySeconds; pendingMessage = message;
            Time.timeScale = 1;
            Map01Mission.ReloadScene(gameObject.scene); // Map 1, or the sandbox copy it is running in.
            return true;
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

        /// <summary>[F9] — called by Map01PlayerInteraction, which owns the key bindings.</summary>
        public void Load()
        {
            try
            {
                FlushCurrentSkillRun();
                if (IsSandbox)
                {
                    if (!File.Exists(SandboxPath(0))) { mission.Say("Map thử chưa có bản lưu nhanh [F5]."); return; }
                    pendingCheckpoint = File.ReadAllText(SandboxPath(0)); pendingSeconds = 0; pendingMessage = null;
                    Time.timeScale = 1; Map01Mission.ReloadScene(gameObject.scene);
                    return;
                }
                if (ForestSaveSlots.Exists(0)) { BeginGame(0); return; }
                if (!File.Exists(SavePath)) { mission.Say("Chưa có bản lưu nhanh."); return; }
                string json = File.ReadAllText(SavePath);
                pendingCheckpoint = JsonUtility.ToJson(NormalizeFailedAttempt(JsonUtility.FromJson<CheckpointData>(json), json)); pendingSeconds = 0; pendingMessage = null;
                Time.timeScale = 1; SceneManager.LoadScene("Map 1");
            }
            catch (Exception e) { mission.Say("Không thể tải: " + e.Message); }
        }

        private static void FlushCurrentSkillRun()
        {
            var active = FindFirstObjectByType<Map01SaveSystem>();
            if (active == null || active.mission == null || !active.mission.IsInitialized || IsRestoring) return;
            active.SyncSkillMission();
            if (!active.skillCampaign.TryFlush(out var error)) throw new IOException(error);
        }

        private void Restore()
        {
            GetComponent<DialogueVoice>()?.Stop();
            GetComponent<Map01StoryDialogue>()?.Close(false);
            GetComponent<Map01RescueCinematic>()?.CancelForRestore();
            try
            {
                var data = JsonUtility.FromJson<CheckpointData>(pendingCheckpoint);
                mission.SetPlaySeconds(pendingSeconds);
                if (data == null || data.version < 4 || data.items == null || data.used == null) throw new IOException();
                if (data.version < LayoutVersion) { mission.Say(OldLayoutMessage, 8); return; }
                var controller = mission.player.GetComponent<CharacterController>();
                if (controller != null) { controller.enabled = false; mission.player.position = data.player; controller.enabled = true; }
                else mission.player.position = data.player;
                mission.hung.GetComponent<NavMeshAgent>()?.Warp(data.hung);
                quest.RestoreStoryStage(data.stage, data.story);
                // Before anything rolls loot: the run's seed and the packs already rolled come first (Map01Logistics).
                Map01Logistics.For(mission).Restore(data.logistics);
                GetComponent<Map01CampChests>()?.Reroll();
                var c3 = GetComponent<Map01C3Mission>();
                c3?.RerollCache();
                c3?.Restore(data.c3);   // before the guards' snapshots: a woken sleeper is back on the field to receive his
                inventory.RestoreFromSave(data.items, data.stones, data.crafting, data.craftRemaining);
                if (data.bag != null) inventory.RestoreLayout(data.bag);
                Map01Drops.For(mission).Restore(data.dropped);
                inventory.RestoreFieldToolkit(data.bag?.fieldKitVersion ?? 0);
                Map01Smoke.ClearAll();
                Map01Blast.ClearAll();
                EnsureSkillCampaign();
                skillCampaign.Initialize(RunId(data, pendingCheckpoint));
                skillCampaign.Restore(data.skills, data.skillMission);
                GetComponent<Map01Minimap>()?.RestoreExploration(data.exploration);
                missionStart = data.missionStart;
                GetComponent<Map01Story>().AfterInventoryRestore();
                GetComponent<Map01Story>().RestoreOperationActors(data.enemies);
                operationStart=data.operationStart;
                mission.Alarmed = data.alarmed;
                mission.RestoreHealthFromSave(Mathf.Clamp(data.hp, 1, mission.Settings.playerHP));
                mission.SetStamina(data.stamina);
                mission.Crouched = data.crouched;
                mission.player.rotation = Quaternion.Euler(0, data.playerYaw, 0);
                mission.hung.rotation = Quaternion.Euler(0, data.hungYaw, 0);
                GetComponent<Map01Rescue>().Restore(data.rescue, data.enemies);
                rescueStart=data.rescueStart;escortStart=data.escortStart;
                RestoreGameplay(data);
                GetComponent<Map01Rescue>().RestoreHealth(data.hungHealth);
                // Loading an escort resumes its pose; only the live rescue plays the rise from captivity.
                mission.hung.GetComponentInChildren<Map01HungVisual>()?.RestoreCheckpointPose();
                GetComponent<Map01Scouting>().RestoreFound(data.scoutedCamps);
                foreach (var point in mission.Points) point.used = data.used.Contains(point.id);
                // Starting a scouting run over keeps the moment it started from; any other load
                // takes whatever moment the save carries (none, outside scouting).
                if (pendingMessage == null) scoutStart = string.IsNullOrEmpty(data.scoutStart) ? null : data.scoutStart;
                if (pendingMessage != null) mission.Say(pendingMessage, 14);
                else mission.Say("Đã khôi phục tiến trình và trạng thái giao chiến.");
            }
            catch (Exception) { mission.Say("Checkpoint không hợp lệ. Bắt đầu lại Map 1."); }
            finally { pendingCheckpoint = null; pendingMessage = null; }
        }

        private void RestoreGameplay(CheckpointData data)
        {
            if (mission.ModernPlayer != null) mission.ModernPlayer.RestoreMotion(mission.Crouched);
            if (mission.ModernCombat != null)
            {
                if (Enum.IsDefined(typeof(ShadowVale.Gameplay.Combat.WeaponKind), data.equippedWeapon))
                {
                    var equipped=(ShadowVale.Gameplay.Combat.WeaponKind)data.equippedWeapon;
                    if(equipped==ShadowVale.Gameplay.Combat.WeaponKind.Rifle && inventory.SlotItem(Map01Inventory.Slot.Primary)=="rifle_mosin")
                        equipped=ShadowVale.Gameplay.Combat.WeaponKind.Sniper;
                    else if(equipped==ShadowVale.Gameplay.Combat.WeaponKind.Rifle && (inventory.SlotItem(Map01Inventory.Slot.Primary)!="rifle_standard" && inventory.SlotItem(Map01Inventory.Slot.Primary)!=Map01M16.Id))
                        equipped=ShadowVale.Gameplay.Combat.WeaponKind.Knife;
                    if(equipped==ShadowVale.Gameplay.Combat.WeaponKind.Pistol && inventory.SlotItem(Map01Inventory.Slot.Pistol)!="pistol_k54")
                        equipped=ShadowVale.Gameplay.Combat.WeaponKind.Knife;
                    if(equipped==ShadowVale.Gameplay.Combat.WeaponKind.Sniper && inventory.SlotItem(Map01Inventory.Slot.Primary)!="rifle_mosin")
                        equipped=ShadowVale.Gameplay.Combat.WeaponKind.Knife;
                    mission.ModernCombat.Equip(equipped);
                }
                inventory.ApplyPrimaryRifle(false);
                mission.ModernCombat.RestoreAttackCooldown(data.attackRemaining);
                // Saves written before magazines existed carry -1; leave the weapon loaded
                // rather than handing the player an empty rifle out of an old checkpoint.
                if (data.roundsInMagazine >= 0) mission.ModernCombat.RestoreMagazine(ShadowVale.Gameplay.Combat.WeaponKind.Rifle, data.roundsInMagazine);
                mission.ModernCombat.RestoreMagazine(ShadowVale.Gameplay.Combat.WeaponKind.Pistol, data.pistolRoundsInMagazine);
                mission.ModernCombat.RestoreMagazine(ShadowVale.Gameplay.Combat.WeaponKind.Sniper, data.mosinRoundsInMagazine);
            }
            foreach (var enemy in mission.Enemies)
            {
                if (enemy == null) continue;
                var saved = data.enemies?.FirstOrDefault(e => e.id == enemy.SaveId);
                // The previous runtime boss inherited an arbitrary camp parent. Import its
                // existing snapshot into the stable third-outpost actor, never duplicate him.
                if (saved == null && enemy.IsBoss && data.story == null)
                    saved = data.enemies?.FirstOrDefault(e => e.id != null && e.id.EndsWith("/Chỉ huy địch", StringComparison.Ordinal));
                if (saved != null) enemy.RestoreSnapshot(saved);
                else {
                    enemy.RefreshRifleLoadout();
                    if (enemy.IsBoss && GetComponent<Map01Story>().Progress.commanderKilled) {
                        var state = enemy.Capture(); state.hp = 0; enemy.RestoreSnapshot(state);
                    }
                }
            }
        }

        private void OnDisable() { Time.timeScale = 1; }
    }
}
