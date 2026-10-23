# Map 2 — sửa chuyển động, vật dụng, gia đình và chốt gác

Project: G:\game\Capstone\ShadowVale, nhánh Khoa.

## Các thay đổi

| Vấn đề người chơi gặp | Xử lý |
|---|---|
| Hùng đi sai dáng, sai hướng | Dùng trực tiếp Hung_DiKhapKhieng của Map 1, căn chu kỳ theo tốc độ thực, căn chân trên mặt đất và giữ góc xoay của model đúng với gốc điều hướng. |
| Cập bến tiếp tục đè animation NPC | Map02Arrival trả hẳn quyền pose của Hùng và anh Thành cho chapter khi vào gameplay. |
| Nam đưa tay dìu khi Hùng còn ở sau | Chỉ bật tư thế hỗ trợ khi Hùng đã tới sát bên; đường hẹp vẫn đi một hàng. |
| Anh Thành cầm súng vô hình | Gắn lại đạo cụ súng thật vào tay, dùng cơ chế Map01Rifle. |
| Súng xuyên thùng khi Nam khiêng | Giữ trạng thái vũ khí/đạn, giấu hình súng trong tay và tạo bản hiển thị không có logic bắn đeo sau vai. Khôi phục hiển thị khi kết thúc/hủy. |
| Khiêng thùng bị lệch hướng | Chuẩn hóa trục thân của riêng clip CarryWalk/AssistWalk Map 2. Thùng có tay nắm và khoảng hở với thân người. |
| Ông Sáu đứng, bé Tí xếp hàng | Ông Sáu có ghế và tư thế ngồi; bé Tí đứng lệch về phía hiên, có khoảng cách riêng với người lớn. Load save cũng áp lại bố cục. |
| Chỉ hai người ăn cơm | Năm người cùng mâm: Nam, Hùng, chị Lành, ông Sáu và bé Tí. Mỗi người có chỗ ngồi/bát; góc máy được kiểm tra nhiều hướng để tránh tre che. |
| Lời thoại không khớp cảnh ăn chung | Sửa lời mời của Hùng và lời chị Lành tại mâm. Giữ trọng tâm lời hứa, u của Hùng và hoàn cảnh Nam. Audio mới vẫn chờ thu, không phát bản thu cũ lệch chữ. |
| Túi đồ khác thiết kế Map 1 | Dùng lại panel, atlas icon, cột trang bị, lưới 6×4 và cột chi tiết. Hai gói thuốc nhiệm vụ hiển thị riêng, lấy số lượng từ trạng thái nhiệm vụ, không cấp thêm vào túi hoặc dùng hồi máu. |
| Không rõ đang kiểm tra gì ở chốt | Một nhắc E cho vật gần nhất; ưu tiên báo cáo khi đứng sát anh Lực. Sơ đồ lối đi chỉ rõ cửa, vùng mở cửa, bó vật liệu và giá cất. Hạng mục đã đúng có phản hồi tương ứng. |

## Kiểm chứng

- tests2.xml: 27 ca logic/mini game/focus đạt; một ca kiểm tra hình còn lỗi truy vấn cây đối tượng của chính bài test (súng đeo thuộc Nam, không thuộc director), đã sửa trong bài test.
- visual2.xml: 2/2 đạt, có ảnh Full HD cho túi đồ, khiêng thùng, mâm cơm và hai màn kiểm tra chốt.
- final.xml: 12/17 đạt; năm đường vào Map 2 cùng phát hiện chân Hùng lệch khoảng 13 cm sau khi trả quyền animation. Đã sửa căn chân và thêm prime pose ngay lúc bàn giao.
- grounding.xml: 3/3 đạt sau sửa, gồm vào Map 2 bằng skip, pose/ảnh thực tế và clip đi Map 1.
- verified.xml: đợt kiểm tra cuối gồm rời bến tự nhiên từ Map 1, skip cập bến, phần trình diễn, mini game, sáu thứ tự công việc và giữ ghim nhiệm vụ. Xem XML để biết kết quả cuối.

## Cách xem bản sửa

Dùng project Capstone. Bản lưu Ô 01 của người chơi được giữ nguyên; tải lại để các đoạn trình diễn và giao diện dùng code mới. Các bản lưu tự động của bài test được tách khỏi ô lưu người chơi.

Ảnh và XML: Logs/Map02Presentation_20261022.
Script hậu xử lý clip tái tạo được: Assets/_Project/Map01/Editor/Map02PresentationRepair.cs.
Không thay skeleton Nam/Hùng, không sửa mini game Map 1, không tạo audio trả phí.

## Kết quả cuối
- verified.xml: 29/30 đạt; ca FullNatural vượt thời hạn mặc định 180 giây của toàn bài (bao gồm tải scene/Play Mode), trong khi log đã ghi cập bến hoàn tất.
- ready.xml: 3/3 đạt sau khi đặt thời hạn toàn bài FullNatural là 300 giây, cùng mức với FullGameView; giới hạn 180 giây bên trong trình tự gameplay và mọi assert được giữ nguyên. Có đủ 30 ca khác nhau đạt qua hai đợt cuối.
- Đã xem ảnh Full HD và bốn hướng máy ăn cơm; chọn hướng A nhìn đủ năm người. Hạ chụp đèn dưới tầm mặt và dời nguồn sáng khỏi sát mặt ông Sáu.
- git diff --check sạch. Không thay bản lưu cá nhân trong đợt này.

