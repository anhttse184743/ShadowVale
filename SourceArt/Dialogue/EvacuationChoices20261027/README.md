# Giọng cho ba hướng sơ tán — 27/10/2026

Bổ sung 8 cue m2v3_evac_* trong catalog runtime và source, giữ nguyên phụ đề/kênh bộ đàm. Nam dùng Adam (tempo/pitch 1/1), Anh Thành dùng preset Minh Đức chỉ huy B (0,92/0,94). Dùng VieNeu v3 Turbo ONNX đã có trong máy, không kết nối dịch vụ trả phí hoặc tải thêm model. Hai câu Rõ dùng bản thu Nam có sẵn qua bộ lọc bộ đàm; sáu câu dài tạo mới.

WAV runtime nằm ở Assets/Resources/Dialogue/VI/*_radio.wav, mono PCM 48 kHz. Raw/clean/radio masters, hash và thời lượng nằm trong gói này. Catalog trước khi bổ sung được lưu ở voice-catalog-before.json. 261 entry âm thanh ngoài phạm vi giữ nguyên hash.

Audit toàn bộ Map02Chapter.json: không còn câu có nội dung bị thiếu clip/không khớp caption hoặc radio; bỏ qua cue chuyển ca có text rỗng. Unity nhập và giải mã đủ 8 clip, khớp caption/radio/thời lượng (unity-checks.txt). Đã cập nhật hàng đợi nghe duyệt CSV. Kiểm tra kỹ thuật không thay cho nghe duyệt phát âm và diễn xuất từng câu mới.