using System.Linq;
using UnityEngine;
namespace ShadowVale.Map01 { public sealed partial class ForestMission {
        private void OnGUI() => DrawHud();

        private void DrawMap(float width, float height)
        {
            var rect = new Rect(width / 2 - 310, 164, 620, 420); Panel(rect);
            GUI.Label(new Rect(rect.x + 20, rect.y + 10, 580, 35), "TUYẾN VẬN CHUYỂN / M để đóng", titleStyle);
            GUI.Label(new Rect(rect.x + 20, rect.y + 60, 260, 300), "BẮC ↑\n\n05  Bến sông / rút lui\n04  Căn cứ / bàn tài liệu\n03  Điểm nghỉ / chế tạo\n02  Khu tuần tra\n      Tây: bụi rậm\n      Giữa: vật chắn\n      Đông: đánh lạc hướng\n01  Nhận hàng / xuất phát", textStyle);
            var area = new Rect(rect.x + 310, rect.y + 60, 280, 335);
            foreach (var p in points.Where(p => p.kind != ForestPointKind.Cover && p.kind != ForestPointKind.Hide))
            {
                var pos = MapPosition(p.transform.position, area);
                GUI.color = p.used ? Color.gray : new Color(.95f, .75f, .32f);
                GUI.DrawTexture(new Rect(pos.x - 4, pos.y - 4, 8, 8), Texture2D.whiteTexture);
            }
            GUI.color = Color.cyan; var playerPos = MapPosition(player.position, area);
            GUI.DrawTexture(new Rect(playerPos.x - 5, playerPos.y - 5, 10, 10), Texture2D.whiteTexture); GUI.color = Color.white;
        }
        private Vector2 MapPosition(Vector3 p, Rect r) => new Vector2(r.x + Mathf.InverseLerp(mapMin.x, mapMax.x, p.x) * r.width, r.yMax - Mathf.InverseLerp(mapMin.y, mapMax.y, p.z) * r.height);
        private static void Panel(Rect rect) { var old = GUI.color; GUI.color = new Color(.035f, .07f, .06f, .94f); GUI.DrawTexture(rect, Texture2D.whiteTexture); GUI.color = old; }
}}
