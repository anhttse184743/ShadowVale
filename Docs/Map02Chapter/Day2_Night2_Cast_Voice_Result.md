# Nhân vật và thoại Ngày 2 / Đêm 2

Đối chiếu ngày 22/10/2026 với tài liệu `ShadowVale Map 2 – Kịch bản chi tiết 3 ngày đêm.docx`, chapter version 3 và model hiện có.

## Model đã gắn vào game

- Anh Thành → `Chỉ huy Thành.fbx`.
- Anh Lực → `Du kích Lực.fbx`.
- Tâm → `Anh Tâm.fbx`.
- Chú Bảy → `Ông 7 chèo thuyền.fbx`.
- Ông Sáu → `Ông 6.fbx`.
- Chị Lành → `Chị Lành.fbx`.
- Bé Tí → `Bé Tí.fbx`.

Bảy source được cấu hình Humanoid, có prefab riêng ở `Assets/_Project/Map01/Resources/Characters/Map02Roles`, dùng chung animation đứng/đi và được gán vào `Map02ChapterLayout.actors`. Bé Tí có tỷ lệ trẻ em; theo điều chỉnh mới ngày 22/10/2026, bé đứng cạnh chị Lành thay vì cạnh trạm. Save cũ cũng được đưa về vị trí này. Tâm xuất hiện từ tuyến Ngày 2. Phần hiệu chỉnh bám nền chỉ dịch visual, không đổi tọa độ lưu hoặc đường đi.

Hùng giữ model Map 1 nếu có đội hình chuyển map. Khi mở riêng Map 2, hệ thống tạo visual Hùng từ resource Map 1, không còn slot trống. Thành dùng model mang đúng tên ở làng; model chỉ huy cũ trên thuyền được ẩn sau chuyển giao để tránh xuất hiện hai chỉ huy.

## Tuổi và người nói do người dùng xác nhận

Tâm 26, Thành 38, Lực 30, chú Bảy 64, ông Sáu 72, chị Lành 25. Ưu tiên giọng Nam Bộ. Bé Tí khoảng 7 tuổi theo kịch bản; tuyến này không có câu thoại riêng của Bé Tí.

Người dùng xác nhận người trả lời pháo qua bộ đàm là **anh Thành**. Ba nhãn `Đơn vị pháo` trong chapter được đổi thành `Anh Thành`, giữ ID câu và trạng thái radio để thống nhất cùng nhân vật cả ở các đoạn sau.

## Lồng tiếng: đã chuẩn bị, chưa tạo bản thu

Có 32 câu thuộc bảy cuộc thoại của Ngày 2 / Đêm 2; năm câu là bộ đàm. Cả 32 ID chapter hiện chưa có bản thu tương ứng trong voice catalog. Ba bản thu `m2_` cũ không thay thế được các ID `m2v3_` đang chạy.

Giữ danh tính giọng đã dùng ở Map 1: Nam–Brodie, Hùng–Clint, Thành/chỉ huy–Tom. Bộ đàm của Hùng và Thành dùng cùng giọng nhân vật, thêm xử lý radio. Tâm cần chọn một giọng nam riêng, 26 tuổi, ưu tiên Nam Bộ; không dùng lại Brodie, Clint, Tom hoặc Martin. Các nhân vật mới còn lại không có thoại trong hai tuyến này; chưa tạo thêm lời thoại ngoài kịch bản.

`SourceArt/Dialogue/Map02DayNight2/cast.json` lưu tuổi, model, giọng cũ và các vai còn chờ chọn giọng. `voice-manifest.json` chứa đúng ID, phụ đề, giọng và kênh trực tiếp/bộ đàm. `prepare_manifest.py` trích từ chapter hiện tại. `build_audio.py` nhận bản thu PCM16 thật, kiểm tra tuổi/giọng trùng, tạo bản sạch và radio rồi đăng ký đúng caption vào voice catalog. Script không tự tạo tiếng nói, không lấy câu Map 1 thay câu Map 2 và không tự đánh dấu đã nghe duyệt.

**Chưa tạo hoặc gắn audio mới.** Runway chưa được cài/kết nối trong phiên làm việc này; cần kết nối dịch vụ tạo thoại trước khi chọn giọng thực tế, tạo bản thu và kiểm tra phát âm/âm sắc Nam Bộ.

## Kiểm chứng

- Unity: **7/7 đạt** tại `Logs/Map02Chapter/cast-results.xml` trong lượt kiểm tra cuối ngày 22/10/2026; kiểm tra prefab/avatar/material, model trong hai tuyến, cảnh D2_01, Hùng khi mở scene riêng, save/load và hồi quy trạng thái Ngày 2/Đêm 2.
- Ảnh từng nhân vật: `Logs/Map02Chapter/Cast`.
- Pipeline PCM 48 kHz và bộ lọc radio đã qua kiểm tra round trip bằng tín hiệu kiểm tra; không phải bản thu lời thoại hoặc kiểm tra nghe giọng.
