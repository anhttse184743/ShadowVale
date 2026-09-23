using System;
using System.Collections.Generic;
using System.Linq;

namespace ShadowVale.Map01
{
    /// <summary>Presentation metadata; actual counts remain owned by the mission/save file.</summary>
    public static class ForestInventory
    {
        public static readonly string[] Order = {
            "rifle_standard", "knife",
            "ammo_rifle", "medkit_small", "cloth", "herb", "stone", "supplies", "river_documents",
            "ammo_sniper", "scrap_metal", "gun_oil", "repair_kit", "ammo_smg", "ammo_pistol", "ammo_shotgun"
        };
        public struct Stack
        {
            public string Id;
            public int Count;
            public Stack(string id, int count) { Id = id; Count = count; }
        }
        public static List<Stack> Split(IEnumerable<KeyValuePair<string, int>> inventory, Func<string, int> limit)
        {
            var result = new List<Stack>();
            foreach (var item in inventory.Where(p => p.Value > 0).OrderBy(p => {
                int index = Array.IndexOf(Order, p.Key); return index < 0 ? int.MaxValue : index;
            }).ThenBy(p => p.Key, StringComparer.Ordinal)) {
                int max = Math.Max(1, limit(item.Key));
                for (int remaining = item.Value; remaining > 0;) {
                    int count = Math.Min(max, remaining); result.Add(new Stack(item.Key, count)); remaining -= count;
                }
            }
            return result;
        }
        public static bool IsWeapon(string id) => id == "rifle_standard" || id == "knife";
        public static bool QuickUsable(string id) => id == "medkit_small" || id == "stone" || IsWeapon(id);
        public static string Name(string id)
        {
            switch (id) {
                case "rifle_standard": return "Súng trường";
                case "knife": return "Dao";
                case "ammo_rifle": return "Đạn súng trường";
                case "ammo_smg": return "Đạn tiểu liên";
                case "ammo_pistol": return "Đạn súng ngắn";
                case "ammo_shotgun": return "Đạn shotgun";
                case "ammo_sniper": return "Đạn bắn tỉa";
                case "medkit_small": return "Băng cứu thương";
                case "cloth": return "Vải";
                case "herb": return "Thảo dược";
                case "stone": return "Đá ném";
                case "supplies": return "Hàng tiếp tế";
                case "river_documents": return "Tài liệu";
                case "scrap_metal": return "Kim loại vụn";
                case "gun_oil": return "Dầu súng";
                case "repair_kit": return "Bộ sửa chữa";
                default: return id ?? "Ô trống";
            }
        }
        public static string Description(string id)
        {
            switch (id) {
                case "rifle_standard": return "Trang bị khởi hành. Dùng đạn súng trường. Nhấn 6 để cầm súng.";
                case "knife": return "Trang bị cận chiến, không tiêu hao đạn. Nhấn 7 để cầm dao.";
                case "medkit_small": return "Hồi máu cho nhân vật. Nhấn H để dùng.";
                case "stone": return "Ném theo hướng ngắm để đánh lạc hướng lính.";
                case "cloth": return "Vật liệu chế tạo. Kết hợp thảo dược tại bàn chế tạo.";
                case "herb": return "Kết hợp 2 vải và 1 thảo dược để làm băng cứu thương.";
                case "supplies": return "Vật phẩm nhiệm vụ. Mang hàng tiếp tế đến bến sông.";
                case "river_documents": return "Vật phẩm nhiệm vụ. Mang tài liệu về điểm rút lui.";
                case "scrap_metal": return "Vật liệu chế tạo và sửa chữa trang bị.";
                case "gun_oil": return "Vật liệu bảo dưỡng trang bị.";
                case "repair_kit": return "Bộ dụng cụ sửa chữa. Map hiện tại chưa có thao tác sử dụng.";
                default: return id != null && id.StartsWith("ammo_") ? "Đạn cho " + Name(id).Substring(4).ToLowerInvariant() + ". Không dùng trực tiếp từ túi đồ." : "Vật phẩm trong túi đồ.";
            }
        }
        public static int IconIndex(string id)
        {
            switch (id) {
                case "ammo_rifle": return 0; case "cloth": return 1; case "herb": return 2;
                case "medkit_small": return 3; case "supplies": return 4; case "river_documents": return 5;
                case "stone": return 6; case "scrap_metal": return 7; case "gun_oil": return 8;
                case "repair_kit": return 9; case "ammo_smg": return 10; case "ammo_pistol": return 11;
                case "ammo_shotgun": return 12; case "ammo_sniper": return 13; default: return -1;
            }
        }
    }
}
