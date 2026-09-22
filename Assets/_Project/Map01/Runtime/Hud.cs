using System.Linq;
using UnityEngine;

namespace ShadowVale.Map01
{
    public sealed partial class ForestMission
    {
        private void OnGUI()
        {
            if (!IsInitialized) return;
            EnsureGuiStyles();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            DrawMissionStatus(width, height);
            DrawWorldPrompts(scale, width, height);
            if (inventoryOpen) DrawInventory(width);
            if (mapOpen) DrawMap(width, height);
            if (Stopped) DrawStoppedPanel(width);
        }

        private void EnsureGuiStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 23, fontStyle = FontStyle.Bold, wordWrap = true };
            textStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
        }

        private void DrawMissionStatus(float width, float height)
        {
            Panel(new Rect(22, 20, 460, 139));
            GUI.Label(new Rect(38, 30, 425, 35), "01 / NHỮNG DẤU CHÂN TRONG RỪNG", textStyle);
            GUI.Label(new Rect(38, 67, 425, 66), Objectives[stage], textStyle);
            Panel(new Rect(width - 265, 20, 245, 138));
            GUI.Label(new Rect(width - 247, 32, 220, 120),
                $"NAM    HP {hp:0} / {Settings.playerHP:0}\nSức bền {stamina:0}    Đạn {Count("ammo_rifle")}\nĐá {stones}   •   {(Hidden ? "ẨN TRONG BỤI" : crouched ? "ĐANG ĐI KHOM" : "ĐANG DI CHUYỂN")}", textStyle);
            Panel(new Rect(22, height - 69, width - 44, 49));
            GUI.Label(new Rect(36, height - 59, width - 65, 43),
                "Space Nhảy • WASD Di chuyển  •  Shift Chạy  •  C Đi khom  •  Chuột Xoay / Trái Bắn  •  Q Ném đá  •  E Tương tác  •  Tab Túi đồ  •  M Bản đồ  •  Esc Dừng", smallStyle);
            if (Time.time < dialogueUntil)
            {
                Panel(new Rect(210, height - 205, width - 420, 110));
                GUI.Label(new Rect(232, height - 193, width - 464, 94), dialogue, textStyle);
            }
            if (nearby != null && !nearby.used && !Stopped)
                GUI.Label(new Rect(width / 2 - 250, height - 247, 500, 40), "[E] " + nearby.label, textStyle);
            if (crafting != null)
                GUI.Label(new Rect(38, 167, 430, 30), $"Đang chế tạo… {Mathf.Max(0, craftUntil - Time.time):0.0}s", textStyle);
        }

        private void DrawWorldPrompts(float scale, float width, float height)
        {
            foreach (var guard in guards.Where(g => g.Alive))
            {
                var screen = gameCamera.WorldToScreenPoint(guard.transform.position + Vector3.up * 2.4f);
                if (screen.z > 0 && Vector3.Distance(player.position, guard.transform.position) < 24)
                    GUI.Label(new Rect(screen.x / scale - 60, (Screen.height - screen.y) / scale, 190, 40),
                        (guard.isCommander ? "CHỈ HUY • " : "") + guard.state +
                        (guard.suspicion > .05f ? $" {Mathf.Min(100, guard.suspicion * 100):0}%" : ""), smallStyle);
            }
            if (Stopped || inventoryOpen || mapOpen) return;
            var savedColor = GUI.color;
            foreach (var point in points)
            {
                if (point == null || !point.isActiveAndEnabled || point.used ||
                    (point.kind != ForestPointKind.Loot && point.kind != ForestPointKind.Supplies)) continue;
                if ((point.transform.position - player.position).sqrMagnitude > 32 * 32) continue;
                var screen = gameCamera.WorldToViewportPoint(point.transform.position + Vector3.up * .7f);
                if (screen.z <= 0 || screen.x < 0 || screen.x > 1 || screen.y < 0 || screen.y > 1) continue;
                float x = screen.x * width, y = (1 - screen.y) * height - 12 - Mathf.Sin(Time.unscaledTime * 3) * 4;
                GUI.color = new Color(.08f, .06f, .015f, .9f);
                GUI.DrawTexture(new Rect(x - 5, y - 16, 10, 17), Texture2D.whiteTexture);
                for (int row = 0; row < 10; row++) GUI.DrawTexture(new Rect(x - 11 + row, y - 2 + row, 22 - row * 2, 2), Texture2D.whiteTexture);
                GUI.color = new Color(1, .83f, .2f, 1);
                GUI.DrawTexture(new Rect(x - 3, y - 14, 6, 14), Texture2D.whiteTexture);
                for (int row = 0; row < 8; row++) GUI.DrawTexture(new Rect(x - 8 + row, y + row, 16 - row * 2, 1), Texture2D.whiteTexture);
            }
            GUI.color = savedColor;
        }

        private void DrawInventory(float width)
        {
            Panel(new Rect(width / 2 - 240, 170, 480, 330));
            GUI.Label(new Rect(width / 2 - 215, 185, 440, 40), "TÚI ĐỒ / VẬT TƯ", titleStyle);
            GUI.Label(new Rect(width / 2 - 215, 232, 430, 210),
                string.Join("\n", inventory.Where(i => i.Value > 0).Select(i => ItemName(i.Key) + "  × " + i.Value)), textStyle);
            GUI.Label(new Rect(width / 2 - 215, 457, 430, 38), "H Dùng băng cứu thương • B Chế tạo ở bàn", smallStyle);
        }

        private void DrawStoppedPanel(float width)
        {
            Panel(new Rect(width / 2 - 300, 235, 600, 180));
            GUI.Label(new Rect(width / 2 - 270, 253, 540, 70),
                hp <= 0 ? "NAM ĐÃ GỤC NGÃ" : stage == 4 ? "ĐÃ HOÀN THÀNH MAP 1" : "TẠM DỪNG", titleStyle);
            GUI.Label(new Rect(width / 2 - 270, 332, 540, 70),
                paused ? "Esc Tiếp tục • F9 Tải checkpoint" : "Enter Chơi lại • F9 Tải checkpoint", textStyle);
        }

        private static string ItemName(string id)
        {
            switch (id)
            {
                case "ammo_rifle": return "Đạn súng trường";
                case "cloth": return "Vải";
                case "herb": return "Thảo dược";
                case "scrap_metal": return "Kim loại";
                case "medkit_small": return "Băng cứu thương";
                case "supplies": return "Hàng tiếp tế";
                case "river_documents": return "Bản đồ và ghi chép bến sông";
                default: return id;
            }
        }

        private void DrawMap(float width, float height)
        {
            var rect = new Rect(width / 2 - 310, 164, 620, 420);
            Panel(rect);
            GUI.Label(new Rect(rect.x + 20, rect.y + 10, 580, 35), "TUYẾN VẬN CHUYỂN / M để đóng", titleStyle);
            GUI.Label(new Rect(rect.x + 20, rect.y + 60, 260, 300),
                "BẮC ↑\n\n05  Bến sông / rút lui\n04  Căn cứ / bàn tài liệu\n03  Điểm nghỉ / chế tạo\n02  Khu tuần tra\n      Tây: bụi rậm\n      Giữa: vật chắn\n      Đông: đánh lạc hướng\n01  Nhận hàng / xuất phát", textStyle);
            var area = new Rect(rect.x + 310, rect.y + 60, 280, 335);
            foreach (var point in points.Where(p => p.kind != ForestPointKind.Cover && p.kind != ForestPointKind.Hide))
            {
                var position = MapPosition(point.transform.position, area);
                GUI.color = point.used ? Color.gray : new Color(.95f, .75f, .32f);
                GUI.DrawTexture(new Rect(position.x - 4, position.y - 4, 8, 8), Texture2D.whiteTexture);
            }
            GUI.color = Color.cyan;
            var playerPosition = MapPosition(player.position, area);
            GUI.DrawTexture(new Rect(playerPosition.x - 5, playerPosition.y - 5, 10, 10), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private Vector2 MapPosition(Vector3 position, Rect rect) => new Vector2(
            rect.x + Mathf.InverseLerp(mapMin.x, mapMax.x, position.x) * rect.width,
            rect.yMax - Mathf.InverseLerp(mapMin.y, mapMax.y, position.z) * rect.height);

        private static void Panel(Rect rect)
        {
            var old = GUI.color;
            GUI.color = new Color(.035f, .07f, .06f, .94f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
