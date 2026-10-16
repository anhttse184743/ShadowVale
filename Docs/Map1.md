# Map 1

`Assets/_Project/Scenes/Maps/Map 1.unity` là Map 1 của game (menu → Chơi mới / Tiếp tục, cutscene mở đầu chạy trực tiếp trong map). Bố cục: đồi, sông uốn qua giữa với ba cầu, hầm căn cứ A ở Tây Nam,
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

## Đá ném

Nam mang tối đa 15 viên (`stoneCapacity` trong `Map01Balance.json`), bắt đầu với 6. Hết đá thì nhặt thêm bằng [E] ở các
điểm rải trên map (`Map01StoneSites`): nhặt ngay, đứng hay khom đều được, chỉ lấy vừa chỗ trống trong túi, phần còn lại
nằm nguyên tại chỗ. Số viên sỏi nhìn thấy đúng bằng số còn lại. Hiện có 34 điểm:

- **Bãi sỏi bờ sông** (7 điểm, 3 viên): có lại sau 90 giây, nên không bao giờ hết hẳn đá.
- **Chân tảng đá** (13 điểm, 2 viên) và **ven lối đi, cạnh bụi** (14 điểm, 2 viên): nhặt một lần.
- Ngoài ra là 10 đống đá quanh bến B của nhiệm vụ cứu Hùng (`Map01RescueStones`): cũng chỉ lấy vừa chỗ trống
  trong túi; các điểm trên không đặt trong 12 m quanh chúng.

Sỏi nhỏ khó thấy trong rừng nên điểm trong vòng 14 m có một chấm mờ trên màn hình (khi túi còn chỗ); khi chỉ còn từ
2 viên trở xuống, bản đồ nhỏ chấm thêm điểm gần nhất trong 30 m. Điểm đã nhặt được lưu cùng bản lưu.

Vị trí do **ShadowVale > Map 1 > Rải đá nhặt được** tính và ghi vào `Map01/Resources/Stones/Map01StoneSites.asset`
(game sinh điểm lúc chạy, không sửa `Map 1.unity`). Luật: bãi sỏi cách nhau khoảng 38 m dọc mỗi bờ và ở gần cầu; quanh
bến B (28–45 m) và mỗi trại (18–40 m) có ít nhất 3 điểm, ưu tiên phía sau tảng đá so với trại; trên các lối hầm A – bến B –
các trại không đoạn nào quá 45 m thiếu đá trong tầm 8 m; không điểm nào trong tầm nhìn của lính (+2 m) hay cách điểm khác
dưới 12 m. Dời trại, tảng đá, cầu hay sông thì chạy lại công cụ; `StoneSiteTests` báo khi dữ liệu đã lưu không còn đúng
luật với map hiện tại.

## Hòm sắt trại địch và vật phẩm

Mỗi trại C1–C3 có một hòm sắt (`Map01CampChests`), đặt trước bạt tiếp tế, giữa hai cột bạt, ổ khóa quay ra sân trại.
Đây là nguồn đồ duy nhất trong trại. Hòm khóa cho tới khi cả 2 lính của trại ngã; [E] khi còn khóa chỉ báo còn mấy lính.
Lính cuối cùng ngã thì game báo một lần và bản đồ nhỏ hiện ô vuông vàng ở trại. [E] lần 1 mở hòm (ổ khóa rơi, nắp bật,
đồ hiện trên khay); [E] lần 2 lấy hết vào túi. Hòm đã lấy được lưu là đã dùng; hòm mở mà chưa lấy thì tải lại sẽ đóng.

| Trại | Trong hòm |
|---|---|
| C1 | súng lục K-54, 16 đạn K-54, 2 băng cứu thương, 2 vải, 3 kim loại vụn, 1 dầu súng, 2 thuốc súng |
| C2 | 60 đạn súng trường, 2 ống tạo khói, 2 vải, 2 thảo dược, 2 linh kiện súng, 1 mô-đun kích hoạt, 3 thuốc súng |
| C3 (xa nhất) | súng Mosin ống ngắm PU, 10 đạn Mosin, 1 kính ngắm, 1 lõi nổ, 1 bộ sửa chữa, 1 băng cứu thương |

