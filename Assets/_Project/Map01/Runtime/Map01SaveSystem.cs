using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace ShadowVale.Map01
{
    /// <summary>Save/load for Map 1 — checkpoint format v6. v4 (the first without the legacy
    /// ForestGuard snapshots) still loads: only its stage numbers predate the report-to-Hùng steps
    /// and are mapped through Map01Quest.FromV4Stage. Saves before v6 predate the jetty rescue
    /// layout: the guards they place where two camps and the patrol used to be are sent to their
    /// new posts, and a rescue still in progress finds Hùng held at the jetty.</summary>
    public sealed class Map01SaveSystem : MonoBehaviour
    {
        [Serializable]
        private sealed class CheckpointData
        {
            public int version = 6, stage, stones;
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
            public float hungHealth = -1; // -1 in older saves: unhurt.
            public string scoutStart; // The checkpoint at Hùng's scouting order, in saves made while scouting.
            public Map01EnemyController.Snapshot[] enemies;
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
        private string SavePath => Path.Combine(Application.persistentDataPath, "shadowvale-map01-checkpoint.json");

        private void Awake()
        {
            mission = GetComponent<Map01Mission>();
            quest = GetComponent<Map01Quest>();
            inventory = GetComponent<Map01Inventory>();
        }
        private void Start() { if (pendingCheckpoint != null) Invoke(nameof(Restore), .1f); }

        public string ManualSaveBlockReason()
        {
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
            if (pendingCheckpoint != null) { error = "Đang khôi phục bản lưu. Vui lòng thử lại sau giây lát."; return false; }
            // Never replace a usable checkpoint with a dead character — or a failed rescue or scouting run.
            if (mission.PlayerHealth <= 0 || GetComponent<Map01Rescue>().HungDown || GetComponent<Map01Scouting>().FailedRun) return true;
            return SaveSlot(ForestSaveSlots.AutoSlot, out error, true);
        }

        /// <summary>Hùng has just given the scouting order: remember this moment, for a failed run to
        /// start over from (<see cref="RestartScouting"/>).</summary>
        public void MarkScoutingStart()
        {
            scoutStart = null; // Not nested into itself.
            scoutStart = JsonUtility.ToJson(Capture());
        }

        /// <summary>Back to the moment Hùng gave the scouting order — Map 1 reloads there, with
        /// <paramref name="message"/> on screen. False if that moment was never recorded.</summary>
        public bool RestartScouting(string message)
        {
            if (string.IsNullOrEmpty(scoutStart)) return false;
            pendingCheckpoint = scoutStart; pendingSeconds = mission.PlaySeconds; pendingMessage = message;
            Time.timeScale = 1;
            SceneManager.LoadScene("Map 1");
            return true;
        }

        public bool SaveSlot(int slot, out string error, bool automatic = false)
        {
            error = null;
            if (!automatic && (error = ManualSaveBlockReason()) != null) { mission.Say(error); return false; }
            var data = Capture();
            try
            {
                string thumbnail = null;
                try { thumbnail = CaptureThumbnail(); } catch (Exception) { /* A missing preview must not prevent saving progress on exit. */ }
                ForestSaveSlots.Write(slot, new ForestSaveSlots.Entry
                {
                    sceneName = SceneManager.GetActiveScene().name,
                    savedAt = DateTime.UtcNow.ToString("o"), playSeconds = mission.PlaySeconds,
                    location = Map01Quest.SaveLocations[Mathf.Clamp(quest.Stage, 0, Map01Quest.CompleteStage)],
                    checkpoint = JsonUtility.ToJson(data), thumbnail = thumbnail
                });
                mission.Say("Đã lưu tiến trình."); return true;
            }
            catch (Exception e) { error = "Không thể lưu: " + e.Message; mission.Say(error); return false; }
        }

        private CheckpointData Capture()
        {
            return new CheckpointData
            {
                stage = quest.Stage, stones = inventory.Stones, hp = mission.PlayerHealth, stamina = mission.Stamina,
                player = mission.player.position, hung = mission.hung.position, alarmed = mission.Alarmed,
                used = mission.Points.Where(p => p.used).Select(p => p.id).ToArray(),
                items = inventory.CaptureItems(),
                crafting = inventory.CraftingId, craftRemaining = inventory.CraftRemaining,
                playerYaw = mission.player.eulerAngles.y, hungYaw = mission.hung.eulerAngles.y,
                crouched = mission.Crouched,
                equippedWeapon = mission.ModernCombat != null ? (int)mission.ModernCombat.EquippedKind : 0,
                attackRemaining = mission.ModernCombat != null ? mission.ModernCombat.AttackCooldownRemaining : 0,
                roundsInMagazine = mission.ModernCombat != null ? mission.ModernCombat.RoundsInMagazine : -1,
                hungHealth = GetComponent<Map01Rescue>().HungHealth,
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
            pendingCheckpoint = null; pendingSeconds = 0; pendingMessage = null;
            if (slot < 0) scoutStart = null;
            string sceneName = "Map 1";
            if (slot >= 0)
            {
                var entry = ForestSaveSlots.Read(slot);
                if (entry == null) throw new IOException("Ô lưu trống.");
                sceneName = string.IsNullOrEmpty(entry.sceneName) ? "Map 1" : entry.sceneName;
                if (sceneName != "Map 1") throw new IOException("Bản lưu thuộc màn chơi chưa được hỗ trợ.");
                var data = JsonUtility.FromJson<CheckpointData>(entry.checkpoint);
                if (data == null || data.version < 4 || data.items == null || data.used == null)
                    throw new IOException("Dữ liệu checkpoint bị hỏng.");
                pendingCheckpoint = entry.checkpoint; pendingSeconds = entry.playSeconds;
            }
            Time.timeScale = 1;
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>[F9] — called by Map01PlayerInteraction, which owns the key bindings.</summary>
        public void Load()
        {
            try
            {
                if (ForestSaveSlots.Exists(0)) { BeginGame(0); return; }
                if (!File.Exists(SavePath)) { mission.Say("Chưa có bản lưu nhanh."); return; }
                pendingCheckpoint = File.ReadAllText(SavePath); pendingSeconds = 0; pendingMessage = null;
                Time.timeScale = 1; SceneManager.LoadScene("Map 1");
            }
            catch (Exception e) { mission.Say("Không thể tải: " + e.Message); }
        }

        private void Restore()
        {
            try
            {
                var data = JsonUtility.FromJson<CheckpointData>(pendingCheckpoint);
                mission.SetPlaySeconds(pendingSeconds);
                if (data == null || data.version < 4 || data.items == null || data.used == null) throw new IOException();
                var controller = mission.player.GetComponent<CharacterController>();
                if (controller != null) { controller.enabled = false; mission.player.position = data.player; controller.enabled = true; }
                else mission.player.position = data.player;
                mission.hung.GetComponent<NavMeshAgent>()?.Warp(data.hung);
                quest.RestoreStage(data.version >= 5 ? data.stage : Map01Quest.FromV4Stage(data.stage));
                inventory.RestoreFromSave(data.items, data.stones, data.crafting, data.craftRemaining);
                mission.Alarmed = data.alarmed;
                mission.RestoreHealthFromSave(Mathf.Clamp(data.hp, 1, mission.Settings.playerHP));
                mission.SetStamina(data.stamina);
                mission.Crouched = data.crouched;
                mission.player.rotation = Quaternion.Euler(0, data.playerYaw, 0);
                mission.hung.rotation = Quaternion.Euler(0, data.hungYaw, 0);
                RestoreGameplay(data);
                var rescue = GetComponent<Map01Rescue>();
                rescue.RestoreHealth(data.hungHealth);
                if (data.version < 6)
                {
                    // The map moved two camps and the patrol since this save; its positions for
                    // them are stale, so everyone goes to his post as he is (alive or not).
                    foreach (var enemy in mission.Enemies) enemy.MoveToPost();
                    if (quest.Stage == Map01Quest.RescueStage) mission.hung.GetComponent<NavMeshAgent>()?.Warp(rescue.CaptivePost);
                }
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
                    mission.ModernCombat.Equip((ShadowVale.Gameplay.Combat.WeaponKind)data.equippedWeapon);
                mission.ModernCombat.RestoreAttackCooldown(data.attackRemaining);
                // Saves written before magazines existed carry -1; leave the weapon loaded
                // rather than handing the player an empty rifle out of an old checkpoint.
                if (data.roundsInMagazine >= 0) mission.ModernCombat.RestoreMagazine(data.roundsInMagazine);
            }
            foreach (var enemy in mission.Enemies)
            {
                var saved = data.enemies?.FirstOrDefault(e => e.id == enemy.SaveId);
                if (saved != null) enemy.RestoreSnapshot(saved);
            }
        }

        private void OnDisable() { Time.timeScale = 1; }
    }
}
