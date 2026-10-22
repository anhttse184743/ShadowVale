# Đồng bộ HUD nhân vật Map 1 sang Map 2

Map 2 dùng lại presenter, dữ liệu vật phẩm và các thành phần HUD của Map 1 thông qua `CharacterHudSession`. Minimap và bản đồ lớn hiển thị địa hình, vị trí nhân vật và tiến trình nhiệm vụ của chính Map 2.

## Đã áp dụng

- Thanh máu, thể lực, trang bị, đạn trong băng và đạn dự trữ; tâm ngắm và hiệu ứng máu thấp dùng thành phần HUD chung.
- Túi đồ, ô trang bị, chia/chuyển/vứt/nhặt vật phẩm, độ bền súng, sửa chữa, chế tạo gần kho và bảng kỹ năng dùng giao diện Map 1.
- Minimap tròn, hướng Bắc, vị trí người chơi, đồng đội, mục tiêu và đường dẫn. Vùng chưa khám phá được che; địch hiện theo khoảng cách, góc nhìn và vật cản. Phím M mở bản đồ lớn.
- Khung mục tiêu, chỉ hướng, thông báo nhặt đồ, ngắm đồ ném, cảnh báo và mặt nạ ống nhòm dùng các thành phần chung; nội dung nhiệm vụ vẫn lấy từ chapter Map 2.
- Lưu/tải bố cục túi, độ bền, băng đạn từng súng, thể lực, công việc chế tạo, đồ đã thả và vùng đã khám phá. Chuyển từ Map 1 giữ bố cục túi và thể lực.
- Khi mang hàng hoặc hộ tống, khóa thao tác đổi trang bị phù hợp trạng thái nhiệm vụ. Tâm ngắm ẩn khi mở bảng, dùng ống nhòm hoặc ngắm kính súng.

## Kiểm thử

- Bộ hồi quy HUD, Map 2, tuyến Ngày 2/Đêm 2, preview, M16 và minimap: 26/26 đạt trước lượt bổ sung cuối về tâm ngắm, máu thấp và nét cắt minimap. Báo cáo: `Logs/Map02Chapter/shared-hud-26-passed.xml`.
- Lượt kiểm tra bổ sung ngày 22/10/2026: 6/6 đạt, gồm tích hợp HUD Map 2, phím trang bị Map 1 và minimap. Xác nhận hiệu ứng máu thấp, ẩn/hiện tâm ngắm, đồ ném, lưu/tải túi và vùng khám phá. Báo cáo: `Logs/Map02Chapter/shared-hud-results.xml`. Các ca này trùng một phần bộ 26 ca trên, không cộng thành 32 ca độc lập.
- Đã xem ảnh Game View 1920×1080 của gameplay, túi, bản đồ, máu thấp và ống nhòm. Minimap đã hết khe sọc ngang khi scale giao diện.
- Ảnh Game View tại `Logs/Map02Chapter/SharedHud/`: gameplay, bag, skills, map, low-health và night-binoculars.

## Tự kiểm tra trong Unity

1. Đợi Unity biên dịch xong.
2. Chọn `ShadowVale > Map 2 > Chạy thử Ngày 2` hoặc `Chạy thử Đêm 2`.
3. Dùng Tab mở túi/kỹ năng, M mở bản đồ, J mở nhiệm vụ, H hồi máu; phím 1–4 đổi trang bị. Khi ở sát kho, E mở bàn chế tạo nếu không có tương tác nhiệm vụ được ưu tiên.
4. Ở tuyến có ống nhòm, dùng F theo lời nhắc. Kiểm tra minimap, mục tiêu và đường dẫn khi di chuyển.

Chế độ chạy thử sử dụng thư mục save riêng trong Temp; dừng Play sẽ khôi phục cấu hình chạy trước đó.
