# Map 1 — Blockout theo ảnh tham khảo

Mở `Assets/_Project/Scenes/Maps/Map01_ReferenceBlockout.unity` trong Unity rồi nhấn Play. Scene mới độc lập với `Map 1.unity` và `Map01_ForestFootprints.unity`.

## Bố cục

Khu chơi khoảng 90 × 148 m. Từ nam lên bắc: điểm nhận tiếp tế → ngã ba tuần tra → điểm nghỉ/chế tạo → căn cứ bỏ hoang → bến giao hàng. Có đường chính và hai lối vòng để thử stealth.

Ảnh vệ tinh được dùng cho ao/ruộng chữ nhật có bờ bao phía tây. Ảnh rừng và kênh được dùng cho rừng thưa, bụi nấp, kênh uốn lượn phía đông, bèo nước và hai xuồng. Ảnh nhà lá và phòng họp được dùng cho hầm đất, mái lá cắt một phần, cột gỗ, bàn dài, ghế, bản đồ và đèn vàng.

Hầm hiện là khối mô phỏng bán chìm, sàn ngang mặt đất để kiểm tra di chuyển. Mái được cắt để thấy nội thất từ camera isometric. Nước là khối màu có vùng chặn; chưa có bơi, chèo thuyền, texture hoặc cây hoàn thiện.

## Chạy thử

WASD di chuyển theo camera, Shift chạy, C đi khom, E tương tác; Q ném đá, chuột trái bắn. Tab mở túi, B chế tạo gần bàn, H hồi máu, M bản đồ. Sử dụng gameplay prototype Nam–Hùng có sẵn trong project.

## Chỉnh sửa và dựng lại

Các khối là GameObject riêng. Địa hình và collider nằm trong `01 • Terrain & collision`; cây, nước và chi tiết nằm trong `02 • Foliage & set dressing`. Asset riêng nằm ở `Assets/_Project/Map01/ReferenceBlockout`.

Menu `ShadowVale > Map 1 > Build Reference Wetland Blockout` dựng lại từ mã nguồn. Lệnh này có hỏi lưu scene đang chỉnh và lưu bản sao scene blockout trước khi dựng lại. Chỉnh generator nếu muốn giữ thay đổi qua các lần dựng. Menu Validate Forest Routes và Capture Forest Previews kiểm tra/xuất ảnh scene đang mở.

## Đã kiểm tra

Unity 6000.3.24f1 biên dịch và dựng scene thành công bằng batch trên bản sao tạm. 36 tuyến NavMesh đi thông, bao gồm đường chính, lối vòng, căn cứ, bến và tuyến tuần tra. Tham chiếu nhiệm vụ đầy đủ. Đã xem ảnh render tổng thể và khu căn cứ tại `Tools/Map01ReferenceReports`. Chưa thử điều khiển thủ công trong Play Mode do công cụ chụp cửa sổ Unity gặp lỗi hệ thống.
