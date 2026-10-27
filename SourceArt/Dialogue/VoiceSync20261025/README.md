# Đồng bộ giọng Map 1 và Map 2 — 25/10/2026

Project: G:\game\Capstone\ShadowVale, nhánh Khoa, HEAD 5e4a0d46.

- Hùng: Anh Khôi, đúng mẫu A đã chọn; 67 cue xuyên hai map.
- Ông Sáu: Thiền Tâm Đức, đúng mẫu A đã chọn; 3 cue Map 2.
- 9 cue Map 1 và 61 cue Map 2 được cập nhật, giữ nguyên chữ và ID, bao gồm alias hồi tưởng.
- Giữ nguyên 26 cue địch theo hash SHA-256 trước/sau. Không dùng các mẫu quát/gằn bị loại.
- Nam, chỉ huy/anh Thành, Lực, Lành, Bảy, Tâm và các vai khác giữ bộ giọng hiện tại; chưa coi là người dùng duyệt từng câu.
- Câu mời về quê của Hùng được cập nhật cả tệp riêng hung_invitation.wav; metadata thời lượng, hash và catalog runtime/source thống nhất.
- Toàn bộ 271 cue được kiểm tra dữ liệu: WAV mono 48 kHz, không im lặng, không clipping, thời lượng khớp. Đối chiếu nguyên văn hai nguồn thoại Map 2 không thiếu dòng.
- Thu miễn phí bằng VieNeu-TTS v3 Turbo chạy offline. Giữ giấy phép/nguồn model và preset có sẵn; không chi credits hoặc gọi dịch vụ trả phí.

## Giới hạn kiểm tra

Xem integration-report.json và Unity_*.log/XML trong cùng thư mục để biết kết quả kỹ thuật. Không coi kiểm tra dữ liệu là đã nghe duyệt phát âm/sắc thái tất cả câu, hoặc đã kiểm thử toàn bộ flow gameplay.
Bé Tí vẫn là giọng ứng viên thay thế, chưa được xác nhận phù hợp bé trai khoảng 7 tuổi.
Các câu chiến đấu/đau đớn vẫn chịu giới hạn diễn xuất của TTS miễn phí; không tự gắn mẫu tiếng hét khác giọng.

## Bản sao lưu

Thư mục before trong gói workspace lưu đúng tệp trước lần thay này. Không reset/git clean hoặc ghi đè công việc khác của nhánh.

## Kết quả Unity

- Import: 271 cue, unresolved=0.
- 15/15 kiểm thử Unity đạt theo kết quả mới nhất của từng ca; lần đầu 14/15, sau đó sửa và kiểm tra lại tên vai ở hai câu cập bến.
- Phát hết toàn bộ 271 cue ở tốc độ 3× trong cảnh test cô lập; kiểm tra pause, chuyển người nói, kênh bộ đàm, metadata thời lượng, thoại Map 1 và toàn bộ thoại hiện tại Map 2.
- 201 cue ngoài Hùng/ông Sáu giữ nguyên SHA-256, trong đó có 26 cue địch. Hai cue cập bến chỉ đổi speaker/caption/match từ Chỉ huy sang Anh Thành, không thay âm thanh hay lời nói.
- Hai nguồn cốt truyện Map 2 không thay đổi; không sửa nhiệm vụ.
- Fixture FullGameAudioTests dùng ForestSceneTestBase để ngăn công cụ Editor chuyển cảnh kiểm thử về Boot và giữ save test tách biệt.
- Đây là kiểm thử âm thanh và dữ liệu; không phải một lượt chơi toàn bộ cốt truyện hay duyệt phát âm/diễn xuất bằng tai của mọi câu.