Số lượng là giá trị khởi đầu, chỉnh sau khi chơi thử. Súng lục K-54 (TT-33 bản Trung Quốc, đạn 7,62×25, băng 8 viên) và
súng Mosin M91/30 ống ngắm PU 3,5× (đạn 7,62×54R, 5 viên) nhặt được và có trong túi đồ nhưng **chưa cầm lên bắn được**:
việc đó cần hoạt ảnh mới cho Nam. Bom khói chưa ném được, bộ sửa chữa chưa sửa được súng, linh kiện súng, mô-đun kích
hoạt, lõi nổ và kính ngắm chưa có công thức dùng; mô tả trong túi đồ ghi rõ điều đó.

Mỗi món nhận được (nhặt, mở hòm, chế tạo) hiện một dòng ở mép phải màn hình: icon, "+số lượng", tên. Món mới nằm trên
cùng, nhặt cùng món trong 2 giây thì cộng dồn, tối đa 7 dòng, mờ đi sau 4 giây (`Map01Hud.Pickup.cs`).

**Chế tạo:** [E] ở bàn chế tạo căn cứ A mở màn hình bàn chế tạo (`Map01Hud.Craft.cs`); [E] hoặc Esc đóng, Tab chuyển sang
túi đồ. Bên trái là các công thức, chia thẻ Hồi máu · Dụng cụ · Ném · Đạn, mỗi dòng ghi làm được mấy lần và có/cần của
từng vật liệu. Ở giữa là món đang chọn: hình lớn, công dụng, mỗi vật liệu một hàng (hình, cần/có, ĐỦ hoặc THIẾU, thiếu thì
ghi nơi kiếm), số lượng [-] [+] TỐI ĐA và nút CHẾ TẠO (bị khóa thì ghi lý do bên dưới). Bên phải là vật liệu trong túi, hàng
chờ và món vừa xong. Rê chuột lên hình vật liệu để xem hình lớn và nơi kiếm. Vật liệu bị trừ ngay khi bấm; bàn làm lần
lượt từng món, Nam đi đâu bàn vẫn làm tiếp (thẻ "Đang chế tạo" ở góc phải màn hình), xong hiện ở dòng báo nhặt đồ. Lưu
game giữ món đang làm và trả lại vật liệu của các món còn chờ. Phím B vẫn làm nhanh một băng cứu thương ở bàn.

| Món | Cần | Thời gian |
|---|---|---|
| Băng cứu thương ×1 | 2 vải + 1 thảo dược | 4 giây |
| Bộ sửa chữa ×1 | 3 kim loại vụn + 1 dầu súng | 6 giây |
| Bom khói ×1 | 1 ống tạo khói + 1 vải | 4 giây |
| Đạn K-54 ×8 | 1 thuốc súng + 1 kim loại vụn | 3 giây |
| Đạn súng trường ×10 | 2 thuốc súng + 1 kim loại vụn | 5 giây |

Thuốc súng (hộp thiếc) có trong hòm sắt trại C1 (2) và C2 (3). Hình lớn của các món ở bàn nằm trong
`Map01/Resources/Hud/ItemRenders` (script Blender xuất cùng icon).

**Đồ test (tạm thời):** `"testCraftingKit": true` trong `Map01Balance.json` cho Nam bắt đầu Chơi mới với đủ vật liệu chế mỗi
món nhiều lần, thêm K-54, Mosin và đạn (`Map01Inventory.TestCraftingKit`). Đổi thành `false` là trở lại bình thường. Bài
kiểm thử luôn bắt đầu không có đồ test.

