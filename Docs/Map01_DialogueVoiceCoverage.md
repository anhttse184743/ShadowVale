# Độ phủ lồng tiếng Map 1

Đối chiếu source và file WAV ngày 15/10/2026. Kiểm kê này không thay thế nghe duyệt phát âm hoặc kiểm thử runtime.

- **47 cue thoại Map 1 hiện đang được gọi đều có audio**, gồm 22 lượt giao/nhận nhiệm vụ chính, tám câu bổ sung, thoại cứu Hùng/báo động, bộ đàm và rời bến.
- Không còn tám câu thiếu đã liệt kê ở bản kiểm kê trước. Phụ đề và audio dùng cùng câu. Câu về căn cứ chỉ người chơi tới chỉ huy nhận nhiệm vụ.
- Lời mời lên thuyền không nói đường đã thông khi vẫn còn địch. Ba câu trao đổi khi rời bến phát nối tiếp theo thời lượng thật; hoàn thành chờ phát xong, skip dừng audio.
- Không đọc thành tiếng tên phím, thông báo lưu game, inventory, lỗi kỹ thuật và mục tiêu HUD.
- Không bổ sung thoại hoặc thay đổi nhiệm vụ Map 2 trong phạm vi này.

## Chữ và camera cuộc trò chuyện đầu

Người chơi vẫn nhấn E nói chuyện với chỉ huy. Nội dung/tên người nói dùng 16 px, hướng dẫn phím 13 px; khung thoại thấp hơn. Góc máy gần hơn, FOV 48°, cận người đang nói, chuyển mềm .65 giây và kiểm tra vật cản. Không đưa lại cutscene chào cũ. Các cuộc nói chuyện khác giữ chữ 18 px và góc máy hiện tại.

## Nguồn và duyệt audio

`SourceArt/Dialogue/CommanderStory`: 22 lượt nói chính, kèm các bản sửa câu hỏi đã được nghe duyệt.
`SourceArt/Dialogue/Map01Remaining`: tám nguồn MP3, WAV sạch, manifest/task ID/hash, request được duyệt và script rebuild offline. Lượt này dùng 13 credits, đúng trần duyệt; không đổi giọng.
File nghe thử: `Map01Remaining/map1-all-eight-preview.wav`, hoặc tách `map1-rescue-preview.wav` và `map1-boat-preview.wav`.
Câu “Có mấy tên lính canh?” được người dùng xác nhận phát âm đúng. Chưa có xác nhận nghe duyệt toàn bộ tám bản mới; không coi kiểm tra Unicode/hash là kiểm tra phát âm.

## Kiểm chứng

Xem kết quả mới nhất trong `Logs/Map01RemainingVoice` và `Logs/Map01StoryVoice`. Bản kiểm kê này không mặc định các test đang chạy đã đạt.
