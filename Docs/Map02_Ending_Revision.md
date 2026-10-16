# Kết cục Map 2 — cập nhật theo xác nhận của người dùng

## Nội dung đã chốt

- Giữ cảnh cập bến hiện tại. Rút cuối chương qua bến nam.
- Hùng hy sinh tại bến nam, ở lại yểm trợ cho chuyến thuyền cuối rút đi.
- Thay kết cục cũ Hùng chèo thuyền và cùng Nam tới nơi tiếp nhận. Không phát các câu của Hùng trên thuyền sau thời điểm hy sinh.
- Giữ các nhiệm vụ giúp dân, cứu trinh sát, chuẩn bị sơ tán và đưa hai nhóm dân tới bến. Không biến cái chết theo kịch bản thành màn thua hộ tống.

## Dàn cảnh đề xuất, chưa phải animation đã triển khai

1. Các chuyến dân và lực lượng đã rút; Nam và chỉ huy chuẩn bị lên chuyến cuối ở bến nam. Không cho dân biến mất vì timer.
2. Hùng ở vị trí có vật che tại bến, yểm trợ đúng mục tiêu có đường ngắm. Camera thể hiện rõ vị trí của anh và hướng thuyền rút, tránh nhầm rằng cả nhóm bỏ anh trong im lặng.
3. Nhịp Hùng trúng đạn và hy sinh phải có animation riêng, bàn chân tiếp đất, tay giữ súng rồi buông hợp lý. Chưa chốt số phát, thời lượng hoặc lời trăn trối; không tự thêm vào lore.
4. Ghi nhận cái chết đúng một lần. Nam và chỉ huy rút theo chuyến cuối; người điều khiển thuyền thay Hùng cần được chốt trong dàn cảnh/asset.
5. Đoạn tiếp nhận xác nhận dân và người còn lại an toàn; phản ứng trước mất mát của Hùng thay cho hội thoại cũ. Câu chữ mới cần duyệt trước thu.

Skip phải đưa tới cùng trạng thái cuối: Hùng đã hy sinh, không xuất hiện lại trên thuyền/nơi tiếp nhận, dân đã tới nơi, nhiệm vụ hoàn thành một lần. Nếu Hùng chết trong gameplay trước nhịp hi sinh được dựng thì vẫn là thất bại bình thường, không tự bỏ qua những nhiệm vụ chưa xong.

## Source đã điều chỉnh

Map02Progress phiên bản 2 có hungSacrificeConfirmed và API ConfirmHungSacrifice. Chỉ nhận sự kiện trong bước Breakout sau khi mở tuyến và đoàn đã sơ tán; lên chuyến cuối cần sự kiện này. Map02ConversationSession chặn m2_departure cũ. Dữ liệu 33 câu nguồn được giữ để đối chiếu, không sửa tài liệu gốc hoặc gán audio cũ cho lời mới.

Chưa có actor/cutscene/animation chết cuối được gắn vào scene. Cờ logic không thay cho hình ảnh và âm thanh cần thực hiện.
