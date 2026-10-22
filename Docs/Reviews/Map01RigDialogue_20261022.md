# Map 1 — Giao điểm 3 và khôi phục rig

## Hội thoại

Sau khi đài và nguồn điện đã bị vô hiệu hóa, báo cáo chính gồm đúng thứ tự:
1. Nam báo đài đã ngừng phát.
2. Chỉ huy giao quan sát sĩ quan ở điểm ba.
3. Cùng chỉ huy chỉ vị trí khẩu Mosin và năm viên ở vai đồi Đông Nam.
4. Nam: “Rõ.” — đúng một lần.

Dùng lại các bản thu đã duyệt, không tạo thoại trả phí. Chuyển stage chỉ cập nhật nhiệm vụ, không phát thêm hội thoại Mosin ngoài cuộc nói chuyện chính.

Thông báo gameplay không được thay gate hoặc dừng bộ phát khi hội thoại chính đang sở hữu nó; ca thử gửi cả thông báo và lệnh xóa giữa lúc Nam báo cáo vẫn phát hết bốn cue đúng thứ tự.

Các cue Map 2 dùng lại bản thu “Rõ.” vẫn gọi được bằng ID. `SpeakText` chọn một cue cho cùng một mẫu câu, tránh xếp hàng nhiều bản thu giống hệt nhau từ các chapter khác.

## Rig và checkpoint

- K54 và Mosin bỏ qua track không có xương tương ứng trên rig cũ; vẫn đăng ký vũ khí và giữ những track có xương thật. Không tạo xương giả hoặc sửa Avatar.
- Chỉ thay mesh tay nếu toàn bộ bone binding của mesh thay thế đều có mặt. Nếu thiếu, giữ tay gốc. Dynamic ForearmTwist không bị tắt khi thiếu track twist được bake.
- Hùng giữ tham chiếu controller theo nhiệm vụ. Tải checkpoint đóng hội thoại/cutscene đang sở hữu Playable trước; giải phóng pose chết quỳ và khôi phục controller rồi mới đặt tư thế stage. Không ghi nhận kết quả skip hay trao thưởng khi chỉ hủy cutscene để tải save.

## Các dòng log khác

Sidecar khởi động solver ở cổng 8001 là thông báo bình thường. Telemetry chưa tới backend cổng 5000 thì giữ session trong hàng đợi; đợt sửa này không xóa dữ liệu hàng đợi hoặc thay cấu hình backend.

## Kiểm chứng

Lần đầu: 6/7 đạt. Ca âm thanh phát hiện 5 cue alias Map 2 làm “Rõ.” bị lặp, đã sửa bộ chọn cue và chạy lại. Kết quả cuối lưu tại `Logs/Map01RigDialogue_20261022/verified.xml`.

## Kết quả cuối

- `verified.xml`: 16/18 đạt. Hai ca cần sửa bộ kiểm tra: cho phép alias cùng câu đã duyệt; closure của callback phải tạo sau EnterPlayMode.
- `followup.xml`: kiểm tra catalog alias và danh sách thoại đã đạt.
- `final-flow.xml`: rig thiếu twist, khôi phục controller/pose Hùng, danh sách cue và tạo–tải save Map 2 đã đạt. Ca phát thoại phát hiện thông báo gameplay có thể chặn gate trong cinematic.
- `report-gate.xml`: ca hội thoại thật sau khi sửa gate đã đạt: Nam báo cáo → chỉ huy giao điểm 3 → chỉ vị trí Mosin → Nam “Rõ.”, sau xác nhận stage 7 và không có thoại nối đuôi. Kiểm tra bao gồm thông báo gameplay và lệnh xóa gửi tới giữa hội thoại.
- `git diff --check`: không có lỗi khoảng trắng.

## Bản lưu test Map 2

Đã tạo ở ô 0 (LƯU NHANH), trước đó ô này trống. Tên hiển thị: **TEST MAP 2 — ĐẦU NGÀY 1**. Vào Tải game → LƯU NHANH. Vị trí bắt đầu ở bờ đi bộ được, Nam/Hùng/anh Thành ở cùng điểm; Ngày 1 chưa hoàn thành D1_01, máu 100, thời gian chơi 0. Bản lưu đã được thử bằng `Map02Mission.BeginGame`, đúng đường tải của menu.

File: `C:/Users/dangk/AppData/LocalLow/DefaultCompany/ShadowVale/shadowvale-slot-0.json`.
Bản sao dự phòng: `Logs/Map01RigDialogue_20261022/map2-start-save.json`.
