using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace ShadowVale.Map01 { public sealed partial class ForestMission {
        [Serializable] private sealed class Checkpoint
        {
            public int version = 3, stage, stones;
            public float hp, stamina;
            public Vector3 player, hung;
            public bool alarmed;
            public string[] used;
            public ForestIngredient[] items;
            public string[] down;
            public ForestGuard.Snapshot[] guards;
            public string crafting;
            public float craftRemaining, playerYaw, hungYaw, nextShotRemaining;
            public bool crouched, encounterLine;
            public string[] quickSlots;
            public bool modernGameplay;
            public int equippedWeapon;
            public float attackRemaining;
            public Map01EnemyController.Snapshot[] enemies;
        }
        private string SavePath => Path.Combine(Application.persistentDataPath, "shadowvale-map01-checkpoint.json");

        public string ManualSaveBlockReason()
        {
            if (modernHealth != null) hp = modernHealth.Current;
            if (pendingCheckpoint != null) return "Đang tải bản lưu. Vui lòng đợi giây lát.";
            if (hp <= 0) return "Không thể lưu khi nhân vật đã gục ngã.";
            if (stage == CompleteStage) return "Màn chơi đã kết thúc. Về menu để tự lưu kết quả.";
            if (stage == BossStage) return "Đang đối đầu chỉ huy địch. Hãy thoát nguy hiểm trước khi lưu thủ công.";
            if (crafting != null) return "Hãy hoàn thành chế tạo trước khi lưu thủ công.";
            if (guards.Any(g => g.Alive && g.state != ForestGuardState.Patrol)) return "Lính đang cảnh giác hoặc giao chiến. Hãy thoát nguy hiểm trước khi lưu.";
            if (guards.Any(g => g.Alive && Vector3.Distance(g.transform.position, player.position) <= 18)) return "Có lính ở quá gần. Hãy cách lính hơn 18 đơn vị để lưu thủ công.";
            if (modernEnemies.Any(g => g.Alive && (g.Alerted || Vector3.Distance(g.transform.position, player.position) <= 18))) return "Lính đang ở gần hoặc đang cảnh giác. Hãy thoát nguy hiểm trước khi lưu thủ công.";
            return null;
        }
        public bool CanSave() => ManualSaveBlockReason() == null;

        public bool AutoSaveOnExit(out string error)
        {
            if (modernHealth != null) hp = modernHealth.Current;
            error = null;
            if (pendingCheckpoint != null) { error = "Đang khôi phục bản lưu. Vui lòng thử lại sau giây lát."; return false; }
            // Never replace a usable checkpoint with a dead character.
            if (hp <= 0) return true;
            return SaveSlot(ForestSaveSlots.AutoSlot, out error, true);
        }

        public bool SaveSlot(int slot, out string error, bool automatic = false)
        {
            if (modernHealth != null) hp = modernHealth.Current;
            error = null;
            if (!automatic && (error = ManualSaveBlockReason()) != null) { Say(error); return false; }
            var data = new Checkpoint { stage = stage, stones = stones, hp = hp, stamina = stamina,
                player = player.position, hung = hung.position, alarmed = Alarmed,
                used = points.Where(p => p.used).Select(p => p.id).ToArray(),
                items = inventory.Select(p => new ForestIngredient { item_id = p.Key, count = p.Value }).ToArray(),
                down = guards.Where(g => !g.Alive).Select(g => g.id).ToArray(),
                guards = guards.Select(g => g.Capture()).ToArray(), crafting = crafting,
                craftRemaining = Mathf.Max(0, craftUntil - Time.time),
                playerYaw = player.eulerAngles.y, hungYaw = hung.eulerAngles.y,
                crouched = crouched, encounterLine = encounterLine, nextShotRemaining = Mathf.Max(0, nextShot - Time.time),
                quickSlots = (string[])quickSlots.Clone(), modernGameplay = modernPlayer != null,
                equippedWeapon = modernCombat != null ? (int)modernCombat.EquippedKind : 0,
                attackRemaining = modernCombat != null ? modernCombat.AttackCooldownRemaining : 0,
                enemies = modernEnemies.Select(e => e.Capture()).ToArray() };
            try {
                string thumbnail = null;
                try { thumbnail = CaptureThumbnail(); } catch (Exception) { /* A missing preview must not prevent saving progress on exit. */ }
                ForestSaveSlots.Write(slot, new ForestSaveSlots.Entry {
                    sceneName = SceneManager.GetActiveScene().name,
                    savedAt = DateTime.UtcNow.ToString("o"), playSeconds = PlaySeconds,
                    location = stage == 0 ? "Đang tìm Hùng" : stage == 1 ? "Trên đường về căn cứ" : stage == 2 ? "Căn cứ chỉ huy"
                        : stage == 3 ? "Doanh trại địch" : stage == BossStage ? "Đối đầu chỉ huy" : "Map 1 hoàn tất",
                    checkpoint = JsonUtility.ToJson(data), thumbnail = thumbnail
                });
                Say("Đã lưu tiến trình."); return true;
            }
            catch (Exception e) { error = "Không thể lưu: " + e.Message; Say(error); return false; }
        }

        private static string pendingCheckpoint;
        private static float pendingSeconds;
        private string CaptureThumbnail()
        {
            var rt = RenderTexture.GetTemporary(320, 180, 24);
            var previous = gameCamera.targetTexture; var active = RenderTexture.active;
            var texture = new Texture2D(320, 180, TextureFormat.RGB24, false);
            try {
                gameCamera.targetTexture = rt; gameCamera.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 320, 180), 0, 0); texture.Apply();
                return Convert.ToBase64String(texture.EncodeToJPG(70));
            } finally { gameCamera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); Destroy(texture); }
        }
        public static void BeginGame(int slot = -1)
        {
            pendingCheckpoint = null; pendingSeconds = 0;
            string sceneName = "Map 1";
            if (slot >= 0) {
                var entry = ForestSaveSlots.Read(slot);
                if (entry == null) throw new IOException("Ô lưu trống.");
                sceneName = string.IsNullOrEmpty(entry.sceneName) ? "Map01_ForestFootprints" : entry.sceneName;
                if (sceneName != "Map 1" && sceneName != "Map01_ForestFootprints") throw new IOException("Bản lưu thuộc màn chơi chưa được hỗ trợ.");
                var data = JsonUtility.FromJson<Checkpoint>(entry.checkpoint);
                if (data == null || data.version < 1 || data.version > 3 || data.items == null || data.used == null || data.down == null)
                    throw new IOException("Dữ liệu checkpoint bị hỏng.");
                pendingCheckpoint = entry.checkpoint; pendingSeconds = entry.playSeconds;
            }
            Time.timeScale = 1;
            SceneManager.LoadScene(sceneName);
        }
        private void Start()
        {
            if (pendingCheckpoint != null) Invoke(nameof(Restore), .1f);
        }
        private void Load()
        {
            try {
                if (ForestSaveSlots.Exists(0)) { BeginGame(0); return; }
                if (!File.Exists(SavePath)) { Say("Chưa có bản lưu nhanh."); return; }
                pendingCheckpoint = File.ReadAllText(SavePath); pendingSeconds = 0;
                Time.timeScale = 1; SceneManager.LoadScene("Map01_ForestFootprints");
            } catch (Exception e) { Say("Không thể tải: " + e.Message); }
        }
        private void Restore()
        {
            try
            {
                var data = JsonUtility.FromJson<Checkpoint>(pendingCheckpoint);
                PlaySeconds = pendingSeconds;
                if (data == null || data.version < 1 || data.version > 3 || data.items == null || data.used == null || data.down == null) throw new IOException();
                verticalVelocity = 0; controller.enabled = false; player.position = data.player; controller.enabled = true;
                companion.Warp(data.hung); stage = Mathf.Clamp(data.stage, 0, CompleteStage); stones = data.stones;
                hp = Mathf.Clamp(data.hp, 1, Settings.playerHP); stamina = Mathf.Clamp(data.stamina, 0, Settings.stamina); Alarmed = data.alarmed;
                inventory.Clear(); foreach (var i in data.items) inventory[i.item_id] = i.count;
                RestoreQuickSlots(data.quickSlots);
                if (data.version >= 2 && data.guards != null) {
                    foreach (var guard in guards) {
                        var saved = data.guards.FirstOrDefault(g => g.id == guard.id);
                        if (saved != null) guard.RestoreSnapshot(saved);
                    }
                    player.rotation = Quaternion.Euler(0, data.playerYaw, 0);
                    hung.rotation = Quaternion.Euler(0, data.hungYaw, 0);
                    crouched = data.crouched; encounterLine = data.encounterLine;
                    crafting = string.IsNullOrEmpty(data.crafting) ? null : data.crafting;
                    craftUntil = Time.time + data.craftRemaining;
                    nextShot = Time.time + data.nextShotRemaining;
                } else foreach (var guard in guards) if (data.down.Contains(guard.id)) guard.Hit(GuardData.max_hp + 1);
                RestoreGameplay(data);
                foreach (var point in points) point.used = data.used.Contains(point.id);
                Say(data.version >= 2 ? "Đã khôi phục tiến trình và trạng thái giao chiến." : "Đã tải bản lưu cũ. Lính còn sống bắt đầu lại tuyến tuần tra.");
            }
            catch (Exception) { Say("Checkpoint không hợp lệ. Bắt đầu lại Map 1."); }
            finally { pendingCheckpoint = null; }
        }
        private void Restart() { Time.timeScale = 1; SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); }
        private void OnDisable() { Time.timeScale = 1; }

}}