**Mô hình:** hòm sắt và mô hình ngoài thế giới của 19 vật phẩm dựng bằng Blender từ
`SourceArt/Map01_Items/build_map01_items.py` (file làm việc `Map01_Items.blend`), theo kích thước thật: K-54 dài 194 mm,
Mosin dài 1234 mm, hòm 1,3 m để vừa khẩu Mosin. Script xuất FBX vào `Art/Environment/Map01Items/Meshes`, icon của 10 món
mới vào `Map01/Resources/Hud/ItemIcons`, và ảnh thiết kế (ảnh chính, hình chiếu có thước, hình bóng, xếp hàng) vào
`SourceArt/Map01_Items/Design`. Súng AK, dao và đá dùng prefab có sẵn của game. Sau khi chạy lại script, chọn
**ShadowVale > Map 1 > Nhập mô hình vật phẩm** để tạo vật liệu và cập nhật `Map01/Resources/Items/Map01ItemCatalog.asset`.
`CampChestTests` kiểm tra chỗ đặt hòm, khóa/mở/lấy, lưu và dòng báo nhặt; bài `[Explicit]` trong đó chụp ảnh 1920×1080
vào `Docs/Previews/Map01Items`.

## Trang bị, túi đồ và đồ ném

Theo bản thiết kế "Trang bị" đã duyệt (16/10/2026), bỏ hẳn tay không:

- **Phím 1–4**: 1 súng chính (AK hoặc Mosin), 2 súng ngắn (K-54), 3 dao, 4 đổi đồ ném (đá → bom khói → lựu đạn F1,
  chỉ những loại đang có). Bấm lại phím của món đang cầm không cất đi. Dao luôn ở ô 3, không cất vào túi, không vứt
  được, nên Nam lúc nào cũng cầm một thứ. Súng đang cầm bị đổi hoặc vứt khỏi ô thì Nam rút dao.
  `PlayerCombat` (của Khoa) bỏ ô tay không, thêm phím 4 và `SlotPressed` để màn chơi tự quyết các phím; ở Map 1 là
  `Map01Inventory.OnSlotKey`.
- **K-54 và Mosin chưa có hoạt ảnh cầm**: vào ô được, hiện trên thanh trang bị, nhưng bấm 1/2 chỉ báo
  "chưa có hoạt ảnh cầm" và giữ món đang cầm. Bản thiết kế hoạt ảnh: file
  `D:\DO_AN_GAME\chuyen_dong\Nam_ThietKe_K54_Mosin_2026-10-16.blend` (chưa đưa vào game). Bản thiết kế đeo súng
  (AK chéo sau lưng, K-54 trong bao da bên hông phải) và động tác đổi súng, ném F1, ném bom khói:
  `D:\DO_AN_GAME\chuyen_dong\thiet_ke_deo_nem\Nam_ThietKe_DeoSung_NemLuuDan_2026-10-16.blend` (chưa đưa vào game).
- **Túi đồ (Tab)**: lưới 6 × 4 = 24 ô, mỗi ô một chồng tới mức tối đa của món. Kéo lên ô trống: đặt vào; lên món khác:
  đổi chỗ; lên cùng món: gộp tới mức tối đa. Tách đôi, SẮP XẾP (súng → đạn → đồ ném → hồi máu → vật liệu → nhiệm vụ),
  kéo súng vào ô trang bị để đổi, chuột phải để có menu. Vị trí từng ô được lưu. Túi hết chỗ: phần nhặt thêm nằm
  dưới chân Nam.
- **Vứt bỏ**: kéo vào ô VỨT BỎ (hoặc nút VỨT), hỏi lại và cho chọn số lượng. Món vứt nằm trước chân Nam bằng mô hình
  3D của nó (`Map01Drops`), [E] để nhặt lại, lưu cùng bản lưu. Hàng tiếp tế, tài liệu không vứt được.
