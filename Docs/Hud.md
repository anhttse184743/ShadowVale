# HUD và túi đồ

Nhấn Play → menu chính → Chơi mới. Kể cả đang mở `Map 1`, Play vẫn vào menu trước. HUD cũng hoạt động
trong map cũ khi tải checkpoint. Không cần gắn thêm component vào scene:
`ForestMission` vẽ HUD từ các phần `ForestMissionHud` và `ForestMissionInventory`.

## Thao tác

- Tab: mở/đóng túi đồ. Esc đóng túi/bản đồ trước, lần tiếp theo mở menu tạm dừng.
- Túi đồ lớn bên phải có 6 cột × 4 hàng nhìn thấy. Cuộn chuột hoặc kéo thanh cuộn
  để xem thêm stack; 24 ô không phải giới hạn tổng sức chứa.
- Bấm ô để xem tên, mô tả, tổng số lượng và giới hạn mỗi stack.
- Không có thanh dùng nhanh/ô gán phím: mỗi vật phẩm có phím riêng. H dùng băng cứu thương,
  Q ném đá, 1 súng trường, 2 dao, 3 tay không (hoặc chọn vũ khí trong túi rồi bấm Trang bị).
  Phím 4–8 không gắn chức năng. C hoặc Ctrl bật/tắt đi khom; Shift bật/tắt chạy.
  Đạn súng lấy từ túi đồ; thanh máu và băng cứu thương dùng cùng Health của nhân vật.
- Máu đầy, hết vật phẩm, đang chế tạo, chết hoặc tạm dừng không tiêu hao băng.
  Đóng túi để ngắm và ném đá. Nguyên liệu, đạn và đồ nhiệm vụ không dùng trực tiếp.
- Mở túi vẫn giữ quy tắc gameplay hiện có: nhân vật dừng di chuyển, thế giới không tạm dừng.

## Dữ liệu và lưu game

Giới hạn stack đọc từ `contentBundle.items[].stack_max`, không lặp lại bảng giới hạn
trong UI. Đá ném dùng giới hạn 20; hàng tiếp tế và tài liệu nhiệm vụ dùng 1/ô.
125 viên đạn súng trường hiển thị thành 90 + 35. UI không cấp thêm đồ cho người chơi.

Checkpoint không còn lưu ô nhanh. Bản lưu cũ có trường `quickSlots` vẫn mở được:
JsonUtility bỏ qua trường thừa. Cả lưu thủ công, F5 và tự lưu khi thoát đều dùng chung dữ liệu này.

## Hình ảnh

- `Assets/_Project/Map01/Resources/Hud/Items.png`: bộ 14 icon đã được người dùng duyệt,
  phiên bản đạn súng trường trong hộp và đạn bắn tỉa trong bao vải. Code dùng UV để lấy
  phần hình từng vật phẩm, không dùng chữ/số đã vẽ trong ảnh làm dữ liệu runtime.
- `HealthFrame.png`: PNG alpha khung mũ lính. Phần ruột thanh máu và chữ HP được vẽ
  động, không ghép một thanh máu cố định vào màn chơi.
- `Panel.png`: khung vuông kim loại/canvas riêng cho HUD, vẽ 9-slice và lặp vùng vải
  giữa để không kéo giãn vân trên bảng lớn. Font Black Ops One dùng chung với menu.

Ảnh UI được tạo bằng built-in **imagegen**, không dùng CLI/API key. Ảnh demo được duyệt
chỉ là tham chiếu bố cục; mọi số lượng và HP trên giao diện chạy thật đến từ mission.

### Prompt tạo HealthFrame.png

Create a production-ready transparent PNG GAME HUD FRAME based on the helmet health bar in reference. Single isolated horizontal frame, landscape aspect 3:1, fills most canvas with 3% transparent padding. Authentic alpha transparency outside artwork, no backdrop, no text, no letters, no numbers, no scene. Preserve dark olive mesh-covered military helmet on LEFT overlapping a long thin rectangular weathered metal bar, brass small rivets, worn khaki canvas wrapping near both ends, hand-painted retro 2D military game bitmap style. A small plain metal nameplate hangs below middle of bar, NO TEXT. IMPORTANT the entire long rectangular interior of health bar must be EMPTY transparent alpha, no red fill, no black fill; will be filled dynamically in game. The outer worn metal border is straight horizontal, no perspective. Helmet should occupy leftmost 20% width and about 80% image height; health bar spans from x18% to x96%, y32% to y62%; blank nameplate x40%..80%, y65%..82%. No dragons, modern tech, extra emblems or logos. Crisp clean silhouette and continuous rectangular empty fill aperture. One reusable HUD asset only.

