# Loot trong hầm kho Đêm 2

- Loot gắn vào ba thùng đã có trên tầng giữa của ShelfFront, ShelfBack và ShelfEast trong model Blender. Không tạo thêm thùng dưới sàn; chỉ thêm điểm tương tác ẩn vào các kệ.
- Mỗi hộp chứa một gói: 30 viên AK (`ammo_rifle`), 21 viên K54 (`ammo_pistol`) hoặc 2 băng cứu thương (`medkit_small`). Ba gói được xáo ngẫu nhiên giữa ba hộp theo một trong sáu thứ tự; tổng vật phẩm luôn giữ nguyên.
- Khi đã xuống hầm, tới trong 1,7 m và có đường nhìn tới hộp, nhấn E để lấy gói đồ. Tương tác có ưu tiên trước thao tác kiểm tra kệ, nhưng không tự hoàn thành nhiệm vụ kiểm tra vật tư.
- Không lấy được khi đang dùng thang, mở menu, pause, hội thoại/cutscene hoặc đứng ngoài hầm. Vách/kệ chặn lấy đồ xuyên vật cản.
- Dùng hệ thống túi hiện có: nhận đúng loại đạn AK dù Nam đang cầm súng khác. Túi đầy thì phần dư được đặt dưới chân và được lưu cùng các đồ rơi của Map 2.
- Save lưu thứ tự xáo (`night2StoreLootOrder`), cờ đã khởi tạo và bitmask ba hộp đã lấy (`night2StoreLootMask`). Mỗi lần lấy cập nhật checkpoint; tải lại không cấp trùng. Save cũ chưa có các trường này được khởi tạo ba hộp lần đầu. Reset tuyến không tự xóa cờ loot hoặc xáo lại đồ.
- Đã bỏ resource thùng riêng `Equipment/NightStoreLootCrate`; hình ảnh thùng dùng trực tiếp model hầm kho hiện có. Điểm lấy đồ nằm sát mặt ngoài kệ để không bị collider kệ chặn.

Kiểm tra biệt lập Unity: `SourceArt/Map02NightBankFlow/store_loot_checks.cs`; kết quả lưu tại `store-loot-checks.txt`. Kiểm tra toàn bộ sáu thứ tự, model/collider hộp, tương tác, tổng đồ thật trong túi, checkpoint, chặn lấy lặp và validation save. Chưa xác minh hình ảnh và lối đi bằng lượt chơi toàn cảnh Map 2.

## Điều khiển thang và hướng dẫn

- Ở miệng hầm: nhấn Ctrl trái hoặc phải để bám thang. Ở chân thang: nhấn Space để bám thang. Khi đã bám thang, W đi lên, S đi xuống; Ctrl/Space không di chuyển trên thang, thả W/S thì dừng. Ctrl được nhận trước thao tác ngồi và Space được nhận trước thao tác nhảy khi dùng thang.
- Nới vùng tương tác thêm bán kính 0,3 m: điểm trên mặt đất từ 0,8 m lên 1,1 m; vùng chân thang được thêm mép tròn 0,3 m. Vẫn yêu cầu cùng cao độ và có nền đỡ chân.
- Bỏ khung hướng dẫn và marker thang cũ. Trong vùng tương tác, hiện chữ trắng size 14 ở giữa màn hình: “Dùng phím Ctrl để bám thang” hoặc “Dùng phím Space để bám thang”. Có bóng chữ mảnh, không có nền/khung. Khi đã bám thang, không hiện dòng hướng dẫn giữa màn hình.
- Kiểm tra điều khiển và ranh giới bằng `SourceArt/Map02NightBankFlow/ladder_control_checks.cs`, báo cáo `ladder-control-checks.txt`. Đây là kiểm tra biệt lập, chưa kiểm tra trực quan trong toàn cảnh Map 2.