- **Đồ ném (giữ Q, thả Q)**, `Map01StoneThrow`:
  - Đá: như trước, tiếng động 14 m.
  - Bom khói: mây khói bán kính 4 m trong 15 giây; lính không nhìn xuyên qua (`Map01Smoke.Blocks` trong
    `Map01EnemyController.CanSeePlayer`). Chế ở bàn: 1 ống tạo khói + 1 vải.
  - Lựu đạn F1: nổ 3,5 giây sau khi rơi; trong 3 m chết, tới 7 m bị thương (60% máu), tiếng nổ 40 m như tiếng súng.
    Nam đứng gần cũng bị. Chế ở bàn: 1 lõi nổ + 1 mô-đun kích hoạt + 1 kim loại vụn. Bán kính là giá trị khởi đầu,
    chỉnh sau khi chơi thử (F1 thật: ngòi 3,2–4,2 giây, mảnh văng tới 200 m).
  - Hình ảnh (dựng bằng code, không cần asset, `Map01Fx`): khoen chốt (dây giật của bom khói) ở lại tay Nam; cần
    bẩy F1 bật ra lúc rời tay và rơi xuống đất; ngòi F1 bốc khói mảnh từ lúc bay tới lúc nổ. Nổ F1 (`Map01Blast`):
    chớp sáng 0,1 giây (không có quả cầu lửa kiểu phim), đất đá bắn hình nón, bụi nâu xám bung ra rồi dừng, vòng
    bụi lăn trên mặt đất, cột khói xám đậm bốc lên trôi theo gió, vết cháy xém nằm 60 giây; camera rung trong
    35 m (`Map01CameraShake`). Bom khói: tia lửa lúc mồi cháy, luồng khói trắng xám phun từ ống cuộn thành mây,
    mây nghiêng theo gió rồi tan.
- Test: `ThrowEffectsTests.BlastAndSmokeBuildTheirEffectsAndClearAway`; bài `[Explicit]`
  `ThrowEffectsTests.PhotographTheThrowEffects` chụp 10 ảnh (khói từ lúc mồi tới lúc tan, F1 từ ngòi cháy tới vết
  cháy) vào `Docs/Previews/Map01Items/game-fx-*.png`. `LoadoutTests`, `ForestHudTests.ItemsUseTheirOwnKeysWithNoShortcutBar`; bài `[Explicit]` `LoadoutTests.PhotographTheLoadoutInTheGame` chụp 8 ảnh 1920×1080 (túi đồ, menu chuột phải, hỏi vứt, đổi sang Mosin, thanh trang bị, ngắm F1, mây khói, đồ vứt) vào `Docs/Previews/Map01Items/game-loadout-*.png`.

## Địa hình bao quanh

Nền rừng Map 1 không còn các dải đường mòn vẽ sẵn trong `Map01Detailed/GroundTexture.asset`.
Màu đất rừng loang tự nhiên, bờ sông chuyển mềm sang nền rừng; cầu, dốc vào hầm, collider và NavMesh giữ nguyên.
Có thể dựng lại bằng **ShadowVale > Map 1 > Remove painted forest trails**; mái hầm dùng chung texture nên tự đồng bộ.

Mái hầm căn cứ A khớp cao độ 4,20 m và dùng chung vật liệu, UV, màu đỉnh với lưới đất quanh hầm.
`ShelterGroundRoof.asset` phủ đúng lỗ địa hình 16 × 14 m; tấm mái kín nối chồng lên tường,
các vách hồi cửa nối khung với mặt tường mà giữ lối đi. Khi chỉnh lại đất/hầm, mở Map 1 và chạy
**ShadowVale > Environment > Seal shelter and match ground** để dựng lại các mesh trong prefab `ShelterShell`.
Công cụ này dành riêng cho vị trí hầm hiện tại trong Map 1, không thay đổi terrain hoặc NavMesh.

Camera góc nhìn thứ ba kiểm tra va chạm cả độ lệch vai và cần camera, co vào ngay khi gặp collider vật lý,
rồi giãn trở lại khi hết vật cản. Khoảng zoom tối thiểu không giới hạn việc co tránh tường. Bán kính kiểm tra
bao cả mặt phẳng cắt gần; bỏ qua trigger và collider của người chơi. Áp dụng chung cho Map 1/Map 2,
còn các góc máy cinematic vẫn do trình điều khiển cinematic quản lý.

