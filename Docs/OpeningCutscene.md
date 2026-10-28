# Map 1 — gặp chỉ huy và nhận nhiệm vụ bằng E

Hướng được người dùng chốt ngày 18/10/2026 trên nhánh `thinh`: bắt đầu Map 1 trong trạng thái có thể điều khiển Nam. Đến cạnh chỉ huy và bấm **E** để mở hội thoại nhận nhiệm vụ cứu Hùng.

## Luồng chơi

1. Chơi mới từ menu; chọn chơi hoặc bỏ qua phần hướng dẫn theo menu hiện có.
2. Khi vào Map 1, mục tiêu hướng tới chỉ huy. Chưa có lệnh cứu Hùng được nhận, chưa phát briefing hoặc phim mở màn tự động.
3. Bấm E cạnh chỉ huy để nghe hội thoại. Các câu có bản thu tự chuyển sau khi phát hết; E có thể bỏ qua một câu sau khoảng khóa nhập một giây.
4. Ở câu cuối, đợi giọng đọc kết thúc rồi **nhả và bấm E lần nữa** để nhận nhiệm vụ. Giữ nguyên E từ lúc bắt đầu hoặc chỉ nghe hết hội thoại không tự nhận lệnh.
5. Giữ Esc một giây để đóng hội thoại nhận lệnh đầu mà chưa nhận nhiệm vụ. Có thể gặp chỉ huy và bấm E để nghe lại. Esc bấm sau khi trở lại gameplay vẫn mở menu tạm dừng theo cơ chế hiện tại.

Trong hội thoại, Nam đứng tại chỗ, tạm khóa di chuyển/chiến đấu/camera gameplay và cất vũ khí. Đóng hoặc xác nhận trả lại điều khiển và trang bị, giữ số đạn. Chỉ huy không cầm súng trong cuộc trò chuyện này. Hệ thống chặn lưu trong lúc hội thoại; lưu trước/sau hội thoại tuân theo `Map01SaveSystem`.

Đóng hội thoại cũng dừng giọng và xóa chú thích của cuộc trò chuyện khỏi HUD, trước khi callback xác nhận có thể tạo thông báo nhiệm vụ mới.

Tải checkpoint không tự phát lại briefing. Tiến độ `rescueAccepted` được khôi phục từ save. Xác nhận nhận nhiệm vụ tạo checkpoint theo luồng cứu Hùng hiện có.

## Code và asset

- `Map01OpeningCutscene.Attach`: tạo nhân vật chỉ huy bằng `Initialize(mission, false)`.
- `Map01Story.TryInteract`/`Brief`: mở hội thoại khi Nam đủ gần và chưa nhận lệnh.
- `Map01StoryDialogue`: điều khiển lời thoại, cất súng, camera, tạm khóa điều khiển và xác nhận cuối.
- `Resources/Cutscenes/Map01Opening.prefab`: giữ asset chỉ huy, mesh/avatar dùng chung và các clip cũ. Các clip chào/pointing và công cụ xem asset vẫn được giữ để kiểm tra riêng; chúng không phải nghi thức bắt buộc khi bắt đầu game.
- Không dùng Intro.mp4 hay VideoPlayer cho mở đầu Map 1. Không thay rig, mesh hoặc texture Nam trong lượt đồng bộ này.

## Kiểm tra

- `ForestMenuIntegrationTests`: Play từ map vẫn tới menu; Chơi mới vào Map 1 chờ chỉ huy; đóng hội thoại bằng Esc rồi tạm dừng; lưu và tải lại.
- `OpeningCutsceneTests`: E thật qua Input System mở hội thoại; giữ E không tự xác nhận; bấm E riêng ở cuối nhận lệnh; cất/trả súng và điều khiển. Kiểm camera khi lội sông được giữ thành bài riêng.
- `DialogueVoiceTests`: Chơi mới không xếp hàng briefing tự động; đóng hội thoại không tự nhận lệnh hoặc phát tiếp tiếng cũ.

Kết quả chạy và danh sách bài đã chọn nằm trong `Docs/Reviews/CommanderOpening_20261018/`. Các ghi nhận mô tả mở màn tự động trước đây là lịch sử, không còn là yêu cầu gameplay hiện hành.
