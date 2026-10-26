# Sửa nhanh Map 2 và tiếp nhận phần Claude — 26/10/2026

## Phạm vi

- Rà soát trên dev `3fd2b9a0`; cập nhật dự án chính `thinh` tới dev mới `cf9d9090` (có thẩm vấn Map 1 của nhóm).
- Người dùng yêu cầu sửa lỗi rõ ràng trước, tiếp nhận phần Claude chưa commit và đẩy lên. Giữ giọng dev; bộ giọng Claude lưu riêng để duyệt sau.
- [Kế hoạch toàn chương](D:/DO_AN_GAME/ShadowVale/Docs/Handoff/Map02_3NgayDem_RaSoat_KeHoach_2026-10-26.md).

## Đã sửa

| Lỗi | Hành vi sau sửa |
|---|---|
| Có code leo nhưng chỉ chỉnh sóng dưới đất | Dựng đủ tuyến/cây/điểm bám; W leo, S xuống, căn sóng rồi cột và xuống mới hoàn thành; Esc hủy an toàn. |
| Thiếu site/tổ tuần tra, đèn từ chỗ Tâm cũ | Tổ 3 lính/3 đèn thật; quan sát đúng tổ; vật che chắn nhìn và bắn; giữ AI tìm vị trí cuối đã thấy và hộ tống xe tăng của dev. |
| Kiểm kho/tuyến sơ tán chưa gọi luật có sẵn | Nhìn đúng chốt rồi E; đi bốn đoạn theo thứ tự; save/load giữ tiến độ; thoại bộ đàm không khóa điều khiển. |
| Diễn xuất Ngày 2/Đêm 2 có code nhưng không chạy | Nối tạo/lấy mẫu/đoạn im lặng/cleanup, camera và menu preview. Chưa coi pose thủ tục là mỹ thuật hoàn thiện. |
| Kết thúc hội thoại hai lần gây NullReference | Bỏ qua lệnh lặp khi không còn sceneTask; không hoàn thành nhiệm vụ hai lần. |
| Catalog cục bộ chỉ có 127 mục, thiếu 134 mục dev | Giữ đủ 261 mục dev và thông tin casting; chỉ giữ chỉnh sửa echo cục bộ. Thêm test kiểm mọi câu có tiếng trong cả bảy giai đoạn Map 2. |
| Test tua cảnh bị tiếng thoại thời gian thật giữ lại | Sửa helper test khi tua đồng hồ; không cắt/rút thoại game thật. |
| Helper thiếu đối số optional; kỳ vọng preview cũ | Reflection cấp default cho tham số thêm; Ngày 2 preview cạnh chỉ huy, Đêm 2 cạnh cầu như dev. |

Code chính: `Assets/_Project/Map01/Runtime/Map02ChapterDirector.cs`, `Map02ChapterCinematics.cs`, `Map02ChapterEnemy.cs`, `Map02ChapterGuidance.cs`; menu `Editor/Map02RoutePreview.cs`. Test sửa: `Map02FinaleTests`, `Map02DayTwoRouteTests`, `Map02PerformanceTests`, `Map02RoutePreviewTests`.

Điểm đối chiếu trong bản giao: [khởi tạo tuyến](D:/DO_AN_GAME/ShadowVale/Assets/_Project/Map01/Runtime/Map02ChapterDirector.cs:149), [cập nhật Đêm 2](D:/DO_AN_GAME/ShadowVale/Assets/_Project/Map01/Runtime/Map02ChapterDirector.cs:248), [chặn kết thúc lặp](D:/DO_AN_GAME/ShadowVale/Assets/_Project/Map01/Runtime/Map02ChapterDirector.cs:401), [cleanup diễn xuất](D:/DO_AN_GAME/ShadowVale/Assets/_Project/Map01/Runtime/Map02ChapterCinematics.cs:11), [test đủ thoại cả chương](D:/DO_AN_GAME/ShadowVale/Assets/_Project/Map01/Tests/Map02ChapterTests.cs:17).

## Giữ phần Claude

