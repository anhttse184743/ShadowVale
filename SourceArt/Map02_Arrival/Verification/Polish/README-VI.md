# Kiểm chứng bản sửa Map 2

Unity 6000.3.24f1: 6/6 test qua. Chạy tự nhiên và skip khi giữ aim, nhả aim, bắn thật 3 phát sau lên bờ; 500 lần yêu cầu tracer giữ đúng pool 12 đối tượng.

Đã ghi từng frame camera thật và xem ảnh góc gần cất chèo, đứng, bước lên bến. Góc đầu tối đa so với hướng trung tính của actor: 10.47°. Sai số khớp chân so với điểm tiếp xúc bậc tối đa: 0.0008 cm. Test kiểm tra đế giày thực sau cập bến trong 2 cm ở cả chạy tự nhiên và skip.

Video arrival-motion-review.mp4 và hung-close-review.mp4 dùng thời gian cảnh từ CSV; đây là video kiểm tra chuyển động camera, không thu GUI và âm thanh. Clip nguồn bake 30 fps, skeleton 28 xương và rest pose được giữ nguyên.

Không có đo FPS riêng trên máy người chơi. Bản sửa tracer xử lý lỗi native object đã bị hủy khi unload scene, ngăn lỗi Console lặp lại và kiểm tra pool không tăng.

Model gốc, cảnh Map 1/Map 2, địa hình, lời thoại và bản chào Map 1 giữ nguyên; published-sha256.json lưu hash đối chiếu và những file được cập nhật.
