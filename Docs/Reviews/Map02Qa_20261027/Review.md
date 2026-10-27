# Map 2 — QA Ngày 1 / Đêm 1 (27-10-2026)

Phạm vi: từ cập bến, 6 nhiệm vụ Ngày 1, 4 nhiệm vụ Đêm 1 đến bàn giao Ngày 2. Không sửa kịch bản/giọng đã duyệt hoặc mini game Map 1/ngày sau.

## Các lỗi đã sửa

- **Thay băng:** vị trí Lành trong tầm với; Hùng dùng tay trái đỡ tay phải. Hai bàn tay Lành theo đường quấn ở bắp tay phải, có giới hạn bẻ cổ tay; cuộn băng nhỏ theo lòng bàn tay. Dải băng là các vòng chồng, hiện dần trên vùng tay áo bị thương, thay cho ống băng ở cẳng tay. Sau cảnh/tải save băng tiếp tục theo đúng cánh tay; cuộn băng rời được ẩn. Camera cho thấy hai bàn tay.
- **Kho:** đầu chuột khớp góc trên-trái hình xem trước; chọn hòm đã cất để nhấc rõ ràng. Đặt chồng báo lỗi, không tự nhấc hòm khác. Có nút xoay/xếp lại; giữ nguyên mẫu nền ẩm, không thưởng lại tiếp tế. Chuột và phím đều dùng được.
- **Bến:** chia ba bước cọc → ván → chú Bảy kiểm tra. Thêm thẻ ván, chọn bậc, nút xoay, thanh trượt và nút căn hai phía. Ván đã dùng/bậc đã hoàn thành không bị thao tác khác ghi đè. Mỗi nhát cần nhả Space, chờ búa hồi; ghi tiến độ tại lúc búa chạm cọc, giữ đúng cọc tới hết động tác thứ ba; chỉ đổi sang kê ván sau nhát thứ sáu. Pause dừng nhát búa; yêu cầu đóng trong lúc vung búa được xử lý khi động tác kết thúc.
- **Trả điều khiển:** chặn E/Space/chuột giữ từ lúc mở bảng; thoát bảng không bắn/vung tay vì còn nút giữ. Trả vũ khí, camera và điều khiển. Lưu từng phần đã xong.
- **Đêm 1:** hướng dẫn lối đi cập nhật đúng khi bó củi đã cất; không còn mô tả đường vẫn bị chắn. Giữ ba hạng mục chốt và báo lại người gác.

## Thao tác

Kho: bấm thẻ chọn hòm, đưa góc trên-trái vào ô, bấm đặt; R hoặc nút Xoay; Xếp lại chỉ dọn cách bố trí. Đủ ba hòm hợp lệ mới bấm Cất xong. Có lựa chọn phím 1–3, mũi tên, E, R, Enter.

Bến: nhấn/nhả Space theo vùng ✓, mỗi cọc ba nhát; sau đó bấm chọn ván, chọn bậc trống, dùng thanh trượt/các nút để căn hai đầu trên giá đỡ, xoay nếu cần, Kê ván. Đủ ba ván thì nhờ chú Bảy kiểm tra.

Chốt: tiếp cận đèn/cửa/lối đi để thấy E. Sửa xong quay lại người gác; kiểm tra vật không tự thay việc báo cáo. Sự kiện Hùng giật mình vẫn là cảnh truyện, không có hình phạt.

## Cách kiểm tra

- Hình trước/sau và số đo tiếp xúc tại 0/3/6/8/11/14/17/20/23 giây; bàn tay trong vùng thao tác cách mục tiêu dưới 4,5 cm. Băng bám trục bắp tay phải; thử tái tạo băng sau fresh load, không chỉ cùng phiên cutscene.
- Sự kiện chuột thật qua EditorGUIUtility.QueueGameViewInputEvent, điều khiển chính OnGUI ở 1200×675, 1600×900 và 1920×1080. Không thay Event.current trong Repaint (cách đó không cập nhật cache vị trí chuột của IMGUI).
- Gõ/giữ/spam Space, pause giữa nhát, Esc giữa nhát; chọn ván nứt/ngắn, kê hụt, xoay sai, không ghi đè bậc đã làm. Xếp chồng, reset kho, mẫu không đổi.
- Bộ giải mọi mẫu kho; sáu thứ tự việc D1_03–05; save/load/idempotency thuốc/tiếp tế; tích hợp chốt đêm và kiểm tra hoạt động D2_06 vẫn dùng xử lý riêng.
- Lượt flow riêng bắt đầu từ cập bến tự nhiên, chỉ dùng di chuyển và phím tương tác; không teleport/Complete/EndScene/Restore-prefix. Lượt này tăng timeScale lên 2 và bấm tiếp thoại; không dùng để suy ra thời lượng chơi thật.

Kết quả: **30 ca khác nhau đạt**, 36 lượt thực thi qua ba đợt (29 + 5 + 2), không có ca hỏng trong các đợt xác nhận cuối. Sau chỉnh camera và cỡ chữ nút, đã chạy lại thao tác chuột và phép chiếu của vùng làm việc. Xem Results.txt và các XML kèm theo.

## Giới hạn

Đây là kiểm thử tự động trên Editor/DX11 và xem hình runtime, chưa phải buổi usability test với người chơi mới hay kiểm thử mọi cấu hình máy. Không khẳng định toàn game tuyệt đối không còn lỗi. Giữ FBX và skeleton hiện có; sửa tiếp xúc được áp dụng trong runtime trên clip sẵn có, không có clip Blender/FBX thay thế được xuất ở đợt này.

Tài liệu API dùng cho bộ kiểm thử: https://docs.unity.com/en-us/engine/7000.0/script-reference/unityeditor/editorguiutility/queuegameviewinputevent


## Bằng chứng hình ảnh

- `BeforeBandage/`: trước khi sửa.
- `Bandage/`: ảnh và số đo sau sửa.
- `DayOneFlow/`: cập bến, hoàn tất thay băng, kho, bến, chuyển đêm và bàn giao Ngày 2.
- `dock-overhead-ui.png`: góc nhìn kê ván và UI cuối đã xem lại.
- `Bandage-review.mp4`: 24 giây phát lại timeline cảnh thay băng bằng chính skeleton, logic pose/IK và camera runtime; lấy mẫu 12 lần/giây, xuất 24 fps. Video kiểm tra chuyển động không có tiếng, không phải bản quay toàn lượt chơi.