- Giữ partial Ngày 1–Đêm 1, mocap, dữ liệu đạo cụ, minigame, âm nền và sửa tiếp xúc/mô hình đã có trong thư mục chính.
- Giữ hệ âm nền cục bộ; không bật thêm hệ Soundscape tương đương gây chồng âm.
- Backup: `main-delivery-before/receipt.json`, `main-delivery.json`. Sao lưu 41 file; trước quyết định giọng, 38 file giữ nguyên byte, Director được ghép và hai catalog chỉ thêm cue mới từ dev. Sau lựa chọn giọng, catalog/test thoại được đổi có chủ đích và lưu bản đề xuất trước đó.
- Bộ giọng đề xuất có **56 ID gốc, dùng tại 58 mục catalog** (hai alias ở lời liên lạc Map 2). Người dùng chọn giọng dev. **132 file âm thanh/metadata** chuyển nguyên vẹn sang `SourceArt/Dialogue/Map01Map2Voice/Audio`, ngoài Resources; catalog/test đề xuất cũng được lưu. [Trạng thái duyệt](D:/DO_AN_GAME/ShadowVale/SourceArt/Dialogue/Map01Map2Voice/REVIEW_STATUS.md).

## Kiểm thử

| Lượt | Kết quả | Ghi chú |
|---|---|---|
| Dev trước sửa | 17 đạt / 9 lỗi / 26 ca | `before-tests.xml`; Unity ghi XML nhưng crash khi thoát. |
| Dev sau sửa lần 1 | 24 đạt / 2 lỗi / 26 ca | `audit-tests.xml`. |
| Kiểm lại hai ca | 1 đạt / 1 lỗi | `retry-two.xml`; lộ EndScene lần hai. |
| Sau chặn kết thúc lặp + kiểm preview | 1 đạt / 1 lỗi | `retry-performance.xml`; diễn xuất đạt, preview còn kỳ vọng cũ. |
| Kiểm preview cuối | 1/1 đạt, exit 0 | `retry-preview.xml`. |
| Tổng hợp dev riêng | **27/27 ca riêng biệt đạt ở lượt mới nhất của từng ca** | `test-summary.json`; không phải một lượt chạy cả 27 ca cùng lúc. |
| Catalog dự án chính sau ghép | **8/8 đạt, exit 0** | `main-catalog.xml`: đủ thoại cả chương, catalog/âm thanh hợp lệ, câu Ngày 1–Đêm 1 khớp, giọng Map 1 và tài nguyên âm thanh. |
| Dự án chính + Claude + giọng đã chọn | **83/83 đạt, exit 0** | `main-integrated-final.xml`; có luồng 10 mục tiêu bằng phím/di chuyển thật, C1, Ngày 2–Đêm 2, kết cục và cảnh thẩm vấn dev mới. |
| Tổng hợp dự án chính | **87/87 ca riêng biệt đạt qua hai lượt hoàn tất** | `main-test-summary.json`; bốn ca trùng giữa hai lượt chỉ đếm một lần. Runner bị dừng không tính là đạt. |

Không dùng probe/video encode làm bằng chứng 60 FPS. Test logic đạt không chứng minh đồ họa/animation đã đẹp.

Kiểm phụ thuộc asset đã chọn: không thiếu meta, không có GUID trỏ tới asset cục bộ bị bỏ khỏi commit; [biên nhận](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/Map02_Audit_20261026/asset-dependencies.json). Đây là kiểm tham chiếu serialized; các chuỗi Resources động được kiểm bởi test runtime, không phải bằng chứng đã build/chơi hết chương.

## Còn lại về thẩm mỹ

Ảnh test Đêm 2 cho thấy lớp ngủ/đắp áo đang thô: vị trí nghỉ, hình áo và tư thế hai tay Nam cần dựng lại trong Blender theo môi trường thật. Đặt bộc phá, trao đài, Nam bị hất/ngồi và bình minh còn thiếu asset/clip trong kế hoạch. Chưa thêm xương hay xuất animation mới trong lượt sửa code.

Ảnh chụp từ test diễn xuất trên dev riêng sau khi nối lại luồng: [Hùng ngủ](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/Map02_Audit_20261026/dev-night2-sleep.png), [Nam đắp áo](D:/DO_AN_GAME/ShadowVale/Docs/Reviews/Map02_Audit_20261026/dev-night2-cover.png). Đây là bằng chứng các điểm còn thô cần sửa ở gói A5, không phải bản mỹ thuật đã duyệt hoặc kết quả FPS.

## Git

Đã kiểm dự án chính trước commit/push `thinh`, sau đó đồng bộ `dev` theo yêu cầu. Biên nhận Git được ghi sau khi đối chiếu remote. Demo cũ không liên quan, scene sandbox, hai ảnh báo cáo M16 và backup cục bộ không thuộc commit này. `testCraftingKit=false`; scene Map 1/Map 2 không đổi.
