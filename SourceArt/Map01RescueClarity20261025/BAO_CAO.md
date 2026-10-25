# Cứu Hùng — camera, đi lén, thuyền và giọng Nam

Ngày 25/10/2026. Project `G:\game\Capstone\ShadowVale`, nhánh Khoa.

## Thay đổi

- Intro tiếp cận vẫn kích hoạt khi Nam vào bán kính 40 m sau khi nhận nhiệm vụ. Sau khung hình Nam, cắt ngắn sang Hùng quỳ bị trói và lính canh đang giữ súng, rồi trở về Nam; chọn lens có kiểm tra vật cản và cả hai nhân vật trong khung. Không dịch chuyển nhân vật hoặc bay camera xuyên rừng.
- Kết thúc hoặc bỏ qua intro đều bật đi lén, xóa đà chạy/sprint và hiển thị hướng dẫn phím C. Có 1 giây bảo vệ tiếp cận khi trả điều khiển. Cancel để load save phục hồi tư thế cũ, không áp dụng kết quả của cutscene.
- Snapshot rescue revision 5: save radio-only cũ được xem shot mới một lần khi khu giam giữ còn yên và đủ lính; save đang giao tranh/hộ tống không bị chèn intro. Retry từ checkpoint cũ nâng phiên bản khi chép cờ đã xem, tránh chiếu lại sau mỗi lần làm lại.
- Chỉ huy trên thuyền: hệ thống briefing nhả căn nền hầm sau khi trao actor cho extraction. Tư thế chân được căn vào trong mạn theo xương chân thực tế, giữ skeleton/clip và cao độ ghế.
- 32 cue Nam Map 1 thu lại theo ba đoạn Nam hiện có ở Map 2 (promise_02, promise_07, promise_09). Mẫu là giọng tổng hợp của chính project; không tải audio lên dịch vụ ngoài. Từ trùng nguyên văn dùng lại đúng file Map 2. Cập nhật cả reply trực tiếp trong prefab mở đầu.
- Giữ nguyên giọng Nam Map 2, Hùng A, ông Sáu A, chỉ huy B và 15 file địch Runway gốc; xác nhận bằng SHA-256. 11 câu địch chưa có Runway vẫn chỉ có phụ đề theo yêu cầu trước.

## Kiểm thử

12/12 ca liên quan đạt theo kết quả mới nhất từng ca trong các XML kèm theo. Có camera thật/ảnh Unity, natural/skip/held input, pause, cancel/load, save cũ/retry, preempt phát hiện, ghế/chân chỉ huy, toàn bộ boarding/departure, cutscene thất bại và kiểm tra clip/giọng. Bộ ghi hình cảnh execution được sửa để không đọc pose đã dispose khi director kết thúc giữa Update và callback LateUpdate.

Các ảnh captive-shot.png và commander-seat.png được chụp trong Unity. Đây là kiểm thử các phân đoạn liên quan, không phải tuyên bố toàn bộ game không còn lỗi. Bộ giọng mới vẫn cần người dùng nghe đánh giá sắc thái/phát âm từng câu.

Chi phí: 0; dùng VieNeu offline. Chưa commit/push ở bước này.
