using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ShadowVale.Map01
{
    public sealed partial class ForestMission
    {
        [Serializable]
        private sealed class Checkpoint
        {
            public int version = 2, stage, stones;
            public float hp, stamina;
            public Vector3 player, hung;
            public bool alarmed;
            public string[] used;
            public ForestIngredient[] items;
            public string[] down;
        }

        private static bool loadAfterRestart;
        private string SavePath => Path.Combine(Application.persistentDataPath, "shadowvale-map01-checkpoint.json");

        private bool CanSave() => !Stopped && crafting == null && guards.All(g => !g.Alive ||
            (g.state == ForestGuardState.Patrol && Vector3.Distance(g.transform.position, player.position) > 18));

        private void Save()
        {
            if (!CanSave()) { Say("Chỉ lưu khi đã thoát nguy hiểm và không đang chế tạo."); return; }
            var data = new Checkpoint
            {
                stage = stage, stones = stones, hp = hp, stamina = stamina,
                player = player.position, hung = hung.position, alarmed = Alarmed,
                used = points.Where(p => p.used).Select(p => p.id).ToArray(),
                items = inventory.Select(p => new ForestIngredient { item_id = p.Key, count = p.Value }).ToArray(),
                down = guards.Where(g => !g.Alive).Select(g => g.id).ToArray()
            };
            try { File.WriteAllText(SavePath, JsonUtility.ToJson(data, true)); Say("Đã lưu checkpoint Map 1. F9 để tải lại."); }
            catch (IOException) { Say("Không thể ghi checkpoint. Hãy kiểm tra quyền truy cập ổ đĩa."); }
        }

        private void Start()
        {
            if (loadAfterRestart) { loadAfterRestart = false; Invoke(nameof(Restore), .1f); }
        }

        private void Load()
        {
            if (!File.Exists(SavePath)) { Say("Chưa có checkpoint Map 1."); return; }
            loadAfterRestart = true;
            Restart();
        }

        private void Restore()
        {
            try
            {
                var data = JsonUtility.FromJson<Checkpoint>(File.ReadAllText(SavePath));
                if (data == null || data.version != 2 || data.items == null || data.used == null || data.down == null)
                    throw new IOException();
                verticalVelocity = 0;
                controller.enabled = false;
                player.position = data.player;
                controller.enabled = true;
                companion.Warp(data.hung);
                stage = Mathf.Clamp(data.stage, 0, 3);
                stones = data.stones;
                hp = Mathf.Clamp(data.hp, 1, Settings.playerHP);
                stamina = Mathf.Clamp(data.stamina, 0, Settings.stamina);
                Alarmed = data.alarmed;
                inventory.Clear();
                foreach (var item in data.items) inventory[item.item_id] = item.count;
                foreach (var guard in guards) if (data.down.Contains(guard.id)) guard.Hit(GuardData.max_hp + 1);
                foreach (var point in points) point.used = data.used.Contains(point.id);
                Say("Đã tải checkpoint. Lính còn sống bắt đầu lại tuyến tuần tra.");
            }
            catch (Exception) { Say("Checkpoint không hợp lệ. Bắt đầu lại Map 1."); }
        }

        private void Restart()
        {
            Time.timeScale = 1;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        private void OnDisable() => Time.timeScale = 1;
    }
}
