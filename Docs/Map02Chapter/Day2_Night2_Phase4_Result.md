# Đợt 4 Diễn xuất và âm thanh

Ngày triển khai: 22/10/2026. Giữ ID nhiệm vụ, save v3 và rig/animation gốc của Map 1. Đây là kết quả triển khai, không thay thế buổi chơi thử liền tuyến của Đợt 5.

## Các cảnh đã dựng

- D2_01: Hùng khựng, hạ đầu và tránh nhìn Nam sau câu “Vướng”; Nam nhìn xuống kiểm tra súng; Hùng quay về hướng máy sau lời anh Thành. Câu đáp của Hùng có khoảng ngừng trước khi phát giọng nếu có bản thu. Bé Tí vẫn ở cạnh chị Lành theo quyết định của người dùng.
- D2_06: Hùng thao tác bằng tay trái và nhìn lên hướng Nam leo; Nam có động tác tay cột dây tại điểm làm việc trên cao. Base pose được lấy mẫu lại mỗi frame để các góc tay/chân không tích lũy sai khi đứng yên hoặc pause. Cancel/xuống cây trả lại controller.
- N2_05: giữ các câu làm lành tại điểm hẹn; từ câu “Nam.” chuyển camera sang chỗ nghỉ. Hùng ngồi, rồi nằm nghiêng ôm tay đau. Sau câu thoại cuối có 12 giây diễn xuất không thoại: Hùng thiếp ngủ, Nam cúi xuống đắp áo, nhìn ra ngoài trực. Không tiếp tục hiện câu thoại cũ trên đoạn không lời.
- Có prop áo khoác mềm để diễn động tác đắp và tiếng vải từ asset sẵn có. Scene không trừ vật phẩm hay đổi trang bị, máu, đạn.
- Skip, kết thúc tự nhiên, cancel và load giải phóng pose, prop, camera và quyền giữ actor. Không clone thêm Hùng hay sửa avatar dùng chung.

## Âm thanh

Chưa tìm thấy bản nhạc chủ đề Hùng đã gắn trong source. Đã tạo motif nhạc cụ tạm do dự án tự sinh: sáu nốt ở cảnh đổi ca Đêm 1, ba nốt nhắc lại bỏ lửng khi Hùng ngủ Đêm 2. Đây không phải bản thu hát/ngân của diễn viên. Source tái tạo ở `SourceArt/Audio/build_hung_theme.py`; WAV ở `Assets/Resources/OperationsAudio/hung_theme_night1.wav` và `hung_theme_night2.wav`.

Tiếng vải phát khi áo được đặt xuống. Nhạc/tiếng vải pause theo menu. Câu radio trong cinematic có tiếng rè trước khi giọng bắt đầu; chỉ phát bản thu khớp ID/caption của voice catalog.

Runway đã được tìm thấy nhưng kiểm tra xác thực hiện trả `USER_NOT_LOGGED_IN`. Chưa tạo/gắn 32 bản thu mới. Tuổi, vai và giọng Map 1 vẫn giữ theo manifest; không lấy bản thu sai câu để lấp chỗ trống.

## Giới hạn mỹ thuật

Diễn xuất hiện dùng base clip sẵn có cộng pose/IK theo nhịp cảnh. Các slot `namCheckRifle`, `hungHurtReaction`, `hungLeftRadio`, `hungNightRest`, `hungNightSleep`, `namCoverJacket`, `namAntennaClimb`, `namAntennaTie` trong ChapterLayout cho phép thay clip chuyên dụng. Chưa có bản motion capture/Blender riêng được duyệt cho những slot này.

Áo khoác hiện là prop dựng cảnh, chưa tách lớp áo khoác khỏi mesh trang phục của Nam. Không thay đổi luật Tâm chỉ đi theo, Nam di chuyển tự do; không thêm động tác dìu bắt buộc hoặc khóa vũ khí hộ tống.

## Kiểm tra

Test: `Map02PerformanceTests`, cộng các ca tuyến Ngày 2, Đêm 2, quan sát tuần tra và cơ chế ăng-ten/kết thúc. Báo cáo tại `Logs/Map02Chapter/phase4-results.xml`; ảnh kiểm tra tại `Logs/Map02Chapter/Phase4`.

Lượt tổng đạt 7/7. Sau khi chỉnh chất liệu áo, bám nền, góc máy và hướng áo, ca diễn xuất riêng chạy lại đạt 1/1 tại `Logs/Map02Chapter/phase4-focus-results.xml`; kiểm tra thêm khoảng trống giữa áo và đầu Hùng. Không suy ra thời lượng chơi cả tuyến từ các ca dựng mốc cảnh này.

## Xem thử trong Unity

Thoát Play rồi chọn `ShadowVale > Map 2 > Xem diễn xuất > Nhận lệnh Ngày 2` hoặc `Kết Đêm 2`. Menu dựng trạng thái thử riêng trong `Temp/Map02RoutePreviewSaves`, mở đúng cinematic bằng handler tương tác thật. E chuyển câu, giữ Esc bỏ cảnh. Cảnh kết Đêm 2 tự chuyển sang Ngày 3 khi hoàn tất; bản lưu thường của người chơi không bị ghi đè.
