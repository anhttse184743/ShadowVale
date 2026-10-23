# Menu ShadowVale

## Chạy

Nhấn Play từ bất kỳ scene nào: Editor tự bắt đầu bằng `00_Boot` rồi vào menu chính.
Scene đang chỉnh sửa được giữ nguyên và trở lại sau khi Stop. Cấu hình được áp dụng
khi Unity nạp script; có thể áp dụng lại bằng **ShadowVale → Menu → Luôn bắt đầu từ menu chính**.
Hoặc chọn menu Unity **ShadowVale → Menu → Mở menu chính**, rồi nhấn Play và xem tab **Game**.
Hai scene có object `ShadowVale Menu` gắn sẵn; giao diện chỉ vẽ khi chạy.
Nếu nhấn Play trong scene trống/chưa lưu hoặc scene chưa có `ForestMission`, menu chính
vẫn xuất hiện thay vì chỉ có skybox. Trong Editor, menu cũng được phục hồi sau khi reload script lúc Play.
Boot tải content rồi chuyển sang menu; Chơi mới mở `Map 1` từ nhánh dev.
Mỗi bản lưu mới giữ tên scene để tải đúng bản đồ. Bản lưu cũ chưa có tên scene
vẫn mở `Map01_ForestFootprints`; cả hai scene được giữ trong Build Settings.
Chọn Chơi mới/Tiếp tục/Tải game để vào map; trong màn chơi nhấn Esc để mở menu tạm dừng.

- Chuột hoặc mũi tên lên/xuống + Enter chọn chức năng và ô lưu.
- Esc quay lại/tiếp tục; Delete yêu cầu xác nhận xóa ô đang chọn.
- F5 lưu nhanh, F9 tải nhanh khi menu đóng.
- Ba ô lưu thủ công, một ô lưu nhanh và **một ô tự lưu khi thoát riêng biệt**. Tiếp tục chọn bản lưu hợp lệ mới nhất.
- Lưu từ menu tạm dừng chỉ khi an toàn và không đang chế tạo. Ô lưu nhanh dành cho F5.
- Để lưu backup: Esc → Lưu game → chọn Ô lưu 01–03 → Lưu vào ô này.
  Bấm vào hàng chỉ chọn ô. Nếu ô đã có dữ liệu, bấm Xác nhận hoặc Enter để ghi đè.
  Giao diện báo rõ lý do nếu chưa thể lưu (lính cảnh giác/ở gần, đang chế tạo, đã chết…).
- Ghi đè, xóa, tải khi đang chơi, về menu và thoát khi đang chơi có xác nhận.
- Về menu chính hoặc thoát game sẽ tự lưu, kể cả giữa giao chiến/chế tạo. Đóng cửa sổ/Alt+F4
  trong bản game cũng yêu cầu tự lưu trước khi đóng. Không ghi đè ô thủ công hoặc ô F5.
- Nếu ghi file thất bại, giữ màn chơi và hiện lỗi. Khi nhân vật đã chết, giữ bản tự lưu trước đó.
- Nút Stop của Unity Editor không phải thao tác thoát trong game; dùng nút Menu chính/Thoát game để thử tự lưu.
- Không thêm cài đặt, âm thanh hoặc tham chiếu chiến dịch Ấp Bắc.

## Dữ liệu

`Application.persistentDataPath/shadowvale-slot-{0..4}.json`: envelope version 1 chứa ngày UTC,
thời gian chơi, tên điểm lưu, checkpoint và thumbnail JPEG base64. Ghi file tạm rồi thay file đích.
Checkpoint cũ `shadowvale-map01-checkpoint.json` vẫn có thể tải qua F9 nếu chưa có ô lưu nhanh mới.
Ảnh thumbnail chụp từ camera gameplay, không chứa menu. Ngày lưu hiển thị theo giờ máy người chơi.

Ô 0 dành cho F5/F9, ô 1–3 là backup thủ công, ô 4 chỉ dành cho tự lưu khi thoát.
Checkpoint payload version 2 giữ vị trí/hướng Nam và Hùng, HP, stamina, vật phẩm, tiến trình,
điểm loot, trạng thái chế tạo còn lại, cùng vị trí/HP/trạng thái cảnh giác và chiến đấu của lính.
Bản version 1 vẫn tải được, nhưng lính còn sống trong bản cũ bắt đầu lại tuyến tuần tra.
Không tự lưu khi tải bản khác hoặc nhấn Chơi mới. Tắt cưỡng bức tiến trình/mất điện không
đảm bảo có cơ hội thực hiện callback tự lưu. Đây vẫn là persistence cho prototype Map 1.

