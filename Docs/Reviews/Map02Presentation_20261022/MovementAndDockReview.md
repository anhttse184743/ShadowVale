# Map 2 — Sửa di chuyển và hoạt động sửa bến (22/10/2026)

Project: G:\game\Capstone\ShadowVale, nhánh Khoa.

## Thay đổi

- Nam không dùng Assist/AssistWalk khi đi với Hùng. Hùng tự theo sau; Nam dùng locomotion thường và đeo súng trong các đoạn hộ tống, kể cả tuần đêm.
- Bỏ SetHandsOccupied lặp trong LateUpdate, tránh CrossFade khởi động lại mỗi khung hình. Giữ chuyển trạng thái tại điểm bắt đầu/kết thúc công việc.
- Map02UpperBodyPose dùng lớp tay giữ thùng trên clip đi thường. Mask không tác động Root và hai chân. Không sửa skeleton hoặc xoay cưỡng ép khớp trong runtime.
- Thùng đang mang chỉ có một chủ sở hữu vị trí/hiển thị tại LateUpdate; cập nhật props không kéo nó về bến hoặc ẩn giữa khung hình.
- Anh Thành đeo súng và dùng idle/walk thường trong Ngày 1–Đêm 1. Từ Ngày 2 trả súng về tay và trả locomotion gốc.
- Chị Lành tiếp cận bên phải Hùng khi thay băng; rời về vị trí riêng sau cảnh. Hùng rời vị trí ghế trước khi trở lại đứng.
- Nam và Hùng đi tới bàn ăn bằng clip đi, xoay theo đường NavMesh. Người tới trước ngồi chờ. Chỉ mở câu thoại đầu khi cả hai tới nơi; cả nhà ngồi chung.

## Mini game sửa bến

1. Hai cọc, mỗi cọc ba nhịp Space. Giữ phím không tự hoàn thành. Trượt không mất nhịp đã đạt.
2. Chọn trong năm ván; hiển thị độ dài và vết nứt. Chọn bậc 1–3, A/D dịch ván, R xoay; sơ đồ cho thấy hai thanh đỡ và ván đang căn. E/nút xác nhận mới kê ván. Không suy ra độ lệch từ vị trí bấm nút nữa.
3. Sau ba ván hợp lệ, E kiểm tra bến và chạy lời xác nhận. Vẫn tính hai cọc + ba ván, giữ ID D1_05.

Sai trả lý do cụ thể. Đóng hoặc load giữ các đơn vị đã hoàn tất và cùng thứ tự vật liệu. Phần căn chưa xác nhận không được tính hoàn thành.

Tham khảo thiết kế: [Xbox XAG 109 — Objective clarity](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/109) về mục tiêu, tiến độ và bước tiếp theo; [Xbox XAG 107 — Input](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/107) về nhu cầu thao tác rõ và các lựa chọn input. Đây là thiết kế sửa bến riêng, không sao chép một mini game cụ thể.

## Kiểm chứng

- release-check.xml: 23/23 đạt (18 kiểm thử dữ liệu/luồng, 4 kiểm thử trình diễn, 1 tích hợp).
- Có test đi bộ với input W liên tục và kiểm tra normalizedTime tăng, controller thường còn hoạt động, không xuất hiện full-body Assist.
- Test mask so sánh vị trí hai bàn chân với clip Walk_Tuned (sai số <3 cm), đồng thời kiểm tra tay thực sự thay đổi vị trí.
- Test cảnh ăn chờ tiếp cận trước thoại, kiểm tra vị trí ghế; ảnh live-meal-start.png chụp ngay câu đầu.
- Test chọn ván nứt, kê lệch, kê đúng, load sau một ván; nhiệm vụ chưa hoàn tất sớm. Test tích hợp kiểm tra Ngày 2 giữ hoạt động riêng.
- git diff --check đạt. Ô lưu 1 người dùng giữ nguyên SHA-256 515A2E7126AD278A836A1791DCABE2C6C8395FED44AE23FEF1DEFA51F3E07697.

Ảnh và XML ở Logs/Map02Presentation_20261022: cargo-front.png, live-meal-start.png, dock-redesign.png, release-check.xml. Các cảnh được kiểm tra trong Unity ở Full HD; chưa có phiên thử với người chơi mới hoặc đo lại toàn bộ thời lượng Ngày 1–Đêm 1.

- bandage-check.xml: 1/1 đạt sau khi bổ sung kiểm tra vị trí chị Lành ở bên cạnh, Hùng cách ghế hơn 0,7 m sau cảnh và súng anh Thành gắn ở ngực khi sinh hoạt. Không sửa code sau lượt release-check; lượt sau bổ sung assertion cho cùng ca live.
