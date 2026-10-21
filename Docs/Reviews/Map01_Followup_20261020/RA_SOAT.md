# Rà soát Map 1 sau merge — 20/10/2026

Mốc nguồn: `thinh`, `6c86097bfbf51b6585b3450e7ca53d90cd44436c`. Phạm vi người dùng chọn: rà gameplay Map 1 còn dang dở và sửa lỗi có căn cứ. Không suy luận tác giả từ tên thư mục. Đây là bản rà code hiện tại, không coi danh sách lỗi ngày 17–18/10 là kết quả kiểm thử hiện tại.

## Đã sửa trong lượt này

| Phần | Kết luận |
|---|---|
| Lời hô khi phát hiện xác chưa có bản thu | `Map01Mission.Say` dùng kết quả `SpeakText`: chưa có giọng thì giữ chữ và tên người nói; đã có giọng thì giữ chính sách phụ đề đang dùng. |
| Hệ số nhận diện khi cảnh giác | `waryDetectionScale` được đọc từ balance; giá trị phát hành vẫn 0,75. File cũ thiếu trường, hoặc trường không dương, dùng 0,75. |
| C3 điểm danh không ai trả lời | `GoWary` ra lệnh đi bộ đến vị trí canh. Khởi tạo/tải save vẫn dùng quy trình đặt vị trí cũ. Không sửa logic chiến đấu/gọi viện của chỉ huy. |

Bằng chứng tái hiện trước/sau ở `before.xml` và `after-r2.xml`. Hồi quy và video được báo riêng trong `KET_QUA.md` khi hoàn tất; không lấy số test lịch sử để chứng nhận lần sửa này.

## Mục cũ đã có, hoặc đã thay đổi chủ ý

| Mục trong bàn giao cũ | Tình trạng đối chiếu code hiện tại |
|---|---|
| Lính ngủ/thức C3 | Có `Map01C3Sleeper`; `MakeReserve` gắn phần hiển thị ngủ, `Wake` phát thức rồi đưa lính ra nhiệm vụ. Có lưu/tải riêng. |
| Đòn dao trên giường | Có `Map01C3BedTakedown`, yêu cầu dao và không dùng đạn. Không còn đòn lục cạnh giường. |
| K54/Mosin | Có cầm/ngắm/bắn/nạp; K54 thường theo quyết định bỏ giảm thanh; Mosin không tự lộ Nam từ phát đạn xa ngoài khả năng nghe/nhìn. |
| Mở đầu Map 1 | Chờ gặp chỉ huy và bấm E, theo lựa chọn người dùng. Test cứu Hùng cần chuẩn bị trạng thái đã nhận lệnh khi kiểm phần sau mở đầu. |
| Ba bản thu đổi lời và âm kẻng C3 | Các resource hiện có; catalog hiện không còn cờ `pendingVoice`. `RingKieng` phát clip thực, không chỉ phát tín hiệu AI. |
| Hùng đi bị thương | `Docs/HungVisual.md` ngày 19/10 giữ `Hung_DiKhapKhieng`, tốc độ 1,8 m/s và tự đi về nơi trú. Không coi bản Mixamo ôm tay chưa nhập là lỗi dang dở. |
| Phát hiện xác, giai đoạn 1 | Có `Map01BodyDiscovery`: thị giác/vật cản/khói/bụi, phản ứng riêng từng nhóm và lưu trạng thái. Các chi tiết còn thiếu ở giai đoạn 2 ghi bên dưới. |

## Còn là phần việc riêng, không tự thêm thiết kế