### Prompt tạo Panel.png

Extract and recreate a reusable blank UI panel texture inspired by the inventory panel in reference. Output one perfectly front-facing square game UI panel, exactly fills canvas edge-to-edge, no external margin or shadow beyond edge. NO text, NO letters, NO icons, NO slots, NO images or scenery. Thin continuous old dark brass metal frame all four sides, small rounded rivet in each corner. Frame width about 2% image width. Inside frame a uniform very dark desaturated olive canvas texture with subtle fine natural grain and worn paint, evenly lit, no prominent stains, no diagonal bands, no scratches longer than 2% width. Texture contrast low so text readable on top. Border modestly scuffed dull brass, not bright gold, gentle metallic highlights. Match vintage military inventory reference. Canvas texture must stay finely detailed in a large panel. This is a production game UI blank panel texture, straight orthographic view, no perspective, no modern gradients, no additional decorations.

## Kiểm thử

`ForestHudTests`: chia stack, bảo toàn số lượng, không tiêu hao khi máu đầy/tạm dừng,
ném đá, khôi phục kho sau lưu/tải, tồn kho vượt 24 ô; phím 1/2/3 đổi vũ khí, H dùng băng,
phím 1–5 không còn tác dụng (`ItemsUseTheirOwnKeysWithNoShortcutBar`).

Test ảnh dùng kho đồ được điền riêng trong môi trường QA để nhìn đủ icon;
không chèn những vật phẩm/số lượng này vào scene hoặc dữ liệu chơi thật.
Ảnh chụp Unity ở 1600×900 và 1280×720 nằm trong `Docs/Previews/Hud`.

Kết quả: toàn bộ Map01 **17/17 đạt** (`Logs/Hud-Final.xml`). Sau khi bổ sung
kiểm tra thả đúng ô/thả ra ngoài, nhóm HUD **2/2 đạt**
(`Logs/Hud-Interaction-Final.xml`). Không kiểm thử bằng dữ liệu lưu của người chơi.

## Trang bị khởi hành

- Súng trường (rifle_standard) và dao (knife): mỗi món x1 khi chơi mới, tối đa 1/ô.
- Tab → chọn món → Trang bị; nhãn Đang cầm đồng bộ với PlayerCombat. Phím 1/2/3 đổi súng/dao/tay không.
- Bản lưu v4 trước khi thêm item được bổ sung khóa còn thiếu, không nhân đôi số lượng đã lưu.
- Hud/StartingWeapons.png giữ ảnh mẫu đã duyệt; HUD dùng UV lấy hai icon nhỏ không có chữ, số lượng được vẽ từ dữ liệu túi.
## Minimap

- Góc trái trên: bản đồ hướng Bắc cố định, vùng nhìn 90 m, tự di chuyển theo Nam.
- M mở bản đồ toàn khu vực từ cùng dữ liệu địa hình; Esc hoặc M đóng. Ẩn minimap khi mở túi hoặc menu.
- Mũi tên trắng là Nam; chấm xanh là Hùng; hình thoi vàng đọc mục tiêu và khoảng cách từ Map01ObjectiveGuide.
- Tam giác đỏ chỉ hiện lính còn sống trong 30 m, trong góc nhìn camera và không bị vật cản che; kiểm tra 4 lần/giây.
- Map01Minimap chụp scene từ trên cao một lần khi vào màn, chuyển sang tông giấy ô-liu. Không dùng địa hình tưởng tượng từ ảnh mẫu. Không chạy thêm camera render mỗi frame.
- Ảnh nền không phản ánh thay đổi địa hình sau khi vào màn. Chưa có hệ thống lưu vùng đã khám phá/fog of war.

## HUD chiến đấu tối giản

