# Map 1 — Những dấu chân trong rừng

Scene: `Assets/_Project/Scenes/Maps/Map01_ForestFootprints.unity`.

Thiết kế theo kịch bản rừng mới nhất của người dùng, thay cho Map 1 làng quê trong GDD cũ. Đây là bản blockout có logic chơi thử, phục vụ duyệt bố cục trước khi thêm character asset, animation và art cuối. Chỉ phạm vi Map 1 offline; chưa tích hợp web, telemetry, QUBO server, các map tiếp theo hoặc menu chính.

## Mở và chạy

Mở scene trên bằng Unity **6000.3.24f1** rồi Play. Scene nguồn có sẵn hình học, collision, điểm tương tác và actor trong Hierarchy. `ForestNavigation` tạo NavMesh lúc vào Play, trước khi kích hoạt agent. Không cần backend. Scene `Map 1.unity` cũ được giữ nguyên.

Để tạo scene bằng Unity CLI, bake NavMesh vào asset, tạo prefab nhân vật placeholder và chụp preview:

```powershell
& .\Tools\Build-Map01.ps1 -RunTests
```

Đóng dự án trong Unity trước khi chạy CLI. Hoặc chọn `ShadowVale > Map 1 > Build Forest Scene` trong Editor. Builder thay scene do nó quản lý, lưu bản sao trước đó trong `Tools/Map01Backups`; nên commit scene đã chỉnh bằng tay trước khi rebuild. Không dùng rebuild sau khi thay art nếu muốn giữ các chỉnh sửa thủ công: bake NavMesh bằng Inspector của NavMeshSurface thay thế.

## Bố cục

Vùng chơi 90 × 148 m, trục Z hướng bắc; camera orthographic (30°, 45°, 0°).

| Khu | Vị trí gần đúng X/Z | Mục đích |
| --- | --- | --- |
| Nhận hàng | 0 / -49 | Nam và Hùng, mái trú, hàng tiếp tế, hướng dẫn WASD và stamina |
| Hướng dẫn loot | 3 / -37 | Vải, thảo dược, đạn; vật chắn thử nghiệm ở phía tây |
| Ngã ba | 0 / -25 | Người chơi chọn cách vượt đội tuần tra |
| Tuần tra | 0 / 0 | Ba lính, các vòng patrol, xe bỏ lại, đá và bao chắn |
| Lối tây | -24 / 8 | Lối vòng và bụi rậm; đi khom trong bụi giảm tầm phát hiện |
| Lối đông | 23 / 9 | Lối vòng, chỗ ném đá dẫn lính khỏi đường; cache bổ sung |
| Điểm nghỉ | -3 / 36 | Bàn chế tạo, vòng lửa nguội, vật tư y tế |
| Căn cứ cũ | 0 / 58 | Tường vỡ, chòi canh, kho trống, phòng chỉ huy mở mái |
| Bàn tài liệu | -5 / 61 | Bản đồ địa hình, tuyến bến sông và tuyến bí mật của đơn vị |
| Bến sông | 27 / 80 | Giao hàng và rút cùng Hùng, hoàn tất Map 1 |

Lối tây và đông nối lại ở phía bắc. Không có cổng buộc phải giết lính. Căn cứ có lối vào phía nam và lối thoát đông bắc. Các vùng nghỉ có khoảng trống, các tuyến đi giữ khoảng hở để camera không bị tán cây che kín.

## Flow hiện có

1. Nhận kiện hàng cạnh Hùng bằng E.
2. Vượt khu tuần tra bằng stealth, đánh lạc hướng hoặc combat. Khi người chơi qua Z > 27, nhiệm vụ chuyển sang khám xét căn cứ.
3. Tương tác bàn bản đồ. Nam nhận ra cả tuyến đường bí mật cũng có trong ghi chép, Hùng đề nghị mang về.
4. Tương tác bến sông khi Hùng đã theo kịp. Có màn hoàn thành và lựa chọn chơi lại.

Nếu có giao chiến, Hùng nói: “Bọn này hôm nay phản ứng nhanh hơn bình thường.” Nhánh vượt lén có câu thoại riêng, không giả định một trận đánh đã diễn ra.

