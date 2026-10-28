# Map 1 — Sửa hội thoại và độ trễ tương tác, 16/10/2026

## Nội dung đã sửa

- Hội thoại dùng khung tham chiếu 1600×900; cỡ chữ nội dung 28, tên 26, hướng dẫn 20, tự tăng theo độ phân giải. Panel theo nội dung, kiểm tra cả 720p và 4K thu nhỏ 26%.
- Đối thoại đứng yên không tải bộ dữ liệu tiếp xúc da 33 MB. Giữ độ cao chân đã đứng trước hội thoại. Animation, Avatar, dữ liệu tiếp xúc, mesh bàn tay và thoại quan trọng được chuẩn bị rải qua nhiều frame từ đầu Map 1. Kéo vào bụi chỉ bắt đầu khi tài nguyên đã sẵn sàng, tránh tải đồng bộ trên lần bấm đầu.
- Khi mang tiếp tế về, Nam nói ngay với chỉ huy; sau hai câu xác nhận mới đặt túi rồi nhận lệnh điểm 2. Hủy trước lúc bàn giao không tiêu thụ hàng hoặc trao thưởng.
- Cảnh báo động cứu Hùng giữ một câu hô hiện có, bỏ câu đáp nối tiếp và bỏ phần thời gian chờ lia camera không còn dùng. Camera vẫn ở phía Nam. Không hiện phụ đề lời lính; giữ tiếng nói, mục tiêu và hướng dẫn tương tác.
- Bỏ câu thì thào cũ của Hùng về bốn lính. Không phát thoại nghe lén khi cả bốn đã chết. Lời Nam/Hùng trong cuộc nói chuyện với chỉ huy vẫn có chữ.
- Súng giám sát hiệu chỉnh hướng qua cánh tay và điểm giữ súng, tính lại sau mỗi bước vì nòng cũng dịch chuyển theo tay. Dây trói giữ xuyên cảnh chết.
- Điểm 3 nhận quan sát khu doanh trại trong 18–120 m, góc 35°, đứng yên và giữ ống nhòm tối đa 1,2 giây. Không yêu cầu chĩa đúng người sĩ quan. Marker và lời nhắc chỉ vào doanh trại.

Không sản xuất audio trả phí trong lần sửa này. Không thay địa hình hoặc rig, không sửa director/scene Map 2.

## Kiểm thử cuối

`Logs/InteractionReview/final-results-2.xml`: **19/19 đạt**, Unity Editor 6000.3.24f1, thoát mã 0.

Gồm 11 trường hợp điều kiện quan sát/lọc phụ đề; kiểm tra hội thoại có audio và chân đứng ổn định; kéo bụi thành công/thất bại, trả controller; xử bắn một câu, hướng nòng và dây trói; toàn luồng lấy chìa–mở hòm–giữ aim–nhận hàng–bàn giao; quan sát doanh trại–báo cáo–trả chiến đấu; ba bài tiến trình truyện/camera/save cũ.

Đo trên máy kiểm thử sau khi hoàn tất chuẩn bị tài nguyên:

| Thao tác | Thời gian khởi đầu đo được |
|---|---:|
| Hội thoại chỉ huy | 14,54 ms |
| Kéo vào bụi, nhánh thành công | 47,37 ms |
| Kéo vào bụi, nhánh thoát | 26,89 ms |

Chân tại hội thoại: độ lệch độ cao cổ chân thấp nhất 0 m. Nòng súng lúc ngắm Hùng: sai lệch hướng nhỏ nhất 0,79°. Đây là phép đo riêng cho thao tác/fixture, không phải số FPS hoặc bảo đảm mọi lần chơi đều có độ trễ bằng nhau.

## Ảnh và giới hạn

- `Logs/InteractionReview/dialogue-4k.png`, `dialogue-720p.png`: ảnh thật trong game, đã xem trực tiếp chữ, panel và bố cục.
- `dialogue-4k-at-26pct.png`: bản thu nhỏ ảnh 4K để đối chiếu Game View của người dùng.
- `execution-no-subtitle.png`: giữ camera phía Nam, không có chữ lời địch; Hùng ngoài khung hình đúng yêu cầu. Hướng ngắm kiểm tra từ transform nòng đến ngực Hùng.
- `bush-pull.png`, `bush-timing.png`: ảnh chẩn đoán; ảnh đầu chuyển camera còn bị lá che, ảnh timing bị chụp trễ sang trạng thái thoát do fixture chuyển nhịp trong cùng frame. Không dùng hai ảnh này làm chứng nhận mọi frame hoạt ảnh hay toàn bộ mini game.

Lượt đầu của bộ mới có 16/19 đạt: hướng ngắm cần sửa, hai bài khác lỗi giả lập nhịp timing/chuột vật lý. Đã sửa hướng ngắm và tách chuột ảo của test khỏi chuột người dùng, sửa thao tác bấm đúng nhịp; chạy lại đạt 19/19. Lượt quay chẩn đoán dài trước đó có 3/5 đạt; hai bài quay nhiều frame vượt thời gian và dùng ngưỡng thời lượng cảnh cũ. Không tính lượt này là nghiệm thu hoàn tất.

Chưa đo lại FPS toàn game hoặc thời lượng lần chơi đầu. Kết quả 38/38 và video 8/8 trong tài liệu ThreeOperations là bằng chứng của đợt trước; bộ 19 bài trên xác nhận các thay đổi lần này.