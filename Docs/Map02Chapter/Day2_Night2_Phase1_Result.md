# Kết quả đợt 1 tuyến Ngày 2 và Đêm 2

Ngày thực hiện: 21/10/2026. Đã triển khai nền tiến trình, checkpoint và save/load; giữ nguyên 43 ID nhiệm vụ, chapter version 3 và snapshot version 1.

## Hành vi đã áp dụng

- D2_05 có hai bước: lấy cuộn dây tại kho, mang về trạm và E giao Hùng. Chỉ bàn giao mới hoàn thành và mở D2_06. Marker/lời nhắc đổi theo dây đang mang; E tại kho không thể giao từ xa hoặc lấy trùng.
- Cho phép lưu khi đang hộ tống. Load khôi phục actor Follow, điểm đến, giảm tốc, khóa chạy nước rút và trạng thái tay đang bận. Có xử lý cho Tâm, Hùng và nhóm cứu dân dùng chung cơ chế.
- Thêm checkpoint tuần tra riêng sau N2_02 tại đầu cầu. Checkpoint thường của kho/bến không ghi đè mốc này. Phát hiện lần hai gọi RetryPatrol, quay về đầu cầu, reset cảnh báo và phần kiểm tra kho/bến.
- Save giữ pha quét đèn và cảnh báo hiện có; load xóa thời gian phơi sáng chưa đủ ngưỡng và cho 2 giây ổn định trước khi tính phát hiện tiếp. Lưu/tải một cảnh báo không tự biến thành lần phát hiện thứ hai.
- Save version 3 cũ đã xong D2_05 được nhận là đã giao dây. Save cũ giữa tuyến Đêm 2 chưa có checkpoint tuần tra được tạo mốc đầu cầu từ chính máu, đạn, trang bị và đồ đang lưu; không tạo thêm tài nguyên.
- Snapshot tuần tra không chứa checkpoint lồng tiếp; parser kiểm tra payload và điều kiện mốc. Checkpoint tuần tra được bỏ sau khi rời tuyến.
- Bổ sung dữ liệu `night2RouteVisited` và `night2ChainCuePlayed` để các đợt sau nối trigger đường bến và âm xích. Đợt này chỉ bổ sung lưu/khôi phục/reset dữ liệu; chưa dựng trigger hoặc phát âm thanh mới.

## File chính

- Runtime/Map02ChapterState.cs: trạng thái cuộn dây, tuần tra, dữ liệu tuyến và tương thích save.
- Runtime/Map02Mission.cs: checkpoint tuần tra độc lập, parse payload và retry.
- Runtime/Map02ChapterDirector.cs: thao tác lấy/giao dây, khôi phục hộ tống, thời gian quét/phát hiện.
- Runtime/Map02ChapterGuidance.cs và Resources/Story/Map02Chapter.json: hướng dẫn và mục tiêu giao dây.
- Tests/Map02DayTwoStateTests.cs: kiểm tra mới và runner chạy theo request rõ ràng trong Editor đang mở.

Các đường dẫn trên thuộc Assets/_Project/Map01. Không thay scene, terrain, model, rig hoặc asset lồng tiếng.

## Kiểm chứng

Unity 6000.3.24f1 đã biên dịch và chạy kiểm thử qua TestRunnerApi trong Editor đang mở project. Batch mode riêng bị chặn vì project đang mở; không đóng Editor để ép chạy batch. Thư mục save của fixture được tách bằng ForestSceneTestBase.

- Bộ đầu: 12/12 đạt, không failure/skip, gồm Chapter, Mechanics và 5 test mới. Bản kết quả giữ tại Logs/Map02Chapter/day2-phase1-first-12-passed.xml.
- Sau chỉnh cuối và bổ sung kiểm tra phát hiện: 8/8 đạt, không failure/skip, gồm 5 test tuyến Ngày 2, 2 Runtime và 1 Clarity. Kết quả tại Logs/Map02Chapter/day2-phase1-results.xml.
- Hai bộ có 5 test trùng; không cộng thành 20 test độc lập và không gọi là một lượt hồi quy toàn dự án.
- git diff --check sạch.

Kiểm tra trên scene Map 2 đã xác nhận: dây không giao từ xa, không nhân đôi; giữ 7 viên trong băng và 19 viên dự trữ qua lấy/giao và load; Tâm thật sự tiếp tục di chuyển sau load; retry quay về đầu cầu thay vì kho/bến và giữ 61 HP, 6 viên trong băng, 23 viên dự trữ theo checkpoint.

Test đèn dùng collider thật của Nam và handler TickPatrol thật: lần đầu cảnh báo, load không tự phát hiện ngay, lần hai quay về đầu cầu dù đã xong kho/bến. Fixture đặt Nam và nguồn quét ở vùng trống trên cao để tách quy tắc phát hiện khỏi cây/nhà của scene; có điều khiển thời gian quét. Vì vậy đây chưa chứng minh tuyến né đèn trên địa hình thật đã cân bằng hoặc có đủ vật che.

## Phần tiếp theo

Đợt 2 cần khảo sát/dựng hai đường tới cầu, dấu vết tìm Tâm, đích tiếp nhận ở chốt và tương tác leo/dựng ăng-ten. Kiểm tra cửa kho, trigger đi hết đường bến, đồng bộ thoại với ánh đèn, diễn xuất và cue âm thanh vẫn theo các đợt 3–4. Chưa coi đoạn dìu có súng ngắn, tư thế nằm, animation hoặc toàn bộ trải nghiệm 30–37 phút là đã hoàn thiện.
