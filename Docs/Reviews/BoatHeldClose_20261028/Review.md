# Nam cầm súng rời Map 1, đeo sát ba lô ở Map 2

## Hành vi theo yêu cầu mới
- Cuối Map 1: Nam cầm đúng một khẩu khi chạy tới bậc thuyền, bước lên thuyền, ngồi xuống, giữ súng sẵn sàng và bắn yểm trợ. Không đeo thêm khẩu trên lưng.
- Bắt đầu bàn giao Map 2: chính khẩu đó chuyển sang ngực/đeo sau vai, hai tay được giải phóng. Không tạo model súng thứ hai.
- Súng đeo được dịch gần thân hơn 12 cm theo hướng trước, nâng khoảng 8,5 cm và giảm nghiêng ngang. Có kiểm tra ảnh đi trên bờ và lúc cúi giúp Hùng; không còn khoảng hở lớn như ảnh người chơi gửi.
- Khi cầm: tay trái bám điểm đỡ nòng; lúc ngồi dùng tư thế giữ súng thấp. Khi đeo ở Map 2 giữ lớp hoạt ảnh tay trống và động tác hỗ trợ C1 đã có.

## Kiểm thử
**Unity 6000.3.24f1: hai ca AK/Mosin đạt ở lượt chạy cuối; bắn yểm trợ và bỏ qua C1 đạt ở lượt trước.** Bài kiểm tra rà độc lập mesh súng, socket tay/ngực, điểm tựa tay trái và trạng thái cầm/đeo trong toàn chuỗi. Bao gồm cả AK/Mosin, bắn yểm trợ và bỏ qua cảnh đỡ Hùng.

Không đổi thoại, âm thanh hoặc tuyến nhiệm vụ. Chưa push.


AK: samples=4004, replacementBodySamples=619
Mosin: samples=3517, replacementBodySamples=650

Ảnh native đã xem: Nam cầm một súng lúc bước xuống thuyền; súng đeo sát mép ba lô khi đi trên bờ và cúi đỡ Hùng. Đây là kiểm thử phạm vi cảnh thuyền, không phải toàn bộ game.

Lượt đầu có hai bài test toàn tuyến đọc chủ sở hữu trước Update rồi so với trạng thái sau LateUpdate, nên báo sai tại đúng khung bàn giao. Đã sửa bài test lấy chủ sở hữu hiện hành trong LateUpdate; hai ca chạy lại đạt. Bản runtime không thay đổi giữa hai lượt.
