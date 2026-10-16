# Thoại ba điểm nhiệm vụ — 16/10/2026

20 câu đã thu, 30/30 credits được người dùng duyệt; không dùng lại ngân sách cũ.
Giữ bộ giọng hiện tại: Nam Brodie, Hùng Clint, chỉ huy Tom; lính Martin.
Đọc tiếng Việt, tốc độ 0,9, thu câu hoàn chỉnh. Mẫu giao nhiệm vụ điểm 1 đã được người dùng nghe và xác nhận rõ chữ.

`Raw` giữ MP3 nguồn; `VI` giữ WAV sạch 48 kHz mono PCM16 và bốn bản đàm riêng.
Chỉ chỉ huy ở đầu đàm bên kia dùng lọc nhẹ 220–4200 Hz và nhiễu thấp; Nam nói tại chỗ dùng bản sạch.
Catalog runtime phát đúng một bản theo ID/caption, không ghép bản sạch với bản đàm.

`install_audio.py --deps <thư viện cục bộ>` tái xử lý và nhập những MP3 đã tải, không gọi tạo thoại và không tính credits.
Nếu mất MP3, dùng task ID trong receipt để lấy lại URL và truyền `--results <receipt tải tạm>`; không tạo job mới.
URL có hạn chỉ giữ trong Logs bị Git bỏ qua. Script/giọng/phí ở approved-request-20261016.json và jobs-20261016.json.

Các file `*-preview.wav` để nghe duyệt theo đoạn. Tín hiệu đã kiểm tra độ dài, clipping, mức âm và caption.
Kiểm tra tín hiệu không chứng minh mọi từ đúng phát âm; trạng thái nghe duyệt từng câu nằm trong voice-manifest.json.
