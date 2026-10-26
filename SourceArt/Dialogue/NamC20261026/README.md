# Nam C — Minh Quân, cả hai map

Người dùng chọn Nam C ngày 26/10/2026. Đã thay 89 cue Nam (80 câu khác nhau): 32 Map 1/mở đầu và 57 Map 2, gồm các câu dùng cho save/kịch bản cũ. Giữ nguyên lời, ID, phụ đề, thứ tự và cờ bộ đàm. Lời đáp Nam mở đầu tham chiếu trực tiếp cũng được thay; giữ GUID/meta.

VieNeu v3 Turbo offline, chi phí 0. Hai câu trong mẫu C được tái dùng. Câu nhận lệnh/báo cáo nhịp 1.12; câu tâm tình/đau buồn nhịp 1.02 và nhẹ hơn, cùng giọng. Âm khô, không thêm vang; bộ đàm giữ lọc riêng. Duyệt casting không được ghi thành người dùng đã nghe duyệt từng câu mới.

12/12 test Unity đạt: phát đủ 89 cue, không overlap/stall, pause; import/duration/48 kHz; casting hai map/mở đầu; cửa sổ thoại theo animation Lời hứa; kết thúc/lưu bữa cơm; độ phủ script Map 2 và Ngày 1–Đêm 1. Kiểm tra dữ liệu/phát tiếng/timing, không phải xác nhận nghe từng chữ bằng tai.

186 file audio khác giữ nguyên SHA-256, gồm địch Runway, Hùng A, ông Sáu A, chỉ huy B và các vai nữ. Entry không phải Nam trong catalog giữ nguyên. Không đổi gameplay/model.

plan.json: danh sách và hash bảo vệ; rendered.json: nguồn audio, kiểu thể hiện, duration; installation.json/report.json: tích hợp và kết quả. approved/C-0.wav và C-1.wav: mẫu đã chọn. nam_c_rollout.py gồm prepare/render/install, cần Python có vieneu/numpy/soundfile và FFmpeg, dùng model cache đã cài. Cấu hình SVL_AUDIO_WORKSPACE, SVL_GAME_ROOT, SVL_FFMPEG khi chuyển máy. Chỉ sửa file Nam trong danh sách; bản trước sửa lưu ở workspace output/Nam_C_20261026/before và lịch sử Git.

Bản nghe trích từ audio đã gắn: Nam_C_ingame_preview.wav. Bản nghe đủ Map 1/Map 2 và JSON mốc thoại nằm trong workspace output/Nam_C_20261026. Chưa push đợt này.
