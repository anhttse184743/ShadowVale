# Nam v3 — tích hợp Unity ngày 22/10/2026

Người dùng đã duyệt bản v3 và yêu cầu thêm vào game. Nguồn duy nhất là `SourceArt/AnimationRefresh_20261022/Nam_AnimationRefresh_2026-10-22_v3.blend`, SHA256 `77727ffeab59bf989dfd9fdd568fbe859ce35541ae6df2a92e6341a32c59ac2c`.

Đã nhập và bake 47 đoạn, 3208 khung ở 60 Hz vào `Assets/_Project/Map01/Resources/Characters/NamMotionV3.asset`. Runtime `NamMotionRefresh` được cài trong luồng khởi tạo Nam của `Map01Mission` ở Map 1. Không ghi lại scene hoặc thay prefab/model Nam. Tutorial dùng Hùng, có bộ xương riêng; các lớp pose Nam/K54/Mosin kiểm đủ tên xương trước khi được cài, tránh lỗi cũ `LeftForeArmTwist1` làm dừng khởi tạo tutorial.

## Hành vi đã nối

- Dao: chờ, rút/cất, đánh thường, đi/chạy/khom, nhảy. Đòn dao giường C3 tiếp tục dùng bản v10 riêng.
- K54: mang thấp, nâng/hạ, ngắm, bắn, thay băng, đi ngắm, đi/chạy/khom, nhảy. Tiếp tục dùng vũ khí K54 và cấu hình đạn/sát thương hiện có.
- F1, đá, khói: chuẩn bị, lấy đà, giữ ngắm, ném/thu tay; đứng và khom; tư thế nhảy cầm từng vật. Đạo cụ, vòng chốt và lever dùng các track riêng của bản đã duyệt.
- Khi di chuyển trong lúc ném, phần chân tiếp tục do locomotion hiện có điều khiển. Khi nhảy cầm súng dài, giữ quyền điều khiển tay cho hệ grip súng dài.
- Cinematic, tương tác toàn thân, ống nhòm, băng bó và chết trả quyền diễn cho hệ hành động hiện có.

## Quy tắc kỹ thuật

`NamMotionRefresh` ghi pose tại LateUpdate thứ tự 1000, sau stance/grip thông thường và trước ForearmTwist 9000. Owner authored pose ngăn twist bị áp dụng thêm lần nữa. Toàn bộ 62 xương của Nam được giữ; mesh/weights/Avatar Nam không được thay bởi FBX xuất mới. Mesh witness trong FBX chỉ phục vụ lấy bind matrix khi bake và không được dựng trong game.

Loại quỹ đạo bay demo và dịch chuyển root trên mặt phẳng khỏi bản xuất Unity. CharacterController tiếp tục dùng gravity −20, jumpHeight 1,4; pha trên không (offset 8–53) được kéo dài tới `isGrounded` thật, pha tiếp đất (54–81) chạy sau va chạm. Không thêm trễ 8 khung chuẩn bị của reel vào nút nhảy hiện có. Jump state chỉ thoát khi Grounded; các state/layer khác giữ nguyên.

Pause giữ pha/đạo cụ hiện tại. Pin F1/khói tiếp tục theo track tay trái sau khi thân đạo cụ rời tay; lever bay chỉ do hệ projectile hiện có dựng một lần. Màu dao/đá được lấy từ diffuse viewport của nguồn duyệt, thay vì màu trắng mặc định do FBX mang sang.

Mốc rời tay vẫn 0,18 giây, hồi tay 1,1 giây; nạp K54 vẫn 1,8 giây và băng 8 viên. Vật lý đạn/vật ném, sát thương và tiêu hao do code gameplay cũ thực hiện.

Lượt QA đầu phát hiện FBX bắt đầu frame 1 nhưng importer lấy frame 0: đầu clip lấy nhầm khung cuối clip trước. Đã sửa importer thành 1–3208 và bake lại; giữ kết quả lỗi ban đầu trong `new-tests.xml` để truy vết. Nền thử nhảy được nâng cao để tránh địa hình/cây bên dưới làm sai giả định của bài thử rơi dài.

## Kiểm chứng

22 bài kiểm thử khác nhau đạt ở kết quả mới nhất, bằng Unity 6000.3.24f1 với thiết bị đồ họa thật:

| Nhóm | Đạt | Kết quả |
|---|---:|---|
| NamMotionRefreshTests | 7/7 | `final-tests.xml`; bài asset/rig kiểm lại ở `rig-tests.xml` |
| WeaponAnimationIntegrationTests | 3/3 | `final-tests.xml` |
| K54IntegrationTests | 4/4 | `regression-tests.xml`; magazine kiểm lại ở `rig-tests.xml` |
| K54NormalRestorationTests | 3/3 | `regression-tests.xml` |
| C3BedAnimationTests chọn lọc | 3/3 | `regression-tests.xml` |
| TutorialAnimationPolishTests | 2/2 | `rig-tests.xml` |

Các lượt đầy đủ: `regression-tests.xml` 19/19; `final-tests.xml` 11/12 trước sửa rig Hùng; `rig-tests.xml` 4/4 sau sửa rig. Tổng hợp theo tên bài và kết quả mới nhất ở `integration_receipt.json`, không cộng trùng các bài chạy lại.

Kiểm nhảy dùng phím Space qua Input System thật: nền ngang, rơi xuống nền thấp hơn để vượt thời gian bay danh nghĩa, nền dốc 18°, pause giữa lúc bay, đổi dao → K54 → F1 giữa lúc bay rồi tiếp đất. Kiểm vật ném giữ chuột qua cả chuẩn bị/lấy đà/ngắm, hủy không mất vật phẩm, thả chỉ tiêu hao một vật; kiểm cả đá và khói khi khom. K54 kiểm bắn/sát thương, hai kho đạn, nạp đạn, đổi vũ khí và ngắt hành động. C3 kiểm hash/timing dữ liệu, ám sát không tiêu hao đạn và hoàn trả điều khiển sau hủy.

Đã xem trực tiếp ảnh Unity: dao đúng điểm cầm, K54 thay băng, tư thế nhảy, F1 rút chốt, khói khom. Ảnh là frame từ nhân vật gameplay, không phải ảnh Blender thay thế:

- [Dao chờ](02-knife-idle.png), [đánh dao](03-knife-attack.png), [cất dao](04-knife-holster.png).
- [K54 mang thấp](05-pistol-carry.png), [thay băng](06-pistol-reload.png), [nhảy](10-jump-pistol.png), [tiếp đất](11-land-pistol.png).
- [F1 lấy đà sau rút chốt](14-f1-pin.png), [giữ ngắm](15-f1-aim.png), [thả/thu tay](08-f1-release.png), [đổi sang F1 lúc nhảy trên dốc](13-slope-jump-f1.png).
- [Khói khi khom](09-smoke-crouch.png), [đá khi khom](12-stone-crouch.png).

Nền thử và camera chỉ tồn tại trong Play Mode; diff không có scene Map 1, Map 2, tutorial hoặc prefab Nam/Hùng. Không tuyên bố đã kiểm toàn bộ tổ hợp địa hình/đạo cụ của mọi màn chơi. Map 2 dùng mission riêng và chưa nối bộ v3 trong lượt này.

Chưa commit/push hoặc xuất bản build phát hành.
