# Rà soát nghiêm khắc Map 2 — 21/10/2026

Project: G:\game\Capstone\ShadowVale. Phạm vi: chương ba ngày đêm, định hướng, thứ tự truyện và tương tác. Không thay địa hình hoặc model/rig Map 1.

## Đánh giá

Bản trước có thể đi hết bằng bộ chạy tự động nhưng chưa đủ rõ để chơi lần đầu. Nhiều bước chỉ đổi cờ, người chơi không biết vì sao phải tới đó; bản đồ và trạng thái nhân vật còn mâu thuẫn. Video chạy theo kịch bản có cắt di chuyển và mô phỏng giao tranh không chứng minh được trải nghiệm chơi thực tế. Không dùng video đó làm bằng chứng nghiệm thu cho đợt sửa này.

## Lỗi đã xử lý

| Mức | Vấn đề thực tế | Thay đổi |
|---|---|---|
| Nặng | Biểu tượng minimap lệch tâm khi xoay tại tỷ lệ HUD khác 1 | Sửa thứ tự nhân ma trận; kiểm tra 20 tổ hợp góc/tỷ lệ, gồm Full HD và 4K. |
| Nặng | Ghim nhiệm vụ tự đổi khi đi gần một việc khác | Theo dõi mục tiêu cố định, chọn qua J/M. Không cho đổi việc khi đang chở hàng/hộ tống/giữ tuyến. |
| Nặng | Người được cứu trở về nhà sau khi xong bước kế tiếp/load | Lưu vị trí actor; giữ ở bến nam và sửa snapshot cũ sai vị trí. Cứu gia đình có cả ông Sáu và chị Lành. |
| Nặng | Gửi thùng đầu ngày lấy hết đạn Nam đang có | Giữ đạn mang theo, ba thùng đóng góp hữu hạn 90 viên cho đêm cuối; nhận một lần. |
| Nặng | Hùng đứng suốt lúc yểm trợ rồi bị kéo thẳng qua cảnh vật | Đi đường NavMesh tới vị trí tiếp cận xe tăng trong phần yểm trợ; cảnh sau nối từ vị trí thật, kể cả load cũ. |
| Vừa | Nhận và đặt hàng dùng cùng lời nhắc, dễ tưởng nhấn E lần hai là xong | Đổi vị trí ghim và lời nhắc theo trạng thái, hiện số chuyến và chặn giao từ xa. |
| Vừa | Giữ tuyến không rõ còn địch hay đang chờ | Hiện số lính, yêu cầu trở lại vùng hoặc thời gian còn lại. Vùng 12 m cho phép tìm vật che. |
| Vừa | Hộ tống bỏ người lại mà không biết | Người cách quá 7 m dừng chờ; ghim xanh và HUD chỉ người tụt xa nhất. |
| Vừa | Mũi tên/đường dẫn không thống nhất với ảnh bản đồ | Chung phép chiếu bắc cố định cho minimap/bản đồ lớn; chấm đường lấy từ NavMesh và được cache. |
| Vừa | Quan sát đêm có thể xác nhận qua công trình | Kiểm tra vật che và hướng nhìn; hướng dẫn F, khu vực cần nhìn và tiến độ. |
| Vừa | Ngồi xuống là né đèn tự động | Đèn kiểm tra góc, đường ngắm và thời gian phản ứng; ngồi chỉ cho thêm thời gian. |
| Vừa | Hùng mời ăn trong báo cáo ở nơi khác; trẻ nói nhờ dẫn đi khi đã tới bến | Chuyển câu đúng trước/sau hành động; phân biệt lời tại chỗ và qua đàm. |
| Vừa | Tuần tra giao thuốc ban đêm chỉ là tới nói chuyện | Hùng đi cùng tới chốt, rồi về chỗ nghỉ/đổi ca. Tâm được dẫn về gặp người giao nhiệm vụ. |
| Vừa | Chuyển sáng/đêm và bố trí lại nhân vật quá đột ngột | Chuyển dưới màn tối ngắn, khóa di chuyển, khôi phục input; chỉ đổi pha khi hoàn thành việc. |

43 bước đều có mô tả mục đích. HUD chỉ đưa việc hiện tại và hành động cần làm, không tiết lộ cái chết của Hùng trước cao trào. Các hội thoại không còn để HUD nhiệm vụ chồng lên khung thoại.

## Những phần chưa được coi là hoàn thiện

