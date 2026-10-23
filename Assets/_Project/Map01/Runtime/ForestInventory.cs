using System;
using System.Collections.Generic;
using System.Linq;

namespace ShadowVale.Map01
{
    /// <summary>Presentation metadata; actual counts remain owned by the mission/save file.</summary>
    public static class ForestInventory
    {
        public static readonly string[] Order = {
            "rifle_standard", "rifle_m16", "knife", "pistol_k54", "rifle_mosin",
            "ammo_rifle", "ammo_556", "ammo_pistol", "ammo_sniper", "medkit_small", "smoke_bomb", "grenade_f1", "cloth", "herb", "stone", "m1_supply_key", "m1_supply_cargo", "supplies", "river_documents",
            "scrap_metal", "gunpowder", "gun_oil", "repair_kit", "weapon_parts", "smoke_module", "fuse_module", "blast_charge", "scope_lens",
            "ammo_smg", "ammo_shotgun", "ammo_recovered", "ak_loading_pack"
        };

        /// <summary>
        /// Items whose bag icon is rendered from their 3D model (Resources/Hud/ItemIcons) rather than painted on the
        /// art board: what the camp chests (Map01CampChests) brought in. A test holds each to having an icon and a name.
        /// </summary>
        public static readonly string[] RenderedIcons = { "pistol_k54", "rifle_mosin", "ammo_pistol", "ammo_sniper", "gunpowder", "weapon_parts", "smoke_module", "fuse_module", "blast_charge", "scope_lens", "smoke_bomb", "grenade_f1" };
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
        public static bool IsWeapon(string id) => id == "rifle_standard" || id == "rifle_m16" || id == "knife" || id == "pistol_k54" || id == "rifle_mosin";
        public static bool QuickUsable(string id) => id == "medkit_small" || id == "stone" || id == "smoke_bomb" || id == "grenade_f1" || IsWeapon(id);
        public static string Name(string id)
        {
            switch (id) {
                case "rifle_standard": return "AK47";
                case "rifle_m16": return "M16";
                case "ammo_556": return "Đạn 5,56×45 mm";
                case "knife": return "Dao";
                case "m1_supply_key": return "Chìa khóa kho — Điểm 1";
                case "m1_supply_cargo": return "Túi thuốc của Hùng";
                case "ammo_rifle": return "Đạn súng trường";
                case "ammo_recovered": return "Vật tư đạn thu hồi";
                case "ak_loading_pack": return "Gói tiếp liệu AK";
                case "ammo_smg": return "Đạn tiểu liên";
                case "ammo_pistol": return "Đạn K-54";
                case "ammo_shotgun": return "Đạn shotgun";
                case "ammo_sniper": return "Đạn Mosin";
                case "pistol_k54": return "Súng lục K-54";
                case "rifle_mosin": return "Súng Mosin ống ngắm PU";
                case "medkit_small": return "Băng cứu thương";
                case "cloth": return "Vải";
                case "herb": return "Thảo dược";
                case "stone": return "Đá ném";
                case "supplies": return "Hàng tiếp tế";
                case "river_documents": return "Tài liệu";
                case "scrap_metal": return "Kim loại vụn";
                case "gun_oil": return "Dầu súng";
                case "repair_kit": return "Bộ sửa chữa";
                case "weapon_parts": return "Linh kiện súng";
                case "smoke_module": return "Ống tạo khói";
                case "fuse_module": return "Mô-đun kích hoạt";
                case "blast_charge": return "Lõi nổ";
                case "scope_lens": return "Kính ngắm";
                case "smoke_bomb": return "Bom khói";
                case "grenade_f1": return "Lựu đạn F1";
                case "gunpowder": return "Thuốc súng";
                default: return id ?? "Ô trống";
            }
        }
        public static string Description(string id)
        {
            switch (id) {
                case "rifle_m16": return "M16, đạn 5,56×45 mm, băng 30 viên, 600 viên/phút. Sát thương 22, tầm bắn tối đa 80 m. Ngắm, bắn nhịp ngắn; giữ cò lâu tăng giật và tản đạn. Sửa chữa và rã lấy linh kiện như AK47.";
                case "ammo_556": return "Đạn 5,56×45 mm dành cho M16. Nạp bằng R; không dùng chung đạn AK47. Tab → Chế dã chiến: thu hồi 10 đạn dự trữ lấy 1 vật tư. Chỉ thu hồi đạn trong túi; giữ nguyên băng đang nạp.";
                case "ammo_recovered": return "Vật tư thu hồi từ đạn M16 dự trữ: 10 viên → 1 vật tư. Cùng 1 gói tiếp liệu AK, dùng 2 vật tư để ráp 10 AK. Tab → Chế dã chiến; không phải đạn dùng trực tiếp.";
                case "ak_loading_pack": return "Vật tư đúng loại được chuẩn bị ở căn cứ. 1 gói + 2 vật tư đạn thu hồi → 10 đạn AK. Có ở bộ dụng cụ ban đầu, thưởng C1, hòm C2, túi đạn trước C3 và kho căn cứ (3 điểm/gói).";
                case "rifle_standard": return "Súng trường AK, ở ô 1, đạn 7,62×39. Mỗi phát mòn một ít độ bền; hỏng (0%) thì không bắn được tới khi sửa. Lính địch gục để lại AK đã mòn: sửa, rã lấy linh kiện hoặc nộp ở kho căn cứ.";
                case "knife": return "Dao, luôn ở ô 3, không vứt được. Cận chiến, hạ gục lặng lẽ từ sau lưng. Nhấn 3 để cầm dao.";
                case "m1_supply_key": return "Nhặt từ lính gác. Dùng tại cửa kho điểm 1 [E]. Hòm bên trong có khóa mã riêng.";
                case "m1_supply_cargo": return "Thuốc và vật tư đã thu hồi. Báo cáo qua bộ đàm rồi mang về giao cho chỉ huy tại hầm [E].";
                case "medkit_small": return "Hồi máu cho nhân vật. Nhấn H để dùng.";
                case "stone": return "Đồ ném: rơi xuống gây tiếng động 14 m, lính nghe thấy bỏ vị trí đi kiểm tra. Nhấn 4 chọn/đổi đồ ném, giữ chuột trái để ngắm, thả để ném.";
                case "cloth": return "Vật liệu chế tạo: băng cứu thương (2 vải + 1 thảo dược) và bom khói (1 vải + 1 ống tạo khói).";
                case "herb": return "Kết hợp 2 vải và 1 thảo dược để làm băng cứu thương.";
                case "supplies": return "Vật phẩm nhiệm vụ. Mang hàng tiếp tế đến bến sông.";
                case "river_documents": return "Vật phẩm nhiệm vụ. Mang tài liệu về điểm rút lui.";
                case "scrap_metal": return "Vật liệu chế tạo: bộ sửa chữa (3, cùng 1 dầu súng) và đạn (1, cùng thuốc súng). Rã súng cũng ra kim loại vụn.";
                case "gun_oil": return "Vật liệu bảo dưỡng: cùng 3 kim loại vụn làm bộ sửa chữa. Sửa súng ở bàn khi đã mất từ một nửa độ bền cũng cần 1 dầu súng.";
                case "repair_kit": return "Sửa dã chiến: +35% độ bền (Kỹ thuật cộng thêm), tối đa 80% (Kỹ thuật cao tới 100%). Tab → chuột phải vào súng → Sửa dã chiến. Chế từ 3 kim loại vụn + 1 dầu súng: dã chiến 8 giây, ở bàn 6 giây.";
                case "weapon_parts": return "Lò xo, chốt, kim hỏa. Sửa súng ở bàn chế tạo lên 100%: 1 linh kiện cho mỗi ¼ độ bền thiếu. Rã súng thu được để có thêm.";
                case "pistol_k54": return "K-54 (TT-33 bản Trung Quốc), đạn 7,62×25, băng 8 viên. Phím 2 để cầm; giữ chuột phải để ngắm, chuột trái bắn, R thay băng. Có độ bền như mọi khẩu súng.";
                case "rifle_mosin": return "Mosin M91/30, ống ngắm PU 3,5×, tầm bắn 200 m, đạn 7,62×54R, 5 viên. Trúng đầu hoặc thân địch: một viên hạ gục. Đứng yên hoặc khom để bắn chính xác. Kéo vào ô 1 để thay AK; giữ chuột phải để ngắm PU, R nạp từng viên. Có độ bền; rã ra cả kính ngắm.";
                case "ammo_rifle": return "Đạn 7,62×39 cho AK. Nhấn R để nạp. Dã chiến: 2 vật tư đạn thu hồi + 1 gói tiếp liệu AK → 10 viên (8 giây). Ở bàn: ráp gói trong 5 giây hoặc dùng 2 thuốc súng + 1 kim loại vụn.";
                case "gunpowder": return "Hộp thiếc thuốc súng đen. Chế đạn ở bàn: đạn K-54 (1), đạn súng trường (2) và đạn Mosin (3) — mỗi hộp chỉ dùng được một lần, nên phải chọn khẩu nào cần đạn hơn.";
                case "ammo_pistol": return "Đạn 7,62×25 cho K-54. Nhấn R để nạp. Chế ở bàn: 1 thuốc súng + 1 kim loại vụn ra 8 viên.";
                case "ammo_sniper": return "Đạn 7,62×54R cho Mosin, nạp từng viên bằng phím R. Hiếm: chế ở bàn 3 thuốc súng + 1 kim loại vụn ra 5 viên.";
                case "smoke_module": return "Ống tạo khói đã chuẩn bị sẵn. Cùng 1 vải làm 1 bom khói: Tab → Chế dã chiến (6 giây) hoặc tại bàn (4 giây).";
                case "fuse_module": return "Mô-đun kích hoạt, trong hòm sắt trại C2. Chế lựu đạn F1: 1 lõi nổ + 1 mô-đun kích hoạt + 1 kim loại vụn.";
                case "blast_charge": return "Lõi nổ hiếm, trong hòm sắt trại C3. Chế lựu đạn F1: 1 lõi nổ + 1 mô-đun kích hoạt + 1 kim loại vụn.";
                case "scope_lens": return "Thấu kính dự phòng cho ống ngắm. Rã Mosin sẽ có; hiếm trong hòm trại C3 và đồ của chỉ huy. Chưa dùng vào việc gì ở bản này.";
                case "smoke_bomb": return "Đồ ném: mây khói bán kính 4 m trong 15 giây, lính không nhìn xuyên qua. Chế từ 1 ống tạo khói + 1 vải. Chọn trong túi, giữ chuột trái ngắm, thả để ném.";
                case "grenade_f1": return $"Lựu đạn F1: nổ ngay khi chạm đất, chết trong {Map01StoneThrow.KillRadius:0} m, bị thương tới {Map01StoneThrow.WoundRadius:0} m, tiếng nổ vang 40 m — cả Nam cũng không tránh được. Chọn trong túi, giữ chuột trái ngắm, thả để ném.";
                default: return id != null && id.StartsWith("ammo_") ? "Đạn cho " + Name(id).Substring(4).ToLowerInvariant() + ". Không dùng trực tiếp từ túi đồ." : "Vật phẩm trong túi đồ.";
            }
        }
        /// <summary>Where a bench material is found, for the bench's "Lấy ở" line when it runs short.</summary>
        public static string Source(string id)
        {
            if (id == "ammo_recovered") return "Tab → Chế dã chiến → Thu hồi đạn M16";
            if (id == "ak_loading_pack") return "Thưởng C1, hòm C2, túi đạn trước C3 / kho căn cứ";
            // Read off Map01Loot.json, so it never drifts from what the tables give.
            return Map01Loot.Load().WhereFound(id) ?? "Hòm sắt các trại địch";
        }
        /// <summary>The bench's tab a recipe's output belongs to.</summary>
        public static string BenchGroup(string outputId)
        {
            switch (outputId) {
                case "medkit_small": return "heal";
                case "smoke_bomb": case "grenade_f1": return "throw";
                case "ammo_556": case "ammo_pistol": case "ammo_rifle": case "ammo_sniper": return "ammo";
                default: return "tool";
            }
        }
        /// <summary>Short facts about a bench output, shown as chips on the bench screen.</summary>
        public static string[] BenchFacts(string outputId)
        {
            switch (outputId) {
                case "medkit_small": return new[] { "Hồi 45 máu", "Phím H" };
                case "repair_kit": return new[] { "+35% độ bền", "Sửa dã chiến" };
                case "smoke_bomb": return new[] { "Khói 4 m · 15 giây", "Giữ/thả chuột trái" };
                case "grenade_f1": return new[] { $"Chết {Map01StoneThrow.KillRadius:0} m · thương {Map01StoneThrow.WoundRadius:0} m", "Nổ khi chạm đất" };
                case "ammo_pistol": return new[] { "Cho K-54", "R nạp đạn" };
                case "ammo_sniper": return new[] { "Cho Mosin", "R nạp từng viên" };
                case "ammo_556": return new[] { "Cho M16 · 5,56×45 mm", "Nạp bằng R" };
                case "ammo_rifle": return new[] { "Cho súng trường", "Nạp bằng R" };
                default: return System.Array.Empty<string>();
            }
        }
        public static int IconIndex(string id)
        {
            switch (id) {
                case "ammo_rifle": return 0; case "cloth": return 1; case "herb": return 2;
                case "medkit_small": return 3; case "supplies": return 4; case "river_documents": return 5;
                case "stone": return 6; case "scrap_metal": return 7; case "gun_oil": return 8;
                case "repair_kit": return 9; case "ammo_smg": return 10;
                case "ammo_shotgun": return 12; default: return -1; // ammo_pistol (11) and ammo_sniper (13) now have rendered icons
            }
        }
    }
}