`03 Surroundings • outside the playable map` (dựng bằng **ShadowVale > Map 1 > Dựng địa hình bao quanh**; chạy lại
sẽ thay bản cũ, tài sản ở `Scenes/Maps/Map 1/Surroundings`): vành đất nối liền mép địa hình trong map (trùng từng
đỉnh ở mép), nhô dần thành đồi ra xa 420 m; sông chảy tiếp ra ngoài phía Bắc và phía Nam. Rừng: trong 28 m sát mép là
đúng các prefab cây/bụi của map (845 cái), xa hơn tới 190 m là cây low-poly gộp theo ô 90 m (khoảng 88 nghìn đỉnh).
Chỉ là cảnh: tường vô hình ở mép vẫn giữ Nam trong map, không nằm dưới môi trường bake NavMesh, cây xa không đổ bóng.
Tốn thêm khoảng 2–4 ms khi nhìn ra ngoài (tổng vẫn chỉ 4–6 ms), khoảng 2 ms ở góc nhìn nặng nhất trong rừng.
Cây low-poly ở dải xa trông khác cây trong map khi nhìn gần (khoảng 30–60 m); nới dải prefab rộng hơn sẽ đẹp hơn nhưng nặng hơn.

## Minimap và khám phá

Minimap dùng ảnh quét scene thật (`Map01Detailed/Map01Survey.png`) với metadata
`Resources/Maps/Map01Survey.asset`. Quét đúng mép trong bốn collider `Boundary`: X −110…110 m,
Z −100…100 m; không lấy vành địa hình ngoài khu vực chơi. Căn cứ A là biểu tượng nhà xanh,
luôn được định vị cả khi ngoài cửa sổ minimap tròn. Nhấn M xem bản đồ lớn, hướng Bắc ở trên.

Nhân vật mở vùng trong bán kính 14 m khi di chuyển; phần chưa đi tới được che tối trên cả hai kiểu bản đồ.
Lưới khám phá 128 × 128 dùng khoảng 2,7 KB chuỗi Base64 trong checkpoint. Save/load và tự lưu khi thoát
qua menu giữ vùng đã mở; thử lại nhiệm vụ giữ kiến thức bản đồ hiện tại. Tải bản lưu cũ chưa có dữ liệu
khám phá bắt đầu mở từ vị trí nhân vật, không tự mở toàn bộ map. Chơi mới có lưới khám phá riêng.

Sau khi thay địa hình, cây, công trình hoặc boundary, mở Map 1 ngoài Play mode và chạy
**ShadowVale > Map 1 > Bake minimap survey**. Công cụ đọc boundary vật lý, loại nhân vật/lính khỏi ảnh,
chờ shader và tạm tắt sương/occlusion khi quét; các thiết lập và renderer được khôi phục sau khi chụp.
Ảnh được tạo sẵn nên lúc chơi không phát sinh camera quét cảnh. Mục tiêu nhiệm vụ vẫn hiện để dẫn đường;
đồng đội chỉ hiện trên vùng đã khám phá, doanh trại giữ quy tắc thông tin trinh sát hiện có.

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

## Cutscene kết thúc nhiệm vụ 4

Hạ chỉ huy địch sẽ kích hoạt Map01EndingCutscene và hoàn tất Map 1 ngay sau cảnh; không cần quay về báo cáo Hùng.
Cảnh tái sử dụng clip Die trong Animator của boss ở tốc độ 0.55, không tạo animation/model mới và không thay đổi Time.timeScale.
Camera chọn phía thoáng, xoay nhẹ 26 độ, tiến gần và theo thân nhân vật khi gục; giữ khung hình cuối phía sau bảng hoàn thành.
Giữ Esc 1 giây để bỏ qua. Input và lưu game bị khóa trong cảnh; bỏ qua vẫn đặt boss ở tư thế chết cuối cùng.
Checkpoint cũ ở bước báo cáo chiến thắng được xem là đã hoàn thành, không phát lại cảnh chết.