Lính có chuỗi `Patrol → Investigate → SpotPlayer → TakeCover → Engage`, suy giảm cảnh giác và trở lại patrol khi không còn thấy Nam. Tiếng động chỉ tác động trong phạm vi nghe; vật cản chặn line of sight và tia bắn. UI hiển thị state/suspicion để duyệt thiết kế. Đây là FSM cục bộ của bản Map 1, chưa phải squad solver của dự án.

## Điều khiển

| Phím | Hành động |
| --- | --- |
| WASD | Di chuyển theo camera isometric |
| Shift | Chạy, tiêu hao stamina và gây tiếng động |
| C | Bật/tắt đi khom; bụi rậm giảm tầm phát hiện |
| Chuột | Ngắm; chuột trái bắn |
| Q | Ném đá về con trỏ, giới hạn khoảng cách và số đá |
| E | Nhận hàng, nhặt loot, đọc tài liệu, rời map |
| Tab / M | Túi đồ / bản đồ nhiệm vụ; không dừng combat |
| B | Chế tạo băng cứu thương khi đứng cạnh bàn, cần 2 vải + 1 thảo dược |
| H | Dùng băng cứu thương |
| F5 / F9 | Lưu / tải checkpoint local |
| Esc / Enter | Tạm dừng / chơi lại khi chết hoặc hoàn thành |

Cover là vật cản thật: đứng phía khuất vật chắn để cắt đường ngắm. Chưa có snap-to-cover hoặc animation áp lưng. Hùng đi theo bằng NavMesh, không bị địch nhắm và không làm lộ người chơi trong prototype này.

## Asset và số liệu

- Thay model con bên dưới `VisualRoot` của Nam, Hùng và các Patrol. Giữ root, CharacterController/NavMeshAgent và các script. Scene builder sinh prefab placeholder khi chạy được trong Unity.
- `Map01Balance.json` giữ chỉ số riêng cho tutorial. Sát thương, tốc độ bắn, tầm nhìn và công thức được đọc từ `StreamingAssets/Content/fallback_bundle.json` hiện có.
- Checkpoint nằm tại `Application.persistentDataPath/shadowvale-map01-checkpoint.json`, lưu vật tư, objective, HP/stamina, vị trí Nam/Hùng, loot đã lấy và lính đã hạ. Khi tải, lính còn sống bắt đầu lại patrol; đây không phải bản lưu chính xác mọi state AI.
- Chưa có animation, SFX/voice, art cuối, kéo thả inventory, nhiều vũ khí, skill progression hay combat của Hùng. Đây là các phần tiếp theo sau khi duyệt map.

## Kiểm tra đã thực hiện và giới hạn

- Biên dịch runtime, editor builder và mã test bằng Roslyn cùng các DLL của Unity 6000.3.24f1: thành công.
- Kiểm tra scene nguồn: 6.933 đối tượng serialized có ID duy nhất, tham chiếu nội bộ đầy đủ.
- Kiểm tra 24 đoạn đường/điểm tiếp cận bằng lưới collision 2D 0,5 m, có khoảng hở cho nhân vật: thành công. Đây là kiểm tra tĩnh, không thay thế NavMesh.
- Hai Unity integration test được chuẩn bị: hoàn thành không giao chiến; tiếng động theo khoảng cách và loot không nhân đôi. **Chưa chạy**, vì Unity CLI trong sandbox trả exit code 198: `No valid Unity Editor license found`.
- **Chưa xác nhận runtime bake, Play Mode hoặc hình ảnh trong Unity.** Cần mở dự án bằng tài khoản Windows có license Unity để hoàn tất kiểm thử này. Không coi compile/static checks là game đã hoàn thiện.

`Tools/Map01Reports/static-validation.txt` chứa kết quả tĩnh. `Tools/Map01Reports/source-layout.json` mô tả snapshot scene nguồn trước các chỉnh sửa trong Editor. Sau khi rebuild trong Unity, dùng `Validate Forest Routes` để kiểm tra NavMesh thực tế, không dùng snapshot cũ để suy ra đường đi của scene mới.
