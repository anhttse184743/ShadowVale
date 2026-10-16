# Lồng tiếng hội thoại chỉ huy — Map 1

Đã tạo và tích hợp 22 lượt thoại: nhận nhiệm vụ cứu Hùng, báo cáo sau hộ tống, nhận lệnh tiếp tế/phá đài, và xác nhận chỉ huy địch. Lượt sản xuất ban đầu dùng 35 credits; ba lần sửa câu hỏi được duyệt riêng 1 credit/lần, tổng 38 credits. Giữ nguyên bộ giọng.

- Nam: giọng hiện có Brodie. Chỉ huy: Tom. Một câu Hùng tham gia báo cáo: Clint.
- Mọi lời Nam và nhân vật đứng gần phát bản sạch. Năm câu chỉ huy từ đầu đàm bên kia dùng bản lọc nhẹ, không phát đồng thời bản sạch.
- 22 master sạch, 5 bản bộ đàm: WAV PCM 16-bit, mono, 48 kHz. Giữ 22 nguồn MP3 và bản master tại `SourceArt/Dialogue/CommanderStory/Raw`.
- Audio điều khiển nhịp hội thoại, tự chuyển câu sau khi nói xong. E bỏ qua câu khi cần. Câu cuối phải nói xong rồi E mới nhận nhiệm vụ. Giữ Esc đóng cuộc trò chuyện, dừng audio và trả điều khiển.
- Các câu vẫn có phụ đề đúng dấu để hỗ trợ tiếp cận; không phải tự đọc để đi qua hội thoại.

## Chỉnh nội dung và giọng

`Map01Story.cs`: mỗi `Line` gắn ID `m1_story_*` riêng. `Assets/Resources/Dialogue/voice-catalog.json` ánh xạ ID tới clip. Không sửa riêng phụ đề rồi giữ một bản thu nói câu khác: runtime kiểm tra caption, kênh sạch/bộ đàm và ID khớp nhau.

`SourceArt/Dialogue/CommanderStory/approved-request.json` lưu đúng kịch bản và trần chi phí. `voice-manifest.json` lưu ID tác vụ, giọng, văn bản, thời lượng, hash MP3/master/clip dùng trong game và thông số kiểm chứng.

## Tạo lại bản xử lý mà không gọi TTS

Chạy `SourceArt/Dialogue/CommanderStory/rebuild_audio.py` từ Python có các thư viện trong `SourceArt/Dialogue/requirements.txt`. Nếu dùng thư viện cục bộ khác, biến môi trường `SHADOWVALE_AUDIO_DEPS` trỏ tới thư mục package. Script chỉ đọc MP3 có sẵn, xác minh hash rồi tạo lại WAV sạch/bộ đàm và các file nghe thử. Không gửi yêu cầu tạo giọng mới.

## Nghe thử

Các file `*-preview.wav` trong `SourceArt/Dialogue/CommanderStory` ghép lần lượt các lượt thoại của từng cuộc nói chuyện. Có thể kéo file vào trình nghe để kiểm tra nhanh giọng của ba nhân vật. Bản `rescue_order-preview.wav` là cuộc nhận nhiệm vụ đầu tiên.

## Kiểm chứng

`Map01StoryVoiceTests`: kiểm tra 22 ID và caption, đủ master sạch, chỉ năm clip từ đầu đàm dùng bản xử lý; chạy hội thoại tự nhiên để xác nhận cả Nam và chỉ huy phát đủ lượt, không chồng tiếng, nhân vật đứng yên và xác nhận nhận lệnh ở cuối.

Phân biệt kiểm tra file/logic với đánh giá phát âm bằng tai: kiểm tra tự động xác nhận nội dung đầu vào đúng Unicode, âm không rỗng/clipping, và clip đúng ID; không thay thế việc nghe duyệt sắc thái từng câu.

## Câu hỏi đã nghe duyệt

Ngày 15/10/2026, người dùng duyệt bản thu **Nam: Có mấy tên lính canh?** thay câu cũ bị đọc sai cụm từ hỏi số lượng. Runtime, phụ đề, catalog và preview dùng đúng câu mới, ID/GUID giữ nguyên. Nguồn và các bản bị loại lưu trong `CommanderStory/Revisions`; chi phí từng lần trong manifest. Các câu còn lại chưa được người nghe duyệt hết.

## Hoàn thiện các câu còn thiếu

Đã bổ sung tám câu ngoài năm cuộc giao/nhận nhiệm vụ chính, dùng thêm 13 credits theo lần duyệt riêng. Xem `Map01_RemainingDialogue.md` để biết nguồn, script rebuild, cơ chế rời bến và kiểm chứng. Rà hiện tại: 47 cue Map 1 có audio; không còn tám câu thiếu trong bản kiểm kê trước.
