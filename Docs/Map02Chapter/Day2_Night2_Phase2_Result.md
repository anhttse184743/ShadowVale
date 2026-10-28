# Đợt 2 — Tuyến Ngày 2

Triển khai ngày 22/10/2026, theo mục Đợt 2 trong `Day2_Night2_Apply_Plan.md`.

## Đã đưa vào game

- Hai đường từ chốt tới cầu: lối đồng phía bắc và lối bờ tre phía nam. Các điểm dẫn đường nằm trong ChapterLayout; phần qua cầu lấy vị trí hai đầu và tim sàn cầu thật, tránh đi chéo qua lan can. Bổ sung cọc chỉ lối và bụi tre có va chạm/cản tầm nhìn ở bên đường.
- Dấu chân xen vết máu từ cầu tới vị trí Tâm. Giữ model và tư thế Tâm bị thương đã có; khi nhận hộ tống, Tâm đứng dậy và theo Nam.
- Tổ tuần tra hữu hạn ba lính được gọi tạo trong cả đoạn tìm và đưa Tâm về, tránh trường hợp nhiệm vụ tới cầu tự hoàn tất trước khi tạo địch. Nhóm đã tạo không sinh lại; snapshot giữ cả lính còn sống và xác địch. Không có điều kiện bắt buộc hạ địch để cứu Tâm.
- Theo điều chỉnh sau khi chơi thử: đưa Tâm về điểm báo cáo gốc của Thành; Thành giữ nguyên vị trí. Sau báo cáo, Tâm nghỉ riêng ở khu nghỉ, tách khỏi Lực ở chốt. Save/load đang hộ tống tiếp tục Follow; save cũ đã hoàn thành được sắp xếp lại về bố trí này.
- Cuộn dây có vật thể trong kho và trên người Nam khi mang; chỉ giao tại trạm mới hoàn tất. Khôi phục save đang mang giữ đúng dây và marker giao.
- Cây dừa cạnh trạm dùng mesh dừa sẵn trong map, có điểm bám và cột ăng-ten. Nhấn E bắt đầu; giữ W leo, thả W dừng, giữ S xuống. Trên cao dùng A/D chỉnh hướng, giữ ổn định hai giây rồi giữ Space 1,5 giây cột dây. Xuống đất mới chạy cảnh nối máy. Esc xuống chân cây và hủy lượt thao tác, không hoàn thành nhiệm vụ.
- Tách logic đường, dấu vết, đạo cụ và leo cây vào `Map02ChapterDayTwo.cs`. Giữ chapter version 3 và các ID nhiệm vụ cũ; không thêm schema lưu giữa chừng khi leo (thao tác đang dở chặn lưu giống các thao tác khác).
- Lan can cầu tham gia NavMesh carving khi chạy, đồng bộ đường đi NPC với va chạm thật. Chờ hai frame cập nhật navigation trước khi khảo sát đường chương.

Giữ yêu cầu đã chốt: Nam đi/chạy tự do khi mang đồ và dẫn Tâm; không khóa vũ khí khi hộ tống, không ép Nam đi chậm. Tâm đi theo NavMesh, không tự đi về và không dịch chuyển tới Nam. Giữ F và chuột phải cho ống nhòm/ngắm bắn như bản trước. Bé Tí tiếp tục ở cạnh chị Lành.

## Cách thử trong Unity

1. Dừng Play nếu đang chạy. Chọn **ShadowVale > Map 2 > Chạy thử Ngày 2**.
2. Nhận lệnh từ Thành. Đi thử riêng lối đồng phía bắc và lối bờ tre phía nam tới cầu; theo dấu vết tìm Tâm. Có thể tránh cả tổ tuần tra hoặc xử lý chúng.
3. Nhấn E nhận Tâm đi cùng, quay về ghim ở chỗ Thành. Có thể save/load dọc đường để kiểm tra Tâm tiếp tục theo Nam.
4. Tới kho lấy dây, trở lại trạm giao cho Hùng. Đi tới ghim ở chân cây để dựng ăng-ten theo hướng dẫn trên màn hình.
5. Thử thả W giữa chừng, xuống khi chưa chỉnh xong, cột sai hướng và Esc hủy. Các trường hợp này không được tính hoàn thành. Chỉ sau khi cột đúng và xuống đất mới được báo cáo Thành.

## Kiểm chứng

`Map02DayTwoRouteTests` dùng scene Map 2 thật: khảo sát NavMesh, đi capsule cùng kích thước/layer của Nam qua cả hai đường hai chiều, thử cứu Tâm với tuần tra còn sống và đã bị hạ, kiểm tra giao dây/save/load, tiến trình leo và hủy/cột/xuống đất. Có ảnh render `Logs/Map02Chapter/day2-antenna.png`.

Bộ kiểm tra đợt 2 còn gồm DayTwoState, ChapterMechanics (phím thật W/Space/S), FreeMovement và BridgeCollision. Lượt cuối ngày 22/10/2026 đạt **10/10**, không có test lỗi. Kết quả ở `Logs/Map02Chapter/day2-phase2-results.xml`.

## Phần mỹ thuật còn lại

Dáng leo hiện là pose xương điều khiển bằng code, chưa có clip leo cây chuyên dụng trong source. Điểm bám, cột và dây dùng hình học đơn giản; có thể thay mỹ thuật sau mà giữ logic nhiệm vụ. Chưa coi các phần này là hoạt ảnh điện ảnh hoàn thiện.

Đợt này không thay cho Đợt 3 (hoàn thiện Đêm 2) hay Đợt 4 (diễn xuất và thu/ghép lồng tiếng).
