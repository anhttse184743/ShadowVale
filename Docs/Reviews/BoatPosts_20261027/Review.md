# Thuyền cuối Map 1 / đầu Map 2 và chốt Đêm 1

## Thay đổi

- Nam đeo **chính khẩu súng hiện có** ở vai/lưng khi lên thuyền, ngồi chờ và cập bến. Không tạo bản sao bỏ lại ở tay; các khẩu khác đang không dùng được tắt có lưu trạng thái.
- Giữ bắn yểm trợ trong cảnh rút lui khi có mục tiêu thật. Khi không còn mục tiêu, Nam trở lại tư thế ngồi nghỉ, súng đeo vai. Đầu Map 2 không còn ép hai tay giữ súng.
- Đổi model trong cảnh Nam đỡ Hùng cũng tạm ẩn dây súng; khi trả điều khiển, trả súng về socket gameplay.
- Đêm 1: bảng lớn có sơ đồ riêng cho hướng sáng, cánh cửa/thanh chốt và lối đi/chỗ cất bó củi. Điều khiển chuột và phím đều có nhãn. Phản hồi sai ở ngay bảng, không tự đóng khi chưa đạt.
- Cửa rào chỉ khép khi bấm Khép cửa, không bật qua lại làm mất tiến độ. Cài chốt khi cửa còn hé sẽ được giải thích; xác nhận thành công mới trả điều khiển.
- Có trạng thái đã kiểm tra lưu cùng checkpoint, không đổi mẫu lỗi hoặc tự mở khóa nhiệm vụ sau. Thông báo và HUD nhắc quay lại đúng người gác để báo cáo.

## Cách quay demo

Khung hình GameView thật từ Unity DX12, 12 fps, có nhãn từng đoạn và thao tác input. Chia hai phần: đoạn thuyền và ba thao tác chốt. Các đoạn chờ dài được lược bỏ bằng cắt, không tăng tốc hoạt ảnh ở phần xuất. Demo hình ảnh không thu tiếng. Fixture đưa game tới đúng mốc để kiểm tra, không phải video chơi toàn bộ game từ đầu.

## Tham khảo UI

Áp dụng tiếp các nguồn đã dùng ở bản trước: [Xbox UI context](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/114), [UI navigation](https://learn.microsoft.com/en-us/xbox/accessibility/xbox-accessibility-guidelines/112), [Game Accessibility Guidelines](https://gameaccessibilityguidelines.com/full-list/). Luật câu đố giữ nguyên; thay cách giải thích, kích thước mục tiêu bấm và sơ đồ phản hồi.

## Kết quả

**25/25 ca đạt, Unity 6000.3.24f1, DX12**, gồm:

- Hai ca quay demo: thuyền và input chuột/bàn phím trên cả ba bảng chốt; xác nhận sai giữ bảng mở, cài chốt sai thứ tự không thành công, chọn chỗ cất chắn cửa bị từ chối, load giữ tiến độ kiểm tra.
- Ca bắn yểm trợ: Nam bắn 6 phát, hạ 2 lính; chỉ huy không bắn; không trừ đạn gameplay hoặc sinh loot từ cảnh phim.
- Hai ca thuyền: di chuyển liên tục qua Map 2 và khôi phục checkpoint giữ hông Hùng trên thuyền.
- Ca chuyển checkpoint hoàn tất sang Map 2; 19 ca luật/save của Ngày 1–Đêm 1 (mẫu kho, thứ tự việc, chốt, thuốc không cấp/trừ lặp).

[Video demo 1 phút 16 giây](ShadowVale_Thuyen_va_ChotDem1_Demo.mp4) · [Kết quả XML](DemoTests.xml).

Ảnh đối chiếu: [lên thuyền](Boat-boarding.png), [cập bến](Boat-arrival.png), [đèn](Post-lamp.png), [cửa rào](Post-gate.png), [lối đi](Post-path.png).

Giới hạn: ca chốt dùng fixture ở đúng mốc nhiệm vụ để quay thao tác; không phải chứng nhận toàn bộ game không còn lỗi hoặc video chạy trọn chiến dịch.
