# Map 2 — kiểm tra thuyền và giao diện ngày 16 tháng 10 năm 2026

- Gia cố thuyền chuyển scene: đợi một frame cập nhật scene đích khi còn màn đen rồi tạo renderer mới; material riêng không instancing, InstanceTone rõ ràng và không dùng occlusion động của scene cũ. Chỉ áp dụng thuyền/ghế/mái chèo, không đổi renderer môi trường hoặc nhân vật.
- Map02Mission có điểm khởi tạo dự phòng sau scene load, bảo đảm một session. HUD nhận dữ liệu trực tiếp của session, không tạo Map01Mission giả.
- HUD dùng Panel, CombatIcons và font hiện có của Map 1; có máu, trạng thái vũ khí, đạn, nhiệm vụ, minimap, marker vàng và khoảng cách. Tắt HUD combat dự phòng để tránh chồng chữ.
- Minimap chụp địa hình thật một lần trong coroutine, không gọi Camera.Render trong OnGUI. Bản đồ lớn và minimap dùng cùng miền tọa độ 220 m; marker lấy từ các object khả dụng của stage hiện tại.
- Checkpoint được tạo sau cập bến; menu Esc dùng phiên Map 2 để lưu/tải. Test xác nhận không nhân đôi object, thuốc hoặc kiện hàng và giữ đúng máu, đạn.

Lượt cuối **11/11 đạt** trong `Logs/Map02-hud-transfer-final.xml`. Đã xem ảnh Game View thực 1920×1080 của luồng Map 1 → Map 2, thuyền cập bến và HUD sau lên bờ (`Logs/GameViewTransfer/gameplay.png`). Luồng tải checkpoint hoàn thành Map 1 và luồng mở Map 2 trực tiếp/nhận việc/giao hàng/lưu/tải cũng đạt; ảnh tại `Logs/Map02ObjectFlow/gameplay.png`. Lượt trước 13/13 trong `Logs/Map02-hud-transfer-review.xml` bổ sung kiểm tra tiến trình và giao dịch vật phẩm.

Phạm vi xác minh là chuyển map, giao diện và các luồng logic nêu trên. Không coi đây là nghiệm thu toàn bộ các trận đánh, NPC sơ tán hoặc cutscene Hùng hy sinh còn cần triển khai trong Map 2.
