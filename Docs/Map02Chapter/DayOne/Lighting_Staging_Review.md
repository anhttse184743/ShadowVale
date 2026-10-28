# Map 2 — Ngày 1 / Đêm 1: ánh sáng và bố trí nhân vật

## Các lỗi đã xác định và sửa

- Model Hùng mang camera Blender và một đèn Point cường độ 1000. Camera render đè góc máy nhiệm vụ; đèn làm cháy sáng da, mũ và áo. Bản FBX dẫn xuất không nhập camera/đèn; actor socket cũng vô hiệu camera/listener nhập kèm, và đèn preview cường độ cao khi bind model.
- Ánh sáng Ngày 1 dùng ambient ba hướng và ánh nắng dịu. Đêm 1 dùng ánh trăng lạnh, đèn dầu ấm, hạ phản xạ gắt trên nhân vật. Material là bản riêng, được khôi phục khi ra khỏi phạm vi Đêm 1.
- Bổ sung đèn dầu có hình thể ở hiên, đặt lệch khỏi khung giữa hai nhân vật; sáng ấm từ đúng vị trí đèn. Căn chân chị Lành bằng tiếp xúc mesh với nền trong cảnh thay băng.
- Thêm hai ghế đẩu ở vị trí ngồi bữa cơm; bố trí Bé Tí xa khỏi cặp thay băng để không che tiếp xúc.
- Camera cảnh thay băng và bữa cơm dùng vị trí riêng quanh hiên. Nhân vật đi theo đường NavMesh tới chỗ ngồi; thoại bữa cơm bắt đầu sau khi tới nơi.

## Phạm vi kiểm chứng

Bộ kiểm thử nhiệm vụ/mini game đạt 36/36 trong `lighting-staging-regression.xml`. Kiểm tra ảnh sau sửa căn nền đạt; lượt chuyển map phát hiện test cũ đang kiểm tra súng ngay khi Nam còn ở cảnh báo cáo nên được sửa để kiểm tra sau khi trả điều khiển. Lượt cuối `grounding-lamp-final.xml` đạt 2/2: ảnh/căn nền và chuyển Map 1 → Map 2 qua nhánh skip. Chưa thay thế kiểm tra chuyển cảnh đầy đủ không skip.

Ảnh Game View Full HD và kết quả Unity Test Runner nằm tại `Logs/Map02DayOne/`. Test ảnh kiểm tra camera trong model không còn bật và chỉ có một camera cảnh xuất hình. Test gameplay kiểm tra mini game, lưu/tải, checkpoint và các mốc nhiệm vụ liên quan.

Ảnh chụp và kiểm thử tự động không thay thế chơi thử với người mới. Chưa coi đây là nghiệm thu cuối toàn bộ animation: còn phải duyệt cử chỉ ăn cơm, tay chạm băng/đồ vật và chuyển động liên tục trên địa hình. Chưa có video chơi thật toàn bộ Ngày 1–Đêm 1 để bàn giao trong đợt kiểm tra này.

## Thoại

48 cue đã lập bảng. 5 cue “Rõ.” dùng lại chính bản Nam đã duyệt. Các thoại mới còn chờ giọng mẫu và thu âm; chưa chi credits Runway trong đợt này. Trần 8 credits chỉ dành cho 4 mẫu giọng mới, không phải ngân sách thu cả chương.
