# Hoàn thiện lồng tiếng Map 1

Ngày 15/10/2026. Bổ sung tám câu còn thiếu, dùng đúng bộ giọng đang có: Nam — Brodie, Hùng — Clint, chỉ huy — Tom. Chi phí lượt này **13 credits**, đúng trần người dùng duyệt.

## Nội dung đã tích hợp

- Nam chưa thể gọi đàm khi còn giao tranh.
- Hùng thì thào lúc bị giữ, cảm ơn sau khi cởi trói, báo đã về căn cứ và nhắc gặp **chỉ huy** nhận nhiệm vụ.
- Hùng mời Nam lên thuyền; Nam hỏi điểm đến, chỉ huy chỉ điểm liên lạc ở làng ven sông, Hùng đáp sẽ giữ thuyền sát bờ.
- Không sử dụng câu cũ “Đường đã thông” trong khi vẫn có địch truy kích.

Toàn bộ tám câu nói tại chỗ, dùng WAV sạch; lời thì thào được thực hiện trong giọng Hùng, không lọc kiểu bộ đàm. Chuỗi rời bến dùng ID audio riêng và phụ đề từ cùng catalog, chờ câu trước nói xong rồi mới chuyển. Màn hoàn thành không cắt câu cuối; skip dừng ngay nguồn thoại và đưa tới trạng thái hoàn thành như cũ.

## Chữ và góc máy cuộc trò chuyện đầu

Giữ thao tác E nói chuyện với chỉ huy và E xác nhận nhận nhiệm vụ. Nội dung/tên người nói 16 px, gợi ý phím 13 px. Camera cận người đang nói, tham chiếu hai góc của cutscene mở đầu cũ, FOV 48°, chuyển mềm .65 giây và kiểm tra vật cản. Nam đứng yên khi nói.

Nền đen co theo chiều cao chữ thực tế, giảm khoảng đệm và cách đáy 12 px. Câu một dòng dùng dải thấp khoảng 80–90 px; câu dài tự tăng đủ chỗ, không giữ ô trống cao cố định. Cách thu nền áp dụng chung hộp thoại; chữ và góc máy của các cuộc nói chuyện khác giữ thiết lập hiện tại.

## File chỉnh sửa và tái tạo

- `Assets/_Project/Map01/Runtime/Map01StoryDialogue.cs`: chữ, khoảng đệm, phép đo nền theo chữ và góc cận đầu game.
- `Map01Extraction.cs`: thứ tự cue rời bến; `Map01Rescue.cs`: nội dung Hùng về căn cứ.
- `Assets/Resources/Dialogue/VI`: tám WAV mới; `Dialogue/voice-catalog.json`: ID/caption/resource/match.
- `SourceArt/Dialogue/Map01Remaining/Raw`: MP3 gốc và PCM sạch. `voice-manifest.json`: task ID, giọng, nội dung, thời lượng và hash. `approved-request.json`: kịch bản, ngân sách được duyệt.

Chạy `SourceArt/Dialogue/Map01Remaining/rebuild_audio.py --check` để tái tạo trong bộ nhớ và so SHA với WAV hiện tại; bỏ `--check` để viết lại WAV từ nguồn MP3. Không gọi dịch vụ hoặc tiêu credits. Cài các thư viện trong `SourceArt/Dialogue/requirements.txt`; có thể dùng biến `SHADOWVALE_AUDIO_DEPS` để trỏ tới nơi thư viện sẵn có.

Nghe thử `map1-all-eight-preview.wav`, hoặc tách `map1-rescue-preview.wav` và `map1-boat-preview.wav`.

## Kiểm chứng

- Rà source: **47 cue thoại Map 1 đang được gọi đều có WAV**, không còn tám câu thiếu trước đó. Không tính chữ hướng dẫn phím, lưu game, inventory hoặc thông báo kỹ thuật là lời nhân vật.
- Tái tạo offline tám WAV: SHA khớp toàn bộ; PCM 16-bit, mono 48 kHz, không rỗng, không clipping.
- `Map01StoryVoice` — **5/5 đạt**: metadata/clip; bốn câu gameplay; thứ tự thoại tự nhiên trên thuyền, hoàn thành và skip; cuộc nhận nhiệm vụ phát tự động, đứng yên, xác nhận cuối và trả điều khiển/nguồn thoại.
- Góc cận sau hiệu chỉnh được chạy lại riêng; kết quả ở `Logs/Map01StoryVoice/close-camera-results.json`. Ảnh chỉ huy và Nam ở cùng thư mục.
- Kết quả lượt năm test ở `Logs/Map01RemainingVoice/voice-results.json`, cue thuyền ở `boat-lines.txt`.

Kiểm tra byte, Unicode và logic không xác nhận phát âm bằng tai. Người dùng đã duyệt riêng câu “Có mấy tên lính canh?”; tám câu mới được gửi file nghe thử, chưa có xác nhận nghe duyệt từng câu.

Không sửa địa hình, model, rig, clip chuyển động hoặc nhiệm vụ Map 2 trong lượt này.

Đã xem trực tiếp hai ảnh Game View bản cuối: cận chỉ huy và cận Nam, nền đen co vừa chữ và không còn khung trống cao. Lượt kiểm thử cận cảnh/nền gọn cuối đạt 1/1.
