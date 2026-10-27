# Chỉ huy chèo thuyền, Hùng đỡ tay phải — cuối Map 1 / đầu Map 2

Khoa, 26/10/2026. Đổi ghế Hùng và chỉ huy ở cả hai cảnh, giữ ghế và đường lên thuyền của Nam. Chỉ huy dùng clip chèo cũ của Hùng trên Avatar humanoid chỉ huy; căn cánh tay, khuỷu và bàn tay theo kích thước thật, không đổi chiều dài/scale xương. Chỉ Nam bắn yểm trợ, Hùng ngồi ôm tay phải. Mọi cue và file thoại giữ nguyên; không thu thêm audio hoặc chi phí.

Hùng: bỏ góc cổ tay từ pose cầm súng, căn hướng bàn tay theo cẳng tay; tay trái đỡ dưới cẳng tay phải. Thử tự động siết góc hướng bàn tay/cẳng tay dưới 10 độ trong hai đoạn chèo. Ảnh cận kèm theo để người dùng đánh giá tư thế.

Model chỉ huy gốc chỉ có xương bàn tay và endpoint, thiếu xương ngón. `CommanderPaddleGripSetup` tạo mesh đọc được từ FBX trong Editor (không đổi Read/Write của nguồn). `Map01PaddleGrip` tạo bản sao runtime, thêm hai blendshape khép bốn ngón quanh cán; thả lần lượt theo nhịp cất chèo. Cuff/cổ tay và vùng ngón cái được giữ/taper; không dựng lại skeleton hoặc giả nhận có rig ngón mới. Bản sao được trả về mesh gốc khi giải phóng. Asset cần đưa vào build: `Assets/Resources/Cutscenes/Commander_Paddle_Readable.asset` cùng meta. Có thể tạo lại từ menu ShadowVale/Cutscene/Prepare commander paddle grip.

Vai chèo được đổi cả lúc lấy chèo, chèo, dừng/cất chèo, chuyển map, đứng lên và bước lên bờ. Ghế và làn ra bến theo vai mới; giữ danh tính mảng hành khách Nam/chỉ huy/Hùng để quest/save/chapter không tráo nhân vật. Hùng không được cấp súng trong đoạn ngồi ôm tay. Chỉ huy nhận lại trang bị khi lên bờ và bàn giao gameplay.

Kết quả kiểm thử trong Final_Tests.xml/report.json: chạy tự nhiên xuyên hai map, tay trên cán, chiều dài tay/Avatar giữ nguyên, cổ tay Hùng, chỉ Nam bắn, ba người lên bờ và điều khiển trở lại; thêm load checkpoint hoàn thành và skip arrival. Các lần thử lỗi fixture được giữ trong thư mục output để truy vết; lỗi closure qua EnterPlayMode đã sửa. Không tuyên bố toàn bộ game không có bug.

Video dùng ảnh camera Unity theo timestamp và âm thanh ghép lại từ cue có sẵn; không phải thu âm hệ thống trực tiếp, không kèm toàn bộ ambience. Hai video tách ở thời gian tải cảnh. Video Map 2 được căn tốc độ về các pha 12/4/26 giây vì việc ghi PNG làm batch chạy chậm; giọng nói giữ tốc độ gốc. Clip, thuyền, đường đi và thoại gốc không đổi. Chưa commit/push đợt thay vai này.

Tham khảo Unity: https://docs.unity.com/en-us/engine/6000.0/manual/animation-section/animation-mecanim/avatar-creationand-setup/retargeting
Về tiếp xúc prop sau retarget khác tỉ lệ: https://unity.com/blog/engine-platform/mecanim-humanoids
