# Ghép Thịnh và dev — 22/10/2026

Yêu cầu: đưa các sửa đổi đã hoàn tất lên `thinh`, kiểm tra thay đổi mới trên `dev`, ghép vào `dev` và push.

## Nội dung tích hợp

- Commit gameplay `e64ccb2a`: animation Nam v3, 47 clip cho dao/K54/vật ném/nhảy; Mosin một phát hạ địch Map 1, scope PU 3,5× và tầm 200 m; M16 22 damage / 600 RPM / 80 m với giật và tản đạn riêng.
- Giữ sửa tiếp tế theo nhiệm vụ: khói/F1/băng xuất hiện sớm, hai điểm tiếp tế C3 lưu riêng, phần thưởng không mất khi túi đầy, kho cuộn đủ món. M16 lính thường bắn loạt 3 viên, nghỉ 1,8 s cộng lệch theo lính, nạp sau 30 viên và tối đa 2 loạt thường cùng lúc.
- `dev` mới nhất lúc bắt đầu là `fadaa874`, hơn mốc chung `edfd6609` 6 commit. Các thay đổi gồm chương Map 2 và điều hướng, HUD/túi đồ dùng chung, ngắm bằng giữ chuột, hệ kỹ năng Map 2, NPC và cinematic ngày 2/đêm 2.

## Giải quyết xung đột

Ba file cần ghép thủ công: `ForestInventory.cs`, `Map01Pistol.cs`, `Map01Mosin.cs`. Giữ adapter `CharacterHudSession` của dev để K54/Mosin hoạt động ở cả hai map; giữ kiểm tra bộ xương Hùng, quyền điều khiển pose Nam v3, lethality Mosin, scope/tâm ngắm và mô tả thông số mới. Hướng dẫn dùng giữ chuột theo điều khiển hiện tại của dev.

Các thay đổi tự ghép ở HUD kho, M16, PlayerCombat và tương tác đã được rà: kho vẫn cuộn đủ vật tư, M16 giữ thông số mới, kỹ năng nhân hệ số thời gian nạp, báo cáo nhiệm vụ bằng E được ưu tiên khi đứng cạnh bàn chế tạo.

Scene Map 1 và bố trí địch C3 giữ nguyên so với dev. Map 2 giữ scene và nguồn nội dung do dev bổ sung. Animation Nam v3 vẫn chỉ cài từ mission Map 1; chưa tuyên bố cài v3 cho toàn bộ Map 2.

Ba file trạng thái review cũ chưa được Git theo dõi trùng tên với dev đã được chuyển vào `LocalBackups/` ở máy này trước khi merge. Các bản sao sandbox, recovery, video và tài liệu nháp ngoài phạm vi được giữ cục bộ. Source Blender cuối, FBX, asset bake, prefab và meta cần cho animation đã được đưa vào commit. GUID của asset mới đã được kiểm tra; shader và AssetVersion thuộc package URP.

## Kiểm chứng

**109 ca riêng biệt đều đạt ở kết quả mới nhất; 0 lỗi còn lại, không bỏ qua.** Đây là tổng hợp lượt 109 ca đầu và các lượt chạy lại có mục tiêu, không phải tuyên bố một lượt 109/109 mới. Lượt cuối `guide-final.xml` đạt 1/1, Unity thoát với mã 0. Runtime giữ nguyên từ lượt 109 ca; các lần sửa sau chỉ cập nhật fixture/test theo hành vi game đã ghép.

Kết quả sau ghép được ghi vào `merged-tests.xml` và tổng hợp bằng `summarize_tests.py` trong `test-summary.json`. Báo cáo các đợt trước trong thư mục animation/Mosin/M16/tiếp tế là bằng chứng tại thời điểm chạy trước merge; các hash và trường committed/pushed ở những receipt cũ cũng thuộc thời điểm đó.

Lượt đầu sau merge chạy 109 ca: 106 đạt, 3 không đạt, không bỏ qua. Hai test hình ảnh K54/Mosin vẫn nhả chuột rồi chờ trạng thái ngắm kiểu bật/tắt cũ; đã cập nhật input giữ chuột, đồng thời kiểm tra nhả chuột hạ ngắm, giữ ngắm trở lại sau nạp/kéo khóa nòng. Test guide nay đi qua báo cáo rút lui bằng hội thoại thật trước khi xác nhận dẫn ra bến, thay vì ép stage giữa luồng hậu chiến. Không đổi runtime để hợp thức hóa ba bài thử này. Kết quả chạy lại có mục tiêu được giữ ở `merge-rerun.xml`.

Lượt chạy lại ba ca đạt hai ca ngắm; guide vẫn vướng điều kiện an toàn vì fixture chỉ tắt Update lính, còn phản ứng khi chỉ huy chết đã ghi trạng thái giao chiến. Đã dừng phản ứng C3/phát hiện xác và cho các lính còn sống về trạng thái tại chốt trong phần setup của fixture trước khi gọi báo cáo. `guide-rerun.xml` xác nhận báo cáo đã mở extraction, nhưng Nam còn đứng ở jetty từ đầu test nên tự chuyển sang Boarding trước assertion. Fixture cuối đặt Nam ở điểm tiếp tế C3 trước khi báo cáo, để còn đoạn đường dẫn ra bến cần kiểm tra. Không hồi sinh chỉ huy, không bỏ qua bước báo cáo hoặc assertion hướng dẫn. Lượt xác nhận cuối được giữ riêng ở `guide-final.xml`.

Lượt kiểm tra bao gồm Nam v3, K54, Mosin thực/stealth, M16/độ tản đạn/đổi loại đạn, loot/tiếp tế/nhịp bắn/save C3, hướng dẫn và báo cáo nhiệm vụ, cùng HUD/túi đồ/Map 2 chapter/kỹ năng/lưu tải. Cảnh báo NavMesh của fixture lính ở làn thử trên cao không làm bài thử save/stealth thất bại; không thay điều hướng game trong đợt merge này.

Thông tin push cuối cùng và hai SHA trên máy chủ được ghi riêng ở `PUSH_RECEIPT.json` sau khi xác minh remote.
