# Map 1

`Assets/_Project/Scenes/Maps/Map 1.unity` là Map 1 của game (menu → Chơi mới / Tiếp tục, video intro
vẫn chạy trước khi vào map). Bố cục: đồi, sông uốn qua giữa với ba cầu, hầm căn cứ A ở Tây Nam,
bến B ở phía Bắc, ba doanh trại gọn (khoảng 12 × 10 m) đều ở bờ Đông: C1 phía Nam gần cầu Nam, C2 giữa, C3 Đông Bắc.

Gameplay: nhiệm vụ, cốt truyện và lời thoại, Hùng, tổ 4 lính ở bến, lính 3 doanh trại, trinh sát bằng
ống nhòm, ném đá, dao, HUD, bản đồ nhỏ, lưu game. Bố cục và vị trí được sửa trực tiếp trong scene này;
NavMesh nằm ở `Scenes/Maps/Map 1/NavMesh.asset` (bake lại sau khi di chuyển nhà, trại, cây).

## Vị trí

- **Căn cứ A** (`A — underground friendly shelter`): Nam bắt đầu trong hầm cạnh bàn họp. Thùng thảo dược
  (`tutorial_loot`) ở đống thùng cạnh cửa, điểm giao hàng tiếp tế (`supplies`) ở kệ trang bị, bàn chế tạo ở bàn họp.
- **Bến B** (`B_HungCaptive`): Hùng bị giữ trên sàn bến. Tổ 4 lính: 1 canh Hùng trên sàn, 1 ở chân dốc
  lên bến, 2 tuần tra dọc bờ. [E] cạnh Hùng chữa bằng thảo dược, hoặc bằng băng cứu thương nếu thảo dược
  đã dùng hết khi chế tạo (thùng cho 2 thảo dược, mỗi băng tốn 1).
- **Trại C1–C3** (`Enemy outpost 1/2/3`, giữ nguyên tên để trinh sát nhận diện): C1 (40, −80), C2 (65, −13),
  C3 (68, 58). Mỗi trại 2 lính tuần vòng ngắn cạnh lều tiếp tế; C2 và C3 có thêm 1 lính gác cổng (`rbl_east_2/3`).
  Lính trại nhìn 14 m. Cả ba trại cách đường đi cứu Hùng (bờ Tây) từ 77 m trở lên.
- Không lính trại nào được đứng hay tuần tra trong tầm nhìn (+2 m) của đường đi cứu Hùng mà la bàn chỉ
  (RescueTests kiểm tra). Tới cách một trại còn lính dưới 40 m, HUD hiện cảnh báo dưới la bàn; dưới khoảng 22 m
  cảnh báo chuyển đỏ, nhấp nháy và nhắc khom người, nấp sau bụi.
- **Dời trại:** kéo `Enemy outpost N` tới chỗ mới trong Map 1, rồi chọn **ShadowVale > Map 1 > Đặt lại doanh trại theo
  vị trí hiện tại**: san phẳng đất dưới trại (lưu ở `Scenes/Maps/Map 1/Terrain.asset`), dọn cây và điểm nấp trong
  10 m, xếp lại lính, dời mốc `Cn_MissionAnchor`, bake lại NavMesh và kiểm tra luật đường cứu Hùng.
- Bụi `CoverShrub_A/B` và `TallGrass` là chỗ ẩn nấp (khom người trong bụi) — các điểm `Hide spots • bushes`.

## Địa hình bao quanh

`03 Surroundings • outside the playable map` (dựng bằng **ShadowVale > Map 1 > Dựng địa hình bao quanh**; chạy lại
sẽ thay bản cũ, tài sản ở `Scenes/Maps/Map 1/Surroundings`): vành đất nối liền mép địa hình trong map (trùng từng
đỉnh ở mép), nhô dần thành đồi ra xa 420 m; sông chảy tiếp ra ngoài phía Bắc và phía Nam. Rừng: trong 28 m sát mép là
đúng các prefab cây/bụi của map (845 cái), xa hơn tới 190 m là cây low-poly gộp theo ô 90 m (khoảng 88 nghìn đỉnh).
Chỉ là cảnh: tường vô hình ở mép vẫn giữ Nam trong map, không nằm dưới môi trường bake NavMesh, cây xa không đổ bóng.
Tốn thêm khoảng 2–4 ms khi nhìn ra ngoài (tổng vẫn chỉ 4–6 ms), khoảng 2 ms ở góc nhìn nặng nhất trong rừng.
Cây low-poly ở dải xa trông khác cây trong map khi nhìn gần (khoảng 30–60 m); nới dải prefab rộng hơn sẽ đẹp hơn nhưng nặng hơn.

## Bản lưu

Checkpoint phiên bản 7. Bản lưu làm trên Map 1 cũ (bản đồ trước khi làm lại) không mở được, menu báo
"Bản lưu thuộc Map 1 cũ … — hãy chọn Chơi mới". Map 1 cũ vẫn còn trong lịch sử git nếu cần xem lại.

## Hiệu năng

Cây cối (khoảng 21 nghìn renderer trong LODGroup) chiếm khoảng 85% thời gian vẽ, nên các thiết lập nhắm vào đó:
- Game giới hạn 60 khung hình/giây (`FramePacing`); test tắt giới hạn này.
- Bóng mặt trời 2 tầng, camera nhìn xa 300 m (sương mù phủ kín từ 240 m).
- SSAO nửa độ phân giải, lấy từ độ sâu sau lượt vẽ vật đặc (Source Depth, After Opaque): trước đây nó lấy pháp tuyến
  (Normals), buộc vẽ lại mọi cây thêm một lượt; đổi xong giảm khoảng 33% thời gian vẽ (18,1 → 12,1 ms), hình gần như không đổi.
- Bụi `CoverShrub_A/B` chỉ đổ bóng ở LOD0.
- Cỏ và bụi nhỏ ẩn sớm (với `lodBias` 2): GroundGrass khoảng 35 m, Fern 45 m, TallGrass 50 m,
  CoverShrub khoảng 100 m (nơi sương mù bắt đầu). Trước đó chúng được vẽ tới 115 m–400 m+.
- Đã đo nhưng không áp dụng: GPU occlusion culling và tắt GPU Resident Drawer (sau khi đổi SSAO, cả hai đều chậm hơn
  khoảng 1 ms trên máy thử); vẽ ở 75% độ phân giải (khoảng −16%, hình mềm hơn); bóng cứng/tầm bóng ngắn (chưa đo).

## Giới hạn

Sông sâu khoảng 1,5 m nhưng chưa có bơi: Nam lội qua lòng sông (chậm và ồn hơn).
NavMesh không cho Hùng/lính đi qua lòng sông, chỉ qua ba cầu.