- Model dân làng, tiếp xúc tay với đồ vật, cảnh ăn/thay băng/nhặt hàng cần nhóm đồ họa tích hợp. Marker hoặc prop tạm vẫn làm giảm cảm xúc dù logic đúng.
- Cảnh đặt bộc phá, rút lui/gục của Hùng và Nam tưởng niệm cần clip thật. Đã thêm chỗ gắn `hungPlantRetreatClip`, `namMournClip`, `coveredHungArt` trong Layout; để trống không tương đương animation đã được nghiệm thu.
- Cảnh bò chưa được chứng minh có animation và capsule sát đất đúng. Không gọi đây là cơ chế bò hoàn chỉnh chỉ dựa vào phím C.
- Thoại chương mới vẫn chờ duyệt/thu âm. Không tạo audio trả phí trong đợt này. 135 dòng trong Dialogue_Review.csv cần nghe duyệt trước phát hành.
- AI chiến đấu còn cơ bản; chưa có lượt chơi độc lập của người mới để chứng minh độ khó, nhịp nghỉ hay thời lượng nhiều ngày đủ hấp dẫn. Không cam kết số phút dựa trên thời gian thoại hoặc bộ test.
- Chi tiết súng ngắn không có sẵn trong bộ vũ khí nên chưa dựng thêm bằng một model bất kỳ.

## Kiểm chứng và giới hạn

Bộ hồi quy trước chỉnh lại dàn cảnh/thoại: 39/39 đạt, không bỏ qua. Kết quả bất biến: map2-clarity-regression-39-passed.xml trong thư mục bàn giao workspace.

Kiểm tra có di chuyển CharacterController thật trên đường hộ tống, chờ cả hai người tới bến; có gọi cơ chế bắn thật và đặt tường chắn để kiểm tra đường đạn. Việc đặt checkpoint bằng test chỉ dựng điều kiện ban đầu, không được tính là chơi tay trọn chương.

Sau chỉnh dàn cảnh/thoại: 15 trường hợp liên quan đạt (7 Clarity, 7 Chapter/Mechanics, 1 Runtime). Riêng Mechanics chạy lại và đạt sau khi thêm kiểm tra Hùng thực sự tiến về bộc phá sau load; đây là kiểm tra lặp của cùng trường hợp, không cộng thành một test mới. Không có failure/skip trong các lần chạy này. Sau đó chỉ chỉnh nền tương phản của panel và một câu hướng dẫn, không đổi luật gameplay.

Kết quả XML tại thư mục workspace `output`: `map2-final-clarity-7-passed.xml`, `map2-final-chapter-7-passed.xml`, `map2-final-cover-movement-passed.xml`, `map2-final-inventory-passed.xml`. Các screenshot chụp từ runtime Full HD để soát bản đồ/HUD; không thay thế kiểm thử cân bằng với người chơi mới. Đã giữ save kiểm thử ở thư mục riêng, không ghi đè save chơi của người dùng.

## Cách kiểm tra lại dành cho nhóm

1. Full HD: đứng yên xoay camera/nhân vật; mũi tên chỉ xoay tại chỗ. Đi ngang việc khác; ghim vàng giữ nguyên. Mở J/M chọn việc khả dụng.
2. Ngày 1: giữ nguyên đạn dự trữ đang có, chuyển đủ ba thùng; xác nhận mục đích, điểm nhận/đặt và đếm chuyến rõ.
3. Đêm 1–Ngày 2: nghe đúng cảnh bữa cơm; Hùng đi cùng tới chốt và về; Tâm về gặp anh Thành, không biến mất khi chuyển bước.
4. Đêm 2: F nhìn đúng khu vực; công trình phải cản; đèn pin cho thời gian phản ứng, cảnh báo đủ hiểu.
5. Đêm 3: cố ý đi nhanh bỏ lại gia đình, quay lại đón; cả hai tới bến mới xong. Làm bước tiếp, lưu/tải rồi kiểm tra họ vẫn ở bến.
6. Giữ tuyến: còn lính, hết lính, bước khỏi vùng rồi quay vào; không kẹt vì thiếu thông báo.
7. Yểm trợ: thấy Hùng thực sự di chuyển theo đường; lưu/tải không đưa về đầu. Cảnh cuối chạy hết và skip đều hoàn thành đúng một lần.
8. Nhóm đồ họa gắn clip/prefab rồi xem trực tiếp tay, chân, vật che và điểm tiếp xúc; sau đó mới duyệt cảnh cuối ở mức hình ảnh.
