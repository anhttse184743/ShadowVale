# Kết quả ghép dev vào thinh — 20/10/2026

> Báo cáo lịch sử của lượt merge đầu. Lượt rà, ghép dev mới và đồng bộ thinh/dev ngày 21/10 được ghi ở [Map01_Followup_20261020/KET_QUA.md](../Map01_Followup_20261020/KET_QUA.md); không dùng trạng thái "Không push" bên dưới làm trạng thái hiện tại.

Dev đã kiểm: `a1e2b9a9b0e71b42316fd9ecf9174c279c6304d1` (remote đã kiểm lại cùng SHA).
Thinh trước ghép: `e44dab1e4be14f14d3524dc731486508b8677242`. HEAD tại lần kiểm tra này: `e44dab1e4be14f14d3524dc731486508b8677242`.

Mã ghép đã áp dụng trên dự án chính.
Không push. Việc commit tuân theo lựa chọn của người dùng ở cuối lượt ghép.

## Nội dung đã chốt

- Lấy AI và cân bằng chiến đấu mới của dev; giữ hàng rào C3, lực lượng hiện có và mức gọi quân 3 người.
- Nhận scene dev; 2.583 block của cụm nhà C3 đã duyệt được đối chiếu không đổi.
- Giữ K54 thường, Mosin/PU/nạp từng viên, cơ chế bắn xa không tự lộ, dao cạnh giường, ngủ/thức, E nhận lệnh và việc đang làm với vật ném.
- Bảo toàn các thay đổi chưa commit bằng bản sao SHA256 trước ghép. Xem `Temp/MergeDev_20261020_Before/manifest.json`.

## Code chính

- Giữ biên C3 cùng chiến thuật lùi sau đồng đội: [BossRelocation.cs:33](D:/DO_AN_GAME/ShadowVale/Assets/_Project/Scripts/AI/Boss/BossRelocation.cs:33).
- Chuyển tiếp body C3 sang giao diện mới: [Map01ConfinedBody.cs:24](D:/DO_AN_GAME/ShadowVale/Assets/_Project/Map01/Runtime/Map01ConfinedBody.cs:24).
- Bố trí lại tiếp xúc tay theo mesh súng và tầm với: [Map01Extraction.cs:280](D:/DO_AN_GAME/ShadowVale/Assets/_Project/Map01/Runtime/Map01Extraction.cs:280).
- Nhả bộ điều khiển vũ khí của Map 1 trước chuyển cảnh: [Map01Mission.cs:332](D:/DO_AN_GAME/ShadowVale/Assets/_Project/Map01/Runtime/Map01Mission.cs:332).
- Ghi nhận vũ khí thực sự của viên đạn chờ xử lý: [Health.cs:99](D:/DO_AN_GAME/ShadowVale/Assets/_Project/Scripts/Gameplay/Combat/Health.cs:99).
- Chặn batch editor ghi vào save thật khi fixture thoát: [ForestSaveSlots.cs:34](D:/DO_AN_GAME/ShadowVale/Assets/_Project/Map01/Runtime/ForestSaveSlots.cs:34).

## Kiểm thử thật

Kết quả mới nhất của **200 ca riêng biệt** trên bản ghép/dự án chính: **200 đạt, 0 chưa đạt**. Các lượt lặp không cộng trùng ca.

| File kết quả | Đạt | Lỗi | Tổng |
|---|---:|---:|---:|
| [dev-pure.xml](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/MergeDev_20261020/dev-pure.xml) | 122 | 0 | 122 |
| [candidate-pure-r2.xml](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/MergeDev_20261020/candidate-pure-r2.xml) | 83 | 0 | 83 |
| [dev-gameplay.xml](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/MergeDev_20261020/dev-gameplay.xml) | 68 | 3 | 71 |
| [dev-grip-diagnostic.xml](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/MergeDev_20261020/dev-grip-diagnostic.xml) | 1 | 0 | 1 |
| [dev-grip-fixed.xml](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/MergeDev_20261020/dev-grip-fixed.xml) | 1 | 1 | 2 |
| [dev-pursuit-diagnostic.xml](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/MergeDev_20261020/dev-pursuit-diagnostic.xml) | 1 | 0 | 1 |
| [dev-extraction-fixed-r2.xml](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/MergeDev_20261020/dev-extraction-fixed-r2.xml) | 3 | 0 | 3 |
| [dev-extraction-fixed-r3.xml](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/MergeDev_20261020/dev-extraction-fixed-r3.xml) | 3 | 0 | 3 |
| [dev-manual-opening.xml](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/MergeDev_20261020/dev-manual-opening.xml) | 2 | 0 | 2 |
| [candidate-gameplay.xml](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/MergeDev_20261020/candidate-gameplay.xml) | 81 | 18 | 99 |
| [candidate-gameplay-r2.xml](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/MergeDev_20261020/candidate-gameplay-r2.xml) | 33 | 3 | 36 |
| [candidate-queued-before.xml](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/MergeDev_20261020/candidate-queued-before.xml) | 1 | 1 | 2 |
| [candidate-final-fixes.xml](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/MergeDev_20261020/candidate-final-fixes.xml) | 14 | 0 | 14 |
| [main-smoke.xml](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/MergeDev_20261020/main-smoke.xml) | 5 | 0 | 5 |

Các lượt đỏ ban đầu vẫn được giữ để kiểm toán; xem `PHAT_HIEN_VA_CACH_SUA.md` về nguyên nhân và ca kiểm lại tương ứng. `test_results.json` chứa tên từng test và stack lỗi gốc.

## Save, hình ảnh và giới hạn

- Hash ô thủ công 0/1 khớp snapshot. Ô tự động bị một đường thoát test ghi nhầm đã được khôi phục; cả ba file lưu tiếp tục khớp sau 5 ca kiểm tra trên dự án chính (`save_verification.json`).
- Ảnh đo tiếp xúc tay: `grip-before.png`, `grip-after.png`; không dựng lại mesh/rig hay diễn xuất ngón tay trong lượt ghép.
- Chưa benchmark 1080p/60 FPS hay kiểm thử bản player phát hành. Kết quả trên là kiểm thử chức năng trong Unity Editor.
- Các cửa sổ Blender đang mở không bị điều khiển. Các file recovery, scene test sinh lại và ảnh/video cũ được giữ ngoài phần stage phát hành.
