# Kiểm chứng lồng tiếng Map 1 và cập bến Map 2

Bổ sung 26 câu tiếng Việt: Nam/Hùng trong nhiệm vụ và giải cứu, bốn câu lính địch, quan sát tài liệu/bến, bộ đàm, lên thuyền/rời bến và ba câu cập bến Map 2. Giữ nguyên script, prefab và hai MP3 giọng đã có của cutscene mở đầu Map 1, có xác minh SHA-256 trước/sau bàn giao.

Đã sửa cốt truyện trong script và thoại: Hùng đưa hàng tiếp tế về; địch nghi anh ấy là lính thông tin. Không có tình tiết Hùng đưa thư hoặc mang mật lệnh trong ba câu liên quan.

Thoại trực tiếp rõ. Hai câu bộ đàm được xử lý dải giọng, bão hòa nhẹ và tiếng rè nhỏ; có WAV giọng rõ gốc để thay mức hiệu ứng. Mono PCM 48 kHz; chuẩn hóa RMS, peak thấp hơn ngưỡng clipping, giữ tốc độ giọng tự nhiên. `audio-validation.json` ghi thời lượng, RMS, peak và hash từng clip.

Runtime phát một người tại một thời điểm, tách thứ tự Nam chữa thương → Hùng trả lời, cập nhật phụ đề theo người đang nói và kéo dài animation nói của Hùng theo clip. Câu radio của Nam bắt đầu sau chỉ huy. Skip dừng giọng; pause tạm dừng; load checkpoint hoặc thử lại giải cứu dọn thoại đang phát.

Kiểm thử trước hiệu chỉnh cốt truyện: 12/12 qua, gồm thoại, mở đầu, extraction và Map 2. Kiểm thử bản cuối: 5/5 qua, gồm đủ 26 audio tiếng Việt, chỉ hai clip radio, giữ nguyên giọng mở đầu, cốt truyện hàng tiếp tế/nghi nhầm, pause/chuyển người nói/hai câu liên tiếp và skip radio. Báo cáo XML kèm trong Verification. Kiểm tra audio tự động và đồng bộ runtime; chưa có vòng duyệt phát âm/ngữ điệu bởi người nghe.

Không thay model, skeleton, animation, địa hình hay nhiệm vụ Map 2. Thông báo lưu game, lỗi kỹ thuật, inventory và nhãn phím giữ dạng thông báo giao diện, không đọc thành giọng nhân vật.

## Sửa phát lặp bộ đàm

File nghe thử chỉ ghép một lệnh chỉ huy qua bộ đàm và một câu trả lời của Nam. Hai cue radio trong extraction dùng PlayOnce để không khởi động lại khi gọi trùng. Không thay WAV radio đã lọc hay cutscene mở đầu. Hai kiểm thử Unity qua: cue không phát lại/không chọn giọng rõ; Nam chờ hết lệnh và skip dừng âm thanh. XML: Verification/radio-once-final.xml.