## Cấu trúc

- `ForestMenu`: gắn sẵn trong scene Boot/Menu, tự cài bổ sung khi chạy trực tiếp Map 1,
  tồn tại qua chuyển scene và tự loại bỏ bản trùng.
- `ForestSaveSlots`: file lưu, kiểm tra envelope và tìm ô gần nhất.
- `ForestMission`: chụp/khôi phục checkpoint, pause và thumbnail.
- `Checkpoint.cs`: phần lưu/tải của `ForestMission`, giữ ô backup, F5 và tự lưu riêng biệt.
- `ForestMissionGameplay.cs`: nối nhiệm vụ, HUD và checkpoint với PlayerController/PlayerCombat
  mới trong Map 1. `Player.cs`/`Combat.cs` vẫn phục vụ map cũ; không chạy song song hai bộ điều khiển.
- Checkpoint v2 bổ sung vũ khí đang cầm, thời gian hồi đòn, HP/vị trí/cảnh giác của
  Map01EnemyController. Bản lưu trước khi chuyển nhân vật vẫn giữ lính đã bị hạ và điểm loot.
- Giao diện IMGUI đồng bộ với prototype hiện tại; bố cục 1600×900 tự scale/letterbox.
- Chữ và nút là UI thật. Nền là ảnh 2D tĩnh, không phải nhân vật 3D tương tác.
- Font stencil đóng gói cùng game, logo alpha và bảng kim loại 9-slice: xem [MenuArt.md](MenuArt.md).

## Nguồn nền

`Assets/_Project/Map01/Resources/Menu/Background.png` được tạo bằng built-in imagegen.
Prompt: Create a 16:9 background art asset for SHADOWVALE retro 2D PC game main menu.
NO TEXT NO LETTERS NO UI NO BUTTONS NO LOGOS anywhere. Hand painted late 1990s strategy
game bitmap aesthetic, slightly pixelated earthy olive and parchment palette. Flat rural
Vietnamese rice fields and irrigation canal, banana leaves, palms and thatched hut on far
right. Right third full body Vietnamese young adult soldier in dark olive long sleeve
uniform with two chest pockets, belt rectangular buckle, loose green trousers, green shoes,
round green mesh covered helmet, standing relaxed hands at sides by wooden crates. Left
half quiet open landscape with sky and rice paddies for overlay menu. No mountains, modern
gear, dates, insignia or campaign reference. Match approved rustic 2D painted game aesthetic
rather than photorealism. No text at all.

## Kiểm tra

Sửa khởi động từ scene đang chỉnh sửa: **18/18 đạt** (`Logs/Menu-Startup-Fix.xml`).
Test mở Map 1, nhấn Play qua Boot → menu, chọn Chơi mới → Map 1, rồi Stop
và xác nhận scene chỉnh sửa được khôi phục. Các test gameplay dùng scene chỉ định riêng.

Unity Test Runner / EditMode / `ShadowVale.Map01.Tests`: gồm kiểm tra dữ liệu lưu và luồng
menu → chơi mới → pause → ghi/ghi đè ô lưu → tải lại → xóa. Test tích hợp dùng thư mục
tạm riêng, không ghi vào các bản lưu của người chơi.
Lượt kiểm tra tính năng tự lưu: **13/13 đạt** (`Logs/Menu-AutoSave-Final.xml`).
Test xác minh tự lưu giữa giao chiến/chế tạo, giữ nguyên nội dung ô thủ công và F5,
khôi phục trạng thái lính, không ghi đè khi chết, và chặn thoát khi không ghi được file.
Các bài test startup mở trực tiếp Boot hoặc MainMenu trước khi vào Play và kiểm tra chỉ có một menu.
Lệnh **ShadowVale → Menu → Ghi trạng thái chẩn đoán** ghi scene đang mở và trạng thái menu
vào `Logs/Menu-EditorStatus.txt` khi cần kiểm tra màn hình trống.
