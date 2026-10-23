# Map 1 — Mini game và hội thoại, 16/10/2026

Lần sửa tiếp theo về chữ 4K, độ trễ, phụ đề cứu Hùng và quan sát doanh trại: xem [Map01_Interaction_Review.md](Map01_Interaction_Review.md), bộ kiểm tra mới 19/19 đạt. Các kết quả dưới đây thuộc đợt mini game/audio trước đó.

## Đã tích hợp vào game chính

- Đủ 20 câu thoại mới cho nhận điểm 1, lấy hàng, bàn giao, điểm 2, báo cáo điểm 3, phát hiện và rút lui. ID/caption/kênh âm thanh được đối chiếu; không ghép lời cũ vào chữ mới.
- Giữ giọng hiện tại theo duyệt: Nam Brodie, Hùng Clint, chỉ huy Tom; lính Martin. Tiếng Việt, nhịp 0,9; mọi câu có master sạch, chỉ bốn câu của chỉ huy ở đầu đàm dùng bản lọc nhẹ. Dùng đúng 30/30 credits được duyệt, không dùng lại ngân sách trước.
- Người dùng đã nghe và duyệt ba câu giao điểm 1 rõ chữ. Những mẫu còn lại được giữ để nghe duyệt; kiểm tra tín hiệu không chứng minh mọi từ đúng phát âm.
- Khóa hòm có khung kết quả riêng: đúng vị trí / đúng nhưng sai vị trí / không khớp, gồm số lặp thừa. Không có số nào đúng được thông báo bằng câu đầy đủ. Đổi số không trừ lượt; Enter/nút kiểm tra mới tính. Lịch sử có cột rõ ràng, cuộn được và giữ qua thử lại.
- Đài có MỘT đường dây qua đủ 20 đoạn trên lưới 5×4. Không còn nhánh thừa, tải giả hoặc nhánh đánh lừa. Dây có điện sáng vàng; đếm đoạn đã nối và gợi ý bắt đầu từ A1 tới đầu NGẮT ĐÀI ở E4.
- Giữ 90 giây sau hướng dẫn, pause giữ đồng hồ. Xoay sai không phạt; chỉ xác nhận sai hoặc hết giờ mới gây báo động. Giải thích lỗi giữ 2,5 giây rồi trả điều khiển; lần sai đầu chỉ gọi một nhóm ba lính.
- HUD và mục tiêu dùng đúng hướng dẫn mới. Save 16 ô cũ giữ tiến độ nhiệm vụ, chỉ tạo lại bảng đố 20 đoạn; không gọi lại quân hoặc thưởng lặp.
- Giữ địa hình, model/skeleton, nhiệm vụ cứu Hùng, bàn giao tiếp tế tại hầm, trận sĩ quan và rời bến. Không sửa scene hoặc director Map 2 trong lần này.

## Kết quả kiểm tra

| Bằng chứng | Kết quả |
|---|---|
| Logs/Map01Operations/clarity-regressions.xml | 38/38 đạt: mini game, thoại, tiến trình, save/load, camera cứu Hùng, vào hầm, tracer, rời bến và Map 2 |
| Logs/Map01Operations/video/clarity-results.txt | 8/8 đạt sau chỉnh UI cuối: xem Game View, phản hồi sai có trì hoãn/pause, nhóm lính một lần, bàn giao, quan sát, sĩ quan chết sớm và audio đúng kênh |
| SourceArt/Dialogue/Map01Operations/audio-validation.json | 20 cue giải mã được, mono 48 kHz, kiểm tra thời lượng, mức âm và biên độ; 24 WAV gồm 20 bản sạch + 4 bản đàm |
| Logs/Map01Operations/navigation-regression.txt | Bố trí scene hiện tại có 52/52 tuyến nối được trong lần kiểm tra trước; lần sửa UI/audio này không thay NavMesh |

Lượt 38 bài và lượt 8 bài là hai lượt riêng, không cộng thành một bộ kiểm thử duy nhất. Bài tích hợp đặt actor/tiến trình bằng fixture, không phải lượt chơi lần đầu 20–30 phút.

## Đã xem trực tiếp

- `Logs/Map01Operations/lock-feedback.png`: ví dụ 1 đúng vị trí, 2 đúng nhưng sai vị trí; lịch sử và khung kết quả không chồng chữ.
- `Logs/Map01Operations/lock-ui.png`: không có số đúng, hết sáu lượt rồi cho thử lại.
- `Logs/Map01Operations/circuit-play.png`: frame thật từ video point-2, đường dây 20 đoạn, trạng thái và nút xác nhận.
- `Logs/Map01Operations/circuit-error.png`: giải thích đường còn đứt trước khi trả điều khiển.
- `Logs/Map01Operations/radio-panel.png`, `radio-contact.txt`: tay thao tác đài; clip nguồn Blender và 62 xương giữ nguyên.
- `Logs/Map01Operations/cargo-in-hand.png`: bàn tay cuộn nắm quai túi, không còn xòe.

Video `video/point-1.mp4`, `point-2.mp4`, `point-3.mp4` là trình diễn tích hợp tự động; hạ lính và một số bước đi được điều khiển bằng fixture. Video không có track audio; phần point-2 dừng quay khi bài test tải lại scene. Audio nghe riêng trong các file preview, không coi video im tiếng là bản dựng thoại.

## File bàn giao và giới hạn

Code và hướng dẫn Inspector/Blender/camera: `Docs/Map01_ThreeOperations.md`.
Thoại, master, phiên bản bộ đàm, script xử lý, task receipts và ngân sách: `SourceArt/Dialogue/Map01Operations`.
Mẫu nghe toàn bộ: `all-missions-preview.wav`; mẫu từng đoạn: `*-preview.wav`.

Chưa đo thời lượng/cân bằng 20–30 phút bằng người chơi lần đầu và chưa có video thực chiến toàn nhiệm vụ. Chưa xác nhận phát âm tất cả 20 câu bằng người nghe; ba câu giao điểm 1 đã được người dùng duyệt. Không coi các bài trạng thái hoặc một góc chụp là chứng nhận mọi frame tay/chân hoàn hảo.