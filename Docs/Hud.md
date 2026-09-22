# HUD, túi đồ và dùng nhanh

Nhấn Play → menu chính → Chơi mới. Kể cả đang mở `Map 1`, Play vẫn vào menu trước. HUD cũng hoạt động
trong map cũ khi tải checkpoint. Không cần gắn thêm component vào scene:
`ForestMission` vẽ HUD từ các phần `ForestMissionHud` và `ForestMissionInventory`.

## Thao tác

- Tab: mở/đóng túi đồ. Esc đóng túi/bản đồ trước, lần tiếp theo mở menu tạm dừng.
- Túi đồ lớn bên phải có 6 cột × 4 hàng nhìn thấy. Cuộn chuột hoặc kéo thanh cuộn
  để xem thêm stack; 24 ô không phải giới hạn tổng sức chứa.
- Bấm ô để xem tên, mô tả, tổng số lượng và giới hạn mỗi stack.
- Kéo băng cứu thương/đá từ túi xuống thanh nhanh, hoặc chọn vật phẩm rồi nhấn 1–5.
- Khi đóng túi, nhấn 1–5 để dùng ô tương ứng. H và Q vẫn dùng được như trước.
- Map 1 dùng nhân vật/animation mới từ dev: 6 đổi súng trường, 7 dao, 8 tay không.
  Các phím 1–5 vẫn dành riêng cho vật phẩm. C hoặc Ctrl bật/tắt đi khom; Shift bật/tắt chạy.
  Đạn súng lấy từ túi đồ; thanh máu và băng cứu thương dùng cùng Health của nhân vật.
- Chuột phải ô nhanh khi mở túi để bỏ gán. Phím tắt chỉ tham chiếu tổng số đồ
  trong túi, không tạo bản sao và không chuyển đồ ra khỏi túi.
- Máu đầy, hết vật phẩm, đang chế tạo, chết hoặc tạm dừng không tiêu hao băng.
  Đóng túi để ngắm và ném đá. Nguyên liệu, đạn và đồ nhiệm vụ không dùng trực tiếp.
- Mở túi vẫn giữ quy tắc gameplay hiện có: nhân vật dừng di chuyển, thế giới không tạm dừng.

## Dữ liệu và lưu game

Giới hạn stack đọc từ `contentBundle.items[].stack_max`, không lặp lại bảng giới hạn
trong UI. Đá ném dùng giới hạn 20; hàng tiếp tế và tài liệu nhiệm vụ dùng 1/ô.
125 viên đạn súng trường hiển thị thành 90 + 35. UI không cấp thêm đồ cho người chơi.

Các ô nhanh được lưu trong trường `quickSlots` của checkpoint v2. Bản lưu cũ chưa
có trường này vẫn mở được, mặc định ô 1 là băng cứu thương, ô 2 là đá ném.
Cả lưu thủ công, F5 và tự lưu khi thoát đều dùng chung dữ liệu này.

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

`ForestHudTests`: chia stack, bảo toàn số lượng, gán ô nhanh không sao chép đồ,
không tiêu hao khi máu đầy/tạm dừng, ném đá, phím số gán/dùng theo trạng thái túi,
khôi phục cấu hình ô nhanh sau lưu/tải, tương thích bản lưu cũ và tồn kho vượt 24 ô.

Test ảnh dùng kho đồ được điền riêng trong môi trường QA để nhìn đủ icon;
không chèn những vật phẩm/số lượng này vào scene hoặc dữ liệu chơi thật.
Ảnh chụp Unity ở 1600×900 và 1280×720 nằm trong `Docs/Previews/Hud`.

Kết quả: toàn bộ Map01 **17/17 đạt** (`Logs/Hud-Final.xml`). Sau khi bổ sung
kiểm tra thả đúng ô/thả ra ngoài, nhóm HUD **2/2 đạt**
(`Logs/Hud-Interaction-Final.xml`). Không kiểm thử bằng dữ liệu lưu của người chơi.