| Phần | Bằng chứng và giới hạn |
|---|---|
| Lính quỳ xem xác | Handoff có nguồn Blender và đề xuất chuỗi quỳ; runtime hiện gọi `InspectBody` để điều tra, chưa nối chuỗi quỳ. Cần kiểm bản Blender và phần duyệt trước khi nhập clip/IK. |
| Kéo/vác xác; hạ lính đang quỳ bằng cặp clip riêng | Là cơ chế/animation riêng trong handoff, chưa có đủ luật và bản duyệt trực tiếp trong lượt rà gameplay này. Không gộp với đòn dao trên giường đã có. |
| Tag từng lính bằng ống nhòm | Trinh sát trại và minimap đã có; chưa thấy trạng thái tag thủ công từng lính cùng thời hạn/lưu tải. Cần chốt thiết kế. |
| Polish toàn bộ bộ động tác K54/dao bằng Mixamo | Demo/tham khảo không đồng nghĩa mọi clip đã được thay trong game. Lượt này không đổi rig hay nhập animation mới. |
| Benchmark 1080p/60 FPS | Chưa có phép đo Player build mới trong lượt này. Video ghi ở 60 khung/s chỉ chứng minh hành vi, không chứng nhận FPS phần cứng. |
| Backend/cache; công dụng `scope_lens` | Bootstrap vẫn có fallback offline; `scope_lens` còn là vật liệu dự trữ. Không tự thiết kế backend/recipe khi rà Map 1. |

## Nhóm cần tái kiểm

Mười hai ca cứu Hùng/đá/HUD từng được liệt kê lỗi trong báo cáo cũ đang được chọn để chạy lại. Phải phân biệt lỗi runtime, fixture chưa theo luồng E, và kiểm tra hình ảnh lỗi thời. Không hạ tiêu chuẩn va chạm/tiếp xúc hoặc sửa luật game chỉ để test xanh.

Không sửa scene/model/controller, không commit/push, không lấy thay đổi từ `origin/dev2`.

## Vị trí code dùng để đối chiếu

Các đường dẫn dưới đây tính từ gốc repo, số dòng tại lần rà này:

- `Assets/_Project/Map01/Runtime/Map01Mission.cs:321`: lời nói có/không có bản thu.
- `Assets/_Project/Map01/Runtime/ForestSettings.cs:20`, `Assets/_Project/Map01/Map01Balance.json:7`, `Assets/_Project/Map01/Runtime/Map01EnemyController.cs:989`: hệ số cảnh giác.
- `Assets/_Project/Map01/Runtime/Map01C3Mission.cs:426`: đi bộ khi điểm danh thiếu; `:784`: khôi phục cảnh giác lúc tải.
- `Assets/_Project/Map01/Runtime/Map01C3Mission.cs:208` và `Assets/_Project/Map01/Runtime/Map01C3Sleeper.cs:40`: tạo hiển thị ngủ; `Map01C3Sleeper.cs:69`: thức dậy.
- `Assets/_Project/Map01/Runtime/Map01C3BedTakedown.cs:45`, `:104`: kiểm dao và thực hiện đòn.
- `Assets/_Project/Map01/Runtime/Map01Pistol.cs:84`, `Assets/_Project/Map01/Runtime/Map01Mosin.cs:40`: nạp K54/Mosin; `Assets/_Project/Scripts/Gameplay/Combat/PlayerCombat.cs:709`: chuột phải bật/tắt ngắm.
- `Assets/_Project/Map01/Runtime/Map01EnemyController.cs:1094`: nguồn sát thương chưa được nhận biết không tự cung cấp vị trí Nam.
- `Assets/_Project/Map01/Runtime/Map01Story.cs:146`, `Assets/_Project/Map01/Runtime/Map01Quest.cs:112`: nhận lệnh E.
- `Assets/_Project/Map01/Runtime/Map01C3Mission.cs:449`: kẻng; catalog thoại ở `Assets/Resources/Dialogue/voice-catalog.json`.
- `Docs/HungVisual.md:3`: quyết định giữ dáng khập khiễng ngày 19/10.
- `Assets/_Project/Map01/Runtime/Map01EnemyController.cs:247`: `InspectBody`, hiện chưa có bước quỳ.
- `Assets/_Project/Map01/Runtime/ForestInventory.cs:103`: công dụng dự trữ của scope_lens; `Assets/_Project/Scripts/Bootstrap/GameBootstrap.cs:33`: TODO backend/cache.