- Góc dưới phải: giọt HP trắng ngà không viền, phần mất máu tối đi, kèm HP hiện tại/tối đa. Hình giọt được tạo bằng code và cắt từ dưới lên theo HP thật.
- Icon súng/dao/tay không đọc từ Hud/CombatIcons.png; phím 1/2/3 đổi trang bị, icon đang cầm sáng hơn. Atlas được tạo bằng công cụ imagegen tích hợp; yêu cầu tạo silhouette súng, dao và nắm tay màu ngà trên nền trong suốt. Phần giọt đỏ trong atlas không được sử dụng.
- Số đạn hiện "trong băng / dự trữ trong ba lô" (R nạp đạn); lúc đang nạp thì hiện % tiến độ.
- Không có thanh dùng nhanh, kể cả trong túi đồ; dùng H/Q và 1/2/3 (thanh vũ khí mặc định WeaponHotbar được giấu trong Map 1).

## Ném đá đánh lạc hướng (Map01StoneThrow)

- Giữ Q để ngắm: đường cong quỹ đạo và vòng tròn chỗ đá rơi (tối đa 20 m, cắt ngắn nếu vướng cây/nhà).
- Mỗi lính Nam nhìn thấy trong 40 m hiện "vùng nghe" bán kính `stoneNoise` (14 m) quanh chân; vòng sáng màu cam nếu chỗ đá rơi nằm trong vùng đó. Bảng dưới màn hình báo bao nhiêu lính sẽ nghe thấy.
- Thả Q để ném, chuột phải để cất. Đá bay theo đúng quỹ đạo; lúc chạm đất mới phát tiếng động.
- Lính nghe thấy chạy tới chỗ đá rơi, đứng nhìn quanh `searchSeconds` (8 giây), rồi quay về đúng chỗ cũ (vị trí gác hoặc điểm đang tuần). Mất dấu Nam cũng vậy: tới chỗ thấy Nam lần cuối, tìm quanh rồi về.
- Hạ gục bằng dao: đâm từ phía sau một lính chưa phát hiện Nam (kể cả khi hắn đang đi kiểm tra tiếng đá) là hạ ngay, không gây tiếng động. Đi khom thì lính chỉ cảm nhận Nam sát sau lưng trong 1,2 m (đứng thẳng: 2,5 m), đủ để áp sát trong tầm dao (2 m). Đâm từ phía trước hoặc khi hắn đã phát hiện chỉ là một nhát thường, và hắn quay sang đánh Nam.
- Trong nhiệm vụ trinh sát: hạ gục lặng lẽ bằng dao một lính đã bị dụ ra xa tâm doanh trại từ 15 m trở lên (`quietKillDistance`) thì không bị lộ; hạ lính ngay trong trại, đâm trực diện hay nổ súng vẫn làm trinh sát thất bại. Kiểm thử: `ScoutingTests.AQuietKnifeTakedownAwayFromTheCampGoesUnnoticed`.
- Kiểm thử: `StoneThrowTests` (ngắm/huỷ, lính đi kiểm tra rồi về chỗ cũ, hạ gục bằng dao qua input chuột thật).

## Trinh sát thất bại

- Bị lính doanh trại nhìn thấy, tấn công lính doanh trại, hoặc hạ lính ngay trong trại: game dừng và hiện bảng "BẠN ĐÃ BỊ PHÁT HIỆN — Nhiệm vụ trinh sát thất bại" kèm lý do.
- Enter: làm lại từ đúng lúc Hùng giao nhiệm vụ. Lúc Hùng giao lệnh, game chụp một checkpoint ẩn (`Map01SaveSystem.MarkScoutingStart`); làm lại là tải lại checkpoint đó: vị trí Nam, túi đồ, máu, lính và các trại về như lúc nhận lệnh, Hùng nhắc lại lời giao nhiệm vụ. F9 tải bản lưu, Esc về menu.
- Bản lưu tạo trong lúc trinh sát mang theo checkpoint đó (`scoutStart`), nên tải lại rồi thất bại vẫn quay về đúng lúc nhận lệnh. Bản lưu cũ không có checkpoint này thì làm lại tại chỗ: trại được tăng cường, mất hết ghi chép, Nam về cạnh Hùng.
- Kiểm thử: `ScoutingTests.ScoutingFailureStartsOverFromHungsOrder`, `BeingSpottedOrAttackingACampFailsTheRun`.
