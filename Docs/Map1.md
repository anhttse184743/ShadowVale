# Map 1

`Assets/_Project/Scenes/Maps/Map 1.unity` là Map 1 của game (menu → Chơi mới / Tiếp tục, video intro
vẫn chạy trước khi vào map). Bố cục: đồi, sông uốn qua giữa với ba cầu, hầm căn cứ A ở Tây Nam,
bến B ở phía Bắc, ba doanh trại C1 (Tây), C2 (Đông Nam), C3 (Đông Bắc).

Gameplay: nhiệm vụ, cốt truyện và lời thoại, Hùng, tổ 4 lính ở bến, lính 3 doanh trại, trinh sát bằng
ống nhòm, ném đá, dao, HUD, bản đồ nhỏ, lưu game. Bố cục và vị trí được sửa trực tiếp trong scene này;
NavMesh nằm ở `Scenes/Maps/Map 1/NavMesh.asset` (bake lại sau khi di chuyển nhà, trại, cây).

## Vị trí

- **Căn cứ A** (`A — underground friendly shelter`): Nam bắt đầu trong hầm cạnh bàn họp. Thùng thảo dược
  (`tutorial_loot`) ở đống thùng cạnh cửa, điểm giao hàng tiếp tế (`supplies`) ở kệ trang bị, bàn chế tạo ở bàn họp.
- **Bến B** (`B_HungCaptive`): Hùng bị giữ trên sàn bến. Tổ 4 lính: 1 canh Hùng trên sàn, 1 ở chân dốc
  lên bến, 2 tuần tra dọc bờ.
- **Trại C1–C3** (`Enemy outpost 1/2/3`, giữ nguyên tên để trinh sát nhận diện): C1 có 2 lính, C2 và C3
  mỗi trại 3 lính — lính gác hướng về lối vào từ căn cứ, lính tuần vòng trong tường bao cát, lính canh phía
  sau bên bếp lửa.
- Không lính trại nào được đứng hay tuần tra trong tầm nhìn (24 m + 2 m) của đường đi cứu Hùng mà la bàn
  chỉ (RescueTests kiểm tra). C1 chỉ cách đường đó khoảng 30 m nên lính C1 ở phía xa đường.
- Bụi `CoverShrub_A/B` và `TallGrass` là chỗ ẩn nấp (khom người trong bụi) — các điểm `Hide spots • bushes`.

## Bản lưu

Checkpoint phiên bản 7. Bản lưu làm trên Map 1 cũ (bản đồ trước khi làm lại) không mở được, menu báo
"Bản lưu thuộc Map 1 cũ … — hãy chọn Chơi mới". Map 1 cũ vẫn còn trong lịch sử git nếu cần xem lại.

## Giới hạn

Sông sâu khoảng 1,5 m nhưng chưa có bơi: Nam lội qua lòng sông (chậm và ồn hơn).
NavMesh không cho Hùng/lính đi qua lòng sông, chỉ qua ba cầu.
